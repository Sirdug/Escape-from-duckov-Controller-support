using System;
using System.Collections.Generic;
using Duckov.Options;
using UnityEngine;

namespace DuckovPad
{
    internal enum OptionKind { Slider, Toggle, Choice }

    /// <summary>
    /// One row on the Controller options page. Values live in <see cref="PadConfig"/> at
    /// runtime and are persisted through the game's own <see cref="OptionsManager"/>, so
    /// they survive restarts and sit in the same save file as every other setting.
    /// </summary>
    internal sealed class PadOption
    {
        public string Key;
        public string Label;
        public OptionKind Kind;

        public float Min;
        public float Max;
        public string Format = "0.00";

        public string[] Choices;

        public Func<PadConfig, float> GetFloat;
        public Action<PadConfig, float> SetFloat;
        public Func<PadConfig, bool> GetBool;
        public Action<PadConfig, bool> SetBool;
        public Func<PadConfig, int> GetChoice;
        public Action<PadConfig, int> SetChoice;

        /// <summary>Section heading this row belongs under.</summary>
        public string Section;
    }

    internal static class PadOptions
    {
        public const string Prefix = "DuckovPad_";

        private static readonly string[] AimModes = { "Easy directional (recommended)", "Free cursor", "Classic twin-stick" };
        private static readonly string[] LockModes = { "Toggle", "Hold" };

        public static readonly List<PadOption> All = new List<PadOption>
        {
            // ---------------- Aiming ----------------
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AimMode", Label = "Aim style", Kind = OptionKind.Choice,
                Choices = AimModes,
                GetChoice = c => string.Equals(c.Aim.Mode, "relative", StringComparison.OrdinalIgnoreCase) ? 1
                    : string.Equals(c.Aim.Mode, "classic", StringComparison.OrdinalIgnoreCase) ? 2 : 0,
                SetChoice = (c, i) => c.Aim.Mode = i == 1 ? "relative" : i == 2 ? "classic" : "easy"
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AdsPrecision", Label = "Aim speed while aiming down sights", Kind = OptionKind.Slider,
                Min = 0.2f, Max = 1f, Format = "0.00",
                GetFloat = c => c.Aim.AdsPrecision, SetFloat = (c, v) => c.Aim.AdsPrecision = v
            },
            new PadOption
            {
                Section = "Steam Deck & precision", Key = Prefix + "MixedPointer", Label = "Trackpad / gyro mouse with controller", Kind = OptionKind.Choice,
                Choices = new[] { "Auto (Steam Deck / Steam Controller)", "On (any controller)", "Off" },
                GetChoice = c => c.Aim.MixedPointer, SetChoice = (c, v) => c.Aim.MixedPointer = v
            },
            new PadOption
            {
                Section = "Steam Deck & precision", Key = Prefix + "PointerSensitivity", Label = "Trackpad / gyro mouse sensitivity", Kind = OptionKind.Slider,
                Min = 0.1f, Max = 4f, Format = "0.00",
                GetFloat = c => c.Aim.PointerSensitivity, SetFloat = (c, v) => c.Aim.PointerSensitivity = v
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AimReach", Label = "Crosshair distance", Kind = OptionKind.Slider,
                Min = 0.2f, Max = 1f, Format = "0.00",
                GetFloat = c => c.Aim.Reach,
                SetFloat = (c, v) => c.Aim.Reach = v
            },
            new PadOption
            {
                // Deliberately not the old "AimResponse" key: that one stored a smoothing rate
                // for a model where the only way to tame a twitchy stick was to lag the whole
                // crosshair, and carrying those values over would make the new curve feel worse
                // than the one it replaces.
                Section = "Aiming", Key = Prefix + "AimTurnResponse", Label = "Aim speed", Kind = OptionKind.Slider,
                Min = 6f, Max = 60f, Format = "0",
                GetFloat = c => c.Aim.TurnResponse,
                SetFloat = (c, v) => c.Aim.TurnResponse = v
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AimTurnCap", Label = "Maximum sweep speed (degrees/sec)", Kind = OptionKind.Slider,
                Min = 240f, Max = 2400f, Format = "0",
                GetFloat = c => c.Aim.MaxTurnSpeed,
                SetFloat = (c, v) => c.Aim.MaxTurnSpeed = v
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AimPrecision", Label = "Fine control at part-stick", Kind = OptionKind.Slider,
                Min = 0f, Max = 1f, Format = "0.00",
                GetFloat = c => c.Aim.PrecisionRange,
                SetFloat = (c, v) => c.Aim.PrecisionRange = v
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AimDeadzone", Label = "Right stick deadzone", Kind = OptionKind.Slider,
                Min = 0.02f, Max = 0.5f, Format = "0.00",
                GetFloat = c => c.Aim.Deadzone,
                SetFloat = (c, v) => c.Aim.Deadzone = v
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "AimRecoil", Label = "Recoil moves the crosshair", Kind = OptionKind.Toggle,
                GetBool = c => c.Aim.PreserveRecoil,
                SetBool = (c, v) => c.Aim.PreserveRecoil = v
            },
            new PadOption
            {
                Section = "Aiming", Key = Prefix + "LookSpeed", Label = "Free cursor look speed", Kind = OptionKind.Slider,
                Min = 60f, Max = 700f, Format = "0",
                GetFloat = c => c.Aim.RelativeSensitivity,
                SetFloat = (c, v) => c.Aim.RelativeSensitivity = v
            },

