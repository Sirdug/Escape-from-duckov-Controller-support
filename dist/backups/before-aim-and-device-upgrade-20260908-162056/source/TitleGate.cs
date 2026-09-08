using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuckovPad
{
    /// <summary>
    /// The boot "Click to Continue" title gate. This is separate from the save-loading
    /// curtain (<see cref="LoadingContinue"/>): it is a <see cref="Title"/> component
    /// whose <c>OnPointerClick</c> only runs while its fade group is shown.
    /// </summary>
    internal static class TitleGate
    {
        private static readonly FieldInfo FadeField = typeof(Title).GetField(
            "fadeGroup", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Title _cached;
        private static float _nextLookupTime;

        /// <summary>True while the title screen is showing its Continue prompt.</summary>
        public static bool IsWaiting
        {
            get
            {
                try
                {
                    var title = FindTitle();
                    if (title == null || !title.isActiveAndEnabled) return false;
                    var fade = FadeField?.GetValue(title) as Duckov.UI.Animations.FadeGroup;
                    // If the fade cannot be read (e.g. a game update renamed it), err on
                    // the side of attempting a confirm rather than stranding the player.
                    if (fade == null) return true;
                    return fade.IsShown;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Invoke the same handler a real click would. Self-guarded by the fade state.</summary>
        public static bool TryConfirm()
        {
            try
            {
                var title = FindTitle();
                if (title == null) return false;
                var eventSystem = EventSystem.current;
                // Title.OnPointerClick ignores its argument; it only checks the fade.
                // EventSystem.current can be null during boot, so tolerate that.
                PointerEventData data = eventSystem != null ? new PointerEventData(eventSystem) : null;
                title.OnPointerClick(data);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("Title gate confirm failed: " + e.Message);
                return false;
            }
        }

        private static Title FindTitle()
        {
            try
            {
                // Unity's == handles destroyed objects; a destroyed title re-triggers lookup below.
                if (_cached != null) return _cached;
                if (Time.unscaledTime < _nextLookupTime) return null;
                _nextLookupTime = Time.unscaledTime + 0.5f;
                _cached = UnityEngine.Object.FindObjectOfType<Title>();
                return _cached;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
