# DuckovPad — Full Controller Support for Escape from Duckov

A gamepad mod for Escape from Duckov that restores controller-first gameplay on top of the game’s Unity Input System.

The base game ships with Unity Input System support, but it has almost no usable gamepad bindings. DuckovPad patches in a complete pad layer: aiming, lock-on, native controller settings, menu navigation, and button prompts while preserving the game’s normal rules and input flow.

## Why this mod exists

- The game includes a Unity input layer, but almost no gamepad bindings are actually configured.
- `Duckov Controls` contains mostly keyboard/mouse paths.
- The joystick aim path in `InputManager` is effectively dead code.
- DuckovPad fills that gap by driving the same gameplay entry points the keyboard path uses.

## What it does

### Aim and combat

- Screen-space directional aiming with a stable crosshair radius
- No slow corner drift from world-angle aiming
- Split aim assist with friction and a capped pull effect
- Lock-on targeting with smooth engagement and release
- Recoil that accumulates over bursts and settles naturally
- Weapon and aim behavior remain tied to the game’s own systems

### Controller support

- Xbox, PlayStation, Switch, Steam Deck, and Steam Input detection
- In-game Controller tab with settings + remapping
- Modifier layer using LB for secondary actions
- Button prompt HUD and contextual help
- Rumble support
- Mouse and keyboard remain usable alongside controller input

> Current support note: PlayStation controller support is still buggy and incomplete right now. It may work for some basic mappings, but full support is not reliable yet and is expected to improve over time.
>
> macOS: version 1.4.1 addresses rapid controller/mouse switching, app focus changes, and reconnect handling. See [Mac setup and testing](dist/DuckovPad/MacSetup.md). Hardware verification is still required.

### Menus and inventory

- Controller navigation through inventory, equipment, and hotbar
- D-pad and stick navigation with repeat behavior and automatic scrolling to the next row
- NPC dialogue choices and A-to-continue text; quest Submit buttons and reward/confirmation popup focus
- Quick item actions, placing, swapping, and confirm flows
- Precision cursor support when needed
- Native UI styling that matches the game’s own options pages

---

## Install

> Mac users: follow [Mac setup](dist/DuckovPad/MacSetup.md); the Windows installation path below does not apply. PlayStation hardware support remains incomplete.

The mod is already set up for a local install at:

```text
E:\SteamLibrary\steamapps\common\Escape from Duckov\Duckov_Data\Mods\DuckovPad\
```

To enable it:

1. Open the game
2. Go to Options → Mods
3. Accept the modding agreement if prompted
4. Turn on DuckovPad

Important:

- Keep the Workshop mod EFD.Controller ("Controller Support") disabled while using DuckovPad.
- Both mods replace the same input handler.
- To install elsewhere, copy the contents of `dist/DuckovPad` into that game’s `Duckov_Data/Mods/` directory.

---

## Default layout (Xbox names)

### On foot

| Control | Action |
| --- | --- |
| Left stick | Move; push fully to sprint |
| Right stick | Aim |
| RT | Fire |
| LT | Aim down sights |
| L3 | Sprint |
| R3 | Lock on / release lock |
| A | Interact |
| B | Dash |
| X | Reload |
| Y | Cycle weapon slots 1 → 2 → 3 (primary, secondary, melee) |
| RB | Character skill (hold to aim, release to use) |
| LB (tap) | Cycle weapon slots 1 → 2 → 3 (hold LB for the layer below) |
| D-pad ↑ / ↓ | Mouse-wheel equivalent for ammo / interaction target / weapon scroll |
| D-pad ← / → | Step back / forward through quick-use item slots 3–8, using the slot you land on (wraps) |
| Start | Inventory / stash |
| Select | Map |

### Hold LB for the second layer

The prompt bar displays these actions while LB is held, so there is no long memorization burden.

| Control | Action |
| --- | --- |
| LB + D-pad ↑ → ↓ ← | Quick-use item slots 3, 4, 5, 6 directly (slots 7 and 8 are reached with D-pad ← / →) |
| LB + A | Put away |
| LB + X | Stop action |
| LB + Y | Melee weapon (slot 3) directly |
| LB + B | Quack |
| LB + L3 | Night vision |
| LB + R3 | Toggle camera view |
| LB + Start | Pause menu |
| LB + Select | Quest log |

### In menus and inventory