            // ---------------- Aim assist ----------------
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistOn", Label = "Enable aim assist", Kind = OptionKind.Toggle,
                GetBool = c => c.AimAssist.Enabled,
                SetBool = (c, v) => c.AimAssist.Enabled = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistStrength", Label = "Assist strength", Kind = OptionKind.Slider,
                Min = 0f, Max = 1f, Format = "0.00",
                GetFloat = c => c.AimAssist.Strength,
                SetFloat = (c, v) => c.AimAssist.Strength = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistFriction", Label = "Slow down near enemies", Kind = OptionKind.Slider,
                Min = 0f, Max = 1f, Format = "0.00",
                GetFloat = c => c.AimAssist.Friction,
                SetFloat = (c, v) => c.AimAssist.Friction = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistMagnet", Label = "Pull toward enemies (degrees/sec)", Kind = OptionKind.Slider,
                Min = 0f, Max = 180f, Format = "0",
                GetFloat = c => c.AimAssist.MagnetDegreesPerSecond,
                SetFloat = (c, v) => c.AimAssist.MagnetDegreesPerSecond = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistPull", Label = "Most the pull may bend aim (degrees)", Kind = OptionKind.Slider,
                Min = 0f, Max = 30f, Format = "0",
                GetFloat = c => c.AimAssist.MaxPullDegrees,
                SetFloat = (c, v) => c.AimAssist.MaxPullDegrees = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistDepth", Label = "Settle crosshair onto the enemy", Kind = OptionKind.Slider,
                Min = 0f, Max = 1f, Format = "0.00",
                GetFloat = c => c.AimAssist.DepthPull,
                SetFloat = (c, v) => c.AimAssist.DepthPull = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistAngle", Label = "Assist cone (degrees)", Kind = OptionKind.Slider,
                Min = 5f, Max = 45f, Format = "0",
                GetFloat = c => c.AimAssist.MaxAngleDegrees,
                SetFloat = (c, v) => c.AimAssist.MaxAngleDegrees = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistRange", Label = "Assist range (metres)", Kind = OptionKind.Slider,
                Min = 8f, Max = 60f, Format = "0",
                GetFloat = c => c.AimAssist.MaxDistance,
                SetFloat = (c, v) => c.AimAssist.MaxDistance = v
            },
            new PadOption
            {
                Section = "Aim assist", Key = Prefix + "AssistFiringOnly", Label = "Only while shooting or aiming", Kind = OptionKind.Toggle,
                GetBool = c => c.AimAssist.OnlyWhileFiringOrAds,
                SetBool = (c, v) => c.AimAssist.OnlyWhileFiringOrAds = v
            },

