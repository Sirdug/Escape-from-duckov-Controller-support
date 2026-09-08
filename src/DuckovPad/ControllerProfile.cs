using System;

namespace DuckovPad
{
    internal enum ControllerFamily { Generic, Xbox, PlayStation, Switch, SteamDeck, SteamController }

    internal static class ControllerProfile
    {
        public static ControllerFamily Detect(string description)
        {
            string name = (description ?? "").ToLowerInvariant();
            if (name.Contains("steamdeck") || name.Contains("steam deck")) return ControllerFamily.SteamDeck;
            if (name.Contains("steamcontroller") || name.Contains("steam controller")) return ControllerFamily.SteamController;
            if (name.Contains("dualsense") || name.Contains("dualshock") || name.Contains("playstation") ||
                name.Contains("ps3controller") || name.Contains("ps4controller") || name.Contains("ps5controller"))
                return ControllerFamily.PlayStation;
            if (name.Contains("switch") || name.Contains("joycon") || name.Contains("joy-con")) return ControllerFamily.Switch;
            if (name.Contains("xinput") || name.Contains("xbox") || name.Contains("x-box")) return ControllerFamily.Xbox;
            return ControllerFamily.Generic;
        }

        public static string Label(string canonical, ControllerFamily family)
        {
            switch (canonical)
            {
                case "buttonSouth": return family == ControllerFamily.PlayStation ? "Cross" : family == ControllerFamily.Switch ? "B" : "A";
                case "buttonEast": return family == ControllerFamily.PlayStation ? "Circle" : family == ControllerFamily.Switch ? "A" : "B";
                case "buttonWest": return family == ControllerFamily.PlayStation ? "Square" : family == ControllerFamily.Switch ? "Y" : "X";
                case "buttonNorth": return family == ControllerFamily.PlayStation ? "Triangle" : family == ControllerFamily.Switch ? "X" : "Y";
                case "leftShoulder": return family == ControllerFamily.PlayStation || family == ControllerFamily.SteamDeck ? "L1" : family == ControllerFamily.Switch ? "L" : "LB";
                case "rightShoulder": return family == ControllerFamily.PlayStation || family == ControllerFamily.SteamDeck ? "R1" : family == ControllerFamily.Switch ? "R" : "RB";
                case "leftTrigger": return family == ControllerFamily.PlayStation || family == ControllerFamily.SteamDeck ? "L2" : family == ControllerFamily.Switch ? "ZL" : "LT";
                case "rightTrigger": return family == ControllerFamily.PlayStation || family == ControllerFamily.SteamDeck ? "R2" : family == ControllerFamily.Switch ? "ZR" : "RT";
                case "start": return family == ControllerFamily.PlayStation ? "Options" : family == ControllerFamily.Switch ? "+" : "Menu";
                case "select": return family == ControllerFamily.PlayStation ? "Share" : family == ControllerFamily.Switch ? "-" : "View";
                default: return null;
            }
        }
    }
}
