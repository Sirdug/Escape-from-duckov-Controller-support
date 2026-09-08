using System;

namespace DuckovPad
{
    // Pure math so the feel-critical cases can be tested outside the Unity player.
    internal static class AimMath
    {
        /// <summary>Shortest signed distance from <paramref name="from"/> to <paramref name="to"/>, in (-180, 180].</summary>
        public static float DeltaAngle(float from, float to)
        {
            double delta = (to - from) % 360.0;
            if (delta > 180) delta -= 360;
            if (delta <= -180) delta += 360;
            return (float)delta;
        }

        public static float SmoothAngle(float current, float target, float response, float dt)
        {
            double blend = response <= 0 ? 1 : 1 - Math.Exp(-response * Math.Max(0, dt));
            return current + (float)(DeltaAngle(current, target) * blend);
        }

        /// <summary>
        /// Exponential settle with a hard speed limit.
        ///
        /// The exponential half alone is what made large sweeps feel uneven: it covers most of
        /// a big turn in the first few milliseconds and then crawls. Capping the step at
        /// <paramref name="maxDegreesPerSecond"/> turns the opening of every sweep into a
        /// constant-rate motion, so a flick across the screen takes a predictable amount of
        /// time no matter which direction it is in, and only the last few degrees ease in.
        /// </summary>
        public static float ApproachAngle(float current, float target, float response, float maxDegreesPerSecond, float dt)
        {
            if (dt <= 0) return current;

            float delta = DeltaAngle(current, target);
            double blend = response <= 0 ? 1 : 1 - Math.Exp(-response * dt);
            double step = delta * blend;

            if (maxDegreesPerSecond > 0)
            {
                double limit = maxDegreesPerSecond * dt;
                if (step > limit) step = limit;
                else if (step < -limit) step = -limit;
            }

            // Never overshoot: the cap only ever slows the approach down.
            if (Math.Abs(step) > Math.Abs(delta)) step = delta;
            return current + (float)step;
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0) return current;
            float delta = target - current;
            if (delta > maxDelta) return current + maxDelta;
            if (delta < -maxDelta) return current - maxDelta;
            return target;
        }

        public static float ShapeMagnitude(float magnitude, float deadzone, float outer, float curve)
        {
            if (magnitude <= deadzone) return 0;
            double value = Math.Max(0, Math.Min(1, (magnitude - deadzone) / Math.Max(0.0001f, outer - deadzone)));
            return (float)Math.Pow(value, Math.Max(0.2f, curve));
        }

        /// <summary>
        /// Turn-rate multiplier for a given stick deflection.
        ///
        /// At full deflection the aim always runs at full speed, so pointing the stick still
        /// points the reticle. <paramref name="precisionRange"/> says how far the rate is
        /// allowed to fall off as the stick comes back to centre: 0 keeps every deflection at
        /// full speed (pure absolute aiming, twitchy near the deadzone), 1 lets a feather
        /// touch creep. Squaring the deflection puts most of that range in the bottom half of
        /// the stick's travel, which is where fine adjustments are actually made.
        /// </summary>
        public static float PrecisionScale(float magnitude, float precisionRange)
        {
            float clampedRange = Clamp01(precisionRange);
            float m = Clamp01(magnitude);
            return 1f - clampedRange * (1f - m * m);
        }

        /// <summary>
        /// 1 at the centre of a cone, easing to 0 at its rim.
        ///
        /// Smoothstep rather than a linear ramp: a linear falloff still has a corner at the
        /// rim, and aim assist crossing that corner is exactly what reads as a target
        /// "popping" on and off.
        /// </summary>
        public static float ConeFalloff(float angle, float coneDegrees)
        {
            if (coneDegrees <= 0) return 0;
            float t = 1f - Clamp01(Math.Abs(angle) / coneDegrees);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smoothstep-eased 0..1, used to soften lock-on transitions at both ends.</summary>
        public static float Ease(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
    }
}
