# DuckovPad on macOS

## 1.4.3: item-action selection

Use / Drop / Mark opens on the first enabled action, with native button selection
matching the controller outline. A calls the highlighted button directly. If a
menu rebuild changes selection on the confirmation frame, that press does nothing
instead of activating a different action. A and a direction pressed together
confirm the existing selection. Disabled actions cannot be confirmed.

## 1.4.2: scrolling and quest/dialogue navigation

- Keep pressing or holding down/up to select rows beyond the visible shop or stash
  page. The selected row scrolls fully into view and keeps its column.
- Quest task descriptions are skipped in favor of the real Submit buttons.
- A advances NPC text; use the stick/D-pad to choose replies and A to select.
- Confirmation and quest reward popups keep focus inside their own controls.
- The right stick still supports manual scrolling. Bumpers and stash page triggers
  retain their existing roles outside dialogs.

Check a long stash/shop list in both directions, submit an eligible quest item,
claim rewards, and test an NPC conversation with multiple replies in game.

## 1.4.1: controller/mouse switching fix

On Mac, active stick input and the 250 ms after controller activity keep control of
the cursor. A delayed or rejected cursor warp no longer counts as mouse activity
by itself. Simultaneous mouse clicks from Steam Input also cannot interrupt active
controller input. Rest the sticks and move/click the mouse to switch back.
Gameplay keyboard input can still take over immediately.

This build also resets controller UI and rumble when switching applications,
handles disabled/re-enabled controllers, and improves device logging.
These changes are compiled against the Mac game assemblies and regression checked;
Bluetooth/USB hardware behavior still needs in-game verification.

## Install or update

Quit Duckov before replacing the mod. Keep EFD.Controller disabled.

For an existing installation, back up its `DuckovPad.dll` and replace that file with
the DLL from this folder. Keep your existing `Settings.json`. Steam may overwrite
a local Workshop DLL modification when it updates the subscribed mod.

For a new local installation, copy this entire `DuckovPad` folder into:

```text
~/Library/Application Support/Steam/steamapps/common/Escape from Duckov/Duckov.app/Contents/Mods/
```

Create `Mods` if needed, then enable DuckovPad in Options → Mods. Use only one
DuckovPad installation. The game derives this folder from `Application.dataPath`,
which is the app bundle's `Contents` on Mac—not its `Resources/Data` folder.
See [Unity's path documentation](https://docs.unity.cn/6000.7/Documentation/ScriptReference/Application-dataPath.html).

## Check the fix

1. Move and sweep the aim stick continuously. The diagnostic overlay should stay
   on `control: pad`, and the crosshair should move smoothly.
2. Test D-pad navigation, scrolling, selecting and dragging inventory items.
3. Rest the controller for a moment, then move the mouse; mouse control should return.
4. Switch to another app and back, then disconnect/reconnect the controller.

Turn on Options → Controller → Troubleshooting → Diagnostic overlay for input
details. F6 (or Fn-F6 with media-key mode) writes a device report into:

```text
~/Library/Logs/TeamSoda/Duckov/Player.log
```

If it reports no gamepad, the game is not receiving a recognized controller;
the switching fix cannot supply a device that macOS/Unity has not exposed.
Rumble availability depends on the controller's Mac input backend.

## Build from source

With the .NET 10 SDK installed, run `bash scripts/build-macos.sh` from the repo.
For a different Steam library, set `GAME_MANAGED` to the app's
`Contents/Resources/Data/Managed` directory. The script builds, runs both check
suites, and refreshes the distributable DLL without changing installed mods.