| Control | Action |
| --- | --- |
| Left stick / D-pad | Move selection; hold to repeat |
| Right stick | Scroll |
| A | Select / confirm; place, stack, swap, equip, or assign to hotbar |
| X | Item actions |
| Y | Quick move / take |
| B | Close popup, cancel selection, or go back |
| LT / RT | Previous / next stash page (stash only; wraps through available pages) |
| L3 | Hold for visible precision cursor |
| LB / RB | Previous / next main menu tab (backpack, stats, quests, map, keys, formulas) |
| LB | Rotate in build mode |
| Start | Close |

Notes:

- Solo inventory opens on the first bag slot, including empty slots. Bodies, containers, and stash open on the first slot of the other inventory.
- Menu and gameplay bindings are independent. Release held buttons after leaving a menu before using them for gameplay.
- Stash page bindings are remappable under In menus and do nothing on bodies or player records.
- Grab/place/swap and quick move work from the focused slot while the tooltip is open.
- `UiUse`, `UiDrop`, and `UiMark` in `Settings.json` can bind direct controller actions to use, drop, and wishlist-mark an item without hover requirements.
- Any pad button can pass the boot title screen and loading curtain, not just A / Start.
- On the native options slider, left/right adjusts the value and up/down moves to another control.

---

## Configuration

The main settings and bindings live under Options → Controller, grouped into expandable sections.

### Save behavior

- `Settings.json` next to the mod DLL holds defaults and advanced file-only settings.
- In-game settings override `Settings.json` values.
- Changes made in-game are written back to `Settings.json` so the two stay in sync.
- Delete the file to regenerate defaults.

### Binding syntax

Button names support:

- Xbox names such as `A`, `B`, `X`, `Y`, `LB`, `RB`, `LT`, `RT`, `L3`, `R3`, `Start`, `Select`, `DpadUp`
- PlayStation names such as `Cross`, `Circle`, `Square`, `Triangle`, `L1`, `R1`
- Unity names such as `buttonSouth`, `leftShoulder`, `dpad/up`

Chords are combined with `+`, for example:

- `LB+DpadUp`
- `LB+RB+Start`

A more specific chord wins over a less specific one. Set a binding to `""` to unbind it.

### File-only settings

| Key | Default | Purpose |
| --- | --- | --- |
| `Aim.OuterDeadzone` | `0.92` | Stick deflection counted as fully pushed |
| `Aim.ResponseCurve` | `1.35` | More of the stick’s travel becomes fine control |
| `Aim.AdsReachMultiplier` | `1.15` | Crosshair distance multiplier while aiming down sights |
| `AimAssist.RequireLineOfSight` | `true` | Ignore targets behind walls |
| `AimAssist.StickinessDegrees` | `8` | Extra tolerance for already-tracked targets |
| `AimAssist.FlickDegreesPerSecond` | `420` | Assist steps aside on deliberate fast sweeps |
| `AimAssist.ReleaseDegreesPerSecond` | `110` | Pull unwinds speed when target leaves the wedge |
| `AimSnap.BreakAngleDegrees` | `75` | Stick angle away from target that breaks lock |
| `AimSnap.ReleaseTime` | `0.2` | Time for the crosshair to hand back control |
| `AimSnap.OutlineColor` | `#FFFFFF` | Lock-on outline color |
| `AimSnap.OutlineWidth` | `1.0` | Outline thickness relative to the game |
| `Cursor.ScrollUnitsPerNotch` | `120` | Adjust menu scroll speed |
| `UiSnap.SnapConeDegrees` | `65` | D-pad snap cone width |
| `Hints.PlayStationLabels` | `false` | Use L1/R1/L2/R2 labels instead of LB/RB/LT/RT |

---

## How it works

DuckovPad patches the game’s input flow instead of replacing it. The mod hooks into `CharacterInputControl.Update`, then feeds the game’s own `InputManager` entry points such as:

- `SetMoveInput`
- `SetTrigger`
- `SetAdsInput`
- `Dash`
- `Interact`

That preserves normal gameplay behavior like pause handling, death state, input blocking, skill state, and UI restrictions.

### Aiming model

The game stores a virtual mouse aim point. DuckovPad writes that directly from the stick direction, then calls `SetAimInputUsingMouse(Vector2.zero)` so the vanilla recoil and target logic still runs normally.

This is important because it:

- keeps crosshair behavior aligned with the game’s systems
- avoids drift from fake mouse deltas
- lets recoil and target checks operate naturally

The mod reasons about aim in screen space rather than world angle. That means the crosshair sweeps at a consistent speed in every direction and avoids the corners/edge slowdown that comes from world-space reticles.

### Aim assist and lock-on

DuckovPad separates the assist system into two parts:

