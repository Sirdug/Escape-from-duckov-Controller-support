# DuckovPad Agent Guide

This repository contains DuckovPad, a controller mod for Escape from Duckov. It is the project-level reference for future AI agents and contributors who need to understand what the mod is doing, how it is structured, and where to change things safely.

## Project summary

- Game: Escape from Duckov
- Steam app ID: 3167020
- Build target mentioned in the README: 2.3.30
- Mod goal: add complete gamerpad support and menu/controller navigation on top of the game’s Unity Input System, which otherwise has almost no gamepad bindings.
- Core idea: patch the game’s input flow rather than replacing it wholesale. DuckovPad drives the same vanilla input entry points the keyboard path uses, so gameplay rules, pause logic, skill state, and input-blocking behavior remain intact.

## What the mod does

From the README:

- Easy directional aiming with a screen-space aim model
- Right-stick aiming with a consistent sweep speed in all directions
- Aim assist split into friction and pull, without forcing aim on the player
- Weapon recoil that accumulates through a burst rather than being wiped each frame by the absolute aim point
- Controller detection and labeling for Xbox, PlayStation, Switch, Steam Deck, and Steam Input virtual pads
- Lock-on aiming with smooth engage/release behavior and target switching when flicking through the break angle
- Native in-game options + button mapping UI under a Controller tab
- Console-style menu navigation using stick/D-pad and item actions in inventory/UI
- Contextual prompt bar and modifier layer prompts
- Rumble support and keyboard/mouse coexistence
- Toggle via F5

## High-level design

The mod works by hooking into the game’s character input update and intercepting the relevant gamepad logic. It does not pretend to be a full replacement input system; instead it feeds the game’s own `InputManager` API the same way the keyboard controls do.

The README explains the key architecture principle:

- The mod prefixes `CharacterInputControl.Update`
- When the pad has focus and the player is in gameplay, DuckovPad takes over the frame
- It drives the game’s own input methods such as `SetMoveInput`, `SetTrigger`, `SetAdsInput`, `Dash`, `Interact`, and related paths
- This preserves normal gameplay rules, pause state, input blocking, death handling, and skill behavior

Aiming is implemented in screen space, not world space. The mod tracks the aim as a circle around the duck measured in pixels, which avoids the old distortion from world-angle math and removes the slow/shortened sweep near screen edges.

## Core technical concepts

### Aim model

The README calls out a number of important implementation details:

- The game stores a virtual mouse position (`InputManager._aimMousePosCache`)
- DuckovPad writes that field directly from the stick direction instead of faking mouse deltas
- It then calls `SetAimInputUsingMouse(Vector2.zero)` so the vanilla recoil and target evaluation logic still runs
- The crosshair follows `InputManager.AimScreenPoint`
- Aim is reasoned about in screen space with a radius measured in pixels and a direction around the duck
- This avoids inconsistent speed based on camera angle and awkward world-to-screen reticle distortion

### Aim assist

There are two separate assist halves:

- Friction: slows turn rate while the crosshair is near an enemy. This helps lock onto targets without moving the aim for the player
- Pull: applies a small tracking offset while the target remains in the assist wedge; capped to about 10 degrees so it never feels like the mod is aiming for the player

The assist respects deliberate flick input and steps aside when the stick sweeps too fast. It ranks targets by screen position rather than world angle, which is a better match for the player’s actual view.

### Lock-on

Lock-on is a deliberate action that uses a target marker and outline overlay. It is not instant or teleporting:

- The crosshair travels onto the target over about 0.13s
- It releases smoothly when the lock ends
- Stick pressure can lead moving targets
- A hard flick past the break angle can switch targets instead of simply dropping the lock
- A target marker is shown, and an outline can be displayed using the game’s existing outline pipeline

### UI and menu support

DuckovPad includes a custom UI layer for menus and inventory:

- persistent highlight navigation using left stick and D-pad
- hold to repeat movement
- hidden pointer during stick navigation
- item actions and quick move support in inventory UI
- snap/cursor magnetism toward interactive elements
- native options pages that mimic the game’s existing controls and styling
- contextual button prompt bar

## Configuration model

The main config object is `PadConfig`, which contains the mod’s runtime and file-based settings:

- The mod loads `Settings.json` next to the DLL
- In-game options override the file values
- The file is used as the default source and for advanced knobs that are intentionally not exposed in-game
- Changes made in-game are written back to the file so they stay in sync
- The config includes sections for aim, assist, snap, UI snap, hints, rumble, debug, and button bindings

Key config categories in `PadConfig`:

- `Aim`: deadzone, response curve, turn response, reach, recoil preservation, mixed pointer mode, relative mode
- `AimAssist`: friction, magnet strength, max angle/distance, line-of-sight checks, stickiness, flick thresholds, release rate
- `AimSnap`: lock-on mode, break angle, switch-on-flick, engage/release timing, lead, marker/outline toggles
- `UiSnap`: stick navigation, cursor hiding, directional snap, magnetism, highlight, snap cone, rescan timing
- `Hints`: playstation label toggle
- `Buttons`: binds for all controller actions, including chords like `LB + Y`

