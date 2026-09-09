using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Duckov.Options;
using Duckov.Scenes;
using Duckov.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovPad
{
    /// <summary>
    /// Entry point. The game's mod loader looks for a type named
    /// "&lt;mod name&gt;.ModBehaviour" deriving from Duckov.Modding.ModBehaviour.
    /// </summary>
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "com.duckovpad.controller";

        /// <summary>How far (squared, in pixels) the real cursor may drift from where we
        /// warped it before we assume the player has gone back to the mouse.</summary>
        private const float CursorDivergenceThresholdSqr = 100f;

        internal static ModBehaviour Instance { get; private set; }

        private Harmony _harmony;
        private ResolveEventHandler _assemblyResolver;
        private PadConfig _config;
        private AimDriver _aim;
        private GameplayDriver _gameplay;
        private UiDriver _ui;
        private Rumble _rumble;
        private HintHud _hud;
        private LockOutline _outline;
        private NativeOptionsUi _optionsUi;
        private NativeItemPrompts _itemPrompts;

        private float _nextOptionsAttempt;
        private bool _settingsDirty;
        private float _saveSettingsAt;

        private Key _toggleKey = Key.F5;
        private bool _userEnabled = true;

        /// <summary>Which device most recently showed real input.</summary>
        private bool _padHasFocus;
        private Vector2 _expectedCursorPosition;
        private bool _expectingCursorPosition;
        private int _drivenFrame = -1;
        private int _activeDeviceId = -1;

        private CharacterMainControl _hookedCharacter;
        private UnityEngine.Events.UnityAction<DamageInfo> _hurtHandler;

        // SceneLoader owns this flag and always clears it, so it can't latch on the way a
        // mod-local "am I mid-load?" bool can if an event is missed during boot.
        private static bool SceneLoading
        {
            get
            {
                try { return SceneLoader.IsSceneLoading; }
                catch (Exception) { return false; }
            }
        }

        internal bool Active => _userEnabled && _config != null && !SceneLoading;

        // ------------------------------------------------------------------

        protected override void OnAfterSetup()
        {
            try
            {
                // Must happen before anything touches a Harmony type, so the runtime can
                // find our bundled 0Harmony.dll if its own probing comes up empty.
                RegisterAssemblyResolver();

                var settingsPath = Path.Combine(info.path, "Settings.json");
                _config = PadConfig.Load(settingsPath);
                _userEnabled = _config.Enabled;

                Pad.Configure(_config);

                if (!Enum.TryParse(_config.ToggleKey, true, out _toggleKey))
                {
                    Log.Warn("Unrecognised ToggleKey \"" + _config.ToggleKey + "\"; falling back to F5.");
                    _toggleKey = Key.F5;
                }

                // Values the player has changed in the in-game options pages win over
                // the JSON file, which now only supplies defaults and the button layout.
                PadOptions.LoadInto(_config);
                PadBindings.LoadInto(_config);

                _rumble = new Rumble(_config);
                _aim = new AimDriver(_config);
                _gameplay = new GameplayDriver(_config, _aim, _rumble);
                _ui = new UiDriver(_config);
                _hud = new HintHud(_config);
                _outline = new LockOutline(_config);
                _optionsUi = new NativeOptionsUi(_config);
                _itemPrompts = new NativeItemPrompts(_config);

                StartHarmony();

                SceneLoader.onStartedLoadingScene += OnSceneLoadStarted;
                SceneLoader.onAfterSceneInitialize += OnSceneReady;
                OptionsManager.OnOptionsChanged += OnOptionChanged;

                Instance = this;

                InputSystem.onDeviceChange += OnDeviceChange;
                LogDevices("startup");
            }
            catch (Exception e)
            {
                Log.Error("Failed to start: " + e);
            }
        }

        /// <summary>
        /// Kept out of OnAfterSetup so that method carries no reference to a Harmony type,
        /// which would otherwise have to resolve before the assembly resolver is installed.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StartHarmony()
        {
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StopHarmony()
        {
            try { _harmony?.UnpatchAll(HarmonyId); }
            catch (Exception e) { Log.Error("Unpatch failed: " + e.Message); }
            _harmony = null;
        }

        protected override void OnBeforeDeactivate()
        {
            Instance = null;

            SceneLoader.onStartedLoadingScene -= OnSceneLoadStarted;
            SceneLoader.onAfterSceneInitialize -= OnSceneReady;
            OptionsManager.OnOptionsChanged -= OnOptionChanged;
            InputSystem.onDeviceChange -= OnDeviceChange;

            UnhookCharacter();

            _ui?.Reset();
            _rumble?.Stop();
            _hud?.Destroy();
            _outline?.Hide();
            _itemPrompts?.Destroy();

            StopHarmony();

            if (_assemblyResolver != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolver;
                _assemblyResolver = null;
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
                change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
            {
                Pad.InvalidateCache();
                ControllerDevice.Invalidate();
                _expectingCursorPosition = false;
                _aim?.Reset();
                _ui?.Reset();
                _rumble?.Stop();
                _outline?.Hide();
                LogDevices(change + ": " + device.displayName);
            }
        }

        /// <summary>
        /// Report exactly what the Input System sees. Windows exposes plenty of things as
        /// "HID-compliant game controller" that are not gamepads, so when a pad appears
        /// dead this is the difference between guessing and knowing.
        /// </summary>
        private static void LogDevices(string reason)
        {
            try
            {
                var report = new System.Text.StringBuilder();
                report.Append("Input devices (").Append(reason).Append("):");

                int gamepads = 0;
                int joysticks = 0;

                foreach (var device in InputSystem.devices)
                {
                    bool isGamepad = device is Gamepad;
                    if (isGamepad) gamepads++;
                    else if (device is Joystick) joysticks++;

                    report.Append("\n  ")
                          .Append(isGamepad ? "[GAMEPAD] " : "          ")
                          .Append(device.GetType().Name)
                          .Append(" \"").Append(device.displayName).Append('"')
                          .Append(device.enabled ? "" : " (disabled)");
                }

                report.Append("\n  Gamepad.current: ")
                      .Append(Gamepad.current != null ? Gamepad.current.displayName : "NONE");

                Log.Info(report.ToString());

                if (gamepads == 0)
                {
                    Log.Warn(joysticks > 0
                        ? "No gamepad recognised, but " + joysticks + " generic joystick device(s) are present. " +
                          "The Input System has not classified your controller as a gamepad, so DuckovPad cannot read it. " +
                          "For an Xbox pad on Bluetooth, try reconnecting it or using a USB cable."
                        : "No gamepad recognised by the Input System yet. Press a button on the controller.");
                }
            }
            catch (Exception e)
            {
                Log.Warn("Could not enumerate input devices: " + e.Message);
            }
        }

        /// <summary>
        /// Mono resolves a mod assembly's dependencies from the app base rather than the
        /// mod folder, so point it at our bundled 0Harmony.dll explicitly.
        /// </summary>
        private void RegisterAssemblyResolver()
        {
            var modFolder = info.path;

            _assemblyResolver = (sender, args) =>
            {
                try
                {
                    var name = new AssemblyName(args.Name).Name;
                    var candidate = Path.Combine(modFolder, name + ".dll");
                    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
                }
                catch (Exception)
                {
                    return null;
                }
            };

            AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolver;
        }

        // ------------------------------------------------------------------

        private void Update()
        {
            if (_config == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
            {
                _userEnabled = !_userEnabled;
                if (!_userEnabled)
                {
                    _ui.Reset();
                    _rumble.Stop();
                    _hud?.Hide();
                }

                var message = "Gamepad control " + (_userEnabled ? "enabled" : "disabled");
                Log.Info(message);
                var main = CharacterMainControl.Main;
                if (main != null) main.PopText(message);
            }

            // F6 reaches the diagnostics without needing to open a menu — which matters
            // when the reason you're debugging is that you can't reach the menu.
            if (keyboard != null && keyboard[Key.F6].wasPressedThisFrame)
            {
                _config.Debug.ShowOverlay = !_config.Debug.ShowOverlay;
                Log.Info("Diagnostic overlay " + (_config.Debug.ShowOverlay ? "on" : "off"));
                LogDevices("F6");
            }

            // Boot-screen and loading-curtain safety net. Either "Click to Continue"
            // gate strands the entire game, so passing them must not depend on
            // device-focus arbitration, the virtual cursor landing on the right
            // graphic, or the player guessing which button we listen to: any pad
            // button confirms. Both confirms self-guard on their own shown state,
            // so firing when no gate is waiting is a harmless no-op.
            if (_userEnabled)
            {
                try
                {
                    Pad.Poll();
                    if (Pad.Available && Pad.AnyButtonDown())
                    {
                        if (LoadingContinue.IsWaiting)
                        {
                            if (!LoadingContinue.TryConfirm()) TitleGate.TryConfirm();
                        }
                        else if (TitleGate.IsWaiting)
                        {
                            TitleGate.TryConfirm();
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Continue gate failed: " + e.Message);
                }
            }

            // The normal input driver is deliberately off during loading. Only the ready
            // gate receives A/Start above, and only its Continue prompt is shown here.
            if (SceneLoading)
            {
                if (_userEnabled && Pad.Available && LoadingContinue.IsWaiting)
                    _hud?.Render(inMenu: true, modifierHeld: false, lockedOn: false, builderActive: false);
                else _hud?.Hide();
            }

            // The options panel is created lazily, so keep trying until it exists.
            if (_optionsUi != null && !_optionsUi.Built && Time.unscaledTime >= _nextOptionsAttempt)
            {
                _nextOptionsAttempt = Time.unscaledTime + 2f;
                _optionsUi.TryBuild();
            }

            RefreshCharacterHook();
            _rumble?.Update(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Diagnostic readout, off by default and switchable from the Controller options
        /// page. It exists so "aim assist isn't working" can be answered with facts:
        /// whether a pad is seen, whether targets are found, and how hard assist is pulling.
        /// </summary>
        private void OnGUI()
        {
            if (_config?.Debug == null || !_config.Debug.ShowOverlay) return;

            const float width = 430f;
            var rect = new Rect(12f, 12f, width, 262f);
            PadGui.Fill(rect, PadGui.Panel);

            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };
            style.normal.textColor = PadGui.Text;

            var pad = Pad.Current;
            string device = ControllerDevice.Name;

            string text =
                "DuckovPad\n" +
                "  gamepad: " + device + (Pad.ModifierHeld ? "   [modifier held]" : "") + "\n" +
                "  control: " + (Active ? (_padHasFocus ? "pad" : "keyboard/mouse") : "disabled") + "\n" +
                "  gates: title=" + (TitleGate.IsWaiting ? "waiting" : "-") +
                " loading=" + (LoadingContinue.IsWaiting ? "waiting" : "-") + "\n" +
                "  options tab: " + (_optionsUi != null && _optionsUi.Built ? "installed" : "not installed") + "\n" +
                "  menu targets: " + (_ui?.SnapTargetCount ?? 0) + "\n" +
                "  ui focus: " + (_ui?.SelectedDescription ?? "-") +
                "   slots: " + (_ui?.SlotCount ?? 0) + "\n" +
                "  bumper: " + (_ui?.LastBumperMessage ?? "-") + "\n";

            if (_aim != null)
            {
                text +=
                    "  assist: " + _aim.AssistStatus + "\n" +
                    "  target: " + _aim.AssistTargetName + "   angle " + _aim.AssistAngle.ToString("0.0") + "°\n" +
                    "  weight " + _aim.AssistWeight.ToString("0.00") +
                    "   friction " + _aim.Friction.ToString("0.00") +
                    "   pull " + _aim.AssistPull.ToString("+0.0;-0.0;0.0") + "°\n" +
                    "  colliders " + _aim.NearbyColliders + "   hostiles " + _aim.NearbyHostiles + "\n" +
                    "  lock-on: " + (_aim.LockedOn ? "engaged" : "off") +
                    "   blend " + _aim.LockBlend.ToString("0.00") + "\n" +
                    "  outline: " + (_outline?.Status ?? "not started");
            }

            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, width - 20f, rect.height - 16f), text, style);
        }

        private void OnOptionChanged(string key)
        {
            if (_config == null) return;
            if (!PadOptions.Apply(_config, key) && !PadBindings.Apply(_config, key)) return;

            // Keep Settings.json in step so the file and the in-game page never disagree.
            _settingsDirty = true;
            _saveSettingsAt = Time.unscaledTime + 1.5f;

            Pad.Configure(_config);
        }

        private void SaveSettingsIfDue()
        {
            if (!_settingsDirty || Time.unscaledTime < _saveSettingsAt) return;
            _settingsDirty = false;

            try
            {
                _config.Save(Path.Combine(info.path, "Settings.json"));
            }
            catch (Exception e)
            {
                Log.Warn("Could not write Settings.json: " + e.Message);
            }
        }

        private void OnSceneLoadStarted(SceneLoadingContext context)
        {

            _ui?.Reset();
            _rumble?.Stop();
            _gameplay?.Reset();
            _hud?.Hide();
            _outline?.Hide();
            _itemPrompts?.Restore();
            _expectingCursorPosition = false;
            UnhookCharacter();
        }

        private void OnSceneReady(SceneLoadingContext context)
        {

            _optionsUi?.Invalidate();
            _aim?.Reset();
            _ui?.Reset();
            Pad.InvalidateCache();
        }

        // ------------------------------------------------------------------
        // Called from the CharacterInputControl.Update prefix.
        // Returns true to let the vanilla keyboard/mouse path run.
        // ------------------------------------------------------------------

        internal bool DriveInput(CharacterInputControl control)
        {
            if (!Active || control == null)
            {
                // Toggled off or mid-load: the HUD render that would normally retire the
                // lock-on outline is not going to run, so retire it here.
                _outline?.Hide();
                return true;
            }

            _drivenFrame = Time.frameCount;

            Pad.Poll();
            if (!Pad.Available)
            {
                _padHasFocus = false;
                _ui.Reset();
                _outline?.Hide();
                return true;
            }

            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool inMenu = InMenu();
            Pad.SetMenuContext(inMenu);

            UpdateDeviceFocus(inMenu);
            if (!_padHasFocus)
            {
                _ui.Reset();
                _hud?.Hide();
                _expectingCursorPosition = false;
                return true;
            }

            if (inMenu)
            {
                _gameplay.Reset();
                if (control.inputManager != null)
                {
                    control.inputManager.SetMoveInput(Vector2.zero);
                    control.inputManager.SetRunInput(false);
                    control.inputManager.SetAdsInput(false);
                    control.inputManager.SetTrigger(false, false, false);
                }
                _ui.Update(deltaTime);
                ExpectCursorAt(_ui.CursorPosition);
                RenderHud(inMenu: true);
                return false;
            }

            if (_ui.CursorActive) _ui.Reset();

            var inputManager = control.inputManager;
            var character = CharacterMainControl.Main;
            if (inputManager == null || character == null) return true;

            _gameplay.Update(control, inputManager, character, deltaTime);

            // Remember where the game will have warped the cursor to, so real mouse
            // movement can still be told apart from our own warping next frame.
            if (_aim.HasReticle) ExpectCursorAt(_aim.ReticleScreenPosition);
            else _expectingCursorPosition = false;

            RenderHud(inMenu: false);
            SaveSettingsIfDue();

            return false; // vanilla mouse/keyboard aiming is fully replaced this frame
        }

        private void RenderHud(bool inMenu)
        {
            if (_hud == null) return;

            bool builderActive = false;
            try
            {
                builderActive = Duckov.Buildings.UI.BuilderView.Instance != null
                                && View.ActiveView == Duckov.Buildings.UI.BuilderView.Instance;
            }
            catch (Exception)
            {
                // Builder isn't loaded in this scene.
            }

            _hud.Render(inMenu, Pad.ModifierHeld, _aim != null && _aim.LockedOn, builderActive);

            if (inMenu && _ui.HasHover) _hud.ShowHover(_ui.HoverRect);
            else _hud.HideHover();

            var levelManager = LevelManager.Instance;
            var camera = levelManager != null && levelManager.GameCamera != null
                ? levelManager.GameCamera.renderCamera
                : null;

            if (!inMenu && _aim != null && _aim.LockedOn) _hud.ShowLockMarker(_aim.LockPoint, camera);
            else _hud.HideLockMarker();

            // White frame around the locked body: always drawn while locked, so the
            // target is unmistakable even when the 3D outline pipeline says no.
            if (!inMenu && _aim != null && _aim.LockedOn && _aim.LockVisualTarget != null
                && TryGetTargetScreenRect(_aim.LockVisualTarget, camera, out var lockRect))
                _hud.ShowLockOutline(lockRect);
            else _hud.HideLockOutline();

            // Outline stays fully opaque while locked so the white target is unmistakable;
            // it only fades during the release ease (LockVisualTarget outlives LockedOn).
            if (!inMenu && _aim != null)
            {
                float blend = _aim.LockedOn ? 1f : _aim.LockBlend;
                _outline.Show(_aim.LockVisualTarget, blend, camera);
            }
            else _outline.Hide();
        }

        /// <summary>
        /// Screen-space box around a locked target's visible body, for the white lock frame.
        /// Encapsulates the active renderers' world bounds (colliders as fallback) and
        /// projects the corners; hidden when the target is behind the camera.
        /// </summary>
        private static bool TryGetTargetScreenRect(Transform target, Camera camera, out Rect rect)
        {
            rect = default;
            if (target == null || camera == null) return false;

            bool has = false;
            var bounds = new Bounds(target.position, Vector3.zero);

            try
            {
                var renderers = target.GetComponentsInChildren<Renderer>(false);
                foreach (var r in renderers)
                {
                    if (r == null || !r.enabled) continue;
                    if (!has) { bounds = r.bounds; has = true; }
                    else bounds.Encapsulate(r.bounds);
                }

                if (!has)
                {
                    var colliders = target.GetComponentsInChildren<Collider>(false);
                    foreach (var c in colliders)
                    {
                        if (c == null || !c.enabled) continue;
                        if (!has) { bounds = c.bounds; has = true; }
                        else bounds.Encapsulate(c.bounds);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (!has) return false;

            // Degenerate bounds (a point): pad to something frameable.
            if (bounds.size.sqrMagnitude < 0.04f) bounds.Expand(1.2f);

            Vector3 center = bounds.center;
            Vector3 ext = bounds.extents;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    ((i & 1) == 0 ? -ext.x : ext.x),
                    ((i & 2) == 0 ? -ext.y : ext.y),
                    ((i & 4) == 0 ? -ext.z : ext.z));
                Vector3 screen;
                try
                {
                    screen = camera.WorldToScreenPoint(corner);
                }
                catch (Exception)
                {
                    return false;
                }
                if (screen.z < 0f) return false;
                if (screen.x < minX) minX = screen.x;
                if (screen.y < minY) minY = screen.y;
                if (screen.x > maxX) maxX = screen.x;
                if (screen.y > maxY) maxY = screen.y;
            }

            if (maxX < 0f || maxY < 0f || minX > Screen.width || minY > Screen.height) return false;

            minX = Mathf.Clamp(minX, 0f, Screen.width);
            minY = Mathf.Clamp(minY, 0f, Screen.height);
            maxX = Mathf.Clamp(maxX, 0f, Screen.width);
            maxY = Mathf.Clamp(maxY, 0f, Screen.height);
            if (maxX - minX < 4f || maxY - minY < 4f) return false;

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        /// <summary>
        /// Menus exist outside gameplay too — the title screen, the mod list, the options
        /// panel — and none of those have a CharacterInputControl to hook. LateUpdate runs
        /// after every Update, so it can tell whether the prefix already handled this frame.
        /// </summary>
        private void LateUpdate()
        {
            if (!Active || _ui == null) return;
            if (_drivenFrame == Time.frameCount)
            {
                _ui.ApplyCursorVisibility();
                return;
            }

            Pad.Poll();
            if (!Pad.Available)
            {
                _padHasFocus = false;
                _ui.Reset();
                _hud?.Hide();
                return;
            }

            Pad.SetMenuContext(true);
            UpdateDeviceFocus(inMenu: true);
            if (!_padHasFocus)
            {
                _ui.Reset();
                _hud?.Hide();
                _expectingCursorPosition = false;
                return;
            }

            _ui.Update(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            ExpectCursorAt(_ui.CursorPosition);
            RenderHud(inMenu: true);
            SaveSettingsIfDue();
        }

        private static bool InMenu()
        {
            return View.ActiveView != null
                   || Dialogues.DialogueUI.Active
                   || ViewUtil.SplitDialogueOpen
                   || (ItemOperationMenu.Instance != null && ItemOperationMenu.Instance.open)
                   || GameManager.Paused
                   || (PauseMenu.Instance != null && PauseMenu.Instance.Shown);
        }

        private void ExpectCursorAt(Vector2 position)
        {
            _expectedCursorPosition = position;
            _expectingCursorPosition = true;
        }

        /// <summary>
        /// Last input device wins. Any stick or button use hands control to the pad;
        /// real mouse movement or a keypress hands it back.
        /// </summary>
        /// <param name="inMenu">
        /// While driving menus we synthesise mouse buttons and keystrokes ourselves, so
        /// those signals can't be treated as the player reaching for the keyboard. Cursor
        /// divergence still works there, because we warp the cursor to a known position.
        /// </param>
        private void UpdateDeviceFocus(bool inMenu)
        {
            ControllerDevice.PointerDelta = Vector2.zero;
            int deviceId = Pad.Current?.deviceId ?? -1;
            if (_activeDeviceId != deviceId)
            {
                _activeDeviceId = deviceId;
                _expectingCursorPosition = false;
                _gameplay?.Reset();
                _ui?.Reset();
                _rumble?.Stop();
            }
            if (Pad.LastActivityTime > 0f && Time.unscaledTime - Pad.LastActivityTime < 0.25f)
                _padHasFocus = true;

            if (!_padHasFocus) return;

            var mouse = Mouse.current;

            if (!inMenu)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboard[_toggleKey].wasPressedThisFrame
                    && !Pad.ExtraButtonDown())
                {
                    _padHasFocus = false;
                    return;
                }

                if (!ControllerDevice.UsePointer && mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                {
                    _padHasFocus = false;
                    return;
                }
            }

            if (mouse == null || !_expectingCursorPosition) return;

            // The cursor is warped to a known point every frame we are in control, so a
            // position that doesn't match means the player physically moved the mouse.
            Vector2 actual = mouse.position.ReadValue();
            if (ControllerDevice.UsePointer)
            {
                Vector2 difference = actual - _expectedCursorPosition;
                // Expected positions are read back after the game's recoil/warping, so
                // this measures external mouse input rather than our own reticle movement.
                if (difference.sqrMagnitude > 0.01f) ControllerDevice.PointerDelta = difference;
                return;
            }
            if ((actual - _expectedCursorPosition).sqrMagnitude > CursorDivergenceThresholdSqr)
                _padHasFocus = false;
        }

        // ------------------------------------------------------------------

        private void RefreshCharacterHook()
        {
            var main = CharacterMainControl.Main;
            if (main == _hookedCharacter) return;

            UnhookCharacter();
            if (main == null) return;

            try
            {
                var health = main.Health;
                if (health?.OnHurtEvent == null) return;

                _hurtHandler = _ => _rumble?.Hurt();
                health.OnHurtEvent.AddListener(_hurtHandler);
                _hookedCharacter = main;
            }
            catch (Exception e)
            {
                Log.Warn("Could not hook damage feedback: " + e.Message);
            }
        }

        private void UnhookCharacter()
        {
            if (_hookedCharacter == null || _hurtHandler == null)
            {
                _hookedCharacter = null;
                _hurtHandler = null;
                return;
            }

            try
            {
                var health = _hookedCharacter.Health;
                health?.OnHurtEvent?.RemoveListener(_hurtHandler);
            }
            catch (Exception)
            {
                // Character already destroyed; nothing to detach from.
            }

            _hookedCharacter = null;
            _hurtHandler = null;
        }

        internal void OnWeaponFired() => _rumble?.Shoot();

        internal void UpdateItemPrompts(ItemHoveringUI tooltip)
        {
            bool controllerActive = false;
            try
            {
                // The readout must not flicker back to keyboard hints mid-inventory
                // just because cursor arbitration wavered: recent pad activity counts.
                controllerActive = Active && Pad.Available && InMenu()
                    && (_padHasFocus || Time.unscaledTime - Pad.LastActivityTime < 3f);
            }
            catch (Exception)
            {
                controllerActive = false;
            }
            _itemPrompts?.Update(tooltip, controllerActive);
        }

        internal void UpdateMenuCursorVisibility()
        {
            if (Active && _padHasFocus && _ui != null && _ui.CursorActive)
                _ui.ApplyCursorVisibility();
        }
    }

    // ----------------------------------------------------------------------

    [HarmonyPatch(typeof(CharacterInputControl), "Update")]
    internal static class CharacterInputControlUpdatePatch
    {
        private static bool Prefix(CharacterInputControl __instance)
        {
            var instance = ModBehaviour.Instance;
            if (instance == null) return true;

            try
            {
                return instance.DriveInput(__instance);
            }
            catch (Exception e)
            {
                Log.Error("Input update failed, handing control back to keyboard/mouse: " + e);
                return true;
            }
        }
    }

    /// <summary>The game calls AddRecoil once per shot, which makes it a precise rumble hook.</summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.AddRecoil))]
    internal static class InputManagerAddRecoilPatch
    {
        private static void Postfix()
        {
            ModBehaviour.Instance?.OnWeaponFired();
        }
    }

    // Vanilla also changes Cursor.visible every frame. Apply the menu preference after
    // that update, regardless of MonoBehaviour execution order.
    [HarmonyPatch(typeof(InputManager), "UpdateCursor")]
    internal static class InputManagerCursorPatch
    {
        private static void Postfix() => ModBehaviour.Instance?.UpdateMenuCursorVisibility();
    }

    [HarmonyPatch(typeof(ItemHoveringUI), "Update")]
    internal static class ItemHoveringPromptsPatch
    {
        private static void Postfix(ItemHoveringUI __instance) => ModBehaviour.Instance?.UpdateItemPrompts(__instance);
    }
}
