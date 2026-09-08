using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Duckov.Options;
using Duckov.Options.UI;
using Duckov.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuckovPad
{
    /// <summary>
    /// Adds a "Controller" tab to the game's own Options panel.
    ///
    /// Nothing here draws any UI. Instead it clones the game's existing option rows —
    /// slider, toggle and dropdown — retargets their storage keys at ours, and drops them
    /// into a cloned tab page. The result uses the game's fonts, spacing, colours,
    /// localisation and save file, and the pad cursor snapping works on it for free.
    ///
    /// Clones are built while parented to an inactive holder so their Awake doesn't run
    /// until after the keys have been rewritten.
    /// </summary>
    internal sealed class NativeOptionsUi
    {
        private readonly PadConfig _config;
        private GameObject _page;
        private GameObject _bindPage;
        private OptionsPanel_TabButton _tabButton;
        private OptionsPanel_TabButton _bindTabButton;
        private bool _built;
        private int _failedAttempts;

        public bool Built => _built;

        public NativeOptionsUi(PadConfig config)
        {
            _config = config;
        }

        public void Invalidate()
        {
            _built = false;
            _page = null;
            _bindPage = null;
            _tabButton = null;
            _bindTabButton = null;
            PadBindingRow.CancelAllSilent();
        }

        /// <summary>Try to attach the tab. Safe to call repeatedly; does nothing once built.</summary>
        public void TryBuild()
        {
            if (_built || _failedAttempts > 12) return;

            OptionsPanel panel = null;
            try
            {
                panel = UnityEngine.Object.FindObjectOfType<OptionsPanel>(true);
            }
            catch (Exception)
            {
                // Older Unity overload; fall through to the single-argument version.
                panel = UnityEngine.Object.FindObjectOfType<OptionsPanel>();
            }

            if (panel == null) return; // options panel not created yet

            try
            {
                Build(panel);
                _built = true;
                Log.Info("Controller options tab added to the game's options panel.");
            }
            catch (Exception e)
            {
                _failedAttempts++;
                Log.Warn("Could not add the Controller options tab (attempt " + _failedAttempts + "): " + e.Message);
                if (_failedAttempts > 12)
                    Log.Warn("Giving up on the options tab — Settings.json still controls everything.");
            }
        }

        // ------------------------------------------------------------------

        private void Build(OptionsPanel panel)
        {
            var tabsField = typeof(OptionsPanel).GetField("tabButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tabsField == null) throw new Exception("OptionsPanel.tabButtons not found");

            var tabButtons = tabsField.GetValue(panel) as List<OptionsPanel_TabButton>;
            if (tabButtons == null || tabButtons.Count == 0) throw new Exception("no existing tabs to clone");

            var tabField = typeof(OptionsPanel_TabButton).GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic);
            var indicatorField = typeof(OptionsPanel_TabButton).GetField("selectedIndicator", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tabField == null) throw new Exception("OptionsPanel_TabButton.tab not found");

            // Pick the tab that has the most option rows — the richest template.
            OptionsPanel_TabButton source = null;
            GameObject sourcePage = null;
            int bestScore = -1;

            foreach (var button in tabButtons)
            {
                if (button == null) continue;
                var page = tabField.GetValue(button) as GameObject;
                if (page == null) continue;

                int score = page.GetComponentsInChildren<OptionsUIEntry_Slider>(true).Length * 2
                            + page.GetComponentsInChildren<OptionsUIEntry_Toggle>(true).Length * 2
                            + page.GetComponentsInChildren<OptionsUIEntry_Dropdown>(true).Length;

                if (score > bestScore)
                {
                    bestScore = score;
                    source = button;
                    sourcePage = page;
                }
            }

            if (sourcePage == null) throw new Exception("no tab page found to clone");

            // The panel can persist across scenes while we rebuild on every scene load,
            // so remove our previous clones first. Otherwise tabs accumulate.
            RemoveStaleClones(tabButtons);

            // Everything is assembled under an inactive holder so no Awake runs early.
            var holder = new GameObject("DuckovPadOptionsHolder");
            holder.transform.SetParent(sourcePage.transform.parent, false);
            holder.SetActive(false);

            _page = BuildValuePage(holder, sourcePage, "Tab_DuckovPad");
            _bindPage = BuildBindingPage(holder, sourcePage, "Tab_DuckovPadBinds");

            // Move the finished pages next to the other tabs and drop the holder.
            _page.transform.SetParent(sourcePage.transform.parent, false);
            _page.SetActive(false);
            _bindPage.transform.SetParent(sourcePage.transform.parent, false);
            _bindPage.SetActive(false);
            UnityEngine.Object.Destroy(holder);

            // Clone the tab button itself, once per page.
            _tabButton = BuildTabButton(panel, source, "Tab_DuckovPad_Button", "Controller", _page,
                tabField, indicatorField);
            _bindTabButton = BuildTabButton(panel, source, "Tab_DuckovPadBinds_Button", "Controls", _bindPage,
                tabField, indicatorField);

            tabButtons.Add(_tabButton);
            tabButtons.Add(_bindTabButton);

            // Make sure our pages start hidden and matches the current selection.
            // The selection may point at a clone we just removed.
            var current = panel.GetSelection();
            if (current == null && tabButtons.Count > 0) panel.SetSelection(tabButtons[0]);
            else if (current != null) panel.SetSelection(current);
        }

        /// <summary>
        /// Drop tab buttons and pages left by an earlier build on the same panel object.
        /// </summary>
        private static void RemoveStaleClones(List<OptionsPanel_TabButton> tabButtons)
        {
            try
            {
                for (int i = tabButtons.Count - 1; i >= 0; i--)
                {
                    var button = tabButtons[i];
                    if (button == null) { tabButtons.RemoveAt(i); continue; }
                    if (!button.name.StartsWith("Tab_DuckovPad", StringComparison.Ordinal)) continue;
                    tabButtons.RemoveAt(i);
                    UnityEngine.Object.Destroy(button.gameObject);
                }

                foreach (var transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                {
                    if (transform == null) continue;
                    if (transform.name != "Tab_DuckovPad" && transform.name != "Tab_DuckovPadBinds"
                        && transform.name != "DuckovPadOptionsHolder") continue;
                    UnityEngine.Object.Destroy(transform.gameObject);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Stale tab cleanup failed: " + e.Message);
            }
        }

        private GameObject BuildValuePage(GameObject holder, GameObject sourcePage, string name)
        {
            var page = UnityEngine.Object.Instantiate(sourcePage, holder.transform);
            page.name = name;

            var templates = ExtractTemplates(page, out var container);
            if (container == null) throw new Exception("could not find the row container");

            BuildRows(templates, container);
            return page;
        }

        private GameObject BuildBindingPage(GameObject holder, GameObject sourcePage, string name)
        {
            var page = UnityEngine.Object.Instantiate(sourcePage, holder.transform);
            page.name = name;

            var templates = ExtractTemplates(page, out var container);
            if (container == null) throw new Exception("could not find the row container");

            BuildBindingRows(templates, container);
            return page;
        }

        private OptionsPanel_TabButton BuildTabButton(OptionsPanel panel, OptionsPanel_TabButton source,
            string name, string label, GameObject page, FieldInfo tabField, FieldInfo indicatorField)
        {
            var buttonClone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
            buttonClone.name = name;
            buttonClone.transform.SetSiblingIndex(source.transform.parent.childCount - 1);

            var tabButton = buttonClone.GetComponent<OptionsPanel_TabButton>();
            if (tabButton == null) throw new Exception("cloned tab button lost its component");

            tabField.SetValue(tabButton, page);

            // The clone's indicator points at the original's child; re-point it at our own.
            if (indicatorField != null)
            {
                var originalIndicator = indicatorField.GetValue(source) as GameObject;
                if (originalIndicator != null)
                {
                    var path = GetPath(originalIndicator.transform, source.transform);
                    var mine = string.IsNullOrEmpty(path) ? null : buttonClone.transform.Find(path);
                    if (mine != null) indicatorField.SetValue(tabButton, mine.gameObject);
                }
            }

            SetTabLabel(source.gameObject, buttonClone, label);

            tabButton.onClicked = null;
            tabButton.onClicked += (button, data) =>
            {
                data?.Use();
                panel.SetSelection(button);
                RefreshLabels();
            };
            return tabButton;
        }

        /// <summary>Take one of each row type out of the cloned page, then clear it.</summary>
        private Dictionary<OptionKind, GameObject> ExtractTemplates(GameObject page, out Transform container)
        {
            var templates = new Dictionary<OptionKind, GameObject>();
            container = null;

            var slider = page.GetComponentsInChildren<OptionsUIEntry_Slider>(true).FirstOrDefault();
            var toggle = page.GetComponentsInChildren<OptionsUIEntry_Toggle>(true).FirstOrDefault();
            var dropdown = page.GetComponentsInChildren<OptionsUIEntry_Dropdown>(true).FirstOrDefault();

            if (slider != null)
            {
                templates[OptionKind.Slider] = slider.gameObject;
                container = slider.transform.parent;
            }
            if (toggle != null)
            {
                templates[OptionKind.Toggle] = toggle.gameObject;
                container ??= toggle.transform.parent;
            }
            if (dropdown != null)
            {
                templates[OptionKind.Choice] = dropdown.gameObject;
                container ??= dropdown.transform.parent;
            }

            if (container == null) return templates;

            // Detach the templates so clearing the container doesn't take them with it.
            foreach (var template in templates.Values)
                template.transform.SetParent(page.transform, false);

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (templates.Values.Any(t => t.transform == child)) continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            return templates;
        }

        private void BuildRows(Dictionary<OptionKind, GameObject> templates, Transform container)
        {
            string section = null;

            foreach (var option in PadOptions.All)
            {
                // Prefer the game's own row, but any template can host a row the mod
                // builds itself — the log showed pages with no slider/toggle rows at
                // all, which used to drop every such setting silently.
                bool native = templates.TryGetValue(option.Kind, out var template) && template != null;
                if (!native)
                {
                    template = AnyTemplate(templates);
                    if (template == null)
                    {
                        Log.Warn("No row template for " + option.Key + "; setting not shown.");
                        continue;
                    }
                    Log.Info("No game " + option.Kind + " row; building " + option.Key + " from a generic row.");
                }

                if (option.Section != section)
                {
                    section = option.Section;
                    AddHeading(container, section, template);
                }

                var row = UnityEngine.Object.Instantiate(template, container, false);
                row.name = "Row_" + option.Key;
                row.SetActive(true);

                try
                {
                    if (native) Configure(row, option);
                    else if (option.Kind == OptionKind.Toggle) ConfigureBuiltSlider(row, option, BuiltKind.Toggle);
                    else if (option.Kind == OptionKind.Choice) ConfigureBuiltChoice(row, option);
                    else ConfigureBuiltSlider(row, option, BuiltKind.Slider);
                }
                catch (Exception e)
                {
                    Log.Warn("Row " + option.Key + " could not be configured: " + e.Message);
                    UnityEngine.Object.DestroyImmediate(row);
                }
            }

            // Templates were parked outside the container; remove them.
            foreach (var template in templates.Values)
            {
                if (template != null) UnityEngine.Object.DestroyImmediate(template);
            }
        }

        private static GameObject AnyTemplate(Dictionary<OptionKind, GameObject> templates)
        {
            if (templates.TryGetValue(OptionKind.Slider, out var template) && template != null) return template;
            if (templates.TryGetValue(OptionKind.Toggle, out template) && template != null) return template;
            if (templates.TryGetValue(OptionKind.Choice, out template) && template != null) return template;
            return null;
        }

        /// <summary>
        /// Build a working slider (or 0/1 toggle slider) from any row template: strip the
        /// game's widgets, keep the label and background, add a draggable slider with a
        /// fill bar plus a numeric readout. The pad adjusts it with left/right through
        /// the existing snap-slider path, like game-native rows.
        /// </summary>
        private void ConfigureBuiltSlider(GameObject row, PadOption option, BuiltKind kind)
        {
            bool isToggle = kind == BuiltKind.Toggle;
            var label = TakeLabel(row);
            var sprite = StealSprite(row);
            StripWidgets(row);
            label.text = option.Label;

            var sliderArea = new GameObject("PadSlider", typeof(RectTransform));
            var areaRect = (RectTransform)sliderArea.transform;
            areaRect.SetParent(row.transform, false);
            areaRect.anchorMin = new Vector2(0.38f, 0f);
            areaRect.anchorMax = new Vector2(0.72f, 1f);
            areaRect.offsetMin = new Vector2(8f, 6f);
            areaRect.offsetMax = new Vector2(-8f, -6f);

            var background = sliderArea.AddComponent<UnityEngine.UI.Image>();
            background.sprite = sprite;
            background.color = sprite != null ? Color.white : new Color(0.16f, 0.22f, 0.3f, 1f);

            var fillObject = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillObject.transform;
            fillRect.SetParent(areaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillObject.AddComponent<UnityEngine.UI.Image>();
            fill.color = new Color(0.25f, 0.85f, 1f, 1f);
            fill.raycastTarget = false;

            var slider = sliderArea.AddComponent<UnityEngine.UI.Slider>();
            slider.targetGraphic = background;
            slider.fillRect = fillRect;
            slider.transition = UnityEngine.UI.Selectable.Transition.None;
            if (isToggle)
            {
                slider.wholeNumbers = true;
                slider.minValue = 0f;
                slider.maxValue = 1f;
            }
            else
            {
                slider.wholeNumbers = false;
                slider.minValue = option.Min;
                slider.maxValue = option.Max;
            }

            var valueText = AddRightText(row, label);

            var built = row.AddComponent<PadBuiltSlider>();
            built.Config = _config;
            built.Option = option;
            built.Kind = kind;
            built.ValueText = valueText;

            float initial = isToggle
                ? (OptionsManager.Load(option.Key, option.GetBool(_config)) ? 1f : 0f)
                : OptionsManager.Load(option.Key, option.GetFloat(_config));
            slider.SetValueWithoutNotify(Mathf.Clamp(initial, slider.minValue, slider.maxValue));
            slider.onValueChanged.AddListener(v =>
            {
                if (isToggle) OptionsManager.Save(option.Key, v > 0.5f);
                else OptionsManager.Save(option.Key, v);
                built.Refresh();
            });
            built.Refresh();
        }

        /// <summary>Choice fallback: full-row button cycling through the options.</summary>
        private void ConfigureBuiltChoice(GameObject row, PadOption option)
        {
            var label = TakeLabel(row);
            StealSprite(row);
            StripWidgets(row);
            label.text = option.Label;

            var valueText = AddRightText(row, label);

            var built = row.AddComponent<PadBuiltSlider>();
            built.Config = _config;
            built.Option = option;
            built.Kind = BuiltKind.Choice;
            built.ValueText = valueText;

            var button = row.AddComponent<UnityEngine.UI.Button>();
            var graphic = row.GetComponent<UnityEngine.UI.Image>()
                ?? row.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (graphic == null) throw new Exception("template has no background graphic for the button");
            button.targetGraphic = graphic;
            button.onClick.AddListener(built.CycleChoice);
            built.Refresh();
        }

        /// <summary>First text that is not part of an input field or dropdown widget.</summary>
        private static TextMeshProUGUI TakeLabel(GameObject row)
        {
            var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            var dropdownObject = dropdown != null ? dropdown.gameObject : null;
            foreach (var text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.GetComponentInParent<TMP_InputField>() != null) continue;
                if (dropdownObject != null && text.transform.IsChildOf(dropdownObject.transform)) continue;
                return text;
            }
            throw new Exception("template has no label");
        }

        private static Sprite StealSprite(GameObject row)
        {
            foreach (var image in row.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.sprite != null) return image.sprite;
            }
            return null;
        }

        /// <summary>Remove game option logic and widgets; layout and background survive.</summary>
        private static void StripWidgets(GameObject row)
        {
            foreach (var entry in row.GetComponentsInChildren<OptionsUIEntry_Slider>(true))
                UnityEngine.Object.DestroyImmediate(entry);
            foreach (var entry in row.GetComponentsInChildren<OptionsUIEntry_Toggle>(true))
                UnityEngine.Object.DestroyImmediate(entry);
            foreach (var entry in row.GetComponentsInChildren<OptionsUIEntry_Dropdown>(true))
                UnityEngine.Object.DestroyImmediate(entry);
            var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown != null) UnityEngine.Object.DestroyImmediate(dropdown.gameObject);
            foreach (var field in row.GetComponentsInChildren<TMP_InputField>(true))
                UnityEngine.Object.DestroyImmediate(field.gameObject);
            foreach (var slider in row.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
                UnityEngine.Object.DestroyImmediate(slider.gameObject);
        }

        private static TextMeshProUGUI AddRightText(GameObject row, TextMeshProUGUI label)
        {
            var value = new GameObject("PadSliderValue", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var rect = (RectTransform)value.transform;
            rect.SetParent(row.transform, false);
            rect.anchorMin = new Vector2(0.72f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            value.font = label.font;
            value.fontSize = label.fontSize;
            value.fontStyle = label.fontStyle;
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.enableWordWrapping = false;
            value.raycastTarget = false;
            return value;
        }

        private void BuildBindingRows(Dictionary<OptionKind, GameObject> templates, Transform container)
        {
            // Any row shape works: the widgets are stripped and replaced by a button.
            if (!templates.TryGetValue(OptionKind.Choice, out var template) || template == null)
                templates.TryGetValue(OptionKind.Slider, out template);
            if (template == null) templates.TryGetValue(OptionKind.Toggle, out template);
            if (template == null) throw new Exception("no row template to clone");

            PadBindingRow.CancelAllSilent();
            string section = null;

            foreach (var binding in PadBindings.All)
            {
                if (binding.Section != section)
                {
                    section = binding.Section;
                    AddHeading(container, section, template);
                }

                var row = UnityEngine.Object.Instantiate(template, container, false);
                row.name = "BindRow_" + (binding.IsReset ? "Reset" : binding.Field);
                row.SetActive(true);

                try
                {
                    ConfigureBinding(row, binding);
                }
                catch (Exception e)
                {
                    Log.Warn("Binding row " + (binding.Field ?? "Reset") + " could not be configured: " + e.Message);
                    UnityEngine.Object.DestroyImmediate(row);
                }
            }

            // Templates were parked outside the container; remove them.
            foreach (var parked in templates.Values)
            {
                if (parked != null) UnityEngine.Object.DestroyImmediate(parked);
            }
        }

        /// <summary>
        /// Turn a cloned option row into a press-to-capture binding row: strip the
        /// game's widgets, keep the label and background, add a value readout and a
        /// full-row button.
        /// </summary>
        private void ConfigureBinding(GameObject row, PadBinding binding)
        {
            var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            var dropdownObject = dropdown != null ? dropdown.gameObject : null;

            // Pick the action label first: the first text that is not part of an input
            // field or the dropdown widget we are about to remove.
            TextMeshProUGUI label = null;
            foreach (var text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.GetComponentInParent<TMP_InputField>() != null) continue;
                if (dropdownObject != null && text.transform.IsChildOf(dropdownObject.transform)) continue;
                label = text;
                break;
            }
            if (label == null) throw new Exception("template has no label");

            // Remove the game's option logic and widgets. Layout components survive.
            foreach (var entry in row.GetComponentsInChildren<OptionsUIEntry_Slider>(true))
                UnityEngine.Object.DestroyImmediate(entry);
            foreach (var entry in row.GetComponentsInChildren<OptionsUIEntry_Toggle>(true))
                UnityEngine.Object.DestroyImmediate(entry);
            foreach (var entry in row.GetComponentsInChildren<OptionsUIEntry_Dropdown>(true))
                UnityEngine.Object.DestroyImmediate(entry);
            if (dropdownObject != null) UnityEngine.Object.DestroyImmediate(dropdownObject);
            foreach (var field in row.GetComponentsInChildren<TMP_InputField>(true))
                UnityEngine.Object.DestroyImmediate(field.gameObject);
            foreach (var slider in row.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
                UnityEngine.Object.DestroyImmediate(slider.gameObject);

            label.text = binding.Label;

            var value = new GameObject("BindingValue", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var rect = (RectTransform)value.transform;
            rect.SetParent(row.transform, false);
            // Right-hand readout; anchor-based so it survives any parent layout.
            rect.anchorMin = new Vector2(0.35f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            value.font = label.font;
            value.fontSize = label.fontSize;
            value.fontStyle = label.fontStyle;
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.enableWordWrapping = false;
            value.raycastTarget = false;

            var button = row.AddComponent<UnityEngine.UI.Button>();
            var graphic = row.GetComponent<UnityEngine.UI.Image>()
                ?? row.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (graphic == null) throw new Exception("template has no background graphic for the button");
            button.targetGraphic = graphic;

            var rowComponent = row.AddComponent<PadBindingRow>();
            button.onClick.AddListener(rowComponent.OnRowClicked);
            rowComponent.Bind(_config, binding, value);
        }

        private void AddHeading(Transform container, string text, GameObject rowTemplate)
        {
            try
            {
                var heading = UnityEngine.Object.Instantiate(rowTemplate, container, false);
                heading.name = "Heading_" + text;

                // Remove only the option logic. Layout components have to survive or the
                // heading collapses inside the page's vertical layout group.
                foreach (var entry in heading.GetComponentsInChildren<OptionsUIEntry_Slider>(true))
                    UnityEngine.Object.DestroyImmediate(entry);
                foreach (var entry in heading.GetComponentsInChildren<OptionsUIEntry_Toggle>(true))
                    UnityEngine.Object.DestroyImmediate(entry);
                foreach (var entry in heading.GetComponentsInChildren<OptionsUIEntry_Dropdown>(true))
                    UnityEngine.Object.DestroyImmediate(entry);

                var labels = heading.GetComponentsInChildren<TextMeshProUGUI>(true);
                bool first = true;
                foreach (var label in labels)
                {
                    if (first)
                    {
                        label.text = text.ToUpperInvariant();
                        label.color = new Color(0.95f, 0.76f, 0.31f, 1f);
                        first = false;
                    }
                    else
                    {
                        label.gameObject.SetActive(false);
                    }
                }

                // Hide any leftover graphics (slider tracks, dropdown arrows).
                foreach (var graphic in heading.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (graphic is TextMeshProUGUI) continue;
                    graphic.gameObject.SetActive(false);
                }

                heading.SetActive(true);
            }
            catch (Exception e)
            {
                Log.Warn("Could not add heading \"" + text + "\": " + e.Message);
            }
        }

        private void Configure(GameObject row, PadOption option)
        {
            switch (option.Kind)
            {
                case OptionKind.Slider:
                    ConfigureSlider(row, option);
                    break;
                case OptionKind.Toggle:
                    ConfigureToggle(row, option);
                    break;
                case OptionKind.Choice:
                    ConfigureChoice(row, option);
                    break;
            }
        }

        private void ConfigureSlider(GameObject row, PadOption option)
        {
            var entry = row.GetComponent<OptionsUIEntry_Slider>();
            if (entry == null) throw new Exception("template is not a slider row");

            var type = typeof(OptionsUIEntry_Slider);
            SetPrivate(type, entry, "key", option.Key);
            SetPrivate(type, entry, "defaultValue", option.GetFloat(_config));
            SetPrivate(type, entry, "valueFormat", option.Format);

            var slider = GetPrivate(type, entry, "slider") as UnityEngine.UI.Slider;
            if (slider != null)
            {
                slider.wholeNumbers = false;
                slider.minValue = option.Min;
                slider.maxValue = option.Max;
            }

            PinLabel(row, type, entry, "label", option.Label);
        }

        private void ConfigureToggle(GameObject row, PadOption option)
        {
            var entry = row.GetComponent<OptionsUIEntry_Toggle>();
            if (entry == null) throw new Exception("template is not a toggle row");

            var type = typeof(OptionsUIEntry_Toggle);
            SetPrivate(type, entry, "key", option.Key);
            SetPrivate(type, entry, "defaultValue", option.GetBool(_config));
            PinLabel(row, type, entry, "label", option.Label);
        }

        private void ConfigureChoice(GameObject row, PadOption option)
        {
            var entry = row.GetComponent<OptionsUIEntry_Dropdown>();
            if (entry == null) throw new Exception("template is not a dropdown row");

            var type = typeof(OptionsUIEntry_Dropdown);
            var provider = row.AddComponent<PadOptionProvider>();
            provider.Bind(_config, option);

            SetPrivate(type, entry, "provider", provider);
            PinLabel(row, type, entry, "label", option.Label);
        }

        /// <summary>
        /// Set the row label and pin it: the game's entries rewrite their label from the
        /// localisation table in Awake, which runs after we configure (and would show a
        /// starred missing key). Our OnEnable runs after theirs on every showing.
        /// </summary>
        private static void PinLabel(GameObject row, Type type, object entry, string fieldName, string text)
        {
            var label = GetPrivate(type, entry, fieldName) as TextMeshProUGUI;
            if (label == null) return;
            label.text = text;
            var pin = row.AddComponent<PadRowLabel>();
            pin.Label = label;
            pin.Text = text;
        }

        private static void SetRowLabel(Type type, object entry, string fieldName, string text)
        {
            if (GetPrivate(type, entry, fieldName) is TextMeshProUGUI label)
                label.text = text;
        }

        /// <summary>
        /// The rows rewrite their labels from the localisation table on Awake and on
        /// language change, so ours are re-applied whenever the tab is opened.
        /// </summary>
        public void RefreshLabels()
        {
            try
            {
                if (_page != null) RefreshPageLabels(_page.transform);
                if (_bindPage != null) RefreshPageLabels(_bindPage.transform);
            }
            catch (Exception e)
            {
                Log.Warn("Label refresh failed: " + e.Message);
            }
        }

        private static void RefreshPageLabels(Transform root)
        {
            foreach (Transform child in AllChildren(root))
            {
                if (child.name.StartsWith("BindRow_", StringComparison.Ordinal))
                {
                    var binding = child.GetComponent<PadBindingRow>();
                    if (binding != null) binding.Refresh();
                    continue;
                }

                if (!child.name.StartsWith("Row_", StringComparison.Ordinal)) continue;

                string key = child.name.Substring(4);
                var option = PadOptions.All.FirstOrDefault(o => o.Key == key);
                if (option == null) continue;

                // Rows the mod built itself have no game entry components.
                var built = child.GetComponent<PadBuiltSlider>();
                if (built != null) { built.Refresh(); continue; }

                var slider = child.GetComponent<OptionsUIEntry_Slider>();
                if (slider != null) { SetRowLabel(typeof(OptionsUIEntry_Slider), slider, "label", option.Label); continue; }

                var toggle = child.GetComponent<OptionsUIEntry_Toggle>();
                if (toggle != null) { SetRowLabel(typeof(OptionsUIEntry_Toggle), toggle, "label", option.Label); continue; }

                var dropdown = child.GetComponent<OptionsUIEntry_Dropdown>();
                if (dropdown != null) SetRowLabel(typeof(OptionsUIEntry_Dropdown), dropdown, "label", option.Label);
            }
        }

        private static IEnumerable<Transform> AllChildren(Transform root)
        {
            foreach (Transform child in root)
            {
                yield return child;
                foreach (var descendant in AllChildren(child))
                    yield return descendant;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Retarget a cloned tab button's visible title. The button may hold several text
        /// objects (icon glyphs, shadow copies), so the source's longest text is treated
        /// as the title and every copy of it in the clone is rewritten.
        /// </summary>
        private static void SetTabLabel(GameObject sourceButton, GameObject buttonClone, string text)
        {
            try
            {
                string sourceLabel = null;
                foreach (var tmp in sourceButton.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (string.IsNullOrEmpty(tmp.text) || tmp.text.Length < 3) continue;
                    if (sourceLabel == null || tmp.text.Length > sourceLabel.Length) sourceLabel = tmp.text;
                }

                if (!string.IsNullOrEmpty(sourceLabel))
                {
                    bool set = false;
                    foreach (var tmp in buttonClone.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (tmp.text == sourceLabel) { tmp.text = text; set = true; }
                    }
                    if (set) return;
                }
            }
            catch (Exception e)
            {
                Log.Warn("Tab label lookup failed: " + e.Message);
            }

            SetLabel(buttonClone, text);
        }

        private static void SetLabel(GameObject go, string text)
        {
            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
        }

        private static string GetPath(Transform target, Transform root)
        {
            if (target == root) return string.Empty;

            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (current == null) return null;
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void SetPrivate(Type type, object instance, string field, object value)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new Exception(type.Name + "." + field + " not found");
            info.SetValue(instance, value);
        }

        private static object GetPrivate(Type type, object instance, string field)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            return info?.GetValue(instance);
        }
    }

    /// <summary>Re-applies a game-template row's label whenever the row is shown.</summary>
    internal sealed class PadRowLabel : MonoBehaviour
    {
        public TextMeshProUGUI Label;
        public string Text;

        private void OnEnable()
        {
            if (Label != null && Text != null) Label.text = Text;
        }
    }

    /// <summary>Feeds a dropdown row from one of our config values.</summary>
    internal sealed class PadOptionProvider : OptionsProviderBase
    {
        private PadConfig _config;
        private PadOption _option;

        public void Bind(PadConfig config, PadOption option)
        {
            _config = config;
            _option = option;
        }

        public override string Key => _option?.Key ?? "DuckovPad_Unbound";

        public override string[] GetOptions() => _option?.Choices ?? new[] { "-" };

        public override string GetCurrentOption()
        {
            if (_option?.Choices == null || _config == null) return "-";
            int index = Mathf.Clamp(OptionsManager.Load(_option.Key, _option.GetChoice(_config)), 0, _option.Choices.Length - 1);
            return _option.Choices[index];
        }

        public override void Set(int index)
        {
            if (_option == null || _config == null) return;
            OptionsManager.Save(_option.Key, index);
            _option.SetChoice(_config, index);
        }
    }
}
