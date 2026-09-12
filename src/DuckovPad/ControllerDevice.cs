using System;
using System.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovPad
{
    /// <summary>Physical type when available, with an honest fallback for virtual pads.</summary>
    internal static class ControllerDevice
    {
        private static readonly InputHandle_t[] Handles = new InputHandle_t[16];
        private static float _nextPoll;
        private static bool _steamInitialized;
        private static bool _steamUnavailable;
        private static float _nextSteamAttempt;
        public static ControllerFamily Family { get; private set; }
        public static string Name { get; private set; } = "No controller connected";
        public static string Source { get; private set; } = "Connect a controller and press a button";
        public static int Revision { get; private set; }
        public static Vector2 PointerDelta;
        public static int PointerMode; // 0 auto for Deck/Steam Controller; 1 on; 2 off
        public static bool UsePointer => PointerMode == 1 || (PointerMode == 0 &&
            (Family == ControllerFamily.SteamDeck || Family == ControllerFamily.SteamController));

        public static void Invalidate() => _nextPoll = 0;

        public static void Poll(Gamepad gamepad)
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 1f;
            var family = ControllerFamily.Generic;
            string name = "No controller connected";
            string source = "Connect a controller and press a button";
            if (gamepad != null)
            {
                family = ControllerProfile.Detect(gamepad.layout + " " + gamepad.displayName + " " +
                    gamepad.description.manufacturer + " " + gamepad.description.product);
                name = gamepad.displayName;
                source = "Detected by the game";
                try
                {
                    // Direct HID / Apple controller layouts already tell us the family.
                    // Steam is optional metadata for virtual or unidentified pads only.
                    bool needsSteam = family == ControllerFamily.Xbox || family == ControllerFamily.Generic;
                    if (needsSteam && !_steamUnavailable && !_steamInitialized && Time.unscaledTime >= _nextSteamAttempt)
                    {
                        _nextSteamAttempt = Time.unscaledTime + 10f;
                        _steamInitialized = SteamInput.Init(false);
                    }
                    if (needsSteam && _steamInitialized)
                    {
                        SteamInput.RunFrame();
                        int count = SteamInput.GetConnectedControllers(Handles);
                        // Unity does not expose a reliable XInput slot on this game build.
                        // Never label a plugged-in Xbox pad as a Deck simply because Steam
                        // itself runs on a Deck. Associate only an unambiguous virtual pad.
                        if ((family == ControllerFamily.Xbox || family == ControllerFamily.Generic) &&
                            count == 1 && Gamepad.all.Count(g => g.added && g.enabled) == 1 &&
                            SteamInput.GetGamepadIndexForController(Handles[0]) >= 0)
                        {
                            string type = SteamInput.GetInputTypeForHandle(Handles[0]).ToString();
                            var physical = ControllerProfile.Detect(type);
                            if (physical != ControllerFamily.Generic)
                            {
                                family = physical;
                                name = type.Replace("k_ESteamInputType_", "").Replace("Controller", " controller");
                                if (family == ControllerFamily.SteamDeck) name = "Steam Deck";
                                source = "Physical controller identified through Steam Input";
                            }
                        }
                        else if (family == ControllerFamily.Xbox && count > 1)
                            source = "Virtual Xbox input; physical controller cannot be matched reliably";
                    }
                }
                catch (Exception e)
                {
                    _steamInitialized = false;
                    _nextSteamAttempt = Time.unscaledTime + 10f;
                    // Missing or incompatible native libraries cannot recover by polling.
                    // Keep normal controller input usable without throwing every second.
                    if (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
                    {
                        _steamUnavailable = true;
                        Log.Warn("Steam Input identification unavailable; using the game's controller detection. " + e.Message);
                    }
                }
            }
            if (Family != family || Name != name || Source != source)
            {
                Family = family;
                Name = name;
                Source = source;
                Revision++;
                Log.Info("Active controller: " + Name + ". " + Source);
            }
        }

        public static string FormatBinding(string binding)
        {
            if (string.IsNullOrWhiteSpace(binding)) return "Unassigned";
            return string.Join(" + ", binding.Split('+').Select(part =>
            {
                string canonical = Pad.CanonicalName(part);
                return ControllerProfile.Label(canonical, Family) ?? Pad.FriendlyName(canonical);
            }));
        }
    }
}
