using Duckov;
using Duckov.UI;

namespace DuckovPad
{
    internal static class QuickSlotInput
    {
        public static void Activate(int slot)
        {
            if (slot < 3 || slot > 8 || !InputManager.InputActived
                || GameManager.Paused || View.ActiveView != null
                || (PauseMenu.Instance != null && PauseMenu.Instance.Shown)) return;

            // CharacterInputControl caches its character in vanilla Update, which
            // our controller prefix skips. Never depend on that private cache.
            var character = CharacterMainControl.Main;
            if (character == null) return;
            var item = ItemShortcut.Get(slot - 3);
            if (item == null) return;

            // Match the keyboard shortcut's use/equip priority and game rules.
            if (item.UsageUtilities != null && item.UsageUtilities.IsUsable(item, character))
                character.UseItem(item);
            else if (item.GetBool("IsSkill") || item.HasHandHeldAgent)
                character.ChangeHoldItem(item);
        }
    }
}
