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
                    if (!_steamInitialized) _steamInitialized = SteamInput.Init(false);
                    if (_steamInitialized)
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
                catch (Exception)
                {
                    _steamInitialized = false;
                    // The game's Steam client may not be ready, or Steam may be disabled.
                    // Direct device input continues to work; retry on the next poll.
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
