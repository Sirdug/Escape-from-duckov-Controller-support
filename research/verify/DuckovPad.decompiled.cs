using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Dialogues;
using Duckov.Buildings.UI;
using Duckov.MiniMaps.UI;
using Duckov.Modding;
using Duckov.Quests.UI;
using Duckov.UI;
using Duckov.Utilities;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETStandard,Version=v2.1", FrameworkDisplayName = ".NET Standard 2.1")]
[assembly: AssemblyCompany("DuckovPad")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("DuckovPad")]
[assembly: AssemblyTitle("DuckovPad")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace DuckovPad
{
	internal sealed class AimDriver
	{
		private static readonly FieldInfo AimCacheField = typeof(InputManager).GetField("_aimMousePosCache", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo AimSyncedField = typeof(InputManager).GetField("aimMousePosFirstSynced", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly PadConfig _config;

		private Vector2 _direction = Vector2.up;

		private float _magnitude;

		private Transform _stickyTarget;

		private float _stickyUntil;

		private readonly Collider[] _candidates = new Collider[24];

		public Vector2 ReticleScreenPosition { get; private set; }

		public bool HasReticle { get; private set; }

		public AimDriver(PadConfig config)
		{
			_config = config;
			if (AimCacheField == null)
			{
				Log.Error("InputManager._aimMousePosCache not found — the game's aim internals changed. Gamepad aiming will fall back to relative mode.");
			}
		}

		public void Reset()
		{
			_direction = Vector2.up;
			_magnitude = 0f;
			_stickyTarget = null;
			HasReticle = false;
		}

		public bool Update(InputManager inputManager, CharacterMainControl character, bool adsHeld, float deltaTime)
		{
			HasReticle = false;
			if (inputManager == null || character == null)
			{
				return false;
			}
			LevelManager instance = LevelManager.Instance;
			if (instance == null || instance.GameCamera == null)
			{
				return false;
			}
			Camera renderCamera = instance.GameCamera.renderCamera;
			if (renderCamera == null)
			{
				return false;
			}
			PadConfig.AimSettings aim = _config.Aim;
			bool num = string.Equals(aim.Mode, "relative", StringComparison.OrdinalIgnoreCase) || AimCacheField == null;
			Vector2 stick = Pad.RightStick(aim.Deadzone, aim.OuterDeadzone, aim.ResponseCurve);
			if (num)
			{
				return UpdateRelative(inputManager, stick, adsHeld, deltaTime);
			}
			if (stick.sqrMagnitude > 0.0001f)
			{
				Vector2 normalized = stick.normalized;
				float num2 = Mathf.Clamp01(stick.magnitude);
				if (aim.Smoothing <= 0f)
				{
					_direction = normalized;
					_magnitude = num2;
				}
				else
				{
					float t = 1f - Mathf.Exp((0f - aim.Smoothing) * deltaTime);
					_direction = Vector2.Lerp(_direction, normalized, t).normalized;
					_magnitude = Mathf.Lerp(_magnitude, num2, t);
				}
			}
			if (_direction.sqrMagnitude < 0.0001f)
			{
				_direction = Vector2.up;
			}
			Vector3 forward = renderCamera.transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = Vector3.forward;
			}
			forward.Normalize();
			Vector3 right = renderCamera.transform.right;
			right.y = 0f;
			if (right.sqrMagnitude < 0.0001f)
			{
				right = Vector3.right;
			}
			right.Normalize();
			Vector3 vector = (right * _direction.x + forward * _direction.y).normalized;
			float num3 = Mathf.Lerp(aim.MinReticleDistance, aim.ReticleDistance, _magnitude);
			if (adsHeld)
			{
				num3 *= aim.AdsDistanceMultiplier;
			}
			Vector3 position = character.transform.position;
			if (_config.AimAssist.Enabled && ShouldAssist(adsHeld))
			{
				vector = ApplyAimAssist(position, vector, deltaTime);
			}
			Vector3 position2 = position + vector * num3;
			position2.y = position.y + 0.5f;
			Vector2 candidate = renderCamera.WorldToScreenPoint(position2);
			candidate = KeepOnScreen(renderCamera, position, vector, num3, candidate);
			WriteAimPosition(inputManager, candidate);
			ReticleScreenPosition = candidate;
			HasReticle = true;
			return true;
		}

		private bool UpdateRelative(InputManager inputManager, Vector2 stick, bool adsHeld, float deltaTime)
		{
			if (stick.sqrMagnitude < 0.0001f)
			{
				return true;
			}
			float num = _config.Aim.RelativeSensitivity * (adsHeld ? _config.Aim.RelativeAdsMultiplier : 1f);
			Vector2 aimInputUsingMouse = stick * num * deltaTime;
			inputManager.SetAimInputUsingMouse(aimInputUsingMouse);
			return true;
		}

		private bool ShouldAssist(bool adsHeld)
		{
			if (!_config.AimAssist.OnlyWhileFiringOrAds)
			{
				return true;
			}
			if (!adsHeld)
			{
				return Pad.Held(_config.Buttons.Fire);
			}
			return true;
		}

		private Vector3 ApplyAimAssist(Vector3 origin, Vector3 direction, float deltaTime)
		{
			PadConfig.AimAssistSettings aimAssist = _config.AimAssist;
			if (aimAssist.Strength <= 0f)
			{
				return direction;
			}
			LayerMask damageReceiverLayerMask;
			LayerMask wallLayerMask;
			try
			{
				damageReceiverLayerMask = GameplayDataSettings.Layers.damageReceiverLayerMask;
				wallLayerMask = GameplayDataSettings.Layers.wallLayerMask;
			}
			catch (Exception)
			{
				return direction;
			}
			int num = Physics.OverlapSphereNonAlloc(origin, aimAssist.MaxDistance, _candidates, damageReceiverLayerMask, QueryTriggerInteraction.Ignore);
			if (num <= 0)
			{
				if (Time.unscaledTime > _stickyUntil)
				{
					_stickyTarget = null;
				}
				return direction;
			}
			Transform transform = null;
			Vector3 vector = direction;
			float num2 = float.MaxValue;
			Vector3 start = origin + Vector3.up * 0.8f;
			for (int i = 0; i < num; i++)
			{
				Collider collider = _candidates[i];
				if (collider == null)
				{
					continue;
				}
				DamageReceiver component = collider.GetComponent<DamageReceiver>();
				if (component == null || component.Team == Teams.player || (component.health != null && component.health.IsDead))
				{
					continue;
				}
				Vector3 center = collider.bounds.center;
				Vector3 vector2 = center - origin;
				vector2.y = 0f;
				float magnitude = vector2.magnitude;
				if (magnitude < 0.75f || magnitude > aimAssist.MaxDistance)
				{
					continue;
				}
				Vector3 vector3 = vector2 / magnitude;
				float num3 = Vector3.Angle(direction, vector3);
				if (!(num3 > aimAssist.MaxAngleDegrees) && (!aimAssist.RequireLineOfSight || !Physics.Linecast(start, center, wallLayerMask, QueryTriggerInteraction.Ignore)))
				{
					float num4 = num3 + magnitude * 0.25f;
					if (collider.transform == _stickyTarget)
					{
						num4 -= aimAssist.MaxAngleDegrees * aimAssist.Stickiness;
					}
					if (num4 < num2)
					{
						num2 = num4;
						transform = collider.transform;
						vector = vector3;
					}
				}
			}
			if (transform == null)
			{
				if (Time.unscaledTime > _stickyUntil)
				{
					_stickyTarget = null;
				}
				return direction;
			}
			_stickyTarget = transform;
			_stickyUntil = Time.unscaledTime + 0.35f;
			float num5 = Vector3.Angle(direction, vector);
			float num6 = 1f - Mathf.Clamp01(num5 / Mathf.Max(0.01f, aimAssist.MaxAngleDegrees));
			float value = aimAssist.Strength * num6;
			value = 1f - Mathf.Pow(1f - Mathf.Clamp01(value), deltaTime * 60f);
			return Vector3.Slerp(direction, vector, value).normalized;
		}

		private Vector2 KeepOnScreen(Camera camera, Vector3 origin, Vector3 direction, float distance, Vector2 candidate)
		{
			float num = Screen.width;
			float num2 = Screen.height;
			if (candidate.x >= 24f && candidate.x <= num - 24f && candidate.y >= 24f && candidate.y <= num2 - 24f)
			{
				return candidate;
			}
			float num3 = 0f;
			float num4 = distance;
			Vector2 result = camera.WorldToScreenPoint(origin + Vector3.up * 0.5f);
			for (int i = 0; i < 8; i++)
			{
				float num5 = (num3 + num4) * 0.5f;
				Vector3 position = origin + direction * num5;
				position.y = origin.y + 0.5f;
				Vector2 vector = camera.WorldToScreenPoint(position);
				if (vector.x >= 24f && vector.x <= num - 24f && vector.y >= 24f && vector.y <= num2 - 24f)
				{
					result = vector;
					num3 = num5;
				}
				else
				{
					num4 = num5;
				}
			}
			return result;
		}

		private void WriteAimPosition(InputManager inputManager, Vector2 screenPoint)
		{
			if (!(AimCacheField == null))
			{
				AimSyncedField?.SetValue(inputManager, true);
				AimCacheField.SetValue(inputManager, screenPoint);
				inputManager.SetMousePosition(screenPoint);
				inputManager.SetAimInputUsingMouse(Vector2.zero);
			}
		}
	}
	internal sealed class GameplayDriver
	{
		private static readonly MethodInfo ShortCutInputMethod = typeof(CharacterInputControl).GetMethod("ShortCutInput", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly PadConfig _config;

		private readonly AimDriver _aim;

		private readonly Rumble _rumble;

		private int _weaponSlot = 1;

		private bool _skillAimActive;

		public bool AdsHeld { get; private set; }

		public GameplayDriver(PadConfig config, AimDriver aim, Rumble rumble)
		{
			_config = config;
			_aim = aim;
			_rumble = rumble;
			if (ShortCutInputMethod == null)
			{
				Log.Warn("CharacterInputControl.ShortCutInput not found — quick-item slots will not work.");
			}
		}

		public void Reset()
		{
			_skillAimActive = false;
			AdsHeld = false;
			_aim.Reset();
		}

		public void Update(CharacterInputControl control, InputManager inputManager, CharacterMainControl character, float deltaTime)
		{
			PadConfig.Bindings buttons = _config.Buttons;
			Vector2 moveInput = Pad.LeftStick(_config.Move.Deadzone, _config.Move.OuterDeadzone, _config.Move.ResponseCurve);
			inputManager.SetMoveInput(moveInput);
			bool runInput = Pad.Held(buttons.Sprint);
			if (_config.Move.AutoRunAtFullStick && Pad.LeftStickRaw.magnitude >= _config.Move.AutoRunThreshold)
			{
				runInput = true;
			}
			inputManager.SetRunInput(runInput);
			AdsHeld = Pad.Held(buttons.Ads);
			inputManager.SetAdsInput(AdsHeld);
			_aim.Update(inputManager, character, AdsHeld, deltaTime);
			bool trigger = Pad.Held(buttons.Fire);
			bool flag = Pad.Down(buttons.Fire);
			bool flag2 = Pad.Up(buttons.Fire);
			inputManager.SetTrigger(trigger, flag, flag2);
			bool flag3 = false;
			try
			{
				flag3 = character.skillAction != null && character.skillAction.holdItemSkillKeeper != null && character.skillAction.holdItemSkillKeeper.CheckSkillAndBinding();
			}
			catch (Exception)
			{
			}
			if (flag3)
			{
				inputManager.SetAimType(AimTypes.handheldSkill);
				if (flag)
				{
					inputManager.StartItemSkillAim();
				}
				else if (flag2)
				{
					inputManager.ReleaseItemSkill();
				}
			}
			else
			{
				inputManager.SetAimType(AimTypes.normalAim);
			}
			if (flag)
			{
				_rumble.Shoot();
			}
			if (Pad.Down(buttons.Dash))
			{
				inputManager.Dash();
			}
			if (Pad.Down(buttons.Interact))
			{
				inputManager.Interact();
			}
			if (Pad.Down(buttons.PutAway))
			{
				inputManager.PutAway();
			}
			if (Pad.Down(buttons.Quack))
			{
				inputManager.Quack();
			}
			if (Pad.Down(buttons.StopAction))
			{
				inputManager.StopAction();
			}
			if (Pad.Down(buttons.Reload))
			{
				CharacterMainControl main = CharacterMainControl.Main;
				if (main != null)
				{
					main.TryToReload();
				}
			}
			if (!GameManager.Paused)
			{
				if (Pad.Down(buttons.NightVision))
				{
					inputManager.ToggleNightVision();
				}
				if (Pad.Down(buttons.ToggleView))
				{
					inputManager.ToggleView();
				}
			}
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
			if (Pad.Down(buttons.SwitchWeapon))
			{
				_weaponSlot = ((_weaponSlot != 1) ? 1 : 2);
				inputManager.SwitchItemAgent(_weaponSlot);
			}
			if (Pad.Down(buttons.MeleeWeapon))
			{
				_weaponSlot = 3;
				inputManager.SwitchItemAgent(3);
			}
			if (Pad.Down(buttons.ShortcutNext))
			{
				CycleItemAgent(inputManager, 1);
			}
			if (Pad.Down(buttons.ShortcutPrevious))
			{
				CycleItemAgent(inputManager, -1);
			}
			if (Pad.Down(buttons.CycleNext))
			{
				Scroll(inputManager, 1);
			}
			if (Pad.Down(buttons.CyclePrevious))
			{
				Scroll(inputManager, -1);
			}
			if (Pad.Down(buttons.QuickItem3))
			{
				ShortCut(control, 3);
			}
			if (Pad.Down(buttons.QuickItem4))
			{
				ShortCut(control, 4);
			}
			if (Pad.Down(buttons.QuickItem5))
			{
				ShortCut(control, 5);
			}
			if (Pad.Down(buttons.QuickItem6))
			{
				ShortCut(control, 6);
			}
			if (Pad.Down(buttons.PauseMenu))
			{
				PauseMenu.Toggle();
			}
			if (Pad.Down(buttons.Inventory))
			{
				ToggleInventory();
			}
			if (Pad.Down(buttons.Map))
			{
				ToggleMap();
			}
			if (Pad.Down(buttons.QuestLog))
			{
				ToggleQuestLog();
			}
		}

		private void CycleItemAgent(InputManager inputManager, int direction)
		{
			_weaponSlot += direction;
			if (_weaponSlot > 3)
			{
				_weaponSlot = 1;
			}
			if (_weaponSlot < 1)
			{
				_weaponSlot = 3;
			}
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
			if (ShortCutInputMethod == null || control == null)
			{
				return;
			}
			try
			{
				ShortCutInputMethod.Invoke(control, new object[1] { index });
			}
			catch (Exception ex)
			{
				Log.Error("Quick-item slot " + index + " failed: " + ex.Message);
			}
		}

		private static void ToggleInventory()
		{
			if (GameManager.Paused || DialogueUI.Active || SceneLoader.IsSceneLoading)
			{
				return;
			}
			if (View.ActiveView == null)
			{
				if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
				{
					PlayerStorage.Instance.InteractableLootBox.InteractWithMainCharacter();
				}
				else
				{
					InventoryView.Show();
				}
			}
			else
			{
				ViewUtil.CloseActive();
			}
		}

		private static void ToggleMap()
		{
			if (!GameManager.Paused && !SceneLoader.IsSceneLoading)
			{
				if (View.ActiveView == null)
				{
					MiniMapView.Show();
				}
				else if (View.ActiveView is MiniMapView miniMapView)
				{
					miniMapView.Close();
				}
			}
		}

		private static void ToggleQuestLog()
		{
			if (!GameManager.Paused && !DialogueUI.Active)
			{
				if (View.ActiveView == null)
				{
					QuestView.Show();
				}
				else if (View.ActiveView is QuestView)
				{
					ViewUtil.CloseActive();
				}
			}
		}
	}
	internal static class Log
	{
		private const string Prefix = "[DuckovPad] ";

		public static void Info(string message)
		{
			UnityEngine.Debug.Log("[DuckovPad] " + message);
		}

		public static void Warn(string message)
		{
			UnityEngine.Debug.LogWarning("[DuckovPad] " + message);
		}

		public static void Error(string message)
		{
			UnityEngine.Debug.LogError("[DuckovPad] " + message);
		}
	}
	public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		private const string HarmonyId = "com.duckovpad.controller";

		private Harmony _harmony;

		private PadConfig _config;

		private AimDriver _aim;

		private GameplayDriver _gameplay;

		private UiDriver _ui;

		private Rumble _rumble;

		private Key _toggleKey = Key.F5;

		private bool _userEnabled = true;

		private bool _suspendedForSceneLoad;

		private bool _padHasFocus;

		private Vector2 _expectedCursorPosition;

		private bool _expectingCursorPosition;

		private CharacterMainControl _hookedCharacter;

		private UnityAction<DamageInfo> _hurtHandler;

		internal static ModBehaviour Instance { get; private set; }

		internal bool Active
		{
			get
			{
				if (_userEnabled && _config != null && _config.Enabled)
				{
					return !_suspendedForSceneLoad;
				}
				return false;
			}
		}

		protected override void OnAfterSetup()
		{
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Expected O, but got Unknown
			try
			{
				ResolveDependencies();
				string path = Path.Combine(base.info.path, "Settings.json");
				_config = PadConfig.Load(path);
				Pad.Configure(_config);
				if (!Enum.TryParse<Key>(_config.ToggleKey, ignoreCase: true, out _toggleKey))
				{
					Log.Warn("Unrecognised ToggleKey \"" + _config.ToggleKey + "\"; falling back to F5.");
					_toggleKey = Key.F5;
				}
				_rumble = new Rumble(_config);
				_aim = new AimDriver(_config);
				_gameplay = new GameplayDriver(_config, _aim, _rumble);
				_ui = new UiDriver(_config);
				_harmony = new Harmony("com.duckovpad.controller");
				_harmony.PatchAll(Assembly.GetExecutingAssembly());
				SceneLoader.onStartedLoadingScene += OnSceneLoadStarted;
				SceneLoader.onAfterSceneInitialize += OnSceneReady;
				Instance = this;
				Log.Info("Ready. " + ((Gamepad.current != null) ? ("Gamepad detected: " + Gamepad.current.displayName) : "No gamepad connected yet — plug one in at any time."));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to start: " + ex);
			}
		}

		protected override void OnBeforeDeactivate()
		{
			Instance = null;
			SceneLoader.onStartedLoadingScene -= OnSceneLoadStarted;
			SceneLoader.onAfterSceneInitialize -= OnSceneReady;
			UnhookCharacter();
			_ui?.ReleaseAllButtons();
			_rumble?.Stop();
			try
			{
				Harmony harmony = _harmony;
				if (harmony != null)
				{
					harmony.UnpatchAll("com.duckovpad.controller");
				}
			}
			catch (Exception ex)
			{
				Log.Error("Unpatch failed: " + ex.Message);
			}
			_harmony = null;
		}

		private void ResolveDependencies()
		{
			AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
			{
				string text = new AssemblyName(args.Name).Name;
				string text2 = Path.Combine(base.info.path, text + ".dll");
				return (!File.Exists(text2)) ? null : Assembly.LoadFrom(text2);
			};
		}

		private void Update()
		{
			if (_config == null)
			{
				return;
			}
			Keyboard current = Keyboard.current;
			if (current != null && current[_toggleKey].wasPressedThisFrame)
			{
				_userEnabled = !_userEnabled;
				if (!_userEnabled)
				{
					_ui.ReleaseAllButtons();
					_rumble.Stop();
				}
				string text = "Gamepad control " + (_userEnabled ? "enabled" : "disabled");
				Log.Info(text);
				CharacterMainControl main = CharacterMainControl.Main;
				if (main != null)
				{
					main.PopText(text);
				}
			}
			RefreshCharacterHook();
			_rumble?.Update(Time.unscaledDeltaTime);
		}

		private void OnSceneLoadStarted(SceneLoadingContext context)
		{
			_suspendedForSceneLoad = true;
			_ui?.ReleaseAllButtons();
			_rumble?.Stop();
			_gameplay?.Reset();
			UnhookCharacter();
		}

		private void OnSceneReady(SceneLoadingContext context)
		{
			_suspendedForSceneLoad = false;
			_aim?.Reset();
			_ui?.Reset();
			Pad.InvalidateCache();
		}

		internal bool DriveInput(CharacterInputControl control)
		{
			if (!Active || control == null)
			{
				return true;
			}
			Pad.Poll();
			if (!Pad.Available)
			{
				_padHasFocus = false;
				return true;
			}
			float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
			UpdateDeviceFocus();
			if (!_padHasFocus)
			{
				_expectingCursorPosition = false;
				return true;
			}
			if (View.ActiveView != null || GameManager.Paused || (PauseMenu.Instance != null && PauseMenu.Instance.Shown))
			{
				_expectingCursorPosition = false;
				_ui.Update(deltaTime);
				return true;
			}
			_ui.ReleaseAllButtons();
			InputManager inputManager = control.inputManager;
			CharacterMainControl main = CharacterMainControl.Main;
			if (inputManager == null || main == null)
			{
				return true;
			}
			_gameplay.Update(control, inputManager, main, deltaTime);
			if (_aim.HasReticle)
			{
				_expectedCursorPosition = _aim.ReticleScreenPosition;
				_expectingCursorPosition = true;
			}
			return false;
		}

		private void UpdateDeviceFocus()
		{
			if (Pad.LastActivityTime > 0f && Time.unscaledTime - Pad.LastActivityTime < 0.25f)
			{
				_padHasFocus = true;
			}
			if (!_padHasFocus)
			{
				return;
			}
			Keyboard current = Keyboard.current;
			if (current != null && current.anyKey.wasPressedThisFrame && !current[_toggleKey].wasPressedThisFrame)
			{
				_padHasFocus = false;
				return;
			}
			Mouse current2 = Mouse.current;
			if (current2 != null)
			{
				if (current2.leftButton.wasPressedThisFrame || current2.rightButton.wasPressedThisFrame)
				{
					_padHasFocus = false;
				}
				else if (_expectingCursorPosition && (current2.position.ReadValue() - _expectedCursorPosition).sqrMagnitude > 64f)
				{
					_padHasFocus = false;
				}
			}
		}

		private void RefreshCharacterHook()
		{
			CharacterMainControl main = CharacterMainControl.Main;
			if (main == _hookedCharacter)
			{
				return;
			}
			UnhookCharacter();
			if (main == null)
			{
				return;
			}
			try
			{
				Health health = main.Health;
				if (health?.OnHurtEvent != null)
				{
					_hurtHandler = delegate
					{
						_rumble?.Hurt();
					};
					health.OnHurtEvent.AddListener(_hurtHandler);
					_hookedCharacter = main;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Could not hook damage feedback: " + ex.Message);
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
				_hookedCharacter.Health?.OnHurtEvent?.RemoveListener(_hurtHandler);
			}
			catch (Exception)
			{
			}
			_hookedCharacter = null;
			_hurtHandler = null;
		}

		internal void OnWeaponFired()
		{
			_rumble?.Shoot();
		}
	}
	[HarmonyPatch(typeof(CharacterInputControl), "Update")]
	internal static class CharacterInputControlUpdatePatch
	{
		private static bool Prefix(CharacterInputControl __instance)
		{
			ModBehaviour instance = ModBehaviour.Instance;
			if (instance == null)
			{
				return true;
			}
			try
			{
				return instance.DriveInput(__instance);
			}
			catch (Exception ex)
			{
				Log.Error("Input update failed, handing control back to keyboard/mouse: " + ex);
				return true;
			}
		}
	}
	[HarmonyPatch(typeof(InputManager), "AddRecoil")]
	internal static class InputManagerAddRecoilPatch
	{
		private static void Postfix()
		{
			ModBehaviour.Instance?.OnWeaponFired();
		}
	}
	internal static class Pad
	{
		private enum EdgeMode
		{
			Down,
			Held,
			Up
		}

		private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "A", "buttonSouth" },
			{ "B", "buttonEast" },
			{ "X", "buttonWest" },
			{ "Y", "buttonNorth" },
			{ "LB", "leftShoulder" },
			{ "RB", "rightShoulder" },
			{ "LT", "leftTrigger" },
			{ "RT", "rightTrigger" },
			{ "L3", "leftStickPress" },
			{ "R3", "rightStickPress" },
			{ "LS", "leftStickPress" },
			{ "RS", "rightStickPress" },
			{ "Start", "start" },
			{ "Menu", "start" },
			{ "Select", "select" },
			{ "Back", "select" },
			{ "View", "select" },
			{ "DpadUp", "dpad/up" },
			{ "DpadDown", "dpad/down" },
			{ "DpadLeft", "dpad/left" },
			{ "DpadRight", "dpad/right" },
			{ "Cross", "buttonSouth" },
			{ "Circle", "buttonEast" },
			{ "Square", "buttonWest" },
			{ "Triangle", "buttonNorth" },
			{ "L1", "leftShoulder" },
			{ "R1", "rightShoulder" },
			{ "L2", "leftTrigger" },
			{ "R2", "rightTrigger" },
			{ "Options", "start" },
			{ "Share", "select" },
			{ "ZL", "leftTrigger" },
			{ "ZR", "rightTrigger" },
			{ "Plus", "start" },
			{ "Minus", "select" }
		};

		private static readonly Dictionary<string, ButtonControl> ControlCache = new Dictionary<string, ButtonControl>(StringComparer.OrdinalIgnoreCase);

		private static readonly HashSet<string> ChordedButtons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private static Gamepad _gamepad;

		private static string _modifier = "leftShoulder";

		public static Gamepad Current => _gamepad;

		public static bool Available => _gamepad != null;

		public static bool ModifierHeld { get; private set; }

		public static float LastActivityTime { get; private set; }

		public static Vector2 LeftStickRaw => _gamepad?.leftStick.ReadValue() ?? Vector2.zero;

		public static Vector2 RightStickRaw => _gamepad?.rightStick.ReadValue() ?? Vector2.zero;

		public static void Configure(PadConfig config)
		{
			ChordedButtons.Clear();
			ControlCache.Clear();
			_modifier = Canonical(config.Buttons.Modifier);
			if (string.IsNullOrEmpty(_modifier))
			{
				_modifier = "leftShoulder";
			}
			FieldInfo[] fields = typeof(PadConfig.Bindings).GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (!(fieldInfo.FieldType != typeof(string)) && !(fieldInfo.Name == "Modifier"))
				{
					string text = fieldInfo.GetValue(config.Buttons) as string;
					if (!string.IsNullOrWhiteSpace(text) && TrySplitChord(text, out var _, out var button))
					{
						ChordedButtons.Add(button);
					}
				}
			}
		}

		public static void Poll()
		{
			_gamepad = Gamepad.current;
			if (_gamepad == null)
			{
				ModifierHeld = false;
				return;
			}
			ModifierHeld = Resolve(_modifier)?.isPressed ?? false;
			if (_gamepad.lastUpdateTime > 0.0 && (_gamepad.leftStick.ReadValue().sqrMagnitude > 0.04f || _gamepad.rightStick.ReadValue().sqrMagnitude > 0.04f || (_gamepad.wasUpdatedThisFrame && AnyButtonPressed())))
			{
				LastActivityTime = Time.unscaledTime;
			}
		}

		private static bool AnyButtonPressed()
		{
			Gamepad gamepad = _gamepad;
			if (gamepad == null)
			{
				return false;
			}
			if (!gamepad.buttonSouth.isPressed && !gamepad.buttonEast.isPressed && !gamepad.buttonWest.isPressed && !gamepad.buttonNorth.isPressed && !gamepad.leftShoulder.isPressed && !gamepad.rightShoulder.isPressed && !gamepad.leftTrigger.isPressed && !gamepad.rightTrigger.isPressed && !gamepad.leftStickButton.isPressed && !gamepad.rightStickButton.isPressed && !gamepad.startButton.isPressed && !gamepad.selectButton.isPressed && !gamepad.dpad.up.isPressed && !gamepad.dpad.down.isPressed && !gamepad.dpad.left.isPressed)
			{
				return gamepad.dpad.right.isPressed;
			}
			return true;
		}

		public static bool Down(string binding)
		{
			return Evaluate(binding, EdgeMode.Down);
		}

		public static bool Held(string binding)
		{
			return Evaluate(binding, EdgeMode.Held);
		}

		public static bool Up(string binding)
		{
			return Evaluate(binding, EdgeMode.Up);
		}

		private static bool Evaluate(string binding, EdgeMode mode)
		{
			if (_gamepad == null || string.IsNullOrWhiteSpace(binding))
			{
				return false;
			}
			if (TrySplitChord(binding, out var modifier, out var button))
			{
				ButtonControl buttonControl = Resolve(string.IsNullOrEmpty(modifier) ? _modifier : Canonical(modifier));
				if (buttonControl == null || !buttonControl.isPressed)
				{
					return false;
				}
			}
			else
			{
				button = Canonical(binding);
				if (ModifierHeld && ChordedButtons.Contains(button))
				{
					return false;
				}
			}
			ButtonControl buttonControl2 = Resolve(button);
			if (buttonControl2 == null)
			{
				return false;
			}
			return mode switch
			{
				EdgeMode.Down => buttonControl2.wasPressedThisFrame, 
				EdgeMode.Up => buttonControl2.wasReleasedThisFrame, 
				_ => buttonControl2.isPressed, 
			};
		}

		public static float Value(string binding)
		{
			if (_gamepad == null || string.IsNullOrWhiteSpace(binding))
			{
				return 0f;
			}
			TrySplitChord(binding, out var _, out var button);
			if (string.IsNullOrEmpty(button))
			{
				button = Canonical(binding);
			}
			return Resolve(button)?.ReadValue() ?? 0f;
		}

		public static Vector2 LeftStick(float deadzone, float outerDeadzone, float curve)
		{
			return Shape(_gamepad?.leftStick.ReadValue() ?? Vector2.zero, deadzone, outerDeadzone, curve);
		}

		public static Vector2 RightStick(float deadzone, float outerDeadzone, float curve)
		{
			return Shape(_gamepad?.rightStick.ReadValue() ?? Vector2.zero, deadzone, outerDeadzone, curve);
		}

		public static Vector2 Shape(Vector2 raw, float deadzone, float outerDeadzone, float curve)
		{
			float magnitude = raw.magnitude;
			if (magnitude <= deadzone)
			{
				return Vector2.zero;
			}
			Vector2 vector = raw / magnitude;
			float num = Mathf.Max(0.0001f, outerDeadzone - deadzone);
			float num2 = Mathf.Clamp01((magnitude - deadzone) / num);
			if (Math.Abs(curve - 1f) > 0.001f)
			{
				num2 = Mathf.Pow(num2, curve);
			}
			return vector * num2;
		}

		private static bool TrySplitChord(string binding, out string modifier, out string button)
		{
			modifier = null;
			button = null;
			if (string.IsNullOrWhiteSpace(binding))
			{
				return false;
			}
			int num = binding.IndexOf('+');
			if (num <= 0 || num >= binding.Length - 1)
			{
				button = Canonical(binding);
				return false;
			}
			modifier = Canonical(binding.Substring(0, num));
			button = Canonical(binding.Substring(num + 1));
			return true;
		}

		private static string Canonical(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return string.Empty;
			}
			name = name.Trim();
			if (!Aliases.TryGetValue(name, out var value))
			{
				return name;
			}
			return value;
		}

		private static ButtonControl Resolve(string canonicalName)
		{
			if (_gamepad == null || string.IsNullOrEmpty(canonicalName))
			{
				return null;
			}
			if (ControlCache.TryGetValue(canonicalName, out var value))
			{
				if (value != null && value.device == _gamepad)
				{
					return value;
				}
				ControlCache.Remove(canonicalName);
			}
			ButtonControl buttonControl = null;
			try
			{
				buttonControl = _gamepad.TryGetChildControl<ButtonControl>(canonicalName);
			}
			catch (Exception)
			{
			}
			if (buttonControl == null)
			{
				Log.Warn("Unknown gamepad button \"" + canonicalName + "\" in Settings.json; that binding is ignored.");
				return null;
			}
			ControlCache[canonicalName] = buttonControl;
			return buttonControl;
		}

		public static void InvalidateCache()
		{
			ControlCache.Clear();
		}
	}
	public class PadConfig
	{
		public class AimSettings
		{
			public string Mode = "absolute";

			public float ReticleDistance = 11f;

			public float MinReticleDistance = 4.5f;

			public float Deadzone = 0.2f;

			public float OuterDeadzone = 0.95f;

			public float ResponseCurve = 1.5f;

			public float AdsDistanceMultiplier = 1.35f;

			public float Smoothing = 28f;

			public float RelativeSensitivity = 260f;

			public float RelativeAdsMultiplier = 0.55f;
		}

		public class AimAssistSettings
		{
			public bool Enabled = true;

			public float Strength = 0.5f;

			public float MaxAngleDegrees = 16f;

			public float MaxDistance = 30f;

			public bool RequireLineOfSight = true;

			public float Stickiness = 0.35f;

			public bool OnlyWhileFiringOrAds;
		}

		public class MoveSettings
		{
			public float Deadzone = 0.18f;

			public float OuterDeadzone = 0.95f;

			public float ResponseCurve = 1f;

			public bool AutoRunAtFullStick = true;

			public float AutoRunThreshold = 0.92f;
		}

		public class CursorSettings
		{
			public float Speed = 1500f;

			public float ResponseCurve = 2f;

			public float Deadzone = 0.18f;

			public float PrecisionMultiplier = 0.35f;

			public float ScrollSpeed = 12f;

			public float ScrollUnitsPerNotch = 120f;

			public bool ScaleWithResolution = true;
		}

		public class RumbleSettings
		{
			public bool Enabled = true;

			public float Scale = 1f;

			public float ShootLow = 0.16f;

			public float ShootHigh = 0.32f;

			public float ShootDuration = 0.07f;

			public float HurtLow = 0.55f;

			public float HurtHigh = 0.45f;

			public float HurtDuration = 0.22f;
		}

		public class Bindings
		{
			public string Modifier = "LB";

			public string Fire = "RT";

			public string Ads = "LT";

			public string Sprint = "L3";

			public string Dash = "B";

			public string Reload = "X";

			public string Interact = "A";

			public string PutAway = "LB+A";

			public string SwitchWeapon = "Y";

			public string MeleeWeapon = "LB+Y";

			public string CharacterSkill = "RB";

			public string NightVision = "R3";

			public string ToggleView = "LB+R3";

			public string Quack = "LB+B";

			public string StopAction = "LB+X";

			public string Inventory = "Start";

			public string Map = "Select";

			public string PauseMenu = "LB+Start";

			public string QuestLog = "LB+Select";

			public string CycleNext = "DpadUp";

			public string CyclePrevious = "DpadDown";

			public string ShortcutPrevious = "DpadLeft";

			public string ShortcutNext = "DpadRight";

			public string QuickItem3 = "LB+DpadUp";

			public string QuickItem4 = "LB+DpadRight";

			public string QuickItem5 = "LB+DpadDown";

			public string QuickItem6 = "LB+DpadLeft";

			public string UiClick = "A";

			public string UiContext = "X";

			public string UiBack = "B";

			public string UiDrop = "Y";

			public string UiDragModifier = "RB";

			public string UiPrecision = "LT";

			public string UiPageNext = "RB";

			public string UiPagePrevious = "LB";

			public string UiRotate = "LB";

			public string UiClose = "Start";
		}

		public bool Enabled = true;

		public string ToggleKey = "F5";

		public AimSettings Aim = new AimSettings();

		public AimAssistSettings AimAssist = new AimAssistSettings();

		public MoveSettings Move = new MoveSettings();

		public CursorSettings Cursor = new CursorSettings();

		public RumbleSettings Rumble = new RumbleSettings();

		public Bindings Buttons = new Bindings();

		public static PadConfig Load(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					PadConfig padConfig = new PadConfig();
					padConfig.Save(path);
					Log.Info("No Settings.json found; wrote defaults to " + path);
					return padConfig;
				}
				PadConfig padConfig2 = JsonConvert.DeserializeObject<PadConfig>(File.ReadAllText(path));
				if (padConfig2 == null)
				{
					Log.Warn("Settings.json was empty; using defaults.");
					return new PadConfig();
				}
				PadConfig padConfig3 = padConfig2;
				if (padConfig3.Aim == null)
				{
					padConfig3.Aim = new AimSettings();
				}
				padConfig3 = padConfig2;
				if (padConfig3.AimAssist == null)
				{
					padConfig3.AimAssist = new AimAssistSettings();
				}
				padConfig3 = padConfig2;
				if (padConfig3.Move == null)
				{
					padConfig3.Move = new MoveSettings();
				}
				padConfig3 = padConfig2;
				if (padConfig3.Cursor == null)
				{
					padConfig3.Cursor = new CursorSettings();
				}
				padConfig3 = padConfig2;
				if (padConfig3.Rumble == null)
				{
					padConfig3.Rumble = new RumbleSettings();
				}
				padConfig3 = padConfig2;
				if (padConfig3.Buttons == null)
				{
					padConfig3.Buttons = new Bindings();
				}
				padConfig2.Validate();
				return padConfig2;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read Settings.json, falling back to defaults: " + ex.Message);
				return new PadConfig();
			}
		}

		public void Save(string path)
		{
			try
			{
				File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write Settings.json: " + ex.Message);
			}
		}

		private void Validate()
		{
			Aim.Deadzone = Mathf.Clamp(Aim.Deadzone, 0f, 0.9f);
			Aim.OuterDeadzone = Mathf.Clamp(Aim.OuterDeadzone, Aim.Deadzone + 0.01f, 1f);
			Aim.ResponseCurve = Mathf.Clamp(Aim.ResponseCurve, 0.2f, 5f);
			Aim.MinReticleDistance = Mathf.Max(0.5f, Aim.MinReticleDistance);
			Aim.ReticleDistance = Mathf.Max(Aim.MinReticleDistance + 0.1f, Aim.ReticleDistance);
			AimAssist.Strength = Mathf.Clamp01(AimAssist.Strength);
			AimAssist.Stickiness = Mathf.Clamp01(AimAssist.Stickiness);
			AimAssist.MaxAngleDegrees = Mathf.Clamp(AimAssist.MaxAngleDegrees, 0f, 90f);
			AimAssist.MaxDistance = Mathf.Max(1f, AimAssist.MaxDistance);
			Move.Deadzone = Mathf.Clamp(Move.Deadzone, 0f, 0.9f);
			Move.OuterDeadzone = Mathf.Clamp(Move.OuterDeadzone, Move.Deadzone + 0.01f, 1f);
			Move.ResponseCurve = Mathf.Clamp(Move.ResponseCurve, 0.2f, 5f);
			Cursor.Deadzone = Mathf.Clamp(Cursor.Deadzone, 0f, 0.9f);
			Cursor.ResponseCurve = Mathf.Clamp(Cursor.ResponseCurve, 0.2f, 5f);
			Cursor.Speed = Mathf.Max(50f, Cursor.Speed);
			Rumble.Scale = Mathf.Clamp(Rumble.Scale, 0f, 2f);
		}
	}
	internal sealed class Rumble
	{
		private readonly PadConfig _config;

		private float _low;

		private float _high;

		private float _decayPerSecond;

		private bool _motorsRunning;

		public Rumble(PadConfig config)
		{
			_config = config;
		}

		public void Shoot()
		{
			PadConfig.RumbleSettings rumble = _config.Rumble;
			Pulse(rumble.ShootLow, rumble.ShootHigh, rumble.ShootDuration);
		}

		public void Hurt()
		{
			PadConfig.RumbleSettings rumble = _config.Rumble;
			Pulse(rumble.HurtLow, rumble.HurtHigh, rumble.HurtDuration);
		}

		public void Pulse(float low, float high, float duration)
		{
			PadConfig.RumbleSettings rumble = _config.Rumble;
			if (rumble.Enabled && !(rumble.Scale <= 0f) && !(duration <= 0f))
			{
				low = Mathf.Clamp01(low * rumble.Scale);
				high = Mathf.Clamp01(high * rumble.Scale);
				if (!(low + high <= _low + _high))
				{
					_low = low;
					_high = high;
					_decayPerSecond = 1f / duration;
				}
			}
		}

		public void Update(float deltaTime)
		{
			Gamepad current = Gamepad.current;
			if (current == null)
			{
				_motorsRunning = false;
			}
			else if (!_config.Rumble.Enabled)
			{
				Stop();
			}
			else if (_low <= 0f && _high <= 0f)
			{
				if (_motorsRunning)
				{
					SafeSetMotors(current, 0f, 0f);
					_motorsRunning = false;
				}
			}
			else
			{
				float num = _decayPerSecond * deltaTime;
				_low = Mathf.Max(0f, _low - num);
				_high = Mathf.Max(0f, _high - num);
				SafeSetMotors(current, _low, _high);
				_motorsRunning = true;
			}
		}

		public void Stop()
		{
			_low = 0f;
			_high = 0f;
			Gamepad current = Gamepad.current;
			if (current != null && _motorsRunning)
			{
				SafeSetMotors(current, 0f, 0f);
			}
			_motorsRunning = false;
		}

		private static void SafeSetMotors(Gamepad gamepad, float low, float high)
		{
			try
			{
				gamepad.SetMotorSpeeds(low, high);
			}
			catch (Exception)
			{
			}
		}
	}
	internal sealed class UiDriver
	{
		private readonly PadConfig _config;

		private Vector2 _cursor;

		private bool _cursorInitialised;

		private bool _leftDown;

		private bool _rightDown;

		private readonly List<Key> _keysToRelease = new List<Key>();

		private readonly List<Key> _keysHeld = new List<Key>();

		public Vector2 CursorPosition => _cursor;

		public bool CursorActive { get; private set; }

		public UiDriver(PadConfig config)
		{
			_config = config;
		}

		public void Reset()
		{
			_cursorInitialised = false;
			CursorActive = false;
			ReleaseAllButtons();
		}

		public void ReleaseAllButtons()
		{
			if (_leftDown || _rightDown || _keysHeld.Count != 0)
			{
				_leftDown = false;
				_rightDown = false;
				_keysHeld.Clear();
				_keysToRelease.Clear();
				Mouse current = Mouse.current;
				if (current != null)
				{
					InputSystem.QueueStateEvent(current, new MouseState
					{
						position = _cursor
					});
				}
				Keyboard current2 = Keyboard.current;
				if (current2 != null)
				{
					InputSystem.QueueStateEvent(current2, default(KeyboardState));
				}
			}
		}

		public void Update(float deltaTime)
		{
			Mouse current = Mouse.current;
			if (current == null)
			{
				return;
			}
			PadConfig.Bindings buttons = _config.Buttons;
			PadConfig.CursorSettings cursor = _config.Cursor;
			if (!_cursorInitialised)
			{
				_cursor = current.position.ReadValue();
				if (_cursor.x <= 0f || _cursor.y <= 0f || _cursor.x >= (float)Screen.width || _cursor.y >= (float)Screen.height)
				{
					_cursor = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
				}
				_cursorInitialised = true;
			}
			Vector2 vector = Pad.LeftStick(cursor.Deadzone, 0.95f, cursor.ResponseCurve);
			float num = cursor.Speed;
			if (cursor.ScaleWithResolution)
			{
				num *= (float)Screen.height / 1080f;
			}
			if (Pad.Held(buttons.UiPrecision))
			{
				num *= cursor.PrecisionMultiplier;
			}
			_cursor += vector * num * deltaTime;
			_cursor.x = Mathf.Clamp(_cursor.x, 0f, (float)Screen.width - 1f);
			_cursor.y = Mathf.Clamp(_cursor.y, 0f, (float)Screen.height - 1f);
			Vector2 vector2 = Pad.RightStick(cursor.Deadzone, 0.95f, cursor.ResponseCurve);
			Vector2 zero = Vector2.zero;
			if (Mathf.Abs(vector2.y) > 0f)
			{
				zero.y = vector2.y * cursor.ScrollSpeed * cursor.ScrollUnitsPerNotch * deltaTime;
			}
			bool flag = Pad.Held(buttons.UiClick) || Pad.Held(buttons.UiDragModifier);
			bool flag2 = Pad.Held(buttons.UiContext);
			_leftDown = flag;
			_rightDown = flag2;
			MouseState state = new MouseState
			{
				position = _cursor,
				scroll = zero
			}.WithButton(MouseButton.Left, flag).WithButton(MouseButton.Right, flag2);
			InputSystem.QueueStateEvent(current, state);
			try
			{
				current.WarpCursorPosition(_cursor);
			}
			catch (Exception)
			{
			}
			CursorActive = true;
			FlushKeyReleases();
			if (Pad.Down(buttons.UiBack))
			{
				CloseActiveView();
			}
			if (Pad.Down(buttons.UiDrop))
			{
				PressKey(Key.X);
			}
			if (IsBuilderActive())
			{
				if (Pad.Down(buttons.UiRotate))
				{
					PressKey(Key.Q);
				}
			}
			else
			{
				if (Pad.Down(buttons.UiPageNext))
				{
					PressKey(Key.S);
				}
				if (Pad.Down(buttons.UiPagePrevious))
				{
					PressKey(Key.W);
				}
			}
			if (Pad.Down(buttons.UiClose))
			{
				CloseActiveView();
			}
		}

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

		private static void CloseActiveView()
		{
			ViewUtil.CloseActive();
		}

		private void PressKey(Key key)
		{
			Keyboard current = Keyboard.current;
			if (current == null || _keysHeld.Contains(key))
			{
				return;
			}
			_keysHeld.Add(key);
			_keysToRelease.Add(key);
			KeyboardState state = default(KeyboardState);
			foreach (Key item in _keysHeld)
			{
				state.Set(item, state: true);
			}
			InputSystem.QueueStateEvent(current, state);
		}

		private void FlushKeyReleases()
		{
			if (_keysToRelease.Count == 0)
			{
				return;
			}
			Keyboard current = Keyboard.current;
			if (current != null)
			{
				foreach (Key item in _keysToRelease)
				{
					_keysHeld.Remove(item);
				}
				KeyboardState state = default(KeyboardState);
				foreach (Key item2 in _keysHeld)
				{
					state.Set(item2, state: true);
				}
				InputSystem.QueueStateEvent(current, state);
			}
			_keysToRelease.Clear();
		}
	}
	internal static class ViewUtil
	{
		private static readonly MethodInfo TryQuitMethod = typeof(View).GetMethod("TryQuit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		public static void TryQuit(View view)
		{
			if (view == null)
			{
				return;
			}
			if (TryQuitMethod != null)
			{
				try
				{
					TryQuitMethod.Invoke(view, null);
					return;
				}
				catch (Exception ex)
				{
					Log.Warn("View.TryQuit failed, closing directly: " + (ex.InnerException ?? ex).Message);
				}
			}
			try
			{
				view.Close();
			}
			catch (Exception)
			{
			}
		}

		public static void CloseActive()
		{
			TryQuit(View.ActiveView);
		}
	}
}
