using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Duckov.Options;
using Duckov.Options.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DuckovPad
{
    /// <summary>Uses the actual option prefabs, artwork and layout from the game's settings.</summary>
    internal sealed class PadSettingsLayout
    {
        private readonly PadConfig _config;
        private readonly Dictionary<OptionKind, GameObject> _templates;
        private readonly Transform _container;

        private PadSettingsLayout(Transform container, PadConfig config, Dictionary<OptionKind, GameObject> templates)
        {
            _container = container;
            _config = config;
            _templates = templates;
        }

        public static void Build(Transform container, PadConfig config, Dictionary<OptionKind, GameObject> templates)
            => new PadSettingsLayout(container, config, templates).BuildPage();

        /// <summary>Every setting sits under a heading, so its row is indented one step.</summary>
        private const string Indent = "   ";

        private void BuildPage()
        {
            // Retain the game's content spacing and padding as well as each row's layout.
            var statusRow = ActionRow("ControllerStatus", "Active controller", out var statusValue, out var statusButton);
            Object.DestroyImmediate(statusButton);
            var status = statusRow.AddComponent<PadControllerStatus>();
            status.Label = statusValue;
            status.Help = Note("How this page works", "", 3);
            status.Config = _config;

            foreach (var group in PadOptions.All.GroupBy(o => o.Section))
            {
                var section = Section(group.Key, "setting", group.Key == "Aiming");
                if (group.Key == "Steam Deck & precision")
                {
                    Note("Steam Deck setup",
                        "Steam Input: Gamepad with Mouse Trackpad. Optional gyro: Mouse while holding L2.\n" +
                        "Rear buttons: L4 = F7, R4 = F8, L5 = F9, R5 = F10.\n" +
                        "Steam + X opens the keyboard.", 3, out var noteRow);
                    section.AddNote(noteRow);
                }
                if (group.Key == "Troubleshooting")
                {
                    status.Details = Note("Controller detection", ControllerDevice.Source, 2, out var detailsRow);
                    section.AddNote(detailsRow);
                }
                foreach (var option in group) section.Add(OptionRow(option));
            }

            Note("Button mapping",
                "Open a heading below and press the action you want to change, then press the button\n" +
                "or combo to assign it. B cancels, Delete clears.", 3);
            PadBindingRow.CancelAllSilent();
            foreach (var group in PadBindings.All.Where(b => !b.IsReset).GroupBy(b => b.Section))
            {
                var section = Section(group.Key, "button", false);
                foreach (var binding in group) section.Add(BindingRow(binding, Indent));
            }

            // A single row, so a heading of its own would only be one more thing to open.
            foreach (var binding in PadBindings.All.Where(b => b.IsReset)) BindingRow(binding, string.Empty);
        }

        private GameObject BindingRow(PadBinding binding, string indent)
        {
            var row = ActionRow("BindRow_" + (binding.Field ?? "Reset"), indent + binding.Label,
                out var value, out var button);
            var capture = row.AddComponent<PadBindingRow>();
            capture.Bind(_config, binding, value);
            button.onClick.AddListener(capture.OnRowClicked);
            return row;
        }

        private GameObject OptionRow(PadOption option)
        {
            // A template must come from the matching native entry, not an arbitrary Image.
            if ((option.Kind == OptionKind.Slider || option.Kind == OptionKind.Toggle) &&
                _templates.TryGetValue(option.Kind, out var template))
            {
                var row = Object.Instantiate(template, _container, false);
                row.name = "Row_" + option.Key;
                Component entry;
                if (option.Kind == OptionKind.Slider)
                {
                    entry = row.GetComponent<OptionsUIEntry_Slider>();
                    Set(entry, "defaultValue", option.GetFloat(_config));
                    Set(entry, "valueFormat", option.Format);
                    var slider = (Slider)Get(entry, "slider");
                    slider.onValueChanged = new Slider.SliderEvent();
                    slider.wholeNumbers = false;
                    slider.minValue = option.Min;
                    slider.maxValue = option.Max;
                    var field = (TMP_InputField)Get(entry, "valueField");
                    field.onEndEdit = new TMP_InputField.SubmitEvent();
                }
                else
                {
                    entry = row.GetComponent<OptionsUIEntry_Toggle>();
                    Set(entry, "defaultValue", option.GetBool(_config));
                    var toggle = (Slider)Get(entry, "toggle");
                    toggle.onValueChanged = new Slider.SliderEvent();
                }
                Set(entry, "key", option.Key);
                Pin(row, (TextMeshProUGUI)Get(entry, "label"), Indent + option.Label);
                row.SetActive(true);
                return row;
            }

            // Builds without a particular slider/switch prefab still use a real game
            // dropdown. Numeric dropdowns preserve custom current values between steps.
            var choice = CloneChoice("Row_" + option.Key, out var native, out var dropdown, out var label);
            RemoveProviders(choice);
            var provider = choice.AddComponent<PadNativeOptionProvider>();
            provider.Config = _config;
            provider.Option = option;
            Set(native, "provider", provider);
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            Pin(choice, label, Indent + option.Label);
            choice.SetActive(true);
            return choice;
        }

        private GameObject CloneChoice(string name, out OptionsUIEntry_Dropdown entry,
            out TMP_Dropdown dropdown, out TextMeshProUGUI label)
        {
            if (!_templates.TryGetValue(OptionKind.Choice, out var template))
                throw new InvalidOperationException("The game's dropdown row template was not found.");
            var row = Object.Instantiate(template, _container, false);
            row.name = name;
            entry = row.GetComponent<OptionsUIEntry_Dropdown>();
            dropdown = (TMP_Dropdown)Get(entry, "dropdown");
            label = (TextMeshProUGUI)Get(entry, "label");
            if (dropdown == null || label == null || dropdown.captionText == null)
                throw new InvalidOperationException("The game's dropdown row is missing its label or widget.");
            return row;
        }

        private GameObject ActionRow(string name, string title, out TextMeshProUGUI value, out Button button)
        {
            var row = CloneChoice(name, out var entry, out var dropdown, out var label);
            RemoveProviders(row);
            Object.DestroyImmediate(entry);
            Pin(row, label, title);
            value = dropdown.captionText as TextMeshProUGUI
                ?? throw new InvalidOperationException("The game's dropdown caption is not a canvas text label.");
            value.enableWordWrapping = true;
            value.enableAutoSizing = true;
            value.fontSizeMax = value.fontSize;
            value.fontSizeMin = value.fontSize * 0.65f;

            var widget = dropdown.gameObject;
            var graphic = dropdown.targetGraphic;
            var colors = dropdown.colors;
            var sprites = dropdown.spriteState;
            var transition = dropdown.transition;
            var triggers = dropdown.animationTriggers;
            var navigation = dropdown.navigation;
            if (dropdown.template != null) Object.DestroyImmediate(dropdown.template.gameObject);
            foreach (var image in widget.GetComponentsInChildren<Image>(true))
                if (image.name.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0)
                    image.gameObject.SetActive(false);
            Object.DestroyImmediate(dropdown);
            button = widget.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.colors = colors;
            button.spriteState = sprites;
            button.transition = transition;
            button.animationTriggers = triggers;
            button.navigation = navigation;
            // No copied callbacks may point back to a vanilla graphics setting.
            button.onClick = new Button.ButtonClickedEvent();
            row.SetActive(true);
            return row;
        }

        /// <summary>
        /// A collapsible heading. Headings are deliberately unlike the rows beneath them:
        /// upper case, accented, and marked [+] / [-], so no heading reads as a setting.
        /// </summary>
        private PadSettingsSection Section(string title, string noun, bool open)
        {
            var row = ActionRow("Section_" + title, title, out var value, out var button);
            value.color = PadGui.TextDim;
            var label = row.GetComponent<PadRowLabel>();
            if (label != null && label.Label != null) label.Label.color = PadGui.Accent;
            var section = row.AddComponent<PadSettingsSection>();
            section.Initialize(label, value, title, noun, open);
            button.onClick.AddListener(section.Toggle);
            return section;
        }

        private TextMeshProUGUI Note(string title, string text, int lines)
            => Note(title, text, lines, out _);

        private TextMeshProUGUI Note(string title, string text, int lines, out GameObject row)
        {
            row = ActionRow("Note_" + title, title, out var value, out var button);
            var widget = (RectTransform)button.transform;
            Object.DestroyImmediate(button);
            value.text = text;
            // Help text is not something you can change, so it must not read like a value.
            value.color = PadGui.TextDim;
            var rect = (RectTransform)row.transform;
            float height = Mathf.Max(LayoutUtility.GetPreferredHeight(rect), rect.rect.height);
            if (height <= 0) height = value.fontSize * 1.8f;
            var layout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            layout.minHeight = height * lines;
            layout.preferredHeight = height * lines;
            // Some native widgets have a fixed preferred height. Grow the note widget
            // too so its caption can wrap without overflowing a one-line dropdown box.
            var widgetLayout = widget.GetComponent<LayoutElement>() ?? widget.gameObject.AddComponent<LayoutElement>();
            widgetLayout.minHeight = height * lines;
            widgetLayout.preferredHeight = height * lines;
            widget.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height * lines);
            return value;
        }

        private static void RemoveProviders(GameObject row)
        {
            foreach (var provider in row.GetComponentsInChildren<OptionsProviderBase>(true))
                Object.DestroyImmediate(provider);
        }

        private static void Pin(GameObject row, TextMeshProUGUI label, string text)
        {
            label.text = text;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * 0.65f;
            var pin = row.AddComponent<PadRowLabel>();
            pin.Label = label;
            pin.Text = text;
        }

        private static object Get(Component entry, string field) => entry.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(entry);

        private static void Set(Component entry, string field, object value)
        {
            var info = entry.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException(entry.GetType().Name + "." + field + " was not found.");
            info.SetValue(entry, value);
        }
    }

    internal sealed class PadNativeOptionProvider : OptionsProviderBase
    {
        public PadConfig Config;
        public PadOption Option;
        public override string Key => Option?.Key ?? "DuckovPad_Unbound";

        private List<float> FloatValues()
        {
            var values = new List<float>();
            for (int i = 0; i <= 20; i++) values.Add(Mathf.Lerp(Option.Min, Option.Max, i / 20f));
            float current = OptionsManager.Load(Option.Key, Option.GetFloat(Config));
            // Put the saved value first when two values have the same displayed rounding.
            values.Insert(0, current);
            return values.GroupBy(v => v.ToString(Option.Format)).Select(g => g.First()).OrderBy(v => v).ToList();
        }

        public override string[] GetOptions()
        {
            if (Option == null) return new[] { "-" };
            if (Option.Kind == OptionKind.Toggle) return new[] { "Off", "On" };
            if (Option.Kind == OptionKind.Slider) return FloatValues().Select(v => v.ToString(Option.Format)).ToArray();
            return Option.Choices;
        }

        public override string GetCurrentOption()
        {
            if (Option == null || Config == null) return "-";
            if (Option.Kind == OptionKind.Toggle)
                return OptionsManager.Load(Option.Key, Option.GetBool(Config)) ? "On" : "Off";
            if (Option.Kind == OptionKind.Slider)
                return OptionsManager.Load(Option.Key, Option.GetFloat(Config)).ToString(Option.Format);
            int index = Mathf.Clamp(OptionsManager.Load(Option.Key, Option.GetChoice(Config)), 0, Option.Choices.Length - 1);
            return Option.Choices[index];
        }

        public override void Set(int index)
        {
            if (Option == null || Config == null || index < 0) return;
            if (Option.Kind == OptionKind.Toggle) OptionsManager.Save(Option.Key, index != 0);
            else if (Option.Kind == OptionKind.Slider)
            {
                var values = FloatValues();
                if (index < values.Count) OptionsManager.Save(Option.Key, values[index]);
            }
            else if (index < Option.Choices.Length) OptionsManager.Save(Option.Key, index);
        }
    }

    internal sealed class PadControllerStatus : MonoBehaviour
    {
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Help;
        public TextMeshProUGUI Details;
        public PadConfig Config;
        private void Update()
        {
            if (Label == null || Config == null) return;
            if (Label.text != ControllerDevice.Name) Label.text = ControllerDevice.Name;
            if (Details != null && Details.text != ControllerDevice.Source) Details.text = ControllerDevice.Source;
            string help = "[+] opens a heading, [-] closes it.  " +
                ControllerDevice.FormatBinding(Config.Buttons.UiClick) + ": select or change a setting.\n" +
                "Left / right adjusts sliders. Right stick scrolls. Changes save as you make them.";
            if (Help != null && Help.text != help) Help.text = help;
        }
    }

    /// <summary>
    /// A collapsible heading row. The caption on the right counts what the section holds
    /// rather than reading "Show" / "Hide", which was indistinguishable from a setting's
    /// own value; open or closed is shown by the [+] / [-] marker on the heading itself.
    /// </summary>
    internal sealed class PadSettingsSection : MonoBehaviour
    {
        private readonly List<GameObject> _rows = new List<GameObject>();
        private TextMeshProUGUI _value;
        private PadRowLabel _label;
        private string _title = string.Empty;
        private string _noun = "setting";
        private int _count;
        private bool _open;

        public void Initialize(PadRowLabel label, TextMeshProUGUI value, string title, string noun, bool open)
        {
            _label = label;
            _value = value;
            _title = title ?? string.Empty;
            _noun = noun ?? "setting";
            _open = open;
            Refresh();
        }

        /// <summary>A row the heading counts.</summary>
        public void Add(GameObject row)
        {
            _count++;
            AddNote(row);
        }

        /// <summary>Help text that opens with the section but is not one of its settings.</summary>
        public void AddNote(GameObject row)
        {
            _rows.Add(row);
            row.SetActive(_open);
            Refresh();
        }

        public void Toggle()
        {
            PadBindingRow.CancelAllSilent();
            _open = !_open;
            foreach (var row in _rows) row.SetActive(_open);
            Refresh();
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
        }

        private void Refresh()
        {
            string heading = (_open ? "[-]  " : "[+]  ") + _title.ToUpperInvariant();
            // PadRowLabel re-applies its own copy every frame, so both have to change.
            if (_label != null)
            {
                _label.Text = heading;
                if (_label.Label != null) _label.Label.text = heading;
            }
            if (_value != null)
                _value.text = _count == 1 ? "1 " + _noun : _count + " " + _noun + "s";
        }
    }
}
