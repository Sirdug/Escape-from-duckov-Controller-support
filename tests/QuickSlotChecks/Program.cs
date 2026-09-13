using System;
using DuckovPad;

// Minimal game API doubles: deliberately no CharacterInputControl or vanilla Update.
static class InputManager { public static bool InputActived = true; }
static class GameManager { public static bool Paused; }
class CharacterMainControl
{
    public static CharacterMainControl Main = new CharacterMainControl();
    public Item Used, Held;
    public void UseItem(Item item) => Used = item;
    public void ChangeHoldItem(Item item) => Held = item;
}
class Item
{
    public Usage UsageUtilities;
    public bool Skill, HasHandHeldAgent;
    public bool GetBool(string key) => key == "IsSkill" && Skill;
}
class Usage
{
    public bool Usable = true;
    public bool IsUsable(Item item, CharacterMainControl character) => Usable;
}
namespace Duckov
{
    static class ItemShortcut
    {
        public static Item[] Items = new Item[6];
        public static Item Get(int index) => Items[index];
    }
}
namespace Duckov.UI
{
    class View { public static View ActiveView; }
    class PauseMenu { public static PauseMenu Instance; public bool Shown; }
}
static class Program
{
    static int checks;
    static void Check(bool condition) { if (!condition) throw new Exception("Check " + (checks + 1)); checks++; }
    static void Main()
    {
        var player = CharacterMainControl.Main;
        for (int slot = 3; slot <= 8; slot++)
        {
            var item = new Item { HasHandHeldAgent = true };
            Duckov.ItemShortcut.Items[slot - 3] = item;
            QuickSlotInput.Activate(slot);
            Check(player.Held == item);
        }
        var usable = new Item { UsageUtilities = new Usage(), HasHandHeldAgent = true };
        Duckov.ItemShortcut.Items[0] = usable;
        player.Held = null;
        QuickSlotInput.Activate(3);
        Check(player.Used == usable && player.Held == null);
        usable.UsageUtilities.Usable = false;
        QuickSlotInput.Activate(3);
        Check(player.Held == usable);
        var skill = new Item { Skill = true };
        Duckov.ItemShortcut.Items[0] = skill;
        QuickSlotInput.Activate(3);
        Check(player.Held == skill);
        Action<Action, Action> blocked = (setup, cleanup) => {
            player.Held = null; player.Used = null;
            setup(); QuickSlotInput.Activate(3);
            Check(player.Held == null && player.Used == null); cleanup();
        };
        blocked(() => InputManager.InputActived = false, () => InputManager.InputActived = true);
        blocked(() => GameManager.Paused = true, () => GameManager.Paused = false);
        blocked(() => Duckov.UI.View.ActiveView = new Duckov.UI.View(), () => Duckov.UI.View.ActiveView = null);
        blocked(() => Duckov.UI.PauseMenu.Instance = new Duckov.UI.PauseMenu { Shown = true }, () => Duckov.UI.PauseMenu.Instance = null);
        blocked(() => CharacterMainControl.Main = null, () => CharacterMainControl.Main = player);
        blocked(() => Duckov.ItemShortcut.Items[0] = null, () => {});
        blocked(() => Duckov.ItemShortcut.Items[0] = new Item(), () => {});
        QuickSlotInput.Activate(2); QuickSlotInput.Activate(9);
        Check(player.Held == null && player.Used == null);
        Console.WriteLine("Passed " + checks + " quick-slot checks without vanilla character initialization.");
    }
}
