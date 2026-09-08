using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Options;

namespace DuckovPad
{
    /// <summary>One remappable controller action on the Controls tab.</summary>
    internal sealed class PadBinding
    {
        /// <summary>Name of the field on <see cref="PadConfig.Bindings"/>. Null for the reset row.</summary>
        public string Field;
        public string Label;
        public string Section;
        public bool IsReset;
    }

    /// <summary>
    /// The remappable subset of <see cref="PadConfig.Bindings"/>, persisted through the
    /// game's own options store like every other setting (so restarts keep them) and
    /// mirrored back to Settings.json by the existing dirty-save flow.
    ///
    /// The shared <c>Modifier</c> is deliberately not listed: chords name it literally
    /// ("LB+DpadUp"), so changing it alone would silently break the whole LB layer.
    /// </summary>
    internal static class PadBindings
    {
        public const string Prefix = "DuckovPad_Bind_";

        public static readonly List<PadBinding> All = new List<PadBinding>
        {
            new PadBinding { Section = "Steam Deck back buttons", Field = "DeckSprint", Label = "Sprint (extra button)" },
            new PadBinding { Section = "Steam Deck back buttons", Field = "DeckReload", Label = "Reload (extra button)" },
            new PadBinding { Section = "Steam Deck back buttons", Field = "DeckInteract", Label = "Interact (extra button)" },
            new PadBinding { Section = "Steam Deck back buttons", Field = "DeckLockOn", Label = "Lock on (extra button)" },
            // ---------------- On foot ----------------
            new PadBinding { Section = "On foot", Field = "Fire", Label = "Fire" },
            new PadBinding { Section = "On foot", Field = "Ads", Label = "Aim down sights" },
            new PadBinding { Section = "On foot", Field = "Sprint", Label = "Sprint" },
            new PadBinding { Section = "On foot", Field = "Dash", Label = "Dash" },
            new PadBinding { Section = "On foot", Field = "Reload", Label = "Reload" },
            new PadBinding { Section = "On foot", Field = "Interact", Label = "Interact" },
            new PadBinding { Section = "On foot", Field = "PutAway", Label = "Put away" },
            new PadBinding { Section = "On foot", Field = "SwitchWeapon", Label = "Swap primary / secondary" },
            new PadBinding { Section = "On foot", Field = "MeleeWeapon", Label = "Melee weapon" },
            new PadBinding { Section = "On foot", Field = "CharacterSkill", Label = "Character skill (hold, release to use)" },
            new PadBinding { Section = "On foot", Field = "LockOn", Label = "Lock on / release lock" },
            new PadBinding { Section = "On foot", Field = "NightVision", Label = "Night vision" },
            new PadBinding { Section = "On foot", Field = "ToggleView", Label = "Toggle camera view" },
            new PadBinding { Section = "On foot", Field = "Quack", Label = "Quack" },
            new PadBinding { Section = "On foot", Field = "StopAction", Label = "Stop action" },
            new PadBinding { Section = "On foot", Field = "PadMenu", Label = "Mod settings overlay" },

            // ---------------- Quick items & menus ----------------
            new PadBinding { Section = "Items & menus", Field = "QuickItem3", Label = "Quick-use item slot 3" },
            new PadBinding { Section = "Items & menus", Field = "QuickItem4", Label = "Quick-use item slot 4" },
            new PadBinding { Section = "Items & menus", Field = "QuickItem5", Label = "Quick-use item slot 5" },
            new PadBinding { Section = "Items & menus", Field = "QuickItem6", Label = "Quick-use item slot 6" },
            new PadBinding { Section = "Items & menus", Field = "Inventory", Label = "Inventory / stash" },
            new PadBinding { Section = "Items & menus", Field = "Map", Label = "Map" },
            new PadBinding { Section = "Items & menus", Field = "PauseMenu", Label = "Pause menu" },
            new PadBinding { Section = "Items & menus", Field = "QuestLog", Label = "Quest log" },
            new PadBinding { Section = "Items & menus", Field = "CycleNext", Label = "Cycle next (scroll up)" },
            new PadBinding { Section = "Items & menus", Field = "CyclePrevious", Label = "Cycle previous (scroll down)" },
            new PadBinding { Section = "Items & menus", Field = "ShortcutPrevious", Label = "Previous weapon slot" },
            new PadBinding { Section = "Items & menus", Field = "ShortcutNext", Label = "Next weapon slot" },

            // ---------------- In menus ----------------
            new PadBinding { Section = "In menus", Field = "UiClick", Label = "Select / grab / place" },
            new PadBinding { Section = "In menus", Field = "UiContext", Label = "Item actions" },
            new PadBinding { Section = "In menus", Field = "UiBack", Label = "Back / cancel" },
            new PadBinding { Section = "In menus", Field = "UiQuickMove", Label = "Quick move" },
            new PadBinding { Section = "In menus", Field = "UiLockSort", Label = "Lock / unlock sort" },
            new PadBinding { Section = "In menus", Field = "UiDrop", Label = "Drop focused item" },
            new PadBinding { Section = "In menus", Field = "UiUse", Label = "Use focused item" },
            new PadBinding { Section = "In menus", Field = "UiMark", Label = "Wishlist mark focused item" },
            new PadBinding { Section = "In menus", Field = "UiPrecision", Label = "Precision cursor (hold)" },
            new PadBinding { Section = "In menus", Field = "UiPageNext", Label = "Next page" },
            new PadBinding { Section = "In menus", Field = "UiPagePrevious", Label = "Previous page" },
            new PadBinding { Section = "In menus", Field = "UiClose", Label = "Close view" },
            new PadBinding { Section = "In menus", Field = "UiRotate", Label = "Rotate (build mode)" },

            // ---------------- Navigate ----------------
            new PadBinding { Section = "Navigate", Field = "UiNavUp", Label = "Navigate up" },
            new PadBinding { Section = "Navigate", Field = "UiNavDown", Label = "Navigate down" },
            new PadBinding { Section = "Navigate", Field = "UiNavLeft", Label = "Navigate left" },
            new PadBinding { Section = "Navigate", Field = "UiNavRight", Label = "Navigate right" },

            new PadBinding { Section = "Defaults", Label = "Reset all bindings", IsReset = true },
        };

