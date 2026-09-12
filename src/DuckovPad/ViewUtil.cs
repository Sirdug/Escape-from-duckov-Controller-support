using System;
using System.Reflection;
using Duckov.UI;
using Duckov.Quests.UI;
using UnityEngine;

namespace DuckovPad
{
    internal static class ViewUtil
    {
        // View.TryQuit is internal virtual. Subclasses override it to add their own
        // "can I close right now?" rules, so going through it — rather than calling
        // Close() directly — is what keeps confirmation prompts and locked views working.
        private static readonly MethodInfo TryQuitMethod =
            typeof(View).GetMethod("TryQuit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo SplitFadeGroup =
            typeof(SplitDialogue).GetField("fadeGroup", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DialogueWaitingForChoice =
            typeof(Dialogues.DialogueUI).GetField("waitingForChoice", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DialogueChoiceFade =
            typeof(Dialogues.DialogueUI).GetField("choiceListFadeGroup", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ConfirmFade =
            typeof(ConfirmDialogue).GetField("fadeGroup", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo QuestCompleteFade =
            typeof(QuestCompletePanel).GetField("mainFadeGroup", BindingFlags.Instance | BindingFlags.NonPublic);
        private static float _nextPopupScan;
        private static Transform _popupRoot;

        public static Transform PopupRoot
        {
            get
            {
                if (Time.unscaledTime < _nextPopupScan) return _popupRoot;
                _nextPopupScan = Time.unscaledTime + 0.1f;
                _popupRoot = null;
                foreach (var popup in UnityEngine.Object.FindObjectsOfType<ConfirmDialogue>())
                    if (PopupShown(popup, ConfirmFade)) return _popupRoot = popup.transform;
                foreach (var popup in UnityEngine.Object.FindObjectsOfType<QuestCompletePanel>())
                    if (PopupShown(popup, QuestCompleteFade)) return _popupRoot = popup.transform;
                return null;
            }
        }

        private static bool PopupShown(Behaviour popup, FieldInfo field)
        {
            if (!popup.isActiveAndEnabled) return false;
            var fade = field?.GetValue(popup) as Duckov.UI.Animations.FadeGroup;
            return fade != null && fade.IsShown && !fade.IsHidingInProgress;
        }

        public static bool DialogueChoosing
        {
            get
            {
                var dialogue = Dialogues.DialogueUI.instance;
                if (!Dialogues.DialogueUI.Active || dialogue == null) return false;
                // Include the fade-in frame before the game's choice wait starts.
                var fade = DialogueChoiceFade?.GetValue(dialogue) as Duckov.UI.Animations.FadeGroup;
                return Equals(DialogueWaitingForChoice?.GetValue(dialogue), true) ||
                    (fade != null && fade.IsShown);
            }
        }

        public static bool StashOpen => View.ActiveView is LootView loot
            && loot.TargetInventory != null && loot.TargetInventory == PlayerStorage.Inventory;

        public static bool SplitDialogueOpen
        {
            get
            {
                var dialogue = SplitDialogue.Instance;
                if (dialogue == null || !dialogue.isActiveAndEnabled) return false;
                var fade = SplitFadeGroup?.GetValue(dialogue) as Duckov.UI.Animations.FadeGroup;
                return fade != null && fade.IsShown && !fade.IsHidingInProgress;
            }
        }

        /// <summary>Ask a view to close the same way the game's own Escape/Tab handling does.</summary>
        public static void TryQuit(View view)
        {
            if (view == null) return;

            if (TryQuitMethod != null)
            {
                try
                {
                    TryQuitMethod.Invoke(view, null);
                    return;
                }
                catch (Exception e)
                {
                    Log.Warn("View.TryQuit failed, closing directly: " + (e.InnerException ?? e).Message);
                }
            }

            try
            {
                view.Close();
            }
            catch (Exception)
            {
                // View already torn down.
            }
        }

        /// <summary>Close whatever view is currently on top, if any.</summary>
        public static void CloseActive() => TryQuit(View.ActiveView);

        /// <summary>Open a view instance (e.g. MasterKeysView, whose static Show is internal).</summary>
        public static void TryOpen(View view)
        {
            if (view == null) return;
            try
            {
                // ManagedUIElement.Open has an optional parent argument. A direct
                // call supplies that argument; reflection with zero arguments fails.
                view.Open(null);
            }
            catch (Exception e)
            {
                Log.Warn("View.Open failed for " + view.GetType().Name + ": " + (e.InnerException ?? e).Message);
            }
        }
    }
}
