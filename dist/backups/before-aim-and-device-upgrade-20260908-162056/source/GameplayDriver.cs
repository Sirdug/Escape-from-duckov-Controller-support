using System;
using System.Reflection;
using Dialogues;
using Duckov.MiniMaps.UI;
using Duckov.Quests.UI;
using Duckov.UI;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Drives the duck while no menu is open. Everything here goes through the game's own
    /// <see cref="InputManager"/> entry points, which is what the keyboard path uses, so
    /// gameplay rules (input blocking, pause, death, skill state) still apply.
    /// </summary>
    internal sealed class GameplayDriver
    {
        private static readonly MethodInfo ShortCutInputMethod =
            typeof(CharacterInputControl).GetMethod("ShortCutInput", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PadConfig _config;
        private readonly AimDriver _aim;
        private readonly Rumble _rumble;

        private int _weaponSlot = 1;
        private bool _skillAimActive;
        private bool _lockFromTrigger;

        public bool AdsHeld { get; private set; }

        public GameplayDriver(PadConfig config, AimDriver aim, Rumble rumble)
        {
            _config = config;
            _aim = aim;
            _rumble = rumble;

            if (ShortCutInputMethod == null)
                Log.Warn("CharacterInputControl.ShortCutInput not found — quick-item slots will not work.");
        }

        public void Reset()
        {
            _skillAimActive = false;
            _lockFromTrigger = false;
            AdsHeld = false;
            _aim.Reset();
        }

        public void Update(CharacterInputControl control, InputManager inputManager, CharacterMainControl character, float deltaTime)
        {
            var buttons = _config.Buttons;

            // ---------------- movement ----------------
            Vector2 move = Pad.LeftStick(_config.Move.Deadzone, _config.Move.OuterDeadzone, _config.Move.ResponseCurve);
            inputManager.SetMoveInput(move);

            bool sprint = Pad.Held(buttons.Sprint);
            if (_config.Move.AutoRunAtFullStick && Pad.LeftStickRaw.magnitude >= _config.Move.AutoRunThreshold)
                sprint = true;
            inputManager.SetRunInput(sprint);

            // ---------------- firing (read before aim; assist depends on it) ----------------
            bool fireHeld = Pad.Held(buttons.Fire);
            bool fireDown = Pad.Down(buttons.Fire);
            bool fireUp = Pad.Up(buttons.Fire);

            // ---------------- lock-on ----------------
            var snap = _config.AimSnap;
            if (snap.Enabled)
            {
                bool hold = string.Equals(snap.Mode, "hold", StringComparison.OrdinalIgnoreCase);
                if (hold)
                {
                    if (Pad.Down(buttons.LockOn)) _aim.AcquireLock(character);
                    if (Pad.Up(buttons.LockOn)) _aim.ClearLock();
                }
                else if (Pad.Down(buttons.LockOn))
                {
                    bool locked = _aim.ToggleLockOn(character);
                    if (locked) _rumble.Pulse(0.0f, 0.25f, 0.08f);
                }

                // Snap-on-fire is a convenience, not a mode: the lock it creates is
                // released again when the trigger comes up, so it never leaves the
                // player stuck on a target they didn't choose.
                if (snap.SnapOnFire && fireDown && !_aim.LockedOn)
                    _lockFromTrigger = _aim.AcquireLock(character);

                if (_lockFromTrigger && fireUp)
                {
                    _aim.ClearLock();
                    _lockFromTrigger = false;
                }
            }

            // ---------------- aim ----------------
            AdsHeld = Pad.Held(buttons.Ads);
            inputManager.SetAdsInput(AdsHeld);
            _aim.Update(inputManager, character, AdsHeld, fireHeld, deltaTime);

            inputManager.SetTrigger(fireHeld, fireDown, fireUp);

            bool holdingItemSkill = false;
            try
            {
                holdingItemSkill = character.skillAction != null
                                   && character.skillAction.holdItemSkillKeeper != null
                                   && character.skillAction.holdItemSkillKeeper.CheckSkillAndBinding();
            }
            catch (Exception)
            {
                // Skill state can be momentarily invalid during scene transitions.
            }

            if (holdingItemSkill)
            {
                inputManager.SetAimType(AimTypes.handheldSkill);
                if (fireDown) inputManager.StartItemSkillAim();
                else if (fireUp) inputManager.ReleaseItemSkill();
            }
            else
            {
                inputManager.SetAimType(AimTypes.normalAim);
            }

            if (fireDown) _rumble.Shoot();

            // ---------------- one-shot actions ----------------
            if (Pad.Down(buttons.Dash)) inputManager.Dash();
            if (Pad.Down(buttons.Interact)) inputManager.Interact();
            if (Pad.Down(buttons.PutAway)) inputManager.PutAway();
            if (Pad.Down(buttons.Quack)) inputManager.Quack();
            if (Pad.Down(buttons.StopAction)) inputManager.StopAction();

            if (Pad.Down(buttons.Reload))
            {
                var main = CharacterMainControl.Main;
                if (main != null) main.TryToReload();
            }

            if (!GameManager.Paused)
            {
                if (Pad.Down(buttons.NightVision)) inputManager.ToggleNightVision();
                if (Pad.Down(buttons.ToggleView)) inputManager.ToggleView();
            }

            // Character skill: press to start aiming, release to fire it.
            if (Pad.Down(buttons.CharacterSkill))
            {
                inputManager.StartCharacterSkillAim();
                _skillAimActive = true;
            }
            else if (_skillAimActive && Pad.Up(buttons.CharacterSkill))
            {
                inputManager.ReleaseCharacterSkill();
                _skillAimActive = false;
            }

            // ---------------- weapons & items ----------------
            if (Pad.Down(buttons.SwitchWeapon))
            {
                _weaponSlot = _weaponSlot == 1 ? 2 : 1;
                inputManager.SwitchItemAgent(_weaponSlot);
            }

            if (Pad.Down(buttons.MeleeWeapon))
            {
                _weaponSlot = 3;
                inputManager.SwitchItemAgent(3);
            }

            if (Pad.Down(buttons.ShortcutNext)) CycleItemAgent(inputManager, 1);
            if (Pad.Down(buttons.ShortcutPrevious)) CycleItemAgent(inputManager, -1);

            // D-pad up/down mirrors the mouse wheel, honouring the player's
            // "scroll wheel behaviour" option just like the vanilla binding does.
            if (Pad.Down(buttons.CycleNext)) Scroll(inputManager, 1);
            if (Pad.Down(buttons.CyclePrevious)) Scroll(inputManager, -1);

            if (Pad.Down(buttons.QuickItem3)) ShortCut(control, 3);
            if (Pad.Down(buttons.QuickItem4)) ShortCut(control, 4);
            if (Pad.Down(buttons.QuickItem5)) ShortCut(control, 5);
            if (Pad.Down(buttons.QuickItem6)) ShortCut(control, 6);

            // ---------------- menus ----------------
            if (Pad.Down(buttons.PauseMenu)) PauseMenu.Toggle();
            if (Pad.Down(buttons.Inventory)) ToggleInventory();
            if (Pad.Down(buttons.Map)) ToggleMap();
            if (Pad.Down(buttons.QuestLog)) ToggleQuestLog();
        }

        private void CycleItemAgent(InputManager inputManager, int direction)
        {
            _weaponSlot += direction;
            if (_weaponSlot > 3) _weaponSlot = 1;
            if (_weaponSlot < 1) _weaponSlot = 3;
            inputManager.SwitchItemAgent(_weaponSlot);
        }

        private static void Scroll(InputManager inputManager, int direction)
        {
            if (ScrollWheelBehaviour.CurrentBehaviour == ScrollWheelBehaviour.Behaviour.AmmoAndInteract)
            {
                inputManager.SetSwitchInteractInput(direction);
                inputManager.SetSwitchBulletTypeInput(direction);
            }
            else
            {
                inputManager.SetSwitchWeaponInput(direction);
            }
        }

        private static void ShortCut(CharacterInputControl control, int index)
        {
            if (ShortCutInputMethod == null || control == null) return;
            try
            {
                ShortCutInputMethod.Invoke(control, new object[] { index });
            }
            catch (Exception e)
            {
                Log.Error("Quick-item slot " + index + " failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // These mirror CharacterInputControl's own menu handlers. We can't call
        // those directly because they take an InputAction.CallbackContext that
        // only the Input System can construct meaningfully.
        // ------------------------------------------------------------------

        private static void ToggleInventory()
        {
            if (GameManager.Paused || DialogueUI.Active || SceneLoader.IsSceneLoading) return;

            if (View.ActiveView == null)
            {
                if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
                    PlayerStorage.Instance.InteractableLootBox.InteractWithMainCharacter();
                else
                    InventoryView.Show();
            }
            else
            {
                ViewUtil.CloseActive();
            }
        }

        private static void ToggleMap()
        {
            if (GameManager.Paused || SceneLoader.IsSceneLoading) return;

            if (View.ActiveView == null)
                MiniMapView.Show();
            else if (View.ActiveView is MiniMapView miniMapView)
                miniMapView.Close();
        }

        private static void ToggleQuestLog()
        {
            if (GameManager.Paused || DialogueUI.Active) return;

            if (View.ActiveView == null)
                QuestView.Show();
            else if (View.ActiveView is QuestView)
                ViewUtil.CloseActive();
        }
    }
}
