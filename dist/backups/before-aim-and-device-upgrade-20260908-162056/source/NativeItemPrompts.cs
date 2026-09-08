using System;
using System.Reflection;
using System.Text;
using Duckov.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovPad
{
    /// <summary>Controller hints inside the game's existing item readout.</summary>
    internal sealed class NativeItemPrompts
    {
        private static readonly FieldInfo ContainerField = typeof(ItemHoveringUI).GetField(
            "interactionIndicatorsContainer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TargetField = typeof(ItemHoveringUI).GetField(
            "target", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly PadConfig _config;
        private readonly UiActions _actions = new UiActions();
        private ItemHoveringUI _owner;
        private GameObject _original;
        private GameObject _root;
        private TextMeshProUGUI _text;
        private LayoutElement _layout;
        private bool _replaced;
        private bool _originalActive;
        private bool _failed;

        public NativeItemPrompts(PadConfig config) => _config = config;

        public void Update(ItemHoveringUI tooltip, bool controllerActive)
        {
            if (_failed) return;
            try
            {
                var display = TargetField?.GetValue(tooltip) as ItemDisplay;
                if (!controllerActive || !ItemHoveringUI.Shown || display == null || display.Target == null)
                {
                    Restore();
                    return;
                }
                if (_owner != tooltip || _root == null)
                {
                    Destroy();
                    Build(tooltip);
                }
                if (_root == null) return;
                if (!_replaced)
                {
                    _originalActive = _original.activeSelf;
                    _replaced = true;
                }
                _original.SetActive(false);
                _root.SetActive(true);

                var buttons = _config.Buttons;
                var text = new StringBuilder();
                bool carrying = ItemUIUtilities.SelectedItem != null;
                // Resolve to the slot so hint visibility uses the exact same rules as
                // the actions in UiDriver (entry/slot gating, not just item flags).
                var slot = UiActions.ResolveSlot(display.gameObject);
                var actionTarget = slot ?? display.gameObject;
                Add(text, buttons.UiClick, carrying ? "Place / swap" : (display.Movable ? "Grab item" : "Select"));
                if (display.Movable) Add(text, buttons.UiQuickMove, "Quick move");
                if (_actions.CanUse(actionTarget)) Add(text, buttons.UiUse, "Use");
                if (display.ShowOperationButtons) Add(text, buttons.UiContext, "Use / equip / item menu");
                if (_actions.CanDrop(actionTarget)) Add(text, buttons.UiDrop, "Drop");
                if (display.CanLockSort) Add(text, buttons.UiLockSort, "Lock / unlock sort");
                if (_actions.TryGetMark(actionTarget, out _, out bool wishlisted))
                    Add(text, buttons.UiMark, wishlisted ? "Unmark wishlist" : "Mark wishlist");
                if (carrying) Add(text, buttons.UiBack, "Cancel move");
                string value = text.ToString().TrimEnd();
                if (_text.text != value)
                {
                    _text.text = value;
                    _layout.preferredHeight = _text.GetPreferredValues(value, 240f, 0f).y + 8f;
                    LayoutRebuilder.MarkLayoutForRebuild(_root.transform.parent as RectTransform);
                }
            }
            catch (Exception e)
            {
                Restore();
                _failed = true;
                Log.Warn("Controller item readout unavailable: " + e.Message);
            }
        }

        private void Add(StringBuilder text, string binding, string label)
        {
            if (string.IsNullOrWhiteSpace(binding)) return;
            var glyphs = new StringBuilder();
            foreach (string part in binding.Split('+'))
            {
                if (glyphs.Length > 0) glyphs.Append('+');
                var info = PadGui.GetGlyphInfo(Pad.CanonicalName(part), _config.Hints.PlayStationLabels);
                string name = info.Arrow != 0 ? part : info.Label;
                glyphs.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(info.Color))
                    .Append(">[").Append(name).Append("]</color>");
            }
            text.Append(glyphs).Append("  ").Append(label).Append('\n');
        }

        private void Build(ItemHoveringUI tooltip)
        {
            _original = ContainerField?.GetValue(tooltip) as GameObject;
            if (_original == null) return;
            _owner = tooltip;
            var template = _original.GetComponentInChildren<TextMeshProUGUI>(true);
            if (template == null) return;
            var originalRect = (RectTransform)_original.transform;
            _root = new GameObject("DuckovPadItemActions", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(originalRect.parent, false);
            rect.SetSiblingIndex(originalRect.GetSiblingIndex() + 1);
            rect.anchorMin = originalRect.anchorMin;
            rect.anchorMax = originalRect.anchorMax;
            rect.pivot = originalRect.pivot;
            rect.anchoredPosition = originalRect.anchoredPosition;
            rect.sizeDelta = new Vector2(240f, 120f);
            _text = _root.AddComponent<TextMeshProUGUI>();
            _text.font = template.font;
            _text.fontSize = Mathf.Clamp(template.fontSize, 13f, 17f);
            _text.color = Color.white;
            _text.richText = true;
            _text.enableWordWrapping = false;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.margin = new Vector4(6f, 4f, 6f, 4f);
            _text.raycastTarget = false;
            _layout = _root.AddComponent<LayoutElement>();
            _layout.minWidth = 240f;
            _layout.preferredWidth = 240f;
            _layout.preferredHeight = 120f;
            // It replaces the keyboard rows within the same layout; it is never a snap target.
            _root.AddComponent<CanvasGroup>().blocksRaycasts = false;
        }

        public void Restore()
        {
            if (_root != null) _root.SetActive(false);
            if (_replaced && _original != null) _original.SetActive(_originalActive);
            _replaced = false;
        }

        public void Destroy()
        {
            Restore();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _owner = null;
            _original = null;
        }
    }
}
