using System;
using System.Collections.Generic;
using Duckov.Buildings.UI;
using Duckov.UI;
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
    /// controller keeps a persistent focus on one slot. LT enables a visible free cursor.
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
            _cursorInitialised = false;
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

            bool operationMenuOpen = ItemOperationMenu.Instance != null && ItemOperationMenu.Instance.open;
            bool splitDialogueOpen = ViewUtil.SplitDialogueOpen;
            if (_view != View.ActiveView || _operationMenuOpen != operationMenuOpen || _splitDialogueOpen != splitDialogueOpen)
            {
                if (operationMenuOpen && !_operationMenuOpen) _returnTarget = _snap.SelectedObject;
                _view = View.ActiveView;
                _operationMenuOpen = operationMenuOpen;
                _splitDialogueOpen = splitDialogueOpen;
                _snap.Invalidate();
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

            // ---- focus navigation ----
            Vector2 stick = Pad.LeftStick(settings.Deadzone, 0.95f, settings.ResponseCurve);
            bool precision = Pad.Held(buttons.UiPrecision);
            bool builder = IsBuilderActive();
            bool freeCursor = precision || builder || !_config.UiSnap.StickNavigation;
            _snap.Refresh();
            if (_wasFreeCursor != freeCursor) _navigation.Reset();
            _wasFreeCursor = freeCursor;
            float speed = settings.Speed;
            if (settings.ScaleWithResolution)
                speed *= Screen.height / 1080f;
            if (precision)
                speed *= settings.PrecisionMultiplier;
            if (freeCursor) _cursor += stick * speed * deltaTime;
            else _snap.KeepSelection(_cursor, out _cursor);

            Vector2 dpad = Vector2.zero;
            if (_config.UiSnap.DirectionalSnap)
            {
                if (Pad.Held(buttons.UiNavUp)) dpad = Vector2.up;
                else if (Pad.Held(buttons.UiNavDown)) dpad = Vector2.down;
                else if (Pad.Held(buttons.UiNavLeft)) dpad = Vector2.left;
                else if (Pad.Held(buttons.UiNavRight)) dpad = Vector2.right;
            }
            Vector2 step = _navigation.ReadStep(freeCursor ? Vector2.zero : Pad.LeftStickRaw,
                dpad, Time.unscaledTime, settings.Deadzone);
            if (step != Vector2.zero && !_snap.AdjustSlider(step))
                _snap.TrySnap(_cursor, step, out _cursor);
            if (freeCursor)
                _cursor = _snap.ApplyMagnetism(_cursor, stick.magnitude, deltaTime,
                    allowPull: !precision && !builder && !_leftDown);

            _cursor.x = Mathf.Clamp(_cursor.x, 0f, Screen.width - 1f);
            _cursor.y = Mathf.Clamp(_cursor.y, 0f, Screen.height - 1f);

            // ---- scrolling ----
            Vector2 scrollStick = Pad.RightStick(settings.Deadzone, 0.95f, settings.ResponseCurve);
            Vector2 scroll = Vector2.zero;
            if (Mathf.Abs(scrollStick.y) > 0f)
                scroll.y = scrollStick.y * settings.ScrollSpeed * settings.ScrollUnitsPerNotch * deltaTime;

            // ---- buttons ----
            bool left = freeCursor && (Pad.Held(buttons.UiClick) || Pad.Held(buttons.UiDragModifier));
            bool right = freeCursor && Pad.Held(buttons.UiContext);

            _leftDown = left;
            _rightDown = right;

            var state = new MouseState { position = _cursor, scroll = scroll };
            state = state.WithButton(MouseButton.Left, left);
            state = state.WithButton(MouseButton.Right, right);
            InputSystem.QueueStateEvent(mouse, state);

            // Keep the visible OS cursor with the virtual one, otherwise the player
            // sees a stationary arrow while the UI reacts somewhere else.
            try
            {
                mouse.WarpCursorPosition(_cursor);
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
            FlushKeyReleases();

            // While a Controls-tab row is capturing a new binding, Back cancels the
            // capture and Back/Close/page-turn stay down so the panel isn't closed
            // or flipped away mid-capture.
            bool capturingBind = PadBindingRow.SuppressMenuKeys();

            if (Pad.Down(buttons.UiBack)) Back();

            if (!capturingBind)
            {
                if (builder)
                {
                    if (Pad.Down(buttons.UiRotate)) PressKey(Key.Q);    // Builder_Rotate
                }
                else
                {
                    if (Pad.Down(buttons.UiPageNext)) PressKey(Key.S);      // UI_NextPage
                    if (Pad.Down(buttons.UiPagePrevious)) PressKey(Key.W);  // UI_PreviousPage
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