            // ---------------- Lock-on ----------------
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockOn", Label = "Enable lock-on", Kind = OptionKind.Toggle,
                GetBool = c => c.AimSnap.Enabled,
                SetBool = (c, v) => c.AimSnap.Enabled = v
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockMode", Label = "Lock-on button style", Kind = OptionKind.Choice,
                Choices = LockModes,
                GetChoice = c => string.Equals(c.AimSnap.Mode, "hold", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                SetChoice = (c, i) => c.AimSnap.Mode = i == 1 ? "hold" : "toggle"
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "SnapOnFire", Label = "Snap to enemy when firing", Kind = OptionKind.Toggle,
                GetBool = c => c.AimSnap.SnapOnFire,
                SetBool = (c, v) => c.AimSnap.SnapOnFire = v
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockRange", Label = "Lock-on range (metres)", Kind = OptionKind.Slider,
                Min = 10f, Max = 70f, Format = "0",
                GetFloat = c => c.AimSnap.MaxDistance,
                SetFloat = (c, v) => c.AimSnap.MaxDistance = v
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockEngage", Label = "Time to settle onto the target (sec)", Kind = OptionKind.Slider,
                Min = 0f, Max = 0.4f, Format = "0.00",
                GetFloat = c => c.AimSnap.EngageTime,
                SetFloat = (c, v) => c.AimSnap.EngageTime = v
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockLead", Label = "Stick lead while locked (degrees)", Kind = OptionKind.Slider,
                Min = 0f, Max = 40f, Format = "0",
                GetFloat = c => c.AimSnap.LeadDegrees,
                SetFloat = (c, v) => c.AimSnap.LeadDegrees = v
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockSwitch", Label = "Flick the stick to change target", Kind = OptionKind.Toggle,
                GetBool = c => c.AimSnap.SwitchTargetOnFlick,
                SetBool = (c, v) => c.AimSnap.SwitchTargetOnFlick = v
            },
            new PadOption
            {
                Section = "Lock-on", Key = Prefix + "LockOutline", Label = "Outline the locked enemy", Kind = OptionKind.Toggle,
                GetBool = c => c.AimSnap.ShowOutline,
                SetBool = (c, v) => c.AimSnap.ShowOutline = v
            },

            // ---------------- Movement ----------------
            new PadOption
            {
                Section = "Movement", Key = Prefix + "MoveDeadzone", Label = "Left stick deadzone", Kind = OptionKind.Slider,
                Min = 0.02f, Max = 0.5f, Format = "0.00",
                GetFloat = c => c.Move.Deadzone,
                SetFloat = (c, v) => c.Move.Deadzone = v
            },
            new PadOption
            {
                Section = "Movement", Key = Prefix + "AutoRun", Label = "Sprint at full stick", Kind = OptionKind.Toggle,
                GetBool = c => c.Move.AutoRunAtFullStick,
                SetBool = (c, v) => c.Move.AutoRunAtFullStick = v
            },

            // ---------------- Menus ----------------
            new PadOption
            {
                Section = "Menus", Key = Prefix + "StickNavigation", Label = "Left stick selects UI elements", Kind = OptionKind.Toggle,
                GetBool = c => c.UiSnap.StickNavigation,
                SetBool = (c, v) => c.UiSnap.StickNavigation = v
            },
            new PadOption
            {
                Section = "Menus", Key = Prefix + "HideMenuCursor", Label = "Hide cursor during slot navigation", Kind = OptionKind.Toggle,
                GetBool = c => c.UiSnap.HideCursor,
                SetBool = (c, v) => c.UiSnap.HideCursor = v
            },
            new PadOption
            {
                Section = "Menus", Key = Prefix + "CursorSpeed", Label = "Menu cursor speed", Kind = OptionKind.Slider,
                Min = 400f, Max = 3200f, Format = "0",
                GetFloat = c => c.Cursor.Speed,
                SetFloat = (c, v) => c.Cursor.Speed = v
            },
            new PadOption
            {
                Section = "Menus", Key = Prefix + "SnapToSlots", Label = "D-pad snaps to slots", Kind = OptionKind.Toggle,
                GetBool = c => c.UiSnap.DirectionalSnap,
                SetBool = (c, v) => c.UiSnap.DirectionalSnap = v
            },
            new PadOption
            {
                Section = "Menus", Key = Prefix + "CursorMagnet", Label = "Cursor stickiness", Kind = OptionKind.Slider,
                Min = 0f, Max = 1f, Format = "0.00",
                GetFloat = c => c.UiSnap.MagnetStrength,
                SetFloat = (c, v) => c.UiSnap.MagnetStrength = v
            },
            new PadOption
            {
                Section = "Menus", Key = Prefix + "ScrollSpeed", Label = "Menu scroll speed", Kind = OptionKind.Slider,
                Min = 2f, Max = 40f, Format = "0",
                GetFloat = c => c.Cursor.ScrollSpeed,
                SetFloat = (c, v) => c.Cursor.ScrollSpeed = v
            },

