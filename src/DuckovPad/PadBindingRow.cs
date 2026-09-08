using System;
using System.Collections.Generic;
using Duckov.Options;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovPad
{
    /// <summary>
    /// A single remappable binding row on the Controls tab: label on the left, current
    /// binding on the right. Selecting it listens for the next pad press (chords like
    /// LB+Y work: hold LB, tap Y). B cancels, Delete clears. Choices apply instantly,
    /// persist through the game's options store, and mirror back to Settings.json.
    /// </summary>
    internal sealed class PadBindingRow : MonoBehaviour
    {
        private static readonly HashSet<PadBindingRow> Listening = new HashSet<PadBindingRow>();
        private static float _suppressBackUntil;

        private PadConfig _config;
        private PadBinding _binding;
        private TextMeshProUGUI _value;
        private Color _normalColor;
        private bool _listening;
        private int _deviceRevision = -1;
        private float _ignoreInputUntil;
        private float _ignoreClickUntil;

        public void Bind(PadConfig config, PadBinding binding, TextMeshProUGUI value)
        {
            _config = config;
            _binding = binding;
            _value = value;
            if (_value != null) _normalColor = _value.color;
            Refresh();
        }

        private void OnDestroy() => Listening.Remove(this);

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            _listening = false;
            Listening.Remove(this);
        }

        /// <summary> wired to the row Button. Ignored while listening or just after a capture. </summary>
        public void OnRowClicked()
        {
            if (_binding == null || _config == null) return;
            if (_listening || Time.unscaledTime < _ignoreClickUntil) return;
            if (_binding.IsReset)
            {
                PadBindings.ResetAll(_config);
                RefreshAllLabels();
                return;
            }
            StartListening();
        }

        private void StartListening()
        {
            CancelAllSilent();
            _listening = true;
            Listening.Add(this);
            // The press that opened listening must not become the new binding.
            _ignoreInputUntil = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Cancel(bool suppressBack)
        {
            _listening = false;
            Listening.Remove(this);
            if (suppressBack) _suppressBackUntil = Time.unscaledTime + 0.5f;
            _ignoreClickUntil = Time.unscaledTime + 0.5f;
            Refresh();
        }

        private void ApplyCapture(string captured)
        {
            _listening = false;
            Listening.Remove(this);
            _ignoreClickUntil = Time.unscaledTime + 0.5f;
            PadBindings.Set(_config, _binding, captured);
            RefreshAllLabels();
        }

        private void Clear()
        {
            _listening = false;
            Listening.Remove(this);
            _ignoreClickUntil = Time.unscaledTime + 0.5f;
            PadBindings.Set(_config, _binding, string.Empty);
            RefreshAllLabels();
        }

        private void Update()
        {
            if (_deviceRevision != ControllerDevice.Revision)
            {
                _deviceRevision = ControllerDevice.Revision;
                Refresh();
            }
            if (!_listening || _binding == null || _config == null) return;
            if (!isActiveAndEnabled) { _listening = false; Listening.Remove(this); return; }
            if (Time.unscaledTime < _ignoreInputUntil) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { Cancel(suppressBack: false); return; }
                if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                {
                    Clear();
                    return;
                }
            }

            if (!Pad.Available) return;
            // B is the universal cancel here, so it cannot be (re)bound from this UI.
            // Settings.json still accepts it for anyone who really wants B somewhere.
            if (Pad.Down("B")) { Cancel(suppressBack: true); return; }

            string captured = Pad.CapturePress();
            if (string.IsNullOrEmpty(captured)) return;
            ApplyCapture(captured);
        }

        public void Refresh()
        {
            if (_value == null || _binding == null) return;
            if (_binding.IsReset)
            {
                _value.text = "Reset";
                _value.color = _normalColor;
                return;
            }
            if (_listening)
            {
                _value.text = "Press a button…  (" + ControllerDevice.FormatBinding("B") + " cancels)";
                _value.color = new Color(1f, 0.85f, 0.3f, 1f);
                return;
            }
            string current = PadBindings.Get(_config, _binding);
            if (string.IsNullOrWhiteSpace(current))
            {
                _value.text = "—";
                _value.color = new Color(0.55f, 0.55f, 0.6f, 1f);
            }
            else
            {
                _value.text = ControllerDevice.FormatBinding(current);
                _value.color = _normalColor;
            }
        }

        /// <summary>True while any row is capturing (or just cancelled): menu Back/Close/Page keys stand down.</summary>
        public static bool SuppressMenuKeys()
        {
            Prune();
            return Listening.Count > 0 || Time.unscaledTime < _suppressBackUntil;
        }

        /// <summary>Called first from the menu Back handler: cancels capture instead of closing.</summary>
        public static bool ConsumeBackForCapture()
        {
            Prune();
            if (Listening.Count == 0) return Time.unscaledTime < _suppressBackUntil;
            foreach (var row in new List<PadBindingRow>(Listening))
            {
                if (row != null) row.Cancel(suppressBack: true);
            }
            Listening.Clear();
            return true;
        }

        public static void CancelAllSilent()
        {
            foreach (var row in Listening)
            {
                if (row == null) continue;
                row._listening = false;
                row.Refresh();
            }
            Listening.Clear();
        }

        public static void RefreshAllLabels()
        {
            try
            {
                foreach (var row in UnityEngine.Object.FindObjectsOfType<PadBindingRow>(true))
                {
                    if (row != null) row.Refresh();
                }
            }
            catch (Exception e)
            {
                Log.Warn("Binding label refresh failed: " + e.Message);
            }
        }

        private static void Prune() => Listening.RemoveWhere(r => r == null);
    }
}
