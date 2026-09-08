using System;
using System.Reflection;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Turns the right stick into an aim point.
    ///
    /// The game aims by keeping a virtual mouse position (<c>InputManager._aimMousePosCache</c>),
    /// raycasting it onto a ground plane at the duck's height, and handing the resulting world
    /// point to the character. Rather than fake mouse movement — which fights the game's own
    /// cursor warping and produces drifting, relative aim — we write that virtual mouse position
    /// directly, then let the untouched vanilla pipeline (recoil, obstacle sweeps, head targeting)
    /// run on top by calling SetAimInputUsingMouse with a zero delta.
    ///
    /// The result is true twin-stick aiming: stick angle maps 1:1 to world aim angle.
    /// </summary>
    internal sealed class AimDriver
    {
        private static readonly FieldInfo AimCacheField =
            typeof(InputManager).GetField("_aimMousePosCache", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AimSyncedField =
            typeof(InputManager).GetField("aimMousePosFirstSynced", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PadConfig _config;
        private readonly TargetFinder _finder = new TargetFinder();

        /// <summary>Smoothed aim direction in camera-relative stick space.</summary>
        private Vector2 _direction = Vector2.up;
        private float _magnitude;

        // Aim assist
        private Transform _assistTarget;
        private Vector3 _assistDirection;
        private float _assistWeight;

        // Lock-on
        private Transform _lockTarget;
        private Vector3 _lockPoint;

        /// <summary>Last aim point written, in screen space.</summary>
        public Vector2 ReticleScreenPosition { get; private set; }
        public bool HasReticle { get; private set; }

        // Diagnostics for the debug overlay.
        public bool LockedOn => _lockTarget != null;
        public Vector3 LockPoint => _lockPoint;
        public string AssistTargetName { get; private set; } = "none";
        public float AssistAngle { get; private set; }
        public float AssistWeight => _assistWeight;
        public int NearbyColliders => _finder.LastColliderCount;
        public int NearbyHostiles => _finder.LastHostileCount;
        public string AssistStatus { get; private set; } = "idle";

        public AimDriver(PadConfig config)
        {
            _config = config;
            if (AimCacheField == null)
                Log.Error("InputManager._aimMousePosCache not found — the game's aim internals changed. " +
                          "Gamepad aiming will fall back to relative mode.");
        }

        public void Reset()
        {
            _direction = Vector2.up;
            _magnitude = 0f;
            _assistTarget = null;
            _assistWeight = 0f;
            _lockTarget = null;
            HasReticle = false;
        }

        public void ClearLock()
        {
            _lockTarget = null;
        }

        /// <summary>Toggle or engage lock-on against the best target in front of the player.</summary>
        public bool ToggleLockOn(CharacterMainControl character)
        {
            if (_lockTarget != null)
            {
                _lockTarget = null;
                return false;
            }

            return AcquireLock(character);
        }

        public bool AcquireLock(CharacterMainControl character)
        {
            if (character == null) return false;

            var snap = _config.AimSnap;
            Vector3 origin = character.transform.position;
            Vector3 currentDirection = CurrentWorldDirection(origin);

            var result = _finder.Find(
                origin, currentDirection, snap.MaxDistance, snap.MaxAngleDegrees,
                snap.RequireLineOfSight, _lockTarget, 0f);

            if (!result.Found) return false;

            _lockTarget = result.Transform;
            _lockPoint = result.Point;
            return true;
        }

        /// <summary>
        /// Drive one frame of aiming. Returns false if the world isn't in a state where
        /// aiming makes sense, in which case the caller should leave aim alone.
        /// </summary>
        public bool Update(InputManager inputManager, CharacterMainControl character, bool adsHeld, bool firing, float deltaTime)
        {
            HasReticle = false;

            if (inputManager == null || character == null) return false;

            var levelManager = LevelManager.Instance;
            if (levelManager == null || levelManager.GameCamera == null) return false;

            var camera = levelManager.GameCamera.renderCamera;
            if (camera == null) return false;

            var aim = _config.Aim;
            bool relative = string.Equals(aim.Mode, "relative", StringComparison.OrdinalIgnoreCase)
                            || AimCacheField == null;

            Vector2 stick = Pad.RightStick(aim.Deadzone, aim.OuterDeadzone, aim.ResponseCurve);

            if (relative)
                return UpdateRelative(inputManager, stick, adsHeld, deltaTime);

            // ---- absolute (twin-stick) aiming ----

            if (stick.sqrMagnitude > 0.0001f)
            {
                Vector2 target = stick.normalized;
                float targetMagnitude = Mathf.Clamp01(stick.magnitude);

                if (aim.Smoothing <= 0f)
                {
                    _direction = target;
                    _magnitude = targetMagnitude;
                }
                else
                {
                    float t = 1f - Mathf.Exp(-aim.Smoothing * deltaTime);
                    _direction = Vector2.Lerp(_direction, target, t).normalized;
                    _magnitude = Mathf.Lerp(_magnitude, targetMagnitude, t);
                }
            }
            // When the stick is released the duck keeps facing the last direction,
            // which is what players expect from a twin-stick shooter.

            if (_direction.sqrMagnitude < 0.0001f) _direction = Vector2.up;

            Vector3 origin = character.transform.position;
            Vector3 stickDirection = StickToWorld(camera, _direction);
            Vector3 worldDirection = stickDirection;

            float distance = Mathf.Lerp(aim.MinReticleDistance, aim.ReticleDistance, _magnitude);
            if (adsHeld) distance *= aim.AdsDistanceMultiplier;

            // ---- lock-on takes priority over soft assist ----
            if (UpdateLockOn(origin, stickDirection, out var lockDirection, out var lockDistance))
            {
                worldDirection = lockDirection;
                distance = Mathf.Clamp(lockDistance, aim.MinReticleDistance, aim.ReticleDistance * 1.6f);
                _assistWeight = 0f;
                AssistStatus = "locked on";
            }
            else
            {
                worldDirection = ApplyAimAssist(origin, stickDirection, adsHeld, firing, deltaTime);
            }

            Vector3 aimWorld = origin + worldDirection * distance;
            aimWorld.y = origin.y + 0.5f; // matches the plane the game raycasts against

            Vector2 screenPoint = camera.WorldToScreenPoint(aimWorld);
            screenPoint = KeepOnScreen(camera, origin, worldDirection, distance, screenPoint);

            WriteAimPosition(inputManager, screenPoint);

            ReticleScreenPosition = screenPoint;
            HasReticle = true;
            return true;
        }

        // ------------------------------------------------------------------

        private Vector3 CurrentWorldDirection(Vector3 origin)
        {
            var levelManager = LevelManager.Instance;
            var camera = levelManager != null && levelManager.GameCamera != null
                ? levelManager.GameCamera.renderCamera
                : null;
            return camera == null ? Vector3.forward : StickToWorld(camera, _direction);
        }

        /// <summary>
        /// Camera-relative flattened basis — the same one the game uses to turn movement
        /// stick input into world motion, so aim and movement agree.
        /// </summary>
        private static Vector3 StickToWorld(Camera camera, Vector2 stick)
        {
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = camera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            Vector3 direction = right * stick.x + forward * stick.y;
            return direction.sqrMagnitude < 0.0001f ? forward : direction.normalized;
        }

        private bool UpdateLockOn(Vector3 origin, Vector3 stickDirection, out Vector3 direction, out float distance)
        {
            direction = stickDirection;
            distance = 0f;

            var snap = _config.AimSnap;
            if (!snap.Enabled || _lockTarget == null) return false;

            // Drop the lock if the target died or was destroyed.
            if (!IsValidTarget(_lockTarget))
            {
                _lockTarget = null;
                return false;
            }

            Vector3 point = TargetPoint(_lockTarget);
            Vector3 offset = point - origin;
            offset.y = 0f;
            float flat = offset.magnitude;

            if (flat < 0.3f || flat > snap.MaxDistance * 1.25f)
            {
                _lockTarget = null;
                return false;
            }

            Vector3 targetDirection = offset / flat;

            if (snap.BreakOnStickInput && Pad.RightStickRaw.magnitude > 0.7f &&
                Vector3.Angle(stickDirection, targetDirection) > snap.BreakAngleDegrees)
            {
                _lockTarget = null;
                return false;
            }

            _lockPoint = point;
            direction = targetDirection;
            distance = flat;
            return true;
        }

        /// <summary>
        /// Bend the aim direction toward a nearby hostile.
        ///
        /// Because this mod aims absolutely — the stick re-specifies the angle every frame —
        /// assist has to be a direct blend between the stick direction and the target
        /// direction. Applying a per-frame rotation instead (the obvious approach) only ever
        /// reaches a fixed fraction of the way and is far too weak to feel.
        /// </summary>
        private Vector3 ApplyAimAssist(Vector3 origin, Vector3 stickDirection, bool adsHeld, bool firing, float deltaTime)
        {
            var settings = _config.AimAssist;

            if (!settings.Enabled || settings.Strength <= 0f)
            {
                _assistWeight = 0f;
                _assistTarget = null;
                AssistStatus = settings.Enabled ? "strength is 0" : "disabled";
                AssistTargetName = "none";
                return stickDirection;
            }

            if (settings.OnlyWhileFiringOrAds && !adsHeld && !firing)
            {
                _assistWeight = Mathf.MoveTowards(_assistWeight, 0f, settings.EaseSpeed * deltaTime);
                AssistStatus = "waiting for fire/ADS";
                return BlendTowardAssist(stickDirection);
            }

            var result = _finder.Find(
                origin, stickDirection, settings.MaxDistance, settings.MaxAngleDegrees,
                settings.RequireLineOfSight, _assistTarget, settings.StickinessDegrees);

            float desiredWeight = 0f;

            if (result.Found)
            {
                _assistTarget = result.Transform;
                _assistDirection = result.Direction;
                AssistTargetName = result.Transform.name;
                AssistAngle = result.Angle;

                // Full strength through the middle of the cone, tapering at the rim so
                // targets fade in and out instead of popping.
                float edge = Mathf.Clamp01(result.Angle / Mathf.Max(0.01f, settings.MaxAngleDegrees));
                float taperStart = Mathf.Clamp01(1f - settings.EdgeTaper);
                float taper = edge <= taperStart
                    ? 1f
                    : 1f - Mathf.Clamp01((edge - taperStart) / Mathf.Max(0.001f, 1f - taperStart));

                desiredWeight = Mathf.Clamp01(settings.Strength) * taper;
                AssistStatus = "assisting";
            }
            else
            {
                _assistTarget = null;
                AssistTargetName = "none";
                AssistStatus = _finder.LastFailureReason ?? "no target";
            }

            _assistWeight = Mathf.MoveTowards(_assistWeight, desiredWeight, settings.EaseSpeed * deltaTime);
            return BlendTowardAssist(stickDirection);
        }

        private Vector3 BlendTowardAssist(Vector3 stickDirection)
        {
            if (_assistWeight <= 0.001f || _assistDirection.sqrMagnitude < 0.0001f)
                return stickDirection;

            return Vector3.Slerp(stickDirection, _assistDirection, _assistWeight).normalized;
        }

        private static bool IsValidTarget(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            var receiver = target.GetComponent<DamageReceiver>();
            if (receiver == null) receiver = target.GetComponentInParent<DamageReceiver>();
            if (receiver == null) return false;

            return !receiver.IsDead && receiver.Team != Teams.player;
        }

        private static Vector3 TargetPoint(Transform target)
        {
            var collider = target.GetComponent<Collider>();
            return collider != null ? collider.bounds.center : target.position;
        }

        /// <summary>Mouse-like mode: the stick nudges the aim point instead of pointing at it.</summary>
        private bool UpdateRelative(InputManager inputManager, Vector2 stick, bool adsHeld, float deltaTime)
        {
            if (stick.sqrMagnitude < 0.0001f) return true;

            float speed = _config.Aim.RelativeSensitivity * (adsHeld ? _config.Aim.RelativeAdsMultiplier : 1f);
            Vector2 delta = stick * speed * deltaTime;

            // Hand the delta to the vanilla path, which applies the game's own
            // mouse sensitivity, recoil and clamping.
            inputManager.SetAimInputUsingMouse(delta);
            return true;
        }

        /// <summary>
        /// The game clamps the aim point to the window, which would distort the aim angle
        /// near screen edges. Instead we shorten the reticle distance until it fits, so the
        /// direction the player asked for is preserved.
        /// </summary>
        private Vector2 KeepOnScreen(Camera camera, Vector3 origin, Vector3 direction, float distance, Vector2 candidate)
        {
            const float margin = 24f;
            float width = Screen.width;
            float height = Screen.height;

            bool inside = candidate.x >= margin && candidate.x <= width - margin &&
                          candidate.y >= margin && candidate.y <= height - margin;
            if (inside) return candidate;

            // Binary search the largest distance that still lands on screen.
            float low = 0f;
            float high = distance;
            Vector2 result = camera.WorldToScreenPoint(origin + Vector3.up * 0.5f);

            for (int i = 0; i < 8; i++)
            {
                float mid = (low + high) * 0.5f;
                Vector3 world = origin + direction * mid;
                world.y = origin.y + 0.5f;
                Vector2 screen = camera.WorldToScreenPoint(world);

                if (screen.x >= margin && screen.x <= width - margin &&
                    screen.y >= margin && screen.y <= height - margin)
                {
                    result = screen;
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return result;
        }

        private void WriteAimPosition(InputManager inputManager, Vector2 screenPoint)
        {
            if (AimCacheField == null) return;

            // Mark the cache as synced first, or the getter/setter will overwrite our
            // value with the real mouse position the first time it is touched.
            AimSyncedField?.SetValue(inputManager, true);
            AimCacheField.SetValue(inputManager, screenPoint);

            inputManager.SetMousePosition(screenPoint);

            // Zero delta: everything downstream (recoil, obstacle sweep, head aim,
            // SetAimPoint on the character) runs exactly as it does for mouse players.
            inputManager.SetAimInputUsingMouse(Vector2.zero);
        }
    }
}
