using System;
using System.Collections.Generic;
using Dialogues;
using Duckov.Buildings.UI;
using Duckov.MasterKeys.UI;
using Duckov.MiniMaps.UI;
using Duckov.Quests.UI;
using Duckov.UI;
using Duckov.UI.PlayerStats;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace DuckovPad
{
    /// <summary>
    /// Menu and inventory control.
    ///
    /// Escape from Duckov's UI is a Tarkov-style grid: drag-and-drop, hover tooltips,
    /// context menus. A hidden pointer supplies the game's hover/tooltips while the
    /// controller keeps a persistent focus on one slot. The precision binding enables a visible free cursor.
    ///
    /// This is done with synthetic Input System events rather than OS-level cursor calls,
    /// so it behaves the same on Windows, Linux and Steam Deck.
    /// </summary>
    internal sealed class UiDriver
    {
        private readonly PadConfig _config;
        private readonly UiSnap _snap;
        private readonly UiNavigation _navigation = new UiNavigation();
        private readonly UiActions _actions = new UiActions();
        private View _view;
        private bool _operationMenuOpen;
        private bool _splitDialogueOpen;
        private GameObject _returnTarget;
        private bool _wasFreeCursor;
        private bool _hidCursor;
        private bool _pointerActive;

        private Vector2 _cursor;
        private bool _cursorInitialised;
        private bool _leftDown;
        private bool _rightDown;

        /// <summary>Keys queued for release on the following frame, so presses are unambiguous.</summary>
        private readonly List<Key> _keysToRelease = new List<Key>();
        private readonly List<Key> _keysHeld = new List<Key>();

        public Vector2 CursorPosition => _cursor;
        public bool CursorActive { get; private set; }
        public bool HidePointer { get; private set; }

        /// <summary>Bumper diagnostics, surfaced by the debug overlay.</summary>
        public string SelectedDescription => _snap.DescribeSelection();
        public int SlotCount => _snap.CountSlots();
        public string LastBumperMessage => _snap.LastCycleMessage;

        /// <summary>The element the cursor is resting on, for the on-screen highlight.</summary>
        public bool HasHover => _snap.HasHover;
        public Rect HoverRect => _snap.HoverRect;
        public int SnapTargetCount => _snap.TargetCount;

        public UiDriver(PadConfig config)
        {
            _config = config;
            _snap = new UiSnap(config);
        }

        public void Reset()
        {
            _view = null;
            _returnTarget = null;
            _operationMenuOpen = false;
            _splitDialogueOpen = false;
            _cursorInitialised = false;
            _pointerActive = false;
            CursorActive = false;
            HidePointer = false;
            if (_hidCursor) UnityEngine.Cursor.visible = true;
            _hidCursor = false;
            _navigation.Reset();
            _snap.Invalidate();
            ReleaseAllButtons();
        }

        /// <summary>Called when leaving UI mode so no button is left stuck down.</summary>
        public void ReleaseAllButtons()
        {
            if (!_leftDown && !_rightDown && _keysHeld.Count == 0) return;

            _leftDown = false;
            _rightDown = false;
            _keysHeld.Clear();
            _keysToRelease.Clear();

            var mouse = Mouse.current;
            if (mouse != null)
                InputSystem.QueueStateEvent(mouse, new MouseState { position = _cursor });

            var keyboard = Keyboard.current;
            if (keyboard != null)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        public void Update(float deltaTime)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var buttons = _config.Buttons;
            var settings = _config.Cursor;
            Vector2 pointerDelta = _cursorInitialised ? ControllerDevice.PointerDelta : Vector2.zero;

            bool operationMenuOpen = ItemOperationMenu.Instance != null && ItemOperationMenu.Instance.open;
            bool splitDialogueOpen = ViewUtil.SplitDialogueOpen;
            if (_view != View.ActiveView || _operationMenuOpen != operationMenuOpen || _splitDialogueOpen != splitDialogueOpen)
            {
                bool freshView = _view != View.ActiveView;
                if (operationMenuOpen && !_operationMenuOpen) _returnTarget = _snap.SelectedObject;
                _view = View.ActiveView;
                _operationMenuOpen = operationMenuOpen;
                _splitDialogueOpen = splitDialogueOpen;
                _snap.Invalidate();
                if (freshView) RequestAutoSelectFor(_view);
                if (!operationMenuOpen && !splitDialogueOpen && _returnTarget != null)
                {
                    _snap.RestoreSelection(_returnTarget);
                    _returnTarget = null;
                }
                _navigation.Reset();
            }

            if (!_cursorInitialised)
            {
                _cursor = mouse.position.ReadValue();
                if (_cursor.x <= 0f || _cursor.y <= 0f || _cursor.x >= Screen.width || _cursor.y >= Screen.height)
                    _cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                _cursorInitialised = true;
            }

            // Tabs are view-level actions. Consume the press before scanning, hovering,
            // or activating slots so focus and fade timing cannot change its meaning.
            FlushKeyReleases();
            bool capturingBind = PadBindingRow.SuppressMenuKeys();
            bool builder = IsBuilderActive();
            if (!capturingBind && !builder && !splitDialogueOpen)
            {
                int direction = Pad.Down(buttons.UiPageNext) ? 1
                    : Pad.Down(buttons.UiPagePrevious) ? -1 : 0;
                if (direction != 0 && TabIndex(View.ActiveView) >= 0)
                {
                    if (operationMenuOpen) ItemOperationMenu.Instance.Close();
                    ReleaseAllButtons();
                    Bumper(direction);
                    return;
                }
                if (!operationMenuOpen && ViewUtil.StashOpen)
                {
                    direction = Pad.Down(buttons.UiStashNext) ? 1
                        : Pad.Down(buttons.UiStashPrevious) ? -1 : 0;
                    if (direction != 0)
                    {
                        ReleaseAllButtons();
                        StashPage(direction);
                        return;
                    }
                }
            }

            // ---- focus navigation ----
            // MenuDeadzone sits above typical drift (~0.15-0.25) so a worn stick can't
            // creep the cursor or steal focus. DpadOnly ignores sticks entirely.
            float menuDeadzone = Mathf.Max(settings.Deadzone, _config.UiSnap.MenuDeadzone);
            bool dpadOnly = _config.UiSnap.DpadOnly;
            Vector2 stick = dpadOnly ? Vector2.zero : Pad.LeftStick(menuDeadzone, 0.95f, settings.ResponseCurve);
            if (!ControllerDevice.UsePointer || stick.sqrMagnitude > 0.04f ||
                Pad.Down(buttons.UiNavUp) || Pad.Down(buttons.UiNavDown) ||
                Pad.Down(buttons.UiNavLeft) || Pad.Down(buttons.UiNavRight)) _pointerActive = false;
            if (ControllerDevice.UsePointer && pointerDelta.sqrMagnitude > 0.01f) _pointerActive = true;
            bool precision = Pad.Held(buttons.UiPrecision)
                && !(ViewUtil.StashOpen && (buttons.UiPrecision == buttons.UiStashPrevious || buttons.UiPrecision == buttons.UiStashNext));
            bool freeCursor = _pointerActive || precision || builder || !_config.UiSnap.StickNavigation;
            _snap.Refresh();
            if (_snap.ConsumeAutoSelect(out var autoCursor))
                _cursor = autoCursor;
            if (_wasFreeCursor != freeCursor) _navigation.Reset();
            _wasFreeCursor = freeCursor;
            float speed = settings.Speed;
            if (settings.ScaleWithResolution)
                speed *= Screen.height / 1080f;
            if (precision)
                speed *= settings.PrecisionMultiplier;
            if (freeCursor) _cursor += stick * speed * deltaTime + (_pointerActive ? pointerDelta : Vector2.zero);
            else _snap.KeepSelection(_cursor, out _cursor);

            Vector2 dpad = Vector2.zero;
            if (_config.UiSnap.DirectionalSnap)
            {
                if (Pad.Held(buttons.UiNavUp)) dpad = Vector2.up;
                else if (Pad.Held(buttons.UiNavDown)) dpad = Vector2.down;
                else if (Pad.Held(buttons.UiNavLeft)) dpad = Vector2.left;
                else if (Pad.Held(buttons.UiNavRight)) dpad = Vector2.right;
            }
            Vector2 step = _navigation.ReadStep(freeCursor || dpadOnly ? Vector2.zero : Pad.LeftStickRaw,
                dpad, Time.unscaledTime, menuDeadzone);
            if (step != Vector2.zero && !_snap.AdjustSlider(step))
                _snap.TrySnap(_cursor, step, out _cursor);
            if (freeCursor)
                _cursor = _snap.ApplyMagnetism(_cursor, stick.magnitude, deltaTime,
                    allowPull: !_pointerActive && !precision && !builder && !_leftDown);

            _cursor.x = Mathf.Clamp(_cursor.x, 0f, Screen.width - 1f);
            _cursor.y = Mathf.Clamp(_cursor.y, 0f, Screen.height - 1f);

            // ---- scrolling ----
            Vector2 scrollStick = dpadOnly ? Vector2.zero : Pad.RightStick(menuDeadzone, 0.95f, settings.ResponseCurve);
            Vector2 scroll = Vector2.zero;
            if (Mathf.Abs(scrollStick.y) > 0f)
                scroll.y = scrollStick.y * settings.ScrollSpeed * settings.ScrollUnitsPerNotch * deltaTime;

            // ---- buttons ----
            bool left = freeCursor && (Pad.Held(buttons.UiClick) || Pad.Held(buttons.UiDragModifier));
            bool right = freeCursor && Pad.Held(buttons.UiContext);

            // When a trackpad/touchscreen owns the pointer, preserve its real click,
            // drag and wheel events. Only synthesize state for actual pad input or to
            // release a mouse button we previously synthesized.
            bool writeMouse = !_pointerActive || left || right || _leftDown || _rightDown || scroll != Vector2.zero;
            bool nativeLeft = _pointerActive && !_leftDown && mouse.leftButton.isPressed;
            bool nativeRight = _pointerActive && !_rightDown && mouse.rightButton.isPressed;

            _leftDown = left;
            _rightDown = right;

            var state = new MouseState { position = _cursor, scroll = scroll };
            state = state.WithButton(MouseButton.Left, left || nativeLeft);
            state = state.WithButton(MouseButton.Right, right || nativeRight);
            if (writeMouse) InputSystem.QueueStateEvent(mouse, state);

            // Keep the visible OS cursor with the virtual one, otherwise the player
            // sees a stationary arrow while the UI reacts somewhere else.
            try
            {
                if (writeMouse) mouse.WarpCursorPosition(_cursor);
            }
            catch (Exception)
            {
                // Not fatal — the synthetic state above still drives the UI.
            }

            CursorActive = true;
            HidePointer = !freeCursor && _snap.HasHover && _config.UiSnap.HideCursor;
            ApplyCursorVisibility();

            bool canActivate = _snap.CanActivateSelection();
            if (!freeCursor && canActivate)
            {
                if (Pad.Down(buttons.UiClick)) _actions.Confirm(_snap.SelectedObject, _cursor);
                if (Pad.Down(buttons.UiContext)) _actions.ClickItemOrControl(_snap.SelectedObject, _cursor, PointerEventData.InputButton.Right);
            }
            if (canActivate && Pad.Down(buttons.UiQuickMove)) _actions.QuickMove(_snap.SelectedObject);
            if (canActivate && Pad.Down(buttons.UiLockSort)) _actions.ToggleLock(_snap.SelectedObject);
            // Direct slot actions. These use the focused slot itself, so they work
            // while the item tooltip is up — unlike their keyboard equivalents,
            // which require mouse hover. Unbound (empty) bindings never fire.
            if (canActivate && Pad.Down(buttons.UiDrop)) _actions.Drop(_snap.SelectedObject);
            if (canActivate && Pad.Down(buttons.UiUse)) _actions.Use(_snap.SelectedObject);
            if (canActivate && Pad.Down(buttons.UiMark)) _actions.ToggleMark(_snap.SelectedObject);

            // ---- discrete UI actions ----
            // While a Controls-tab row is capturing a new binding, Back cancels the
            // capture and Back/Close/page-turn stay down so the panel isn't closed
            // or flipped away mid-capture.
            if (Pad.Down(buttons.UiBack)) Back();

            if (!capturingBind)
            {
                if (builder)
                {
                    if (Pad.Down(buttons.UiRotate)) PressKey(Key.Q);    // Builder_Rotate
                }

                if (Pad.Down(buttons.UiClose)) CloseActiveView();
            }

            // Either Continue gate can strand the whole game; any pad button passes.
            // Both confirms self-guard on their own shown state, so this is harmless
            // when no gate is waiting.
            if (Pad.AnyButtonDown())
            {
                if (!LoadingContinue.TryConfirm()) ConfirmTitleScreen();
            }
        }

        public void ApplyCursorVisibility()
        {
            if (HidePointer && Application.isFocused)
            {
                UnityEngine.Cursor.visible = false;
                _hidCursor = true;
            }
            else if (_hidCursor)
            {
                UnityEngine.Cursor.visible = true;
                _hidCursor = false;
            }
        }

        private static void Back()
        {
            // A Controls-tab binding row is listening: cancel it instead of backing out.
            if (PadBindingRow.ConsumeBackForCapture()) return;
            if (ViewUtil.SplitDialogueOpen)
                SplitDialogue.Instance.Cancel();
            else if (ItemOperationMenu.Instance != null && ItemOperationMenu.Instance.open)
                ItemOperationMenu.Instance.Close();
            else if (ItemUIUtilities.SelectedItem != null)
                ItemUIUtilities.Select(null);
            else CloseActiveView();
        }

        /// <summary>
        /// The "Click to Continue" title screen is a single full-screen click target and it
        /// gates the whole game, so the pad must never be unable to pass it. Delegates to
        /// the dedicated gate: direct handler invocation that self-guards on the fade
        /// state, so firing when no gate is waiting is harmless.
        /// </summary>
        internal static void ConfirmTitleScreen() => TitleGate.TryConfirm();

        private static bool IsBuilderActive()
        {
            try
            {
                return BuilderView.Instance != null && View.ActiveView == BuilderView.Instance;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void CloseActiveView() => ViewUtil.CloseActive();

        /// <summary>
        /// Fresh loot/inventory views start focused: the crate/body's first item out
        /// in the field, the player's first slot for solo inventory, the other inventory for loot and stash.
        /// </summary>
        private void RequestAutoSelectFor(View view)
        {

            try
            {
                if (view is LootView loot)
                {
                    // InventoryView.Show also opens LootView, with no target inventory.
                    // Choose the panel by its actual contents, not the view's type.
                    bool hasOtherInventory = loot.TargetInventory != null;
                    _snap.RequestAutoSelect(hasOtherInventory ? UiSnap.AutoSelectSide.Loot : UiSnap.AutoSelectSide.Player, new List<Transform>
                    {
                        Panel(loot, hasOtherInventory ? "lootTargetInventoryDisplay" : "characterInventoryDisplay")
                    });
                }
                else if (view is InventoryView inv)
                {
                    _snap.RequestAutoSelect(UiSnap.AutoSelectSide.Player, new List<Transform>
                    {
                        Panel(inv, "inventoryDisplay")
                    });
                }
            }
            catch (Exception e)
            {
                Log.Warn("Auto-select setup failed: " + e.Message);
            }
        }

        /// <summary>Read a private panel reference off a view; null when renamed or missing.</summary>
        private static Transform Panel(Component view, string field)
        {
            try
            {
                var info = view.GetType().GetField(field,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var component = info?.GetValue(view) as Component;
                return component != null ? component.transform : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Bumpers flip pages and bottom-tab views — never walk slots, so one press
        /// always moves exactly one page/tab: the six
        /// HUD-tab views (inventory, stats, quests, map, keys, formulas) hop to the
        /// next tab without keyboard navigation fallbacks. Slot-to-slot movement
        /// stays on the D-pad and stick, which is what keeps the focus stable.
        /// </summary>
        private void Bumper(int direction)
        {
            int tab = TabIndex(View.ActiveView);
            if (tab >= 0 && !ViewUtil.SplitDialogueOpen) SwitchTabView(tab, direction);
        }

        private void StashPage(int direction)
        {
            if (!ViewUtil.StashOpen) return;
            var root = Panel(View.ActiveView, "lootTargetInventoryDisplay");
            var display = root != null ? root.GetComponent<InventoryDisplay>() : null;
            if (display == null || display.MaxPage <= 1) return;
            if (direction > 0) display.NextPage();
            else display.PreviousPage();
            _snap.Invalidate();
            RequestAutoSelectFor(View.ActiveView);
            _navigation.Reset();
            _snap.NotePageTurn(direction > 0 ? "stash next" : "stash previous");
        }

        /// <summary>Bottom-tab order, matching the HUD bar left to right.</summary>
        private const int TabCount = 6;

        private static int TabIndex(View view)
        {
            if (view == null) return -1;
            if (view is InventoryView || view is LootView) return 0;
            if (view is PlayerStatsView) return 1;
            if (view is QuestView) return 2;
            if (view is MiniMapView) return 3;
            if (view is MasterKeysView) return 4;
            if (view is FormulasIndexView) return 5;
            return -1;
        }

        private static string TabName(int tab)
        {
            switch (tab)
            {
                case 0: return "inventory";
                case 1: return "stats";
                case 2: return "quests";
                case 3: return "map";
                case 4: return "keys";
                case 5: return "formulas";
                default: return "?";
            }
        }

        /// <summary>Open the next/previous bottom-tab view, like its HUD button would.</summary>
        private void SwitchTabView(int currentTab, int direction)
        {
            int next = (currentTab + direction + TabCount) % TabCount;
            if (next == currentTab) return;
            try
            {
                switch (next)
                {
                    case 0: OpenInventoryTab(); break;
                    case 1:
                        if (PlayerStatsView.Instance != null) PlayerStatsView.Instance.Open();
                        break;
                    case 2:
                        if (!GameManager.Paused && !DialogueUI.Active) QuestView.Show();
                        break;
                    case 3:
                        if (!GameManager.Paused && !SceneLoader.IsSceneLoading) MiniMapView.Show();
                        break;
                    case 4:
                        // MasterKeysView.Show is internal to the game; open the instance.
                        if (!GameManager.Paused && !DialogueUI.Active && !SceneLoader.IsSceneLoading)
                            ViewUtil.TryOpen(MasterKeysView.Instance);
                        break;
                    case 5:
                        if (!GameManager.Paused && !DialogueUI.Active && !SceneLoader.IsSceneLoading)
                            FormulasIndexView.Show();
                        break;
                }
                _snap.NotePageTurn((direction > 0 ? "RB" : "LB") + ": tab → " + TabName(next));
            }
            catch (Exception e)
            {
                Log.Warn("Tab switch failed: " + e.Message);
            }
        }

        /// <summary>Same open rules as the inventory button: stash at base, pack elsewhere.</summary>
        private static void OpenInventoryTab()
        {
            if (GameManager.Paused || DialogueUI.Active || SceneLoader.IsSceneLoading) return;
            if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
                PlayerStorage.Instance.InteractableLootBox.InteractWithMainCharacter();
            else
                InventoryView.Show();
        }

        // ------------------------------------------------------------------
        // Some UI actions only have keyboard bindings in the game's action asset.
        // Pressing the key for one full frame is the least invasive way to reach
        // them without duplicating the game's own view logic.
        // ------------------------------------------------------------------

        private void PressKey(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_keysHeld.Contains(key)) return;
            _keysHeld.Add(key);
            _keysToRelease.Add(key);

            var state = new KeyboardState();
            foreach (var held in _keysHeld)
                state.Set(held, true);

            InputSystem.QueueStateEvent(keyboard, state);
        }

        private void FlushKeyReleases()
        {
            if (_keysToRelease.Count == 0) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (var key in _keysToRelease)
                    _keysHeld.Remove(key);

                var state = new KeyboardState();
                foreach (var held in _keysHeld)
                    state.Set(held, true);

                InputSystem.QueueStateEvent(keyboard, state);
            }

            _keysToRelease.Clear();
        }
    }
}