            // ---------------- Feedback ----------------
            new PadOption
            {
                Section = "Feedback", Key = Prefix + "Hints", Label = "Button prompts", Kind = OptionKind.Toggle,
                GetBool = c => c.Hints.Enabled,
                SetBool = (c, v) => c.Hints.Enabled = v
            },
            new PadOption
            {
                Section = "Feedback", Key = Prefix + "HintScale", Label = "Prompt size", Kind = OptionKind.Slider,
                Min = 0.6f, Max = 2f, Format = "0.0",
                GetFloat = c => c.Hints.Scale,
                SetFloat = (c, v) => c.Hints.Scale = v
            },
            new PadOption
            {
                Section = "Feedback", Key = Prefix + "Rumble", Label = "Vibration", Kind = OptionKind.Toggle,
                GetBool = c => c.Rumble.Enabled,
                SetBool = (c, v) => c.Rumble.Enabled = v
            },
            new PadOption
            {
                Section = "Feedback", Key = Prefix + "RumbleScale", Label = "Vibration strength", Kind = OptionKind.Slider,
                Min = 0f, Max = 2f, Format = "0.0",
                GetFloat = c => c.Rumble.Scale,
                SetFloat = (c, v) => c.Rumble.Scale = v
            },

            // ---------------- Troubleshooting ----------------
            new PadOption
            {
                Section = "Troubleshooting", Key = Prefix + "DebugOverlay", Label = "Diagnostic overlay", Kind = OptionKind.Toggle,
                GetBool = c => c.Debug.ShowOverlay,
                SetBool = (c, v) => c.Debug.ShowOverlay = v
            },
        };

        // ------------------------------------------------------------------

        /// <summary>
        /// Pull saved values out of the game's options store, falling back to whatever
        /// Settings.json supplied. Call once at startup.
        /// </summary>
        public static void LoadInto(PadConfig config)
        {
            foreach (var option in All)
            {
                try
                {
                    switch (option.Kind)
                    {
                        case OptionKind.Slider:
                            option.SetFloat(config, OptionsManager.Load(option.Key, option.GetFloat(config)));
                            break;
                        case OptionKind.Toggle:
                            option.SetBool(config, OptionsManager.Load(option.Key, option.GetBool(config)));
                            break;
                        case OptionKind.Choice:
                            option.SetChoice(config, OptionsManager.Load(option.Key, option.GetChoice(config)));
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Could not load option " + option.Key + ": " + e.Message);
                }
            }
        }

        /// <summary>Re-read a single key after the player changed it in the options UI.</summary>
        public static bool Apply(PadConfig config, string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix, StringComparison.Ordinal)) return false;

            foreach (var option in All)
            {
                if (option.Key != key) continue;

                try
                {
                    switch (option.Kind)
                    {
                        case OptionKind.Slider:
                            option.SetFloat(config, OptionsManager.Load(key, option.GetFloat(config)));
                            break;
                        case OptionKind.Toggle:
                            option.SetBool(config, OptionsManager.Load(key, option.GetBool(config)));
                            break;
                        case OptionKind.Choice:
                            option.SetChoice(config, OptionsManager.Load(key, option.GetChoice(config)));
                            break;
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Could not apply option " + key + ": " + e.Message);
                }

                return true;
            }

            return false;
        }
    }
}
