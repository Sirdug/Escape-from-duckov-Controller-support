using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Duckov.Options.UI;
using Duckov.UI;
using TMPro;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Adds a "Controller" tab to the game's own Options panel.
    ///
    /// Reuses the game's scroll page and font. Controller rows use explicit geometry
    /// and expandable sections, with options and bindings together on a single page.
    ///
    /// Clones are built while parented to an inactive holder so their Awake doesn't run
    /// until after the keys have been rewritten.
    /// </summary>
    internal sealed class NativeOptionsUi
    {
        private readonly PadConfig _config;
        private GameObject _page;
        private OptionsPanel_TabButton _tabButton;
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
            _tabButton = null;
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
                if (button == null || button.name.StartsWith("Tab_DuckovPad", StringComparison.Ordinal)) continue;
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

            // Move the finished pages next to the other tabs and drop the holder.
            _page.transform.SetParent(sourcePage.transform.parent, false);
            _page.SetActive(false);
            UnityEngine.Object.Destroy(holder);

            // One tab contains both controller preferences and button mapping.
            _tabButton = BuildTabButton(panel, source, "Tab_DuckovPad_Button", "Controller", _page,
                tabField, indicatorField);

            tabButtons.Add(_tabButton);

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

            var font = TakeLabel(AnyTemplate(templates)).font;
            PadSettingsLayout.Build(container, _config, font);
            foreach (var template in templates.Values)
                if (template != null) UnityEngine.Object.DestroyImmediate(template);
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

        private static GameObject AnyTemplate(Dictionary<OptionKind, GameObject> templates)
        {
            return templates.Values.FirstOrDefault(t => t != null);
        }

        private static TextMeshProUGUI TakeLabel(GameObject row)
        {
            if (row == null) throw new Exception("no row template found");
            return row.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault()
                ?? throw new Exception("template has no font label");
        }

        public void RefreshLabels()
        {
            try
            {
                if (_page != null) RefreshPageLabels(_page.transform);
            }
            catch (Exception e)
            {
                Log.Warn("Label refresh failed: " + e.Message);
            }
        }

        private static void RefreshPageLabels(Transform root)
        {
            foreach (var binding in root.GetComponentsInChildren<PadBindingRow>(true)) binding.Refresh();
            foreach (var option in root.GetComponentsInChildren<PadBuiltSlider>(true)) option.Refresh();
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
                        if (tmp.text == sourceLabel)
                        {
                            tmp.text = text;
                            var pin = tmp.gameObject.AddComponent<PadRowLabel>();
                            pin.Label = tmp;
                            pin.Text = text;
                            set = true;
                        }
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
            if (label == null) return;
            label.text = text;
            var pin = label.gameObject.AddComponent<PadRowLabel>();
            pin.Label = label;
            pin.Text = text;
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

        private void LateUpdate()
        {
            if (Label != null && Text != null && Label.text != Text) Label.text = Text;
        }
    }

}
