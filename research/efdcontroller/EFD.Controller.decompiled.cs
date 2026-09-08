using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Dialogues;
using Duckov.Buildings;
using Duckov.Buildings.UI;
using Duckov.Modding;
using Duckov.Options;
using Duckov.UI;
using EFD.Controller.Patches;
using EFD.Controller.Utils;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETStandard,Version=v2.1", FrameworkDisplayName = ".NET Standard 2.1")]
[assembly: AssemblyCompany("EFD.Controller")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("EFD.Controller")]
[assembly: AssemblyTitle("EFD.Controller")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
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
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableAttribute : Attribute
	{
		public readonly byte[] NullableFlags;

		public NullableAttribute(byte P_0)
		{
			NullableFlags = new byte[1] { P_0 };
		}

		public NullableAttribute(byte[] P_0)
		{
			NullableFlags = P_0;
		}
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableContextAttribute : Attribute
	{
		public readonly byte Flag;

		public NullableContextAttribute(byte P_0)
		{
			Flag = P_0;
		}
	}
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
namespace EFD.Controller
{
	public class ControllerSettings
	{
		public class ButtonMapping
		{
			public string Sprint { get; set; } = "rightShoulder";

			public string Attack { get; set; } = "rightTrigger";

			public string Reload { get; set; } = "buttonNorth";

			public string Back { get; set; } = "buttonEast";

			public string Interact { get; set; } = "buttonWest";

			public string Dash { get; set; } = "buttonSouth";

			public string Rotate { get; set; } = "leftShoulder";

			public string Select { get; set; } = "buttonSouth";

			public string PickupPlace { get; set; } = "buttonWest";

			public string Drop { get; set; } = "buttonNorth";
		}

		public ButtonMapping Buttons { get; set; } = new ButtonMapping();

		public static ControllerSettings Load(string path)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException("Settings file not found", path);
			}
			try
			{
				return JsonConvert.DeserializeObject<ControllerSettings>(File.ReadAllText(path)) ?? new ControllerSettings();
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("Failed to load settings from " + path + ": " + ex.Message);
				return new ControllerSettings();
			}
		}
	}
	public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		private Harmony? Harmony;

		private bool EnsureEnabled;

		internal static ControllerSettings? Settings { get; private set; }

		protected override void OnAfterSetup()
		{
			Harmony = new Harmony("com.duckov.controllerpatch");
			Harmony.PatchAll();
			SceneLoader.onStartedLoadingScene += OnStartedLoadingScene;
			SceneLoader.onAfterSceneInitialize += OnAfterSceneInitialize;
			Settings = ControllerSettings.Load(Path.Join(base.info.path.AsSpan(), "Settings.json".AsSpan()));
		}

		protected override void OnBeforeDeactivate()
		{
			Harmony?.UnpatchAll("com.duckov.controllerpatch");
			CharacterInputControl_Update_Patch.CleanupLinuxDisplay();
		}

		private void LateUpdate()
		{
			if (Input.GetKeyDown(KeyCode.F5))
			{
				CharacterInputControl_Update_Patch.IsEnabled = !CharacterInputControl_Update_Patch.IsEnabled;
				UnityEngine.Debug.Log("[Controller] input is now " + (CharacterInputControl_Update_Patch.IsEnabled ? "enabled" : "disabled"));
				CharacterMainControl.Main.PopText("Controller input is now " + (CharacterInputControl_Update_Patch.IsEnabled ? "enabled" : "disabled"));
			}
		}

		private void OnStartedLoadingScene(SceneLoadingContext context)
		{
			if (CharacterInputControl_Update_Patch.IsEnabled)
			{
				CharacterInputControl_Update_Patch.IsEnabled = false;
				EnsureEnabled = true;
			}
		}

		private void OnAfterSceneInitialize(SceneLoadingContext context)
		{
			if (EnsureEnabled && !CharacterInputControl_Update_Patch.IsEnabled)
			{
				CharacterInputControl_Update_Patch.IsEnabled = true;
				EnsureEnabled = false;
			}
		}
	}
}
namespace EFD.Controller.Utils
{
	public static class Reflection
	{
		public static TReturn? GetFieldValue<TReturn, TType>(string field) where TType : class
		{
			return GetFieldValue<TReturn, TType>(null, field);
		}