        public static string KeyFor(PadBinding binding) => Prefix + binding.Field;

        private static FieldInfo FieldFor(string field)
        {
            return typeof(PadConfig.Bindings).GetField(field, BindingFlags.Public | BindingFlags.Instance);
        }

        public static string Get(PadConfig config, PadBinding binding)
        {
            if (binding == null || binding.IsReset || config?.Buttons == null) return string.Empty;
            var field = FieldFor(binding.Field);
            if (field == null) return string.Empty;
            return field.GetValue(config.Buttons) as string ?? string.Empty;
        }

        /// <summary>
        /// Assign a binding: clear the same chord anywhere else so two actions can never
        /// share one button, persist, and re-resolve the pad tables.
        /// </summary>
        public static void Set(PadConfig config, PadBinding binding, string value)
        {
            if (binding == null || binding.IsReset || config?.Buttons == null) return;
            var field = FieldFor(binding.Field);
            if (field == null || field.FieldType != typeof(string)) return;

            value = (value ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(value)) ClearDuplicates(config, binding.Field, value);
            field.SetValue(config.Buttons, value);
            OptionsManager.Save(KeyFor(binding), value);
            Pad.Configure(config);
            Log.Info("Binding " + binding.Label + " set to " + (string.IsNullOrEmpty(value) ? "(unbound)" : value) + ".");
        }

        private static void ClearDuplicates(PadConfig config, string exceptField, string value)
        {
            foreach (var other in All)
            {
                if (other.IsReset || string.Equals(other.Field, exceptField, StringComparison.Ordinal)) continue;
                var field = FieldFor(other.Field);
                if (field == null) continue;
                var current = field.GetValue(config.Buttons) as string;
                if (string.Equals((current ?? string.Empty).Trim(), value, StringComparison.OrdinalIgnoreCase))
                {
                    field.SetValue(config.Buttons, string.Empty);
                    OptionsManager.Save(Prefix + other.Field, string.Empty);
                    Log.Info("Binding " + other.Label + " unbound (duplicate of the new assignment).");
                }
            }
        }

        public static void ResetAll(PadConfig config)
        {
            if (config?.Buttons == null) return;
            var defaults = new PadConfig.Bindings();
            foreach (var binding in All)
            {
                if (binding.IsReset) continue;
                var field = FieldFor(binding.Field);
                var fallback = field?.GetValue(defaults) as string;
                if (field == null) continue;
                field.SetValue(config.Buttons, fallback ?? string.Empty);
                OptionsManager.Save(Prefix + binding.Field, fallback ?? string.Empty);
            }
            Pad.Configure(config);
            Log.Info("Controller bindings reset to defaults.");
        }

        /// <summary>Stored values win over Settings.json, same as the slider options.</summary>
        public static void LoadInto(PadConfig config)
        {
            if (config?.Buttons == null) return;
            foreach (var binding in All)
            {
                if (binding.IsReset) continue;
                try
                {
                    var field = FieldFor(binding.Field);
                    if (field == null) continue;
                    var current = field.GetValue(config.Buttons) as string ?? string.Empty;
                    field.SetValue(config.Buttons, OptionsManager.Load(KeyFor(binding), current));
                }
                catch (Exception e)
                {
                    Log.Warn("Could not load binding " + binding.Field + ": " + e.Message);
                }
            }
        }

        /// <summary>Re-read a single key after it changed in the options store.</summary>
        public static bool Apply(PadConfig config, string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            if (config?.Buttons == null) return false;

            string fieldName = key.Substring(Prefix.Length);
            foreach (var binding in All)
            {
                if (binding.IsReset || binding.Field != fieldName) continue;
                try
                {
                    var field = FieldFor(binding.Field);
                    if (field == null) return false;
                    var current = field.GetValue(config.Buttons) as string ?? string.Empty;
                    field.SetValue(config.Buttons, OptionsManager.Load(key, current));
                }
                catch (Exception e)
                {
                    Log.Warn("Could not apply binding " + key + ": " + e.Message);
                }
                return true;
            }
            return false;
        }
    }
}
