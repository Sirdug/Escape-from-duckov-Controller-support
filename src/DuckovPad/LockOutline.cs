using System;
using System.Collections.Generic;
using EPOOutline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DuckovPad
{
    /// <summary>
    /// Draws an outline around whatever lock-on is holding.
    ///
    /// The game already ships Easy Performant Outline and drives it from the URP renderer —
    /// `HalfObsticle` outlines the cover you are standing behind — so this borrows that pipeline
    /// rather than adding a second one. All it takes is an <see cref="Outlinable"/> that points
    /// at the target's renderers; the outline material, dilation and blur are shared and come
    /// from the <see cref="Outliner"/> already sitting on the render camera.
    ///
    /// The <see cref="Outlinable"/> lives on a throwaway child object of the target rather than
    /// on the target itself, so nothing the mod does can disturb an outline the game set up, and
    /// releasing the lock is a plain Destroy.
    /// </summary>
    internal sealed class LockOutline
    {
        private const string HolderName = "DuckovPadLockOutline";

        /// <summary>Enough for a character and its gear; a guard against pathological hierarchies.</summary>
        private const int MaxRenderers = 32;

        private readonly PadConfig _config;
        private readonly List<Renderer> _renderers = new List<Renderer>();

        private Transform _root;
        private GameObject _holder;
        private Outlinable _outlinable;
        private int _addedCount;

        /// <summary>Set when a root has no usable renderers, so we don't rescan it every frame.</summary>
        private bool _rootRejected;

        private bool _warnedNoOutliner;

        private string _colourSource;
        private Color _colour = Color.white;

        public LockOutline(PadConfig config)
        {
            _config = config;
        }

        /// <summary>Diagnostics for the debug overlay.</summary>
        public string Status { get; private set; } = "idle";

        /// <summary>
        /// Outline <paramref name="target"/> at <paramref name="blend"/> opacity. A null target,
        /// or a blend of zero, tears the outline down.
        /// </summary>
        public void Show(Transform target, float blend, Camera camera)
        {
            var snap = _config.AimSnap;

            if (target == null || blend <= 0.001f || !snap.ShowOutline)
            {
                Hide();
                Status = snap.ShowOutline ? "idle" : "disabled";
                return;
            }

            Transform root = VisualRoot(target);
            if (root == null)
            {
                Hide();
                Status = "target has no visual root";
                return;
            }

            if (root != _root)
            {
                Hide();
                _root = root;
                _rootRejected = !Build(root);
            }

            if (_rootRejected || _outlinable == null)
            {
                Status = "no renderers on " + root.name;
                return;
            }

            bool hasOutliner = HasOutliner(camera);

            Color colour = ParseColour(snap.OutlineColor);
            colour.a *= Mathf.Clamp01(blend);

            var parameters = _outlinable.OutlineParameters;
            parameters.Enabled = true;
            parameters.Color = colour;
            parameters.DilateShift = Mathf.Clamp01(snap.OutlineWidth);

            // The holder is built inactive (see Build) so EPO sees a fully-registered
            // Outlinable the moment it activates, mirroring how the game's own
            // HalfObsticle prefab keeps its outline disabled until it is needed.
            if (!_holder.activeSelf) _holder.SetActive(true);
            if (!_outlinable.enabled) _outlinable.enabled = true;
            Status = "outlining " + root.name + " (" + _addedCount + " renderers"
                + (hasOutliner ? "" : ", NO outliner on camera") + ")";
        }

        public void Hide()
        {
            // Disable before destroying: Destroy is deferred to the end of the frame, and an
            // Outlinable that is still enabled would draw one last time — long enough to show
            // the old target outlined alongside a newly locked one.
            if (_outlinable != null) _outlinable.enabled = false;
            if (_holder != null) Object.Destroy(_holder);

            _holder = null;
            _outlinable = null;
            _root = null;
            _rootRejected = false;
            _addedCount = 0;
            Status = "idle";
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// The lock target is a damage-receiver collider, which is often on a physics-only layer
        /// and carries no renderers of its own. Climb to the thing that actually has a body.
        /// Walks up the hierarchy looking for an ancestor that actually owns outlineable
        /// geometry, so a leaf collider can never resolve to a renderer-less root.
        /// </summary>
        private static Transform VisualRoot(Transform target)
        {
            if (target == null) return null;

            var character = target.GetComponentInParent<CharacterMainControl>();
            if (character != null && HasOutlineableGeometry(character.transform)) return character.transform;

            var receiver = target.GetComponent<DamageReceiver>();
            if (receiver == null) receiver = target.GetComponentInParent<DamageReceiver>();

            // Climb from the collider toward the root, preferring the closest ancestor
            // that actually has something to outline. Falls back to the character root,
            // then the receiver, then the raw target so we never return null for a live target.
            Transform fallback = character != null ? character.transform : (receiver != null ? receiver.transform : target);
            for (Transform t = target; t != null; t = t.parent)
            {
                if (HasOutlineableGeometry(t)) return t;
                if (t == fallback) break;
            }
            if (fallback != null && HasOutlineableGeometry(fallback)) return fallback;
            // Last resort: highest ancestor with any renderer, so Build() still has a chance.
            Transform top = target;
            while (top.parent != null) top = top.parent;
            if (top != target && HasOutlineableGeometry(top)) return top;
            return fallback;
        }

        private static bool HasOutlineableGeometry(Transform root)
        {
            if (root == null) return false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh != null) return true;
                }
                else if (r is MeshRenderer)
                {
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null) return true;
                }
            }
            return false;
        }

        private bool Build(Transform root)
        {
            _renderers.Clear();
            root.GetComponentsInChildren(false, _renderers);

            _holder = new GameObject(HolderName);
            // Built inactive: targets are registered before EPO ever sees the component.
            _holder.SetActive(false);
            _holder.transform.SetParent(root, false);

            _outlinable = _holder.AddComponent<Outlinable>();
            _outlinable.RenderStyle = RenderStyle.Single;
            _outlinable.DrawingMode = OutlinableDrawingMode.Normal;
            _outlinable.ComplexMaskingMode = ComplexMaskingMode.None;
            _outlinable.OutlineLayer = 0;
            // Front/Back parameters intentionally left at EPO defaults to match the
            // game's own HalfObsticle outline setup.

            int added = 0;
            int layer = -1;

            foreach (var renderer in _renderers)
            {
                if (added >= MaxRenderers) break;
                if (!IsOutlineable(renderer)) continue;

                try
                {
                    _outlinable.AddRenderer(renderer);
                }
                catch (Exception e)
                {
                    // A renderer with a mesh EPO cannot measure is not worth failing over.
                    Log.Warn("Could not outline " + renderer.name + ": " + e.Message);
                    continue;
                }

                if (layer < 0) layer = renderer.gameObject.layer;
                added++;
            }

            _renderers.Clear();

            if (added == 0)
            {
                Object.Destroy(_holder);
                _holder = null;
                _outlinable = null;
                _addedCount = 0;
                return false;
            }

            // The outline pass filters by the *Outlinable's* own layer against the camera
            // culling mask, so the holder has to sit on a layer the camera actually draws —
            // borrowing one from a renderer we just outlined guarantees that.
            _holder.layer = layer;
            _addedCount = added;
            return true;
        }

        /// <summary>
        /// Only the renderer kinds EPO measures safely. It reads a submesh count straight off
        /// the mesh, so anything without one — a particle system, a trail, a stripped mesh —
        /// would throw rather than simply be skipped.
        /// </summary>
        private static bool IsOutlineable(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled) return false;

            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh != null;

            if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                return filter != null && filter.sharedMesh != null;
            }

            return false;
        }

        /// <summary>Cached, because this runs every frame the lock is held.</summary>
        private Color ParseColour(string html)
        {
            if (html == _colourSource) return _colour;

            _colourSource = html;
            _colour = !string.IsNullOrWhiteSpace(html) && ColorUtility.TryParseHtmlString(html.Trim(), out var parsed)
                ? parsed
                : Color.white;

            return _colour;
        }

        private bool HasOutliner(Camera camera)
        {
            if (camera == null) return false;
            if (camera.GetComponent<Outliner>() != null) return true;

            if (!_warnedNoOutliner)
            {
                _warnedNoOutliner = true;
                Log.Warn("The render camera has no EPO Outliner component, so the lock-on 3D outline " +
                         "may not draw; the on-screen white lock frame is used instead. " +
                         "Everything else about lock-on still works.");
            }
            return false;
        }
    }
}