		public static TReturn? GetPropertyValue<TReturn, TType>(string property) where TType : class
		{
			return GetPropertyValue<TReturn, TType>(null, property);
		}

		public static void SetFieldValue<TType, TValue>(string field, TValue value) where TType : class
		{
			SetFieldValue<TType, TValue>(null, field, value);
		}

		public static void SetPropertyValue<TType, TValue>(string property, TValue value) where TType : class
		{
			SetPropertyValue<TType, TValue>(null, property, value);
		}

		public static TReturn? Invoke<TReturn, TType>(string method, params object[] args) where TType : class
		{
			return Invoke<TReturn, TType>(null, method, args);
		}

		public static void InvokeVoid<TType>(string method, params object[] args) where TType : class
		{
			InvokeVoid<TType>(null, method, args);
		}

		public static TReturn? InvokeInternal<TReturn>(Assembly assembly, string @class, string method)
		{
			return InvokeInternal<TReturn>(assembly, @class, method, null);
		}

		public static void InvokeInternalVoid(Assembly assembly, string @class, string method)
		{
			InvokeInternalVoid(assembly, @class, method, null);
		}

		public static TReturn? GetFieldValue<TReturn, TType>(TType? pThis, string field)
		{
			return (TReturn)(typeof(TType).GetField(field, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(pThis));
		}

		public static TReturn? GetPropertyValue<TReturn, TType>(TType? pThis, string property)
		{
			return (TReturn)(typeof(TType).GetProperty(property, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(pThis));
		}

		public static void SetFieldValue<TType, TValue>(TType? pThis, string field, TValue value)
		{
			typeof(TType).GetField(field, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(pThis, value);
		}

		public static void SetPropertyValue<TType, TValue>(TType? pThis, string property, TValue value)
		{
			typeof(TType).GetProperty(property, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(pThis, value);
		}

		public static TReturn? Invoke<TReturn, TType>(TType? pThis, string method, params object[] args)
		{
			return (TReturn)(typeof(TType).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(pThis, args));
		}

		public static void InvokeVoid<TType>(TType? pThis, string method, params object[] args)
		{
			typeof(TType).GetMethod(method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(pThis, args);
		}

		public static TReturn? InvokeInternal<TReturn>(Assembly assembly, string @class, string method, params object[]? args)
		{
			return (TReturn)((assembly.GetType(@class)?.GetMethod(method))?.Invoke(null, args));
		}

		public static void InvokeInternalVoid(Assembly assembly, string @class, string method, params object[]? args)
		{
			(assembly.GetType(@class)?.GetMethod(method))?.Invoke(null, args);
		}

		public static Assembly? GetAssembly(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().ToList().Find((Assembly x) => x.GetName().Name == name);
		}

		public static IEnumerable<Type> FindDerivedTypes(Assembly assembly, Type baseType)
		{
			return assembly.GetTypes().Where(baseType.IsSubclassOf);
		}

		public static Type FindDerivedType(Assembly assembly, Type baseType)
		{
			return FindDerivedTypes(assembly, baseType).First();
		}
	}
}
namespace EFD.Controller.Patches
{
	[HarmonyPatch(typeof(CharacterInputControl), "Update")]
	public static class CharacterInputControl_Update_Patch
	{
		private static readonly RuntimePlatform Platform = Application.platform;

		private const uint MOUSEEVENTF_LEFTDOWN = 2u;

		private const uint MOUSEEVENTF_LEFTUP = 4u;

		private const uint MOUSEEVENTF_WHEEL = 2048u;

		private const float ScrollSensitivity = 720f;

		private static readonly ControllerSettings? Settings = ModBehaviour.Settings;

		private static int CurrentShortcutIndex = 1;

		private static bool IsDragging = false;

		private static bool IsHoldingClick = false;

		private static bool ClickPending = false;

		private static float ClickReleaseTime = 0f;

		private const float ClickHoldDuration = 0.05f;

		private static IntPtr LinuxDisplay = IntPtr.Zero;

		public static bool IsEnabled = true;

		[DllImport("user32.dll")]
		private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

		[DllImport("ApplicationServices.framework/ApplicationServices")]
		private static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, float x, float y, uint mouseButton);

		[DllImport("ApplicationServices.framework/ApplicationServices")]
		private static extern IntPtr CGEventCreateScrollWheelEvent(IntPtr source, uint units, uint wheelCount, int wheel1);

		[DllImport("ApplicationServices.framework/ApplicationServices")]
		private static extern void CGEventPost(uint tap, IntPtr cgEvent);

		[DllImport("libX11.so.6")]
		private static extern IntPtr XOpenDisplay(string display_name);

		[DllImport("libX11.so.6")]
		private static extern int XCloseDisplay(IntPtr display);

		[DllImport("libXtst.so.6")]
		private static extern int XTestFakeButtonEvent(IntPtr display, uint button, bool is_press, ulong delay);

		[DllImport("libX11.so.6")]
		private static extern int XFlush(IntPtr display);

		private static void InitializeLinuxDisplay()
		{
			if (LinuxDisplay == IntPtr.Zero)
			{
				LinuxDisplay = XOpenDisplay(null);
				if (LinuxDisplay == IntPtr.Zero)
				{
					UnityEngine.Debug.LogError("Failed to open X11 display connection");
				}
			}
		}

		public static void CleanupLinuxDisplay()
		{
			if (LinuxDisplay != IntPtr.Zero)
			{
				XCloseDisplay(LinuxDisplay);
				LinuxDisplay = IntPtr.Zero;
			}
		}

		public static bool Prefix(CharacterInputControl __instance)
		{
			if (!Application.isFocused || !IsEnabled)
			{
				return true;
			}
			if (Settings == null)
			{
				return true;
			}
			Gamepad current = Gamepad.current;
			if (current != null)
			{
				InputManager inputManager = __instance.inputManager;
				if (inputManager == null)
				{
					return true;
				}
				Vector2 leftStick = current.leftStick.ReadValue();
				if (ClickPending && Time.unscaledTime >= ClickReleaseTime)
				{
					ReleaseMouseClick();
				}
				if (!(View.ActiveView != null))
				{
					return OverrideControls(current, __instance, inputManager, leftStick);
				}
				return OverrideUIControls(current, leftStick);
			}
			return true;
		}

		private static bool OverrideUIControls(Gamepad gamepad, Vector2 leftStick)
		{
			if (Settings == null)
			{
				return true;
			}
			UpdateScroll(gamepad);
			UpdateMousePosition(leftStick);
			if (gamepad.rightShoulder.isPressed)
			{
				HandleDragAndDrop(gamepad);
				return false;
			}
			if (IsDragging)
			{
				EndDrag();
			}
			if (GetButtonState(gamepad, Settings.Buttons.Back, singlePress: true))
			{
				CloseUI();
			}
			if (GetButtonState(gamepad, Settings.Buttons.PickupPlace, singlePress: true))
			{
				SimulateKeyPress(Key.F);
			}
			if (GetButtonState(gamepad, Settings.Buttons.Drop, singlePress: true))
			{
				SimulateKeyPress(Key.X);
			}
			if (View.ActiveView == BuilderView.Instance && GetButtonState(gamepad, Settings.Buttons.Rotate, singlePress: true))
			{
				BuildingRotation fieldValue = Reflection.GetFieldValue<BuildingRotation, BuilderView>(BuilderView.Instance, "previewRotation");
				Reflection.SetFieldValue(BuilderView.Instance, "previewRotation", (BuildingRotation)(((float)fieldValue + 1f + 4f) % 4f));
			}
			if (GetButtonState(gamepad, "leftTrigger", singlePress: true))
			{
				SimulateMouseClick(leftButton: false);
			}
			if (GetButtonState(gamepad, Settings.Buttons.Select, singlePress: true))
			{
				SimulateMouseClick(leftButton: true);
			}
			if (GetButtonState(gamepad, Settings.Buttons.Select))
			{
				SimulateMouseHold(leftButton: true);
			}
			return false;
		}

		private static bool OverrideControls(Gamepad gamepad, CharacterInputControl instance, InputManager inputManager, Vector2 leftStick)
		{
			if (Settings == null)
			{
				return true;
			}
			bool isPressed = gamepad.leftShoulder.isPressed;
			Vector2 aimInputUsingMouse = gamepad.rightStick.ReadValue() * (isPressed ? (OptionsManager.MouseSensitivity / 1.75f) : OptionsManager.MouseSensitivity);
			inputManager.SetMousePosition(Mouse.current.position.ReadValue());
			inputManager.SetAimInputUsingMouse(aimInputUsingMouse);
			inputManager.SetMoveInput(leftStick);
			inputManager.SetTrigger(gamepad.rightTrigger.isPressed, gamepad.rightTrigger.wasPressedThisFrame, gamepad.rightTrigger.wasReleasedThisFrame);
			inputManager.SetRunInput(GetButtonState(gamepad, Settings.Buttons.Sprint));
			inputManager.SetAdsInput(isPressed);
			if (CharacterMainControl.Main.skillAction.holdItemSkillKeeper.CheckSkillAndBinding())
			{
				inputManager.SetAimType(AimTypes.handheldSkill);
				if (gamepad.rightTrigger.wasPressedThisFrame)
				{
					inputManager.StartItemSkillAim();
				}
				else if (gamepad.rightTrigger.wasReleasedThisFrame)
				{
					inputManager.ReleaseItemSkill();
				}
			}
			Reflection.InvokeVoid(instance, "UpdateScollerInput");
			if (gamepad.selectButton.wasPressedThisFrame)
			{
				PauseMenu.Show();
			}
			if (gamepad.rightShoulder.isPressed)
			{
				if (gamepad.dpad.up.isPressed)
				{
					ShortCutInput(instance, 3);
				}
				else if (gamepad.dpad.right.isPressed)
				{
					ShortCutInput(instance, 4);
				}
				else if (gamepad.dpad.down.isPressed)
				{
					ShortCutInput(instance, 5);
				}
				else if (gamepad.dpad.left.isPressed)
				{
					ShortCutInput(instance, 6);
				}
			}
			else
			{
				if (gamepad.dpad.left.wasPressedThisFrame)
				{
					CurrentShortcutIndex--;
					if (CurrentShortcutIndex < 1)
					{
						CurrentShortcutIndex = 3;
					}
					inputManager.SwitchItemAgent(CurrentShortcutIndex);
				}
				else if (gamepad.dpad.right.wasPressedThisFrame)
				{
					CurrentShortcutIndex++;
					if (CurrentShortcutIndex > 3)
					{
						CurrentShortcutIndex = 1;
					}
					inputManager.SwitchItemAgent(CurrentShortcutIndex);
				}
				if (gamepad.dpad.up.wasPressedThisFrame)
				{
					if (ScrollWheelBehaviour.CurrentBehaviour == ScrollWheelBehaviour.Behaviour.AmmoAndInteract)
					{
						inputManager.SetSwitchInteractInput(1);
						inputManager.SetSwitchBulletTypeInput(1);
					}
					else
					{
						inputManager.SetSwitchWeaponInput(1);
					}
				}
				if (gamepad.dpad.down.wasPressedThisFrame)
				{
					if (ScrollWheelBehaviour.CurrentBehaviour == ScrollWheelBehaviour.Behaviour.AmmoAndInteract)
					{
						inputManager.SetSwitchInteractInput(-1);
						inputManager.SetSwitchBulletTypeInput(-1);
					}
					else
					{
						inputManager.SetSwitchWeaponInput(-1);
					}
				}
			}
			if (GetButtonState(gamepad, "leftTrigger", singlePress: true))
			{
				SimulateMouseClick(leftButton: false);
			}
			if (gamepad.leftStickButton.wasPressedThisFrame && !GameManager.Paused)
			{
				inputManager.ToggleNightVision();
			}
			if (gamepad.rightStickButton.wasPressedThisFrame && !GameManager.Paused)
			{
				SimulateKeyPress(Key.T);
			}
			if (GetButtonState(gamepad, Settings.Buttons.Dash, singlePress: true))
			{
				inputManager.Dash();
			}
			if (gamepad.startButton.wasPressedThisFrame)
			{
				OnUIInventoyInput();
			}
			if (GetButtonState(gamepad, Settings.Buttons.Interact, singlePress: true))
			{
				inputManager.Interact();
			}
			if (GetButtonState(gamepad, Settings.Buttons.Interact, singlePress: true))
			{
				inputManager.PutAway();
			}
			if (GetButtonState(gamepad, Settings.Buttons.Reload, singlePress: true))
			{
				CharacterMainControl.Main?.TryToReload();
			}
			return false;
		}

		private static void ShortCutInput(CharacterInputControl instance, int index)
		{
			Reflection.InvokeVoid(instance, "ShortCutInput", index);
		}

		private static bool GetButtonState(Gamepad gamepad, string buttonName, bool singlePress = false)
		{
			if (string.IsNullOrEmpty(buttonName))
			{
				return false;
			}
			ButtonControl buttonControl = gamepad.TryGetChildControl<ButtonControl>(buttonName);
			if (buttonControl == null)
			{
				return false;
			}
			if (!singlePress)
			{
				return buttonControl.isPressed;
			}
			return buttonControl.wasPressedThisFrame;
		}

		private static void CloseUI()
		{
			if (View.ActiveView != null)
			{
				View.ActiveView.Close();
			}
		}

		private static void OnUIInventoyInput()
		{
			if (!GameManager.Paused && !DialogueUI.Active && !SceneLoader.IsSceneLoading)
			{
				if (View.ActiveView != null)
				{
					View.ActiveView.Close();
				}
				else if (LevelManager.Instance.IsBaseLevel)
				{
					PlayerStorage.Instance.InteractableLootBox.InteractWithMainCharacter();
				}
				else
				{
					InventoryView.Show();
				}
			}
		}

		private static void SimulateKeyPress(Key key)
		{
			Keyboard current = Keyboard.current;
			if (current != null)
			{
				InputSystem.QueueStateEvent(current, new KeyboardState(key));
				InputSystem.QueueStateEvent(current, default(KeyboardState));
				InputSystem.Update();
			}
		}

		private static void SimulateMouseClick(bool leftButton)
		{
			if (!ClickPending)
			{
				Mouse current = Mouse.current;
				if (current != null)
				{
					Vector2 position = current.position.ReadValue();
					MouseState mouseState = new MouseState
					{
						position = position
					};
					InputSystem.QueueStateEvent(current, mouseState.WithButton((!leftButton) ? MouseButton.Right : MouseButton.Left));
					InputSystem.Update();
					ClickPending = true;
					ClickReleaseTime = Time.unscaledTime + 0.05f;
				}
			}
		}

		private static void SimulateMouseHold(bool leftButton)
		{
			Mouse current = Mouse.current;
			if (current != null)
			{
				Vector2 position = current.position.ReadValue();
				bool buttonState = GetButtonState(Gamepad.current, Settings.Buttons.Select);
				if (buttonState && !IsHoldingClick)
				{
					MouseState mouseState = new MouseState
					{
						position = position
					};
					InputSystem.QueueStateEvent(current, mouseState.WithButton((!leftButton) ? MouseButton.Right : MouseButton.Left));
					InputSystem.Update();
					IsHoldingClick = true;
				}
				else if (!buttonState && IsHoldingClick)
				{
					MouseState mouseState = new MouseState
					{
						position = position
					};
					InputSystem.QueueStateEvent(current, mouseState.WithButton((!leftButton) ? MouseButton.Right : MouseButton.Left, state: false));
					InputSystem.Update();
					IsHoldingClick = false;
				}
			}
		}

		private static void ReleaseMouseClick()
		{
			Mouse current = Mouse.current;
			if (current == null)
			{
				ClickPending = false;
				return;
			}
			Vector2 position = current.position.ReadValue();
			MouseState mouseState = new MouseState
			{
				position = position
			};
			InputSystem.QueueStateEvent(current, mouseState.WithButton(MouseButton.Left, state: false));
			InputSystem.Update();
			ClickPending = false;
		}

		private static void UpdateMousePosition(Vector2 leftStick)
		{
			Vector2 position = Mouse.current.position.ReadValue() + leftStick * 1000f * Time.deltaTime;
			position.x = Mathf.Clamp(position.x, 0f, Screen.width);
			position.y = Mathf.Clamp(position.y, 0f, Screen.height);
			Mouse.current.WarpCursorPosition(position);
		}

		private static void UpdateScroll(Gamepad gamepad)
		{
			if (gamepad == null)
			{
				return;
			}
			float num = gamepad.rightStick.ReadValue().y * 720f * Time.deltaTime;
			if (!(Mathf.Abs(num) > 0.01f))
			{
				return;
			}
			int num2 = Mathf.RoundToInt(num);
			switch (Platform)
			{
			case RuntimePlatform.WindowsPlayer:
			case RuntimePlatform.WindowsEditor:
				mouse_event(2048u, 0u, 0u, (uint)num2, UIntPtr.Zero);
				break;
			case RuntimePlatform.OSXEditor:
			case RuntimePlatform.OSXPlayer:
			{
				IntPtr cgEvent = CGEventCreateScrollWheelEvent(IntPtr.Zero, 0u, 1u, num2 / 120);
				CGEventPost(0u, cgEvent);
				break;
			}
			case RuntimePlatform.LinuxPlayer:
			case RuntimePlatform.LinuxEditor:
				InitializeLinuxDisplay();
				if (LinuxDisplay != IntPtr.Zero)
				{
					uint button = ((num2 > 0) ? 4u : 5u);
					XTestFakeButtonEvent(LinuxDisplay, button, is_press: true, 0uL);
					XTestFakeButtonEvent(LinuxDisplay, button, is_press: false, 0uL);
					XFlush(LinuxDisplay);
				}
				break;
			default:
				FallbackScroll(num);
				break;
			}
		}

		private static void HandleDragAndDrop(Gamepad gamepad)
		{
			if (Settings != null)
			{
				bool buttonState = GetButtonState(gamepad, Settings.Buttons.Select);
				if (buttonState && !IsDragging)
				{
					StartDrag();
				}
				else if (!buttonState && IsDragging)
				{
					EndDrag();
				}
			}
		}

		private static void StartDrag()
		{
			switch (Platform)
			{
			case RuntimePlatform.WindowsPlayer:
			case RuntimePlatform.WindowsEditor:
				mouse_event(2u, 0u, 0u, 0u, UIntPtr.Zero);
				break;
			case RuntimePlatform.OSXEditor:
			case RuntimePlatform.OSXPlayer:
			{
				IntPtr cgEvent = CGEventCreateMouseEvent(IntPtr.Zero, 1u, 0f, 0f, 0u);
				CGEventPost(0u, cgEvent);
				break;
			}
			case RuntimePlatform.LinuxPlayer:
			case RuntimePlatform.LinuxEditor:
				InitializeLinuxDisplay();
				if (LinuxDisplay != IntPtr.Zero)
				{
					XTestFakeButtonEvent(LinuxDisplay, 1u, is_press: true, 0uL);
					XFlush(LinuxDisplay);
				}
				break;
			default:
				FallbackStartDrag();
				break;
			}
			IsDragging = true;
		}

		private static void EndDrag()
		{
			switch (Platform)
			{
			case RuntimePlatform.WindowsPlayer:
			case RuntimePlatform.WindowsEditor:
				mouse_event(4u, 0u, 0u, 0u, UIntPtr.Zero);
				break;
			case RuntimePlatform.OSXEditor:
			case RuntimePlatform.OSXPlayer:
			{
				IntPtr cgEvent = CGEventCreateMouseEvent(IntPtr.Zero, 2u, 0f, 0f, 0u);
				CGEventPost(0u, cgEvent);
				break;
			}
			case RuntimePlatform.LinuxPlayer:
			case RuntimePlatform.LinuxEditor:
				if (LinuxDisplay != IntPtr.Zero)
				{
					XTestFakeButtonEvent(LinuxDisplay, 1u, is_press: false, 0uL);
					XFlush(LinuxDisplay);
				}
				break;
			default:
				FallbackEndDrag();
				break;
			}
			IsDragging = false;
		}

		private static void FallbackStartDrag()
		{
			Mouse current = Mouse.current;
			if (current != null)
			{
				Vector2 position = current.position.ReadValue();
				MouseState mouseState = new MouseState
				{
					position = position
				};
				InputSystem.QueueStateEvent(current, mouseState.WithButton(MouseButton.Left));
				InputSystem.Update();
			}
		}

		private static void FallbackEndDrag()
		{
			Mouse current = Mouse.current;
			if (current != null)
			{
				Vector2 position = current.position.ReadValue();
				MouseState mouseState = new MouseState
				{
					position = position
				};
				InputSystem.QueueStateEvent(current, mouseState.WithButton(MouseButton.Left, state: false));
				InputSystem.Update();
			}
		}

		private static void FallbackScroll(float scrollAmount)
		{
			Mouse current = Mouse.current;
			if (current != null)
			{
				Vector2 position = current.position.ReadValue();
				Vector2 scroll = new Vector2(0f, scrollAmount);
				InputSystem.QueueStateEvent(current, new MouseState
				{
					position = position,
					scroll = scroll
				});
				InputSystem.Update();
			}
		}
	}
}
