using System;
using Duckov.Options;
using TMPro;
using UnityEngine;

namespace DuckovPad
{
    internal enum BuiltKind { Slider, Toggle, Choice }

    /// <summary>
    /// Value readout for a slider/toggle/choice row the mod built itself (used when the
    /// game's options pages offer no matching row template to clone). Choices advance
    /// through their list on click.
    /// </summary>
    internal sealed class PadBuiltSlider : MonoBehaviour
    {
        public PadConfig Config;
        public PadOption Option;
        public BuiltKind Kind;
        public TextMeshProUGUI ValueText;

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (ValueText == null || Option == null || Config == null) return;
            try
            {
                switch (Kind)
                {
                    case BuiltKind.Toggle:
                        ValueText.text = OptionsManager.Load(Option.Key, Option.GetBool(Config)) ? "On   /   Turn off" : "Off   /   Turn on";
                        break;
                    case BuiltKind.Choice:
                        var choices = Option.Choices ?? new[] { "-" };
                        int index = Mathf.Clamp(OptionsManager.Load(Option.Key, Option.GetChoice(Config)), 0, choices.Length - 1);
                        ValueText.text = choices[index] + "  >";
                        break;
                    default:
                        ValueText.text = OptionsManager.Load(Option.Key, Option.GetFloat(Config)).ToString(Option.Format);
                        var slider = GetComponentInChildren<UnityEngine.UI.Slider>(true);
                        if (slider != null) slider.SetValueWithoutNotify(OptionsManager.Load(Option.Key, Option.GetFloat(Config)));
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Warn("Built row refresh failed: " + e.Message);
            }
        }

        public void CycleChoice()
        {
            if (Option == null || Option.Choices == null || Option.Choices.Length == 0) return;
            try
            {
                int fallback = Config != null ? Option.GetChoice(Config) : 0;
                int index = OptionsManager.Load(Option.Key, fallback);
                int next = (index + 1) % Option.Choices.Length;
                OptionsManager.Save(Option.Key, next);
                Refresh();
            }
            catch (Exception e)
            {
                Log.Warn("Built row cycle failed: " + e.Message);
            }
        }
    }
}
