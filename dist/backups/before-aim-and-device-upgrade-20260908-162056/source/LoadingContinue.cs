using System;
using System.Reflection;
using Duckov.UI.Animations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DuckovPad
{
    /// <summary>The save-loading gate is separate from Title and remains active during scene loading.</summary>
    internal static class LoadingContinue
    {
        private static readonly FieldInfo ReceiverField = typeof(SceneLoader).GetField(
            "pointerClickEventRecevier", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IndicatorField = typeof(SceneLoader).GetField(
            "clickIndicator", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ClickedField = typeof(SceneLoader).GetField(
            "clicked", BindingFlags.Instance | BindingFlags.NonPublic);

        private static OnPointerClick WaitingReceiver()
        {
            try
            {
                var loader = SceneLoader.Instance;
                if (!SceneLoader.IsSceneLoading || loader == null) return null;
                var receiver = ReceiverField?.GetValue(loader) as OnPointerClick;
                var indicator = IndicatorField?.GetValue(loader) as FadeGroup;
                if (receiver == null || !receiver.isActiveAndEnabled || indicator == null || !indicator.IsShown
                    || indicator.IsHidingInProgress || ClickedField?.GetValue(loader) is not bool clicked || clicked)
                    return null;
                return receiver;
            }
            catch (Exception e)
            {
                LogWarnOnce("Loading gate state unreadable: " + e.Message);
                return null;
            }
        }

        private static bool _warned;

        private static void LogWarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            Log.Warn(message);
        }

        public static bool IsWaiting => WaitingReceiver() != null;

        public static bool TryConfirm()
        {
            OnPointerClick receiver;
            try
            {
                receiver = WaitingReceiver();
            }
            catch (Exception e)
            {
                Log.Warn("Loading gate confirm failed: " + e.Message);
                return false;
            }
            if (receiver == null) return false;
            // Invoke the same receiver as a real click. Never force scene activation or
            // latch input while the loading indicator is still running.
            try
            {
                ((IPointerClickHandler)receiver).OnPointerClick(new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 1,
                    clickTime = Time.unscaledTime
                });
            }
            catch (Exception e)
            {
                Log.Warn("Loading gate click failed: " + e.Message);
            }
            // Belt and braces: the loader waits on its `clicked` flag, so set it
            // directly. A handler failure can never strand the player on the curtain.
            try
            {
                ClickedField?.SetValue(SceneLoader.Instance, true);
            }
            catch (Exception e)
            {
                Log.Warn("Loading gate latch failed: " + e.Message);
            }
            return true;
        }
    }
}
