using System;
using System.Collections.Generic;
using Duckov.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DuckovPad
{
    /// <summary>Find visible controls and keep the pointer on the selected slot.</summary>
    internal sealed class UiSnap
    {
        private readonly PadConfig _config;
        private readonly List<Rect> _rects = new List<Rect>(256);
        private readonly List<GameObject> _objects = new List<GameObject>(256);
        private readonly List<RaycastResult> _hits = new List<RaycastResult>(32);
        private readonly Vector3[] _corners = new Vector3[4];
        private PointerEventData _pointer;
        private GameObject _selected;
        private float _nextScanTime;

        public Rect HoverRect { get; private set; }
        public bool HasHover { get; private set; }
        public int TargetCount => _rects.Count;
        public GameObject SelectedObject => _selected;

        public UiSnap(PadConfig config) => _config = config;

        public void Invalidate()
        {
            _nextScanTime = 0f;
            _rects.Clear();
            _objects.Clear();
            ClearSelection();
        }

        public void ClearSelection()
        {
            _selected = null;
            HasHover = false;
        }

        public void Refresh()
        {
            if (Time.unscaledTime < _nextScanTime) return;
            _nextScanTime = Time.unscaledTime + _config.UiSnap.RescanInterval;
            _rects.Clear();
            _objects.Clear();
            try
            {
                var eventSystem = EventSystem.current;
                if (eventSystem == null) return;
                _pointer = new PointerEventData(eventSystem);
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (!canvas.isActiveAndEnabled || canvas.rootCanvas != canvas) continue;
                    Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    AddAll(canvas.GetComponentsInChildren<IPointerClickHandler>(false), camera);
                    AddAll(canvas.GetComponentsInChildren<IDropHandler>(false), camera);
                    AddAll(canvas.GetComponentsInChildren<Selectable>(false), camera);
                }
            }
            catch (Exception e)
            {
                Log.Warn("UI scan failed: " + e.Message);
            }
        }

        private void AddAll<T>(T[] handlers, Camera camera) where T : class
        {
            foreach (var handler in handlers)
            {
                var component = handler as Component;
                if (component == null) continue;
                if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                // This game's backdrop click handler is intentionally a no-op. It was
                // attracting the cursor to the middle of the entire inventory view.
                if (component is CloseViewOnPointerClick || component is View) continue;

                // Occupied slots have an ItemDisplay child. Use the slot itself so empty
                // and occupied slots have the same box, and each slot is only one stop.
                if (component is ItemDisplay)
                {
                    for (var parent = component.transform.parent; parent != null; parent = parent.parent)
                    {
                        Component slot = parent.GetComponent<InventoryEntry>();
                        if (slot == null) slot = parent.GetComponent<SlotDisplay>();
                        if (slot == null) slot = parent.GetComponent<ItemShortcutEditorEntry>();
                        if (slot == null) continue;
                        component = slot;
                        break;
                    }
                }

                var go = component.gameObject;
                if (ViewUtil.SplitDialogueOpen
                    && !go.transform.IsChildOf(SplitDialogue.Instance.transform)) continue;
                var menu = ItemOperationMenu.Instance;
                if (menu != null && menu.open && !go.transform.IsChildOf(menu.transform)) continue;
                if (!go.activeInHierarchy || _objects.Contains(go)) continue;
                if (component is Behaviour targetBehaviour && !targetBehaviour.isActiveAndEnabled) continue;
                var entry = go.GetComponent<InventoryEntry>();
                if (entry != null && entry.Disabled) continue;
                if (!(component.transform is RectTransform transform)) continue;
                if (!IsInteractable(go)) continue;
                if (!TryGetScreenRect(transform, camera, out var rect)) continue;
                if (!UiNavigation.IsTargetSize(rect, Screen.width, Screen.height)) continue;
                if (!IsExposed(go, rect.center)) continue;
                _objects.Add(go);
                _rects.Add(rect);
            }
        }

        private bool IsExposed(GameObject go, Vector2 position)
        {
            if (position.x < 0f || position.y < 0f || position.x >= Screen.width || position.y >= Screen.height)
                return false;
            _pointer.position = position;
            _hits.Clear();
            EventSystem.current.RaycastAll(_pointer, _hits);
            // The top UI hit excludes controls behind popups and scroll masks.
            // The item tooltip follows the cursor, so it would otherwise occlude the
            // very slot it describes and make that slot unselectable: skip it, and if
            // nothing but the tooltip covers the point, the slot underneath still counts.
            bool tooltipOnly = false;
            foreach (var hit in _hits)
            {
                if (!(hit.module is GraphicRaycaster)) continue;
                if (hit.gameObject.GetComponentInParent<ItemHoveringUI>() != null)
                {
                    tooltipOnly = true;
                    continue;
                }
                return hit.gameObject == go || hit.gameObject.transform.IsChildOf(go.transform);
            }
            return tooltipOnly;
        }

        private static bool IsInteractable(GameObject go)
        {
            var group = go.GetComponentInParent<CanvasGroup>();
            while (group != null)
            {
                if (!group.interactable || !group.blocksRaycasts || group.alpha < 0.1f) return false;
                if (group.ignoreParentGroups) break;
                var parent = group.transform.parent;
                group = parent == null ? null : parent.GetComponentInParent<CanvasGroup>();
            }
            var selectable = go.GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }

        private bool TryGetScreenRect(RectTransform transform, Camera camera, out Rect rect)
        {
            transform.GetWorldCorners(_corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, _corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width >= 6f && rect.height >= 6f;
        }

        private Vector2 Select(int index)
        {
            _selected = _objects[index];
            HoverRect = _rects[index];
            HasHover = true;
            return HoverRect.center;
        }

        public bool KeepSelection(Vector2 cursor, out Vector2 destination)
        {
            int index = _selected != null ? _objects.IndexOf(_selected) : -1;
            if (index < 0) index = UiNavigation.FindNearest(_rects, cursor, float.MaxValue);
            destination = cursor;
            if (index < 0)
            {
                ClearSelection();
                return false;
            }
            destination = Select(index);
            return true;
        }

        public void RestoreSelection(GameObject target) => _selected = target;

        public bool CanActivateSelection()
        {
            return _selected != null && _selected.activeInHierarchy && IsInteractable(_selected)
                && IsExposed(_selected, HoverRect.center);
        }

        public bool TrySnap(Vector2 cursor, Vector2 direction, out Vector2 destination)
        {
            int index = UiNavigation.FindNext(_rects, cursor, direction, _config.UiSnap.SnapConeDegrees);
            destination = cursor;
            if (index < 0) return false;
            destination = Select(index);
            return true;
        }

        public bool AdjustSlider(Vector2 direction)
        {
            if (_selected == null || direction.x == 0f) return false;
            var slider = _selected.GetComponent<Slider>();
            if (slider == null || !slider.IsInteractable()) return false;
            if (slider.wholeNumbers) slider.value += direction.x;
            else slider.normalizedValue += direction.x * 0.05f;
            return true;
        }

        public Vector2 ApplyMagnetism(Vector2 cursor, float stickMagnitude, float deltaTime, bool allowPull)
        {
            ClearSelection();
            var settings = _config.UiSnap;
            int index = UiNavigation.FindNearest(_rects, cursor, settings.MagnetRadius * Screen.height / 1080f);
            if (index < 0) return cursor;
            HoverRect = _rects[index];
            HasHover = true;
            _selected = _objects[index];
            if (!allowPull || !settings.Magnetism || settings.MagnetStrength <= 0f) return cursor;
            float pull = settings.MagnetStrength * (1f - Mathf.Clamp01(stickMagnitude));
            return Vector2.Lerp(cursor, HoverRect.center, 1f - Mathf.Exp(-pull * 12f * deltaTime));
        }
    }
}