Important configuration facts from the README:

- Defaults live in `Settings.json`; delete it to regenerate defaults
- Button names may use Xbox, PlayStation, or raw Unity control names
- Chords are written with `+` (for example `LB+DpadUp`, `LB+RB+Start`)
- A more specific chord wins over a less specific one
- Use `""` to unbind a binding
- The shared `Modifier` button is file-only because naming it literally would break the `LB` layer

## Default control layout

The README provides the default control map for Xbox naming.

### On foot

- Left stick: move / sprint
- Right stick: aim
- RT: fire
- LT: aim down sights
- L3: sprint
- R3: lock on / release lock
- A: interact
- B: dash
- X: reload
- Y: swap primary / secondary
- RB: character skill
- D-pad up/down: ammo / interaction target / weapon scroll equivalent
- D-pad left/right: cycle weapon slots
- Start: inventory / stash
- Select: map

### Modifier layer (hold LB)

- LB + D-pad up/down/left/right: quick-use item slots 3-6
- LB + A: put away
- LB + X: stop action
- LB + Y: melee weapon
- LB + B: quack
- LB + L3: night vision
- LB + R3: toggle camera view
- LB + Start: pause menu
- LB + Select: quest log

### Menu and inventory

- left stick / D-pad: select next UI element; hold to repeat
- right stick: scroll
- A: select / confirm
- X: item actions
- Y: quick move / take
- B: cancel / go back
- LT: hold for visible precision cursor
- LB / RB: previous / next page or bottom-tab view (menus only; slots via D-pad / stick)
- LB: rotate in build mode
- Start: close

## File and folder guide

### Top-level structure

- `README.md`: project overview and gameplay description
- `src/DuckovPad/`: main mod implementation
- `tests/`: controller and navigation validation programs
- `lib/`: dependencies and runtime support files
- `research/`: decompiled game code and reverse-engineering notes

### Main source files

- `src/DuckovPad/ModBehaviour.cs`
  - Entry point for the mod
  - Handles setup, patching, scene lifecycle, device changes, and config load
  - Registers a custom assembly resolver for Harmony and bundled dependencies

- `src/DuckovPad/PadConfig.cs`
  - Defines runtime settings and default values
  - Includes all aim, assist, UI, button, and debug settings

- `src/DuckovPad/ControllerDevice.cs`
  - Detects and labels the active controller
  - Uses Steam Input when possible to identify physical controller type behind virtual Xbox pads
  - Formats labels for bindings

- `src/DuckovPad/AimDriver.cs`
  - Main aiming logic and input-to-crosshair transformation

- `src/DuckovPad/GameplayDriver.cs`
  - Drives gameplay actions and input mapping for movement, firing, ADS, interact, and similar actions

- `src/DuckovPad/UiDriver.cs`
  - Covers menu, inventory, and UI navigation behavior

- `src/DuckovPad/UiNavigation.cs`
  - Logic for stuck highlight, item navigation, and D-pad cursor movement

- `src/DuckovPad/NativeOptionsUi.cs`
  - Native in-game settings UI for the Controller tab

- `src/DuckovPad/PadSettingsLayout.cs`
  - Layout and sections used by the settings UI

- `src/DuckovPad/PadBindings.cs`
  - Binding names, chord parsing, and mapping between abstract actions and buttons

- `src/DuckovPad/PadOptions.cs`
  - Option persistence and in-game changes

- `src/DuckovPad/LockOutline.cs`
  - Target lock marker and outline rendering

- `src/DuckovPad/HintHud.cs`
  - Prompt bar HUD and modifier guidance

- `src/DuckovPad/Rumble.cs`
  - Vibration handling

- `src/DuckovPad/TitleGate.cs`
  - Handles title/loading gate input behavior

- `src/DuckovPad/LoadingContinue.cs`
  - Title screen / loading continue behavior

## Build and project setup

The project is a .NET SDK project targeting `netstandard2.1`.

Relevant build facts from `src/DuckovPad/DuckovPad.csproj`:

- Target framework: `netstandard2.1`
- Assembly name: `DuckovPad`
- Version: `1.4.0`
- Uses `0Harmony` and references the game’s managed assemblies from the installed game folder
- The game DLL path defaults to:
  - `E:\SteamLibrary\steamapps\common\Escape from Duckov\Duckov_Data\Managed`
- It supports overriding the managed folder with `-p:GameManaged=...`
- It expects `0Harmony.dll` from `lib/0Harmony.dll` unless overridden

The project is built around the actual game’s managed assemblies, so a local game install is expected for compilation and testing.

## Important implementation patterns for agents

