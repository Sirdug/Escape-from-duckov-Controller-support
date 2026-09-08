using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovPad
{
    /// <summary>
    /// The contextual button-prompt bar, the lock-on marker and the menu hover highlight.
    ///
    /// This is real in-game UI: a Unity canvas built at runtime, using a font borrowed from
    /// the game's own text so it matches (and keeps CJK coverage). Prompts change with
    /// context — gameplay, menus, and the modifier layer while LB is held — which is the
    /// thing that makes a pad layout learnable without a manual.
    /// </summary>
    internal sealed class HintHud
    {
        private readonly PadConfig _config;

        private Canvas _canvas;
        private RectTransform _bar;
        private RectTransform _lockMarker;
        private RectTransform _hoverBox;

        private TMP_FontAsset _font;
        private Sprite _pillSprite;
        private Sprite _frameSprite;

        private readonly List<GameObject> _entries = new List<GameObject>();
        private string _signature = "";
        private bool _unavailable;

        public HintHud(PadConfig config)
        {
            _config = config;
        }

        public void Destroy()
        {
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas.gameObject);
            _canvas = null;
            _bar = null;
            _entries.Clear();
            _signature = "";
        }

        public void Hide()
        {
            if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------

        public struct Hint
        {
            public string Binding;
            public string Label;

            public Hint(string binding, string label)
            {
                Binding = binding;
                Label = label;
            }
        }

        private readonly List<Hint> _hints = new List<Hint>(10);

        public void Render(bool inMenu, bool modifierHeld, bool lockedOn, bool builderActive)
        {
            if (_unavailable) return;

            if (!_config.Hints.Enabled && !(inMenu && _config.UiSnap.HideCursor))
            {
                Hide();
                return;
            }

            if (!EnsureCanvas()) return;
            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
            _bar.gameObject.SetActive(_config.Hints.Enabled);

            BuildHintList(inMenu, modifierHeld, lockedOn, builderActive);

            var signature = new StringBuilder();
            foreach (var hint in _hints) signature.Append(hint.Binding).Append('|').Append(hint.Label).Append(';');
            signature.Append(_config.Hints.Scale).Append(_config.Hints.PlayStationLabels).Append(ControllerDevice.Revision);

            string key = signature.ToString();
            if (key != _signature)
            {
                _signature = key;
                Rebuild();
            }
        }

        private void BuildHintList(bool inMenu, bool modifierHeld, bool lockedOn, bool builderActive)
        {
            var buttons = _config.Buttons;
            _hints.Clear();

            if (LoadingContinue.IsWaiting || TitleGate.IsWaiting)
            {
                _hints.Add(new Hint(buttons.UiClick, "Continue"));
                _hints.Add(new Hint(buttons.UiClose, "Continue"));
                return;
            }

            if (inMenu)
            {
                if (builderActive)
                {
                    _hints.Add(new Hint(buttons.UiClick, "Place"));
                    _hints.Add(new Hint(buttons.UiRotate, "Rotate"));
                    _hints.Add(new Hint(buttons.UiBack, "Cancel"));
                    return;
                }

                bool selected = Duckov.UI.ItemUIUtilities.SelectedItem != null
                    && !(Duckov.UI.ItemOperationMenu.Instance != null && Duckov.UI.ItemOperationMenu.Instance.open);
                _hints.Add(new Hint(buttons.UiClick, selected ? "Place / swap" : "Select / grab"));
                _hints.Add(new Hint(buttons.UiContext, "Actions"));
                _hints.Add(new Hint(buttons.UiQuickMove, "Quick move"));
                // Unbound (empty) bindings are skipped when the bar rebuilds, so these
                // only appear once the player assigns them in Settings.json.
                _hints.Add(new Hint(buttons.UiUse, "Use"));
                _hints.Add(new Hint(buttons.UiDrop, "Drop"));
                _hints.Add(new Hint(buttons.UiLockSort, "Lock sort"));
                _hints.Add(new Hint(buttons.UiMark, "Wishlist"));
                _hints.Add(new Hint(buttons.UiBack, selected ? "Cancel" : "Back"));
                _hints.Add(new Hint("leftStick", _config.UiSnap.StickNavigation ? "Navigate" : "Cursor"));
                _hints.Add(new Hint(buttons.UiPagePrevious, "Previous page"));
                _hints.Add(new Hint(buttons.UiPageNext, "Next page"));
                return;
            }

            if (modifierHeld)
            {
                // Holding the modifier turns the bar into a cheat-sheet for the second layer.
                _hints.Add(new Hint(buttons.QuickItem3, "Item 3"));
                _hints.Add(new Hint(buttons.QuickItem4, "Item 4"));
                _hints.Add(new Hint(buttons.QuickItem5, "Item 5"));
                _hints.Add(new Hint(buttons.QuickItem6, "Item 6"));
                _hints.Add(new Hint(buttons.MeleeWeapon, "Melee"));
                _hints.Add(new Hint(buttons.PauseMenu, "Pause"));
                return;
            }

            _hints.Add(new Hint(buttons.Interact, "Interact"));
            _hints.Add(new Hint(buttons.Reload, "Reload"));
            _hints.Add(new Hint(buttons.SwitchWeapon, "Swap"));
            _hints.Add(new Hint(buttons.Dash, "Dash"));

            if (_config.AimSnap.Enabled)
                _hints.Add(new Hint(buttons.LockOn, lockedOn ? "Release lock" : "Lock on"));

            _hints.Add(new Hint(buttons.Modifier, "More"));
        }

        // ------------------------------------------------------------------

        private bool EnsureCanvas()
        {
            if (_canvas != null) return true;

            _font = FindFont();
            if (_font == null)
            {
                _unavailable = true;
                Log.Warn("No TextMeshPro font found in the scene — button prompts disabled.");
                return false;
            }

            var root = new GameObject("DuckovPadHud");
            UnityEngine.Object.DontDestroyOnLoad(root);

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000; // above the game's own UI, below nothing

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Purely decorative: never intercept clicks meant for the game's UI.
            root.AddComponent<CanvasGroup>().blocksRaycasts = false;

            _pillSprite = BuildRoundedSprite(48, 12, filled: true);
            _frameSprite = BuildRoundedSprite(48, 10, filled: false);

            var bar = new GameObject("Hints", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            _bar = (RectTransform)bar.transform;
            _bar.anchorMin = new Vector2(0.5f, 0f);
            _bar.anchorMax = new Vector2(0.5f, 0f);
            _bar.pivot = new Vector2(0.5f, 0f);

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = bar.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _lockMarker = BuildImage("LockMarker", root.transform, _frameSprite, new Color(0.95f, 0.3f, 0.25f, 0.95f));
            _lockMarker.gameObject.SetActive(false);

            _hoverBox = BuildImage("HoverBox", root.transform, _frameSprite, new Color(0.95f, 0.76f, 0.31f, 0.85f));
            _hoverBox.gameObject.SetActive(false);

            return true;
        }

        private RectTransform BuildImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;

            return (RectTransform)go.transform;
        }

        private static TMP_FontAsset FindFont()
        {
            try
            {
                var texts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>();
                foreach (var text in texts)
                {
                    if (text != null && text.font != null) return text.font;
                }

                return TMP_Settings.defaultFontAsset;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Rebuild()
        {
            foreach (var entry in _entries)
                if (entry != null) UnityEngine.Object.Destroy(entry);
            _entries.Clear();

            float scale = _config.Hints.Scale;
            float height = 30f * scale;
            int fontSize = Mathf.RoundToInt(15f * scale);

            _bar.anchoredPosition = new Vector2(0f, _config.Hints.BottomMargin * scale);

            foreach (var hint in _hints)
            {
                if (string.IsNullOrWhiteSpace(hint.Binding)) continue;

                var entry = new GameObject("Hint", typeof(RectTransform));
                entry.transform.SetParent(_bar, false);

                var layout = entry.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6f * scale;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = true;

                var fitter = entry.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var parts = hint.Binding.Split('+');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(parts[i])) continue;
                    if (i > 0) AddText(entry.transform, "+", fontSize, PadGui.TextDim, height);
                    AddGlyph(entry.transform, Pad.CanonicalName(parts[i]), fontSize, height, scale);
                }

                AddText(entry.transform, hint.Label, fontSize, PadGui.Text, height);
                _entries.Add(entry);
            }
        }

        private void AddGlyph(Transform parent, string canonical, int fontSize, float height, float scale)
        {
            var info = PadGui.GetGlyphInfo(canonical, _config.Hints.PlayStationLabels);

            var go = new GameObject("Glyph", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.sprite = _pillSprite;
            image.type = Image.Type.Sliced;
            image.color = info.Color;
            image.raycastTarget = false;

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.preferredWidth = height * info.WidthFactor;

            var child = new GameObject(info.Arrow > 0 ? "Arrow" : "Label", typeof(RectTransform));
            child.transform.SetParent(go.transform, false);

            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (info.Arrow > 0)
            {
                // D-pad directions are geometry, not characters, so they can't fall foul
                // of a font that lacks arrow glyphs.
                float inset = height * 0.28f;
                rect.offsetMin = new Vector2(inset, inset);
                rect.offsetMax = new Vector2(-inset, -inset);
                rect.localRotation = Quaternion.Euler(0f, 0f, PadGui.ArrowRotation(info.Arrow));

                var arrow = child.AddComponent<Image>();
                arrow.sprite = PadGui.ArrowSprite;
                arrow.color = Color.white;
                arrow.raycastTarget = false;
                arrow.preserveAspect = true;
                return;
            }

            var text = child.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = info.Label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
        }

        private void AddText(Transform parent, string content, int fontSize, Color color, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Left;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            // Only pin the height. TextMeshProUGUI is itself an ILayoutElement and reports
            // a correct preferred width once laid out; overriding it here would measure
            // before the glyphs exist and collapse the label to nothing.
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
        }

        // ------------------------------------------------------------------

        /// <summary>Position the lock-on bracket over a world point.</summary>
        public void ShowLockMarker(Vector3 worldPoint, Camera camera)
        {
            if (_lockMarker == null || camera == null) return;

            if (!_config.AimSnap.ShowMarker)
            {
                _lockMarker.gameObject.SetActive(false);
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPoint);
            if (screen.z < 0f)
            {
                _lockMarker.gameObject.SetActive(false);
                return;
            }

            float size = 64f;
            _lockMarker.gameObject.SetActive(true);
            _lockMarker.anchorMin = Vector2.zero;
            _lockMarker.anchorMax = Vector2.zero;
            _lockMarker.pivot = new Vector2(0.5f, 0.5f);
            _lockMarker.sizeDelta = new Vector2(size, size);
            _lockMarker.anchoredPosition = ScreenToCanvas(new Vector2(screen.x, screen.y));
        }

        public void HideLockMarker()
        {
            if (_lockMarker != null) _lockMarker.gameObject.SetActive(false);
        }

        /// <summary>Outline the menu element the cursor is resting on.</summary>
        public void ShowHover(Rect screenRect)
        {
            if (_hoverBox == null) return;
            if (!_config.UiSnap.HighlightTarget && !_config.UiSnap.HideCursor)
            {
                _hoverBox.gameObject.SetActive(false);
                return;
            }

            _hoverBox.gameObject.SetActive(true);
            _hoverBox.anchorMin = Vector2.zero;
            _hoverBox.anchorMax = Vector2.zero;
            _hoverBox.pivot = new Vector2(0.5f, 0.5f);

            float scale = CanvasScale();
            _hoverBox.sizeDelta = new Vector2(screenRect.width / scale + 6f, screenRect.height / scale + 6f);
            _hoverBox.anchoredPosition = ScreenToCanvas(screenRect.center);
        }

        public void HideHover()
        {
            if (_hoverBox != null) _hoverBox.gameObject.SetActive(false);
        }

        private float CanvasScale()
        {
            return _canvas == null || _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;
        }

        private Vector2 ScreenToCanvas(Vector2 screenPoint) => screenPoint / CanvasScale();

        // ------------------------------------------------------------------

        /// <summary>
        /// Generate a rounded-rectangle sprite with 9-slice borders, so pills stay round
        /// at any width. <paramref name="filled"/> false produces an outline frame.
        /// </summary>
        private static Sprite BuildRoundedSprite(int size, int radius, bool filled)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            const float thickness = 3f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to the rounded-rect edge.
                    float dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (!filled)
                    {
                        float inner = Mathf.Clamp01(radius - distance - thickness + 0.5f);
                        alpha = Mathf.Clamp01(alpha - inner);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            int border = radius + 2;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }
    }
}
