using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DuckovPad
{
    /// <summary>
    /// Thin, allocation-free wrapper over <see cref="Gamepad"/>.
    ///
    /// Handles friendly button aliases, "Modifier+Button" chords, stick deadzones and
    /// response curves, so the rest of the mod can ask plain questions like
    /// <c>Pad.Down(cfg.Buttons.Reload)</c>.
    /// </summary>
    internal static class Pad
    {
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Xbox
                { "A", "buttonSouth" },
                { "B", "buttonEast" },
                { "X", "buttonWest" },
                { "Y", "buttonNorth" },
                { "LB", "leftShoulder" },
                { "RB", "rightShoulder" },
                { "LT", "leftTrigger" },
                { "RT", "rightTrigger" },
                { "L3", "leftStickPress" },
                { "R3", "rightStickPress" },
                { "LS", "leftStickPress" },
                { "RS", "rightStickPress" },
                { "Start", "start" },
                { "Menu", "start" },
                { "Select", "select" },
                { "Back", "select" },
                { "View", "select" },
                { "DpadUp", "dpad/up" },
                { "DpadDown", "dpad/down" },
                { "DpadLeft", "dpad/left" },
                { "DpadRight", "dpad/right" },

                // PlayStation
                { "Cross", "buttonSouth" },
                { "Circle", "buttonEast" },
                { "Square", "buttonWest" },
                { "Triangle", "buttonNorth" },
                { "L1", "leftShoulder" },
                { "R1", "rightShoulder" },
                { "L2", "leftTrigger" },
                { "R2", "rightTrigger" },
                { "Options", "start" },
                { "Share", "select" },
                { "L4", "deckL4" },
                { "R4", "deckR4" },
                { "L5", "deckL5" },
                { "R5", "deckR5" },

                // Nintendo (physical position, not label)
                { "ZL", "leftTrigger" },
                { "ZR", "rightTrigger" },
                { "Plus", "start" },
                { "Minus", "select" },
            };

        /// <summary>Resolved control cache, keyed by the canonical control name.</summary>
        private static readonly Dictionary<string, ButtonControl> ControlCache =
            new Dictionary<string, ButtonControl>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Every modifier combination registered against each main button, so a binding can
        /// tell whether a more specific chord on the same button is also being pressed.
        /// "A" and "LB+A" both register under "buttonSouth", with modifier sets {} and {LB}.
        /// </summary>
        private static readonly Dictionary<string, List<string[]>> ModifierSetsByButton =
            new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] NoModifiers = new string[0];
        private static readonly Dictionary<string, List<string[]>> MenuModifierSets =
            new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> TransitionButtons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _menuContext;

        public static void SetMenuContext(bool menu)
        {
            if (_menuContext == menu) return;
            _menuContext = menu;
            foreach (var name in CaptureTriggerOrder)
                if (Resolve(name)?.isPressed == true) TransitionButtons.Add(name);
        }

        private static Gamepad _gamepad;
        private static string _modifier = "leftShoulder";

        public static Gamepad Current => _gamepad;
        public static bool Available => _gamepad != null;

        /// <summary>True while the configured modifier button is held.</summary>
        public static bool ModifierHeld { get; private set; }

        /// <summary>Time (unscaled) of the most recent gamepad activity of any kind.</summary>
        public static float LastActivityTime { get; private set; }

        public static void ResetActivity()
        {
            LastActivityTime = float.NegativeInfinity;
            ModifierHeld = false;
            foreach (var name in CaptureTriggerOrder)
                if (Resolve(name)?.isPressed == true) TransitionButtons.Add(name);
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// Rebuild the binding tables. Call whenever the config is (re)loaded.
        /// </summary>
        public static void Configure(PadConfig config)
        {
            ControllerDevice.PointerMode = config.Aim.MixedPointer;
            ModifierSetsByButton.Clear();
            MenuModifierSets.Clear();
            ControlCache.Clear();
            _modifier = Canonical(config.Buttons.Modifier);
            if (string.IsNullOrEmpty(_modifier)) _modifier = "leftShoulder";

            foreach (var field in typeof(PadConfig.Bindings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(string)) continue;
                if (field.Name == nameof(PadConfig.Bindings.Modifier)) continue;

                var raw = field.GetValue(config.Buttons) as string;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                Parse(raw, out var modifiers, out var button);
                if (string.IsNullOrEmpty(button)) continue;

                var table = field.Name.StartsWith("Ui", StringComparison.Ordinal) ? MenuModifierSets : ModifierSetsByButton;
                if (!table.TryGetValue(button, out var sets))
                {
                    sets = new List<string[]>();
                    table[button] = sets;
                }

                bool alreadyRegistered = false;
                foreach (var existing in sets)
                {
                    if (SameSet(existing, modifiers)) { alreadyRegistered = true; break; }
                }
                if (!alreadyRegistered) sets.Add(modifiers);
            }
        }

        private static bool SameSet(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            foreach (var item in a)
            {
                bool found = false;
                foreach (var other in b)
                {
                    if (string.Equals(item, other, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>True if <paramref name="candidate"/> contains every entry of <paramref name="subset"/> and more.</summary>
        private static bool IsStrictSuperset(string[] candidate, string[] subset)
        {
            if (candidate.Length <= subset.Length) return false;
            foreach (var item in subset)
            {
                bool found = false;
                foreach (var other in candidate)
                {
                    if (string.Equals(item, other, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>Refresh the cached device and modifier state. Call once at the top of each frame.</summary>
        public static void Poll()
        {
            var next = _gamepad;
            if (next == null || !next.added || !next.enabled) next = null;
            // Select on real input rather than noisy packets or connection order.
            foreach (var candidate in Gamepad.all)
            {
                if (!candidate.added || !candidate.enabled) continue;
                if (next == null) next = candidate;
                if (candidate.wasUpdatedThisFrame && HasActivity(candidate) &&
                    (next == null || candidate.lastUpdateTime >= next.lastUpdateTime || !HasActivity(next)))
                    next = candidate;
            }
            if (next != _gamepad)
            {
                if (_gamepad != null) { try { _gamepad.SetMotorSpeeds(0, 0); } catch (Exception) { } }
                _gamepad = next;
                TransitionButtons.Clear();
                LastActivityTime = float.NegativeInfinity;
                InvalidateCache();
                ControllerDevice.Invalidate();
            }
            ControllerDevice.Poll(_gamepad);
            TransitionButtons.RemoveWhere(name => Resolve(name)?.isPressed != true);
            if (_gamepad == null)
            {
                ModifierHeld = false;
                return;
            }
            if (ExtraButtonDown()) LastActivityTime = Time.unscaledTime;

            var mod = Resolve(_modifier);
            ModifierHeld = mod != null && mod.isPressed;

            if (_gamepad.lastUpdateTime > 0d)
            {
                // Stick drift must not count as activity: small resting deflections
                // (up to ~0.25) are ignored so a worn stick can't steal focus from
                // the mouse or keep menus awake. Intentional pushes still register.
                if (LeftStickRaw.sqrMagnitude > 0.0625f ||
                    RightStickRaw.sqrMagnitude > 0.0625f ||
                    _gamepad.wasUpdatedThisFrame && AnyButtonPressed())
                {
                    LastActivityTime = Time.unscaledTime;
                }
            }
        }

        private static bool AnyButtonPressed()
        {
            var g = _gamepad;
            if (g == null) return false;
            return g.buttonSouth.isPressed || g.buttonEast.isPressed || g.buttonWest.isPressed ||
                   g.buttonNorth.isPressed || g.leftShoulder.isPressed || g.rightShoulder.isPressed ||
                   g.leftTrigger.isPressed || g.rightTrigger.isPressed ||
                   g.leftStickButton.isPressed || g.rightStickButton.isPressed ||
                   g.startButton.isPressed || g.selectButton.isPressed ||
                   g.dpad.up.isPressed || g.dpad.down.isPressed ||
                   g.dpad.left.isPressed || g.dpad.right.isPressed;
        }

        private static bool HasActivity(Gamepad g)
        {
            if (g.leftStick.ReadUnprocessedValue().sqrMagnitude > 0.09f ||
                g.rightStick.ReadUnprocessedValue().sqrMagnitude > 0.09f) return true;
            foreach (var control in g.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            return false;
        }

        // ----------------------------- queries -----------------------------

        /// <summary>True on the frame the binding is first satisfied.</summary>
        public static bool Down(string binding) => Evaluate(binding, EdgeMode.Down);

        /// <summary>True for as long as the binding is satisfied.</summary>
        public static bool Held(string binding) => Evaluate(binding, EdgeMode.Held);

        /// <summary>True on the frame the binding stops being satisfied.</summary>
        public static bool Up(string binding) => Evaluate(binding, EdgeMode.Up);

        /// <summary>
        /// True on the frame any gamepad button is first pressed. Used by gates that
        /// must never strand the player (title / loading continue), where demanding a
        /// specific button risks ignoring the one the player actually pressed.
        /// </summary>
        public static bool AnyButtonDown()
        {
            if (_gamepad == null) return false;
            return ExtraButtonDown() || WasPressed("buttonSouth") || WasPressed("buttonEast")
                || WasPressed("buttonWest") || WasPressed("buttonNorth")
                || WasPressed("leftShoulder") || WasPressed("rightShoulder")
                || WasPressed("leftTrigger") || WasPressed("rightTrigger")
                || WasPressed("leftStickPress") || WasPressed("rightStickPress")
                || WasPressed("start") || WasPressed("select")
                || WasPressed("dpad/up") || WasPressed("dpad/down")
                || WasPressed("dpad/left") || WasPressed("dpad/right");
        }

        private static bool WasPressed(string canonicalName)
        {
            var control = Resolve(canonicalName);
            return control != null && control.wasPressedThisFrame;
        }

        // ----------------------------- capture -----------------------------

        /// <summary>All button controls, trigger-friendly first for capture priority.</summary>
        private static readonly string[] CaptureTriggerOrder =
        {
            "deckL4", "deckR4", "deckL5", "deckR5",
            "buttonSouth", "buttonEast", "buttonWest", "buttonNorth",
            "dpad/up", "dpad/down", "dpad/left", "dpad/right",
            "start", "select",
            "leftStickPress", "rightStickPress",
            "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger",
        };

        /// <summary>Same controls in modifier-first order for chord display.</summary>
        private static readonly string[] CaptureModifierOrder =
        {
            "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger",
            "leftStickPress", "rightStickPress", "start", "select",
            "dpad/up", "dpad/down", "dpad/left", "dpad/right",
            "buttonSouth", "buttonEast", "buttonWest", "buttonNorth",
        };

        /// <summary>Canonical control name back to the friendly Xbox-style name.</summary>
        private static readonly Dictionary<string, string> FriendlyNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "deckL4", "L4" }, { "deckR4", "R4" }, { "deckL5", "L5" }, { "deckR5", "R5" },
                { "buttonSouth", "A" },
                { "buttonEast", "B" },
                { "buttonWest", "X" },
                { "buttonNorth", "Y" },
                { "leftShoulder", "LB" },
                { "rightShoulder", "RB" },
                { "leftTrigger", "LT" },
                { "rightTrigger", "RT" },
                { "leftStickPress", "L3" },
                { "rightStickPress", "R3" },
                { "start", "Start" },
                { "select", "Select" },
                { "dpad/up", "DpadUp" },
                { "dpad/down", "DpadDown" },
                { "dpad/left", "DpadLeft" },
                { "dpad/right", "DpadRight" },
            };

        public static string FriendlyName(string canonicalName)
        {
            if (string.IsNullOrEmpty(canonicalName)) return string.Empty;
            return FriendlyNames.TryGetValue(canonicalName, out var friendly) ? friendly : canonicalName;
        }

        /// <summary>
        /// If any button went down this frame, describe the press as a binding string
        /// ("X", "LB+Y"): the newly pressed button is the trigger, anything else held
        /// at the same time becomes a modifier. Null when nothing new was pressed.
        /// </summary>
        public static string CapturePress()
        {
            if (_gamepad == null) return null;

            string trigger = null;
            foreach (var name in CaptureTriggerOrder)
            {
                var control = Resolve(name);
                if (control != null && control.wasPressedThisFrame) { trigger = name; break; }
            }
            if (trigger == null) return null;

            var parts = new List<string>();
            foreach (var name in CaptureModifierOrder)
            {
                if (string.Equals(name, trigger, StringComparison.OrdinalIgnoreCase)) continue;
                var control = Resolve(name);
                if (control != null && control.isPressed) parts.Add(FriendlyName(name));
            }
            parts.Add(FriendlyName(trigger));
            return string.Join("+", parts);
        }

        private enum EdgeMode { Down, Held, Up }

        private static bool Evaluate(string binding, EdgeMode mode)
        {
            if (_gamepad == null || string.IsNullOrWhiteSpace(binding)) return false;

            Parse(binding, out var modifiers, out var buttonName);
            if (string.IsNullOrEmpty(buttonName) || TransitionButtons.Contains(buttonName)) return false;

            // Every listed modifier must be held.
            foreach (var modifier in modifiers)
            {
                var control = Resolve(modifier);
                if (control == null || !control.isPressed) return false;
            }

            // A more specific chord on the same button wins. "A" stays quiet while
            // "LB+A" is being pressed, and "LB+Start" stays quiet during "LB+RB+Start".
            if ((_menuContext ? MenuModifierSets : ModifierSetsByButton).TryGetValue(buttonName, out var sets))
            {
                foreach (var other in sets)
                {
                    if (!IsStrictSuperset(other, modifiers)) continue;
                    if (AllHeld(other)) return false;
                }
            }

            var button = Resolve(buttonName);
            if (button == null) return false;

            switch (mode)
            {
                case EdgeMode.Down: return button.wasPressedThisFrame;
                case EdgeMode.Up: return button.wasReleasedThisFrame;
                default: return button.isPressed;
            }
        }

        private static bool AllHeld(string[] controls)
        {
            foreach (var name in controls)
            {
                var control = Resolve(name);
                if (control == null || !control.isPressed) return false;
            }
            return true;
        }

        /// <summary>Analog value of a binding's button (triggers report 0..1, digital buttons 0 or 1).</summary>
        public static float Value(string binding)
        {
            if (_gamepad == null || string.IsNullOrWhiteSpace(binding)) return 0f;
            Parse(binding, out _, out var buttonName);
            if (string.IsNullOrEmpty(buttonName)) return 0f;
            var control = Resolve(buttonName);
            return control?.ReadValue() ?? 0f;
        }

        // ----------------------------- sticks -----------------------------

        public static Vector2 LeftStick(float deadzone, float outerDeadzone, float curve)
            => Shape(LeftStickRaw, deadzone, outerDeadzone, curve);

        public static Vector2 RightStick(float deadzone, float outerDeadzone, float curve)
            => Shape(RightStickRaw, deadzone, outerDeadzone, curve);

        public static Vector2 LeftStickRaw => _gamepad?.leftStick.ReadUnprocessedValue() ?? Vector2.zero;
        public static Vector2 RightStickRaw => _gamepad?.rightStick.ReadUnprocessedValue() ?? Vector2.zero;

        /// <summary>
        /// Radial deadzone + response curve. Preserves direction exactly; only the
        /// magnitude is remapped, so diagonals are not clipped to a square.
        /// </summary>
        public static Vector2 Shape(Vector2 raw, float deadzone, float outerDeadzone, float curve)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= deadzone) return Vector2.zero;

            Vector2 direction = raw / magnitude;
            float normalized = AimMath.ShapeMagnitude(magnitude, deadzone, outerDeadzone, curve);

            return direction * normalized;
        }

        // ----------------------------- helpers -----------------------------

        /// <summary>
        /// Split "LB+RB+Start" into modifiers {leftShoulder, rightShoulder} and button "start".
        /// A bare name yields no modifiers.
        /// </summary>
        private static void Parse(string binding, out string[] modifiers, out string button)
        {
            modifiers = NoModifiers;
            button = null;
            if (string.IsNullOrWhiteSpace(binding)) return;

            if (binding.IndexOf('+') < 0)
            {
                button = Canonical(binding);
                return;
            }

            var parts = binding.Split('+');
            int count = 0;
            for (int i = 0; i < parts.Length; i++)
                if (!string.IsNullOrWhiteSpace(parts[i])) count++;

            if (count == 0) return;
            if (count == 1)
            {
                foreach (var part in parts)
                    if (!string.IsNullOrWhiteSpace(part)) button = Canonical(part);
                return;
            }

            var result = new string[count - 1];
            int index = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                var canonical = Canonical(parts[i]);
                if (index < result.Length) result[index++] = canonical;
                else button = canonical;
            }

            modifiers = result;
        }

        /// <summary>Resolve a friendly alias ("A", "LB") to its Input System control name.</summary>
        public static string CanonicalName(string name) => Canonical(name);

        private static string Canonical(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();
            return Aliases.TryGetValue(name, out var mapped) ? mapped : name;
        }

        private static ButtonControl Resolve(string canonicalName)
        {
            if (_gamepad == null || string.IsNullOrEmpty(canonicalName)) return null;
            // Steam Input's legacy gamepad has no extra grip channels. Reserved keyboard
            // outputs allow all four grips to be independently remapped by this mod.
            var keyboard = Keyboard.current;
            switch (canonicalName)
            {
                case "deckL4": return keyboard?.f7Key;
                case "deckR4": return keyboard?.f8Key;
                case "deckL5": return keyboard?.f9Key;
                case "deckR5": return keyboard?.f10Key;
            }

            if (ControlCache.TryGetValue(canonicalName, out var cached))
            {
                // Cache is invalidated when the device changes.
                if (cached != null && cached.device == _gamepad) return cached;
                ControlCache.Remove(canonicalName);
            }

            ButtonControl control = null;
            try
            {
                control = _gamepad.TryGetChildControl<ButtonControl>(canonicalName);
            }
            catch (Exception)
            {
                // Unknown control name in the config; treated as unbound below.
            }

            if (control == null)
            {
                Log.Warn("Unknown gamepad button \"" + canonicalName + "\" in Settings.json; that binding is ignored.");
                return null;
            }

            ControlCache[canonicalName] = control;
            return control;
        }

        public static bool ExtraButtonDown() => _gamepad != null &&
            (WasPressed("deckL4") || WasPressed("deckR4") || WasPressed("deckL5") || WasPressed("deckR5"));

        /// <summary>Drop cached controls, e.g. after a controller is swapped.</summary>
        public static void InvalidateCache() => ControlCache.Clear();
    }
}