### Prefer the existing input pipeline

Do not bypass or rewrite the game’s own control flow. The mod is designed to feed the same input methods the game expects. Changing the underlying flow can break pause logic, death handling, weapon state, and UI behavior.

### Respect the config split

- `Settings.json` is a default store and advanced settings source
- In-game menu changes override file defaults
- Save behavior is deliberately synchronized to avoid disagreement
- Some settings are intentionally file-only, especially around modifier semantics and advanced tuning

### Keep controller detection honest

The detection logic in `ControllerDevice.cs` deliberately avoids guessing when a virtual Xbox pad cannot be matched reliably to a physical controller. This is important for avoiding false “Deck” labels or wrong type identification.

### Preserve performance and UI continuity

- the mod uses the game’s native UI styling and row prefabs
- lock-on and other visual features draw through the game’s existing rendering pipeline instead of inventing a custom UI system
- prompt HUD and input overlay are context-driven and should remain consistent with native controls

## Diagnostics and troubleshooting

The README outlines the mod’s debugging overlay and the most practical fix workflow.

### Diagnostic overlay

In game, under `Options -> Controller -> Troubleshooting -> Diagnostic overlay`, the mod can report:

- whether a pad is seen
- whether the pad or mouse currently owns focus
- whether the options tab installed
- how many hostiles are in range
- which target aim assist picked
- target angle and distance
- the friction and pull currently applied

This is the first step when aim assist or input issues appear.

### Known issues

- **Bumper slot-cycling can skip some gun slots.** `UiSnap.TryCycleSlot` (`src/DuckovPad/UiSnap.cs`) recognises `InventoryEntry`/`SlotDisplay`/`ItemShortcutEditorEntry`/`WeaponButton` plus raw `ItemDisplay` holders, but some equipment gun UI still isn't classified, so LB/RB can skip guns while the D-pad reaches them. The overlay's `ui focus` line reports `name [bag|equip|shortcut|gun|item|other]` plus the slot count, and the `bumper` line reports the last bumper action (`slot n/m kind`, `no slots in view`, `end of slots, turning page`, `RB/LB: page next/prev`) — collect those before changing the filter again. Workaround: D-pad.

### Common aim tuning advice from the README

- Crosshair swings too far on a light touch: raise fine control at part-stick or adjust right-stick deadzone
- Aim feels laggy or mushy: increase aim speed or maximum sweep speed; lower fine control at part-stick if mid-stick is the issue
- Assist feels like it is playing for you: lower “pull” strength and leave friction high
- Assist is too weak: raise pull and assist cone first
- Lock-on snaps too hard: raise time to settle onto the target

## Known project context and history

The README includes a version note for 1.4.0:

- Rebuild of aim feel on a screen-space model
- Crosshair sweeps at one speed in every direction
- Turn rate scales with stick deflection instead of global smoothing lag
- Aim assist split into friction and capped pull
- Eased lock-on with stick lead and target cycling
- Recoil is preserved rather than overwritten by the absolute aim write
- Existing custom bindings and saved options are retained, except the old `Aim speed` value, which was repurposed and reset to the new default
- The game should be reopened after installing an updated DLL

This tells agents that seemingly simple changes to aim behavior may affect saved user config compatibility and must be handled carefully.

## Safe change strategy for agents

When working in this repo, the safest approach is:

1. Start with the README and the config model in `PadConfig.cs`
2. Trace the relevant behavior through `ModBehaviour`, the driver classes, and the specific patch or action handler
3. Keep input routing aligned with the game’s own `InputManager` API
4. Preserve the default JSON config, in-game override flow, and bind naming conventions
5. Prefer updating existing config sections rather than inventing new structures
6. Validate by checking the code path and the game’s expected behavior, especially around aim, lock-on, UI navigation, and menu state

## Useful search anchors

If you need to locate functionality quickly, search for these symbols and files:

- `ModBehaviour`
- `PadConfig`
- `AimDriver`
- `GameplayDriver`
- `UiDriver`
- `UiNavigation`
- `ControllerDevice`
- `PadBindings`
- `PadOptions`
- `LockOutline`
- `HintHud`
- `ToggleKey`
- `Settings.json`
- `InputManager._aimMousePosCache`
- `SetAimInputUsingMouse`
- `CharacterInputControl.Update`

## Summary for AI agents

DuckovPad is a mod that overlays a full controller input layer onto Escape from Duckov without breaking the game’s internal input logic. The project is primarily centered around:

- aim and assist tuning
- controller/device detection
- UI navigation and native settings integration
- bind mapping and options persistence
- safe injection into the game through Harmony patches and the game’s own input pipeline

Any agent working here should treat this as a gameplay input integration project, not a generic UI or gamepad library. The most relevant files are in `src/DuckovPad`, and the design intent is explicitly described in the README and encoded in `PadConfig` and `ModBehaviour`.