- Friction: slows turn rate while near a target without moving aim on the player
- Pull: adds a bounded offset to keep the crosshair on a moving enemy

Lock-on is smooth and deliberate:

- target mark appears
- crosshair travels onto the locked target
- release is eased back to player control
- stick lead follows moving enemies
- a hard flick can switch targets instead of dropping the lock instantly

### UI and controller integration

The Controller tab reuses the game’s native option row types, dropdowns, sliders, hover states, and spacing. The menu system keeps a hidden pointer for hover and tooltip logic while allowing D-pad/stick navigation between visible UI elements.

---

## Troubleshooting and tuning

Turn on Options → Controller → Troubleshooting → Diagnostic overlay to inspect:

- whether a pad is detected
- whether the pad or mouse has focus
- whether options are installed
- hostiles in range
- selected assist target
- assist angle and applied friction/pull

### Common tuning fixes

- Crosshair swings too far on a light touch: raise fine control or the right-stick deadzone
- Aim feels laggy or mushy: raise aim speed or max sweep speed; lower fine control if mid-stick is the issue
- Assist feels like it is playing for you: lower pull strength and keep friction high
- Assist feels too weak: raise pull and assist cone first
- Lock-on snaps too hard: raise the time to settle onto the target

---

## Development notes

### Build

On macOS, `bash scripts/build-macos.sh` builds and checks against the default Steam installation. Set `GAME_MANAGED` for another library location.

```bash
dotnet build src/DuckovPad/DuckovPad.csproj -c Release
```

If the game is not in the default Steam path:

```bash
dotnet build src/DuckovPad/DuckovPad.csproj -c Release -p:GameManaged="D:\Games\Escape from Duckov\Duckov_Data\Managed"
```

Then copy the built DLL into the mod install folder.

### Validation checks

```bash
dotnet run --project tests/NavigationChecks/NavigationChecks.csproj -c Release
```

```bash
dotnet run --project tests/ControllerChecks/ControllerChecks.csproj -c Release
```

### Project layout

```text
src/DuckovPad/
  ModBehaviour.cs        entry point, Harmony patches, device arbitration, diagnostics
  Pad.cs                 gamepad abstraction, aliases, chords, deadzones
  AimDriver.cs           aiming, assist, recoil, lock-on
  AimMath.cs             angular smoothing and response calculations
  ControllerDevice.cs    device detection and Steam Input identification
  ControllerProfile.cs   family classification and button labels
  TargetFinder.cs        hostile target search
  GameplayDriver.cs      gameplay inputs
  UiDriver.cs            menu/input focus flow
  UiSnap.cs              UI target discovery and snap behavior
  UiNavigation.cs        directional selection and repeat logic
  UiActions.cs           inventory/UI confirm actions
  HintHud.cs             prompt bar and display UI
  LockOutline.cs         lock-on outline rendering
  NativeOptionsUi.cs     native controller settings page
  PadSettingsLayout.cs   layout and option rows
  PadOptions.cs          option mapping and persistence
  PadBindings.cs         mapping system and chord logic
  PadBindingRow.cs       remapping row UI
  PadGui.cs              glyphs and textures
  Rumble.cs              haptics
  PadConfig.cs           Settings.json model and defaults
  TitleGate.cs           boot screen handling
  LoadingContinue.cs     continue/loading gate handling
  NativeItemPrompts.cs   item tooltip prompts
  Log.cs                 logging helper
  ViewUtil.cs            view helpers

lib/
  0Harmony.dll           patching library

research/
  Decompiled game sources and reverse-engineering notes

tests/
  NavigationChecks/
  ControllerChecks/
```

---

## Known limitations

- UI and binding validation needs live in-game testing.
- PlayStation controller support is still buggy and incomplete right now. Full support is not reliable yet and is expected to improve over time.
- macOS fixes are compiled against the Mac game and regression checked; live Xbox Bluetooth/USB input, cursor handoff, and rumble still require verification.
- Steam Deck gyro, trackpad, rear buttons, touch, rumble, and reconnect behavior need hardware verification.
- The game’s native interaction prompts still show keyboard keys; DuckovPad adds a controller prompt bar alongside them.
- Text entry still requires a keyboard.
- A game update that renames `InputManager._aimMousePosCache` or `CharacterInputControl.Update` would break core aim logic.

---

## Publishing notes

`info.ini` deliberately has no `publishedFileId`. Use the game’s in-game mod uploader to publish the mod; the game writes the ID back into `info.ini` after the first upload.

`preview.png` is included as the workshop thumbnail.
