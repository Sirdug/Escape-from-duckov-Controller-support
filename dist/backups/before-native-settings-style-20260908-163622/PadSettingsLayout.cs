using System.Collections.Generic;
using System.Linq;
using Duckov.Options;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovPad
{
    /// <summary>One controller page, using the game's scroll view and font with explicit row geometry.</summary>
    internal static class PadSettingsLayout
    {
        private static readonly Color RowColor = new Color(0.075f, 0.15f, 0.21f, 0.96f);
        private static readonly Color Accent = new Color(0.35f, 0.86f, 0.92f, 1f);

        public static void Build(Transform container, PadConfig config, TMP_FontAsset font)
        {
            // The cloned horizontal row layouts used to overwrite widget anchors. All new
            // rows own their geometry; only the scroll content controls their vertical position.
            foreach (var layout in container.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            var list = container.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 6f;
            list.padding = new RectOffset(8, 8, 8, 12);
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            var fitter = container.GetComponent<ContentSizeFitter>() ?? container.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var help = Row(container, "ControllerHelp", 96f);
            var status = help.AddComponent<PadControllerStatus>();
            status.Label = Text(help.transform, font, "", 0.015f, 0.985f, 19f);
            status.Config = config;

            foreach (var group in PadOptions.All.GroupBy(o => o.Section))
            {
                var section = Section(container, font, group.Key, group.Count(), group.Key == "Aiming");
                if (group.Key == "Steam Deck & precision")
                {
                    var setup = Row(container, "SteamDeckSetup", 116f);
                    Text(setup.transform, font,
                        "Steam Input: use Gamepad with Mouse Trackpad. Optional gyro: Mouse, enabled while holding L2.\n" +
                        "Rear buttons: L4 = F7, R4 = F8, L5 = F9, R5 = F10. Remap them below.\n" +
                        "Touchscreen uses the game's mouse UI. Steam + X opens the Steam keyboard.",
                        0.015f, 0.985f, 19f);
                    section.Add(setup);
                }
                foreach (var option in group)
                {
                    var row = Row(container, "Row_" + option.Key);
                    Text(row.transform, font, option.Label, 0.015f, 0.51f);
                    var built = row.AddComponent<PadBuiltSlider>();
                    built.Config = config;
                    built.Option = option;
                    built.Kind = option.Kind == OptionKind.Slider ? BuiltKind.Slider
                        : option.Kind == OptionKind.Toggle ? BuiltKind.Toggle : BuiltKind.Choice;

                    if (option.Kind == OptionKind.Slider)
                    {
                        built.ValueText = Text(row.transform, font, "", 0.87f, 0.985f);
                        built.ValueText.alignment = TextAlignmentOptions.MidlineRight;
                        AddSlider(row.transform, option, config, built);
                    }
                    else
                    {
                        var control = Area(row.transform, "Change", 0.55f, 0.985f, 7f);
                        var button = Button(control);
                        built.ValueText = Text(control.transform, font, "", 0.03f, 0.97f);
                        built.ValueText.alignment = TextAlignmentOptions.Center;
                        if (option.Kind == OptionKind.Toggle)
                            button.onClick.AddListener(() =>
                            {
                                bool value = !OptionsManager.Load(option.Key, option.GetBool(config));
                                OptionsManager.Save(option.Key, value);
                                built.Refresh();
                            });
                        else button.onClick.AddListener(built.CycleChoice);
                    }
                    built.Refresh();
                    section.Add(row);
                }
            }

            var bindHelp = Row(container, "BindingHelp", 64f);
            Text(bindHelp.transform, font,
                "BUTTON MAPPING\nSelect a binding, then press a button or combo. The capture row shows how to cancel; Delete clears.",
                0.015f, 0.985f, 19f);
            PadBindingRow.CancelAllSilent();
            foreach (var group in PadBindings.All.GroupBy(b => b.Section))
            {
                var section = Section(container, font,
                    group.Key == "Defaults" ? "Restore button defaults" : "Button mapping: " + group.Key,
                    group.Count(), false);
                foreach (var binding in group)
                {
                    var row = Row(container, "BindRow_" + (binding.Field ?? "Reset"));
                    Text(row.transform, font, binding.Label, 0.015f, 0.51f);
                    var control = Area(row.transform, "Rebind", 0.55f, 0.985f, 7f);
                    var button = Button(control);
                    var value = Text(control.transform, font, "", 0.02f, 0.98f);
                    value.alignment = TextAlignmentOptions.Center;
                    var capture = row.AddComponent<PadBindingRow>();
                    capture.Bind(config, binding, value);
                    button.onClick.AddListener(capture.OnRowClicked);
                    section.Add(row);
                }
            }
        }

        private static PadSettingsSection Section(Transform parent, TMP_FontAsset font, string title, int count, bool open)
        {
            var header = Row(parent, "Section_" + title, 48f);
            var button = Button(header);
            var label = Text(header.transform, font, "", 0.015f, 0.985f);
            label.color = Accent;
            var section = header.AddComponent<PadSettingsSection>();
            section.Initialize(label, title + "  (" + count + ")", open);
            button.onClick.AddListener(section.Toggle);
            return section;
        }

        private static GameObject Row(Transform parent, string name, float height = 52f)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            var image = row.GetComponent<Image>();
            image.color = RowColor;
            image.raycastTarget = false;
            return row;
        }

        private static GameObject Area(Transform parent, string name, float left, float right, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(0f, inset);
            rect.offsetMax = new Vector2(0f, -inset);
            return go;
        }

        private static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string text,
            float left, float right, float size = 22f)
        {
            var label = Area(parent, "Label", left, right, 4f).AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.color = new Color(0.93f, 0.96f, 0.98f, 1f);
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = size;
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(GameObject go)
        {
            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.27f, 0.35f, 1f);
            colors.highlightedColor = new Color(0.20f, 0.43f, 0.52f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.12f, 0.52f, 0.60f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            return button;
        }

        private static void AddSlider(Transform parent, PadOption option, PadConfig config, PadBuiltSlider built)
        {
            var area = Area(parent, "Slider", 0.55f, 0.85f, 8f);
            var background = area.AddComponent<Image>();
            background.color = new Color(0.16f, 0.28f, 0.35f, 1f);
            var fill = Area(area.transform, "Fill", 0f, 1f, 10f).AddComponent<Image>();
            fill.color = Accent;
            fill.raycastTarget = false;
            var handle = Area(area.transform, "Handle", 0f, 0f, 3f).AddComponent<Image>();
            handle.color = Color.white;
            handle.raycastTarget = false;
            handle.rectTransform.sizeDelta = new Vector2(12f, -6f);
            var slider = area.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = option.Min;
            slider.maxValue = option.Max;
            slider.SetValueWithoutNotify(OptionsManager.Load(option.Key, option.GetFloat(config)));
            slider.onValueChanged.AddListener(value =>
            {
                OptionsManager.Save(option.Key, value);
                built.Refresh();
            });
        }
    }

    internal sealed class PadControllerStatus : MonoBehaviour
    {
        public TextMeshProUGUI Label;
        public PadConfig Config;
        private void Update()
        {
            if (Label == null || Config == null) return;
            string value = "Controller: " + ControllerDevice.Name + "\n" + ControllerDevice.Source + "\n" +
                ControllerDevice.FormatBinding(Config.Buttons.UiClick) + ": select / change   |   Left / right: adjust   |   Right stick: scroll\n" +
                "Open a section to customize. Changes save automatically.";
            if (Label.text != value) Label.text = value;
        }
    }

    internal sealed class PadSettingsSection : MonoBehaviour
    {
        private readonly List<GameObject> _rows = new List<GameObject>();
        private TextMeshProUGUI _label;
        private string _title;
        private bool _open;

        public void Initialize(TextMeshProUGUI label, string title, bool open)
        {
            _label = label;
            _title = title;
            _open = open;
            Refresh();
        }

        public void Add(GameObject row)
        {
            _rows.Add(row);
            row.SetActive(_open);
        }

        public void Toggle()
        {
            PadBindingRow.CancelAllSilent();
            _open = !_open;
            foreach (var row in _rows) row.SetActive(_open);
            Refresh();
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
        }

        private void Refresh() => _label.text = (_open ? "−  " : "+  ") + _title;
    }
}
