using System;

namespace DuckovPad
{
    // Pure math so the feel-critical cases can be tested outside the Unity player.
    internal static class AimMath
    {
        public static float SmoothAngle(float current, float target, float response, float dt)
        {
            double delta = (target - current) % 360.0;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            double blend = response <= 0 ? 1 : 1 - Math.Exp(-response * Math.Max(0, dt));
            return (float)(current + delta * blend);
        }

        public static float ShapeMagnitude(float magnitude, float deadzone, float outer, float curve)
        {
            if (magnitude <= deadzone) return 0;
            double value = Math.Max(0, Math.Min(1, (magnitude - deadzone) / Math.Max(0.0001f, outer - deadzone)));
            return (float)Math.Pow(value, Math.Max(0.2f, curve));
        }
    }
}
