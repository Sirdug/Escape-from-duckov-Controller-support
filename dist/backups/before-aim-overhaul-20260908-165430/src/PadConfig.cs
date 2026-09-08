using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// User-editable configuration, loaded from Settings.json next to the mod DLL.
    /// Every field has a working default, so a missing or partial file is fine.
    /// </summary>
    public class PadConfig
    {
        public bool Enabled = true;

        /// <summary>Keyboard key that toggles gamepad control on/off at runtime.</summary>
        public string ToggleKey = "F5";

        public AimSettings Aim = new AimSettings();
        public AimAssistSettings AimAssist = new AimAssistSettings();
        public AimSnapSettings AimSnap = new AimSnapSettings();
        public MoveSettings Move = new MoveSettings();
        public CursorSettings Cursor = new CursorSettings();
        public UiSnapSettings UiSnap = new UiSnapSettings();
        public HintSettings Hints = new HintSettings();
        public RumbleSettings Rumble = new RumbleSettings();
        public DebugSettings Debug = new DebugSettings();
        public Bindings Buttons = new Bindings();

        public class AimSettings
        {
            /// <summary>"easy" (and legacy "absolute") = screen-aligned directional aim.
            /// "classic" = old world-basis twin-stick; "relative" = free cursor.</summary>
            public string Mode = "easy";
            public float AdsPrecision = 0.65f;
            public float PointerSensitivity = 1f;
            /// <summary>0: automatic for Steam Deck / Steam Controller; 1: on; 2: off.</summary>
            public int MixedPointer = 0;

            /// <summary>World-space distance of the reticle from the duck at full deflection.</summary>
            public float ReticleDistance = 11f;

            /// <summary>World-space distance at the edge of the deadzone.</summary>
            public float MinReticleDistance = 4.5f;

            /// <summary>Stick values below this are ignored.</summary>
            public float Deadzone = 0.20f;

            /// <summary>Stick values above this count as full deflection.</summary>
            public float OuterDeadzone = 0.95f;

            /// <summary>Response curve exponent applied to stick magnitude. 1 = linear, higher = finer near centre.</summary>
            public float ResponseCurve = 1.5f;

            /// <summary>Reticle distance multiplier while aiming down sights.</summary>
            public float AdsDistanceMultiplier = 1.35f;

            /// <summary>How quickly the reticle catches up to the stick, in units/sec. 0 = instant.</summary>
            public float Smoothing = 28f;

            /// <summary>Degrees/second for "relative" mode.</summary>
            public float RelativeSensitivity = 260f;

            /// <summary>Extra sensitivity multiplier while ADS in relative mode.</summary>
            public float RelativeAdsMultiplier = 0.55f;
        }

        public class AimAssistSettings
        {
            public bool Enabled = true;

            /// <summary>
            /// How far the reticle is pulled from where the stick points toward the target:
            /// 0 = no help, 0.5 = halfway, 1 = fully on the target.
            /// </summary>
            public float Strength = 0.6f;

            /// <summary>Only assist targets within this angle of where you are already pointing.</summary>
            public float MaxAngleDegrees = 22f;

            /// <summary>Only assist targets within this world distance of the duck.</summary>
            public float MaxDistance = 32f;

            /// <summary>Skip targets with a wall between them and the duck.</summary>
            public bool RequireLineOfSight = true;

            /// <summary>Extra degrees of tolerance for the target already being tracked,
            /// so aim doesn't flick between enemies standing near each other.</summary>
            public float StickinessDegrees = 6f;

            /// <summary>Only assist while the trigger or ADS is held.</summary>
            public bool OnlyWhileFiringOrAds = false;

            /// <summary>How quickly assist fades in and out, in units/sec. Higher = snappier.</summary>
            public float EaseSpeed = 10f;

            /// <summary>Fraction of the cone over which assist tapers off at the edge,
            /// so targets don't pop in and out at the boundary.</summary>
            public float EdgeTaper = 0.3f;
        }

        public class AimSnapSettings
        {
            /// <summary>Hard lock-on: aim tracks a chosen enemy until you break it.</summary>
            public bool Enabled = true;

            /// <summary>"toggle" = press to lock, press again to release. "hold" = hold the button.</summary>
            public string Mode = "toggle";

            /// <summary>Wider than aim assist — this is a deliberate action.</summary>
            public float MaxAngleDegrees = 70f;
            public float MaxDistance = 40f;
            public bool RequireLineOfSight = true;

            /// <summary>Instantly snap onto the best target the moment you pull the trigger.</summary>
            public bool SnapOnFire = false;

            /// <summary>Break the lock by pushing the right stick hard away from the target.</summary>
            public bool BreakOnStickInput = true;

            /// <summary>Angle between stick direction and the locked target that breaks the lock.</summary>
            public float BreakAngleDegrees = 75f;

            /// <summary>Draw a marker over the locked target.</summary>
            public bool ShowMarker = true;
        }

        public class UiSnapSettings
        {
            public bool StickNavigation = true;
            public bool HideCursor = true;

            /// <summary>D-pad jumps the cursor between buttons, slots and inventory items.</summary>
            public bool DirectionalSnap = true;

            /// <summary>The free cursor is gently pulled toward the nearest interactive element.</summary>
            public bool Magnetism = true;

            /// <summary>0 = no pull, 1 = the cursor sticks hard to element centres.</summary>
            public float MagnetStrength = 0.35f;

            /// <summary>Only elements whose centre is within this many pixels attract the cursor (1080p reference).</summary>
            public float MagnetRadius = 46f;

            /// <summary>Highlight the element the cursor is currently over.</summary>
            public bool HighlightTarget = true;

            /// <summary>Widen the directional search cone; higher accepts more off-axis targets.</summary>
            public float SnapConeDegrees = 65f;

            /// <summary>How often the list of on-screen elements is rebuilt, in seconds.</summary>
            public float RescanInterval = 0.25f;
        }

        public class HintSettings
        {
            /// <summary>Show a contextual button-prompt bar along the bottom of the screen.</summary>
            public bool Enabled = true;

            /// <summary>Scale of the hint bar and its glyphs.</summary>
            public float Scale = 1f;

            /// <summary>Distance from the bottom of the screen, in pixels.</summary>
            public float BottomMargin = 28f;

            /// <summary>Use PlayStation letters (Cross/Circle) instead of Xbox (A/B) on the glyphs.</summary>
            public bool PlayStationLabels = false;
        }

        public class DebugSettings
        {
            /// <summary>On-screen readout of gamepad state, aim assist targets and lock-on.</summary>
            public bool ShowOverlay = false;
        }

        public class MoveSettings
        {
            public float Deadzone = 0.18f;
            public float OuterDeadzone = 0.95f;
            public float ResponseCurve = 1.0f;

            /// <summary>Push the stick past this to sprint without holding the sprint button.</summary>
            public bool AutoRunAtFullStick = true;
            public float AutoRunThreshold = 0.92f;
        }

        public class CursorSettings
        {
            /// <summary>Virtual cursor speed in pixels/sec at full deflection (1080p reference).</summary>
            public float Speed = 1500f;

            /// <summary>Exponent on stick magnitude; higher = more precision near centre.</summary>
            public float ResponseCurve = 2.0f;

            public float Deadzone = 0.18f;

            /// <summary>Multiplier while the precision modifier button is held.</summary>
            public float PrecisionMultiplier = 0.35f;

            /// <summary>Scroll wheel notches per second at full right-stick deflection.</summary>
            public float ScrollSpeed = 12f;

            /// <summary>Raw scroll units the Input System reports per wheel notch.
            /// 120 matches Windows/Linux desktop builds; lower this if menus scroll too fast.</summary>
            public float ScrollUnitsPerNotch = 120f;

            /// <summary>Scale cursor speed with screen height so it feels the same at any resolution.</summary>
            public bool ScaleWithResolution = true;
        }

        public class RumbleSettings
        {
            public bool Enabled = true;

            /// <summary>Global multiplier for all rumble. 0 disables.</summary>
            public float Scale = 1.0f;

            public float ShootLow = 0.16f;
            public float ShootHigh = 0.32f;
            public float ShootDuration = 0.07f;

            public float HurtLow = 0.55f;
            public float HurtHigh = 0.45f;
            public float HurtDuration = 0.22f;
        }

        /// <summary>
        /// Button names use Unity Input System gamepad control names, with friendly aliases:
        /// A/B/X/Y, LB/RB, LT/RT, L3/R3, Start/Select, DpadUp/DpadDown/DpadLeft/DpadRight.
        /// Prefix with "Mod+" (e.g. "LB+DpadUp") to require the modifier button.
        /// Leave empty to unbind.
        /// </summary>
        public class Bindings
        {
            public string DeckSprint = "L4";
            public string DeckReload = "R4";
            public string DeckInteract = "L5";
            public string DeckLockOn = "R5";
            /// <summary>Held to reach the alternate layer (quick items, page nav, pause).</summary>
            public string Modifier = "LB";

            // --- Gameplay ---
            public string Fire = "RT";
            public string Ads = "LT";
            public string Sprint = "L3";
            public string Dash = "B";
            public string Reload = "X";
            public string Interact = "A";
            public string PutAway = "LB+A";
            public string SwitchWeapon = "Y";
            public string MeleeWeapon = "LB+Y";
            public string CharacterSkill = "RB";
            /// <summary>Lock aim onto an enemy.</summary>
            public string LockOn = "R3";
            public string NightVision = "LB+L3";
            public string ToggleView = "LB+R3";
            public string Quack = "LB+B";
            public string StopAction = "LB+X";

            /// <summary>Opens DuckovPad's own settings overlay. Works anywhere, including menus.</summary>
            public string PadMenu = "LB+RB+Start";

            public string Inventory = "Start";
            public string Map = "Select";
            public string PauseMenu = "LB+Start";
            public string QuestLog = "LB+Select";

            // Scroll-equivalent: cycles weapons, or ammo/interaction target
            // depending on the game's own scroll-wheel behaviour setting.
            public string CycleNext = "DpadUp";
            public string CyclePrevious = "DpadDown";
            public string ShortcutPrevious = "DpadLeft";
            public string ShortcutNext = "DpadRight";

            // Quick-use item slots 3..6, reached with the modifier held.
            public string QuickItem3 = "LB+DpadUp";
            public string QuickItem4 = "LB+DpadRight";
            public string QuickItem5 = "LB+DpadDown";
            public string QuickItem6 = "LB+DpadLeft";

            // --- Menus / inventory ---
            public string UiClick = "A";
            public string UiContext = "X";
            public string UiBack = "B";
            public string UiQuickMove = "Y";
            public string UiLockSort = "L3";
            public string UiDrop = "";
            /// <summary>Direct use on the focused item. Same rules as the keyboard use key.</summary>
            public string UiUse = "";
            /// <summary>Wishlist mark/unmark on the focused item. Same toggle as the N key.</summary>
            public string UiMark = "";
            public string UiDragModifier = "";
            public string UiPrecision = "LT";
            public string UiPageNext = "RB";
            public string UiPagePrevious = "LB";
            public string UiRotate = "LB";
            public string UiClose = "Start";

            // D-pad jumps the cursor between buttons and inventory slots.
            public string UiNavUp = "DpadUp";
            public string UiNavDown = "DpadDown";
            public string UiNavLeft = "DpadLeft";
            public string UiNavRight = "DpadRight";
        }

        // ---------------------------------------------------------------

        public static PadConfig Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    var fresh = new PadConfig();
                    fresh.Save(path);
                    Log.Info("No Settings.json found; wrote defaults to " + path);
                    return fresh;
                }

                var json = File.ReadAllText(path);
                var cfg = JsonConvert.DeserializeObject<PadConfig>(json);
                if (cfg == null)
                {
                    Log.Warn("Settings.json was empty; using defaults.");
                    return new PadConfig();
                }

                cfg.Aim ??= new AimSettings();
                cfg.AimAssist ??= new AimAssistSettings();
                cfg.Move ??= new MoveSettings();
                cfg.Cursor ??= new CursorSettings();
                cfg.Rumble ??= new RumbleSettings();
                cfg.AimSnap ??= new AimSnapSettings();
                cfg.UiSnap ??= new UiSnapSettings();
                cfg.Hints ??= new HintSettings();
                cfg.Debug ??= new DebugSettings();
                cfg.Buttons ??= new Bindings();
                // Upgrade only the old conflicting defaults; retain custom bindings.
                bool upgraded = false;
                if (cfg.Buttons.UiDrop == "Y" && cfg.Buttons.UiQuickMove == "Y")
                {
                    cfg.Buttons.UiDrop = "";
                    upgraded = true;
                }
                if (cfg.Buttons.UiDragModifier == "RB" && cfg.Buttons.UiPageNext == "RB")
                {
                    cfg.Buttons.UiDragModifier = "";
                    upgraded = true;
                }
                cfg.Validate();
                if (upgraded) cfg.Save(path);
                return cfg;
            }
            catch (Exception e)
            {
                Log.Error("Failed to read Settings.json, falling back to defaults: " + e.Message);
                return new PadConfig();
            }
        }

        public void Save(string path)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Error("Failed to write Settings.json: " + e.Message);
            }
        }

        private void Validate()
        {
            Aim.Deadzone = Mathf.Clamp(Aim.Deadzone, 0f, 0.9f);
            Aim.OuterDeadzone = Mathf.Clamp(Aim.OuterDeadzone, Aim.Deadzone + 0.01f, 1f);
            Aim.ResponseCurve = Mathf.Clamp(Aim.ResponseCurve, 0.2f, 5f);
            Aim.AdsPrecision = Mathf.Clamp(Aim.AdsPrecision, 0.2f, 1f);
            Aim.PointerSensitivity = Mathf.Clamp(Aim.PointerSensitivity, 0.1f, 4f);
            Aim.MixedPointer = Mathf.Clamp(Aim.MixedPointer, 0, 2);
            Aim.MinReticleDistance = Mathf.Max(0.5f, Aim.MinReticleDistance);
            Aim.ReticleDistance = Mathf.Max(Aim.MinReticleDistance + 0.1f, Aim.ReticleDistance);

            AimAssist.Strength = Mathf.Clamp01(AimAssist.Strength);
            AimAssist.StickinessDegrees = Mathf.Clamp(AimAssist.StickinessDegrees, 0f, 45f);
            AimAssist.MaxAngleDegrees = Mathf.Clamp(AimAssist.MaxAngleDegrees, 0f, 90f);
            AimAssist.MaxDistance = Mathf.Max(1f, AimAssist.MaxDistance);
            AimAssist.EaseSpeed = Mathf.Clamp(AimAssist.EaseSpeed, 0.5f, 60f);
            AimAssist.EdgeTaper = Mathf.Clamp01(AimAssist.EdgeTaper);

            AimSnap.MaxAngleDegrees = Mathf.Clamp(AimSnap.MaxAngleDegrees, 5f, 180f);
            AimSnap.MaxDistance = Mathf.Max(1f, AimSnap.MaxDistance);
            AimSnap.BreakAngleDegrees = Mathf.Clamp(AimSnap.BreakAngleDegrees, 15f, 180f);

            UiSnap.MagnetStrength = Mathf.Clamp01(UiSnap.MagnetStrength);
            UiSnap.MagnetRadius = Mathf.Max(0f, UiSnap.MagnetRadius);
            UiSnap.SnapConeDegrees = Mathf.Clamp(UiSnap.SnapConeDegrees, 15f, 89f);
            UiSnap.RescanInterval = Mathf.Clamp(UiSnap.RescanInterval, 0.05f, 2f);

            Hints.Scale = Mathf.Clamp(Hints.Scale, 0.5f, 3f);

            Move.Deadzone = Mathf.Clamp(Move.Deadzone, 0f, 0.9f);
            Move.OuterDeadzone = Mathf.Clamp(Move.OuterDeadzone, Move.Deadzone + 0.01f, 1f);
            Move.ResponseCurve = Mathf.Clamp(Move.ResponseCurve, 0.2f, 5f);

            Cursor.Deadzone = Mathf.Clamp(Cursor.Deadzone, 0f, 0.9f);
            Cursor.ResponseCurve = Mathf.Clamp(Cursor.ResponseCurve, 0.2f, 5f);
            Cursor.Speed = Mathf.Max(50f, Cursor.Speed);

            Rumble.Scale = Mathf.Clamp(Rumble.Scale, 0f, 2f);
        }
    }
}
