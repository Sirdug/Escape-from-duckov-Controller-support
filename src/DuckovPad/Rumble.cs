using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovPad
{
    /// <summary>
    /// Simple decaying-pulse haptics. Each pulse sets a target intensity that falls off
    /// over its duration; the strongest active pulse wins, so a hit taken mid-burst
    /// doesn't get swallowed by gunfire.
    /// </summary>
    internal sealed class Rumble
    {
        private readonly PadConfig _config;

        private float _low;
        private float _high;
        private float _decayPerSecond;
        private bool _motorsRunning;

        public Rumble(PadConfig config)
        {
            _config = config;
        }

        public void Shoot()
        {
            var settings = _config.Rumble;
            Pulse(settings.ShootLow, settings.ShootHigh, settings.ShootDuration);
        }

        public void Hurt()
        {
            var settings = _config.Rumble;
            Pulse(settings.HurtLow, settings.HurtHigh, settings.HurtDuration);
        }

        public void Pulse(float low, float high, float duration)
        {
            var settings = _config.Rumble;
            if (!settings.Enabled || settings.Scale <= 0f) return;
            if (duration <= 0f) return;

            low = Mathf.Clamp01(low * settings.Scale);
            high = Mathf.Clamp01(high * settings.Scale);

            // Only override if this pulse is stronger than what's already playing.
            if (low + high <= _low + _high) return;

            _low = low;
            _high = high;
            _decayPerSecond = 1f / duration;
        }

        public void Update(float deltaTime)
        {
            var gamepad = Pad.Current;
            if (gamepad == null)
            {
                _motorsRunning = false;
                return;
            }

            if (!_config.Rumble.Enabled)
            {
                Stop();
                return;
            }

            if (_low <= 0f && _high <= 0f)
            {
                if (_motorsRunning)
                {
                    SafeSetMotors(gamepad, 0f, 0f);
                    _motorsRunning = false;
                }
                return;
            }

            float decay = _decayPerSecond * deltaTime;
            _low = Mathf.Max(0f, _low - decay);
            _high = Mathf.Max(0f, _high - decay);

            SafeSetMotors(gamepad, _low, _high);
            _motorsRunning = true;
        }

        public void Stop()
        {
            _low = 0f;
            _high = 0f;

            var gamepad = Pad.Current;
            if (gamepad != null && _motorsRunning)
                SafeSetMotors(gamepad, 0f, 0f);

            _motorsRunning = false;
        }

        private static void SafeSetMotors(Gamepad gamepad, float low, float high)
        {
            try
            {
                gamepad.SetMotorSpeeds(low, high);
            }
            catch (Exception)
            {
                // Some pads/backends don't support rumble; ignore rather than spam the log.
            }
        }
    }
}
