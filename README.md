# DuckovPad — Full Controller Support for Escape from Duckov

A gamepad mod for **Escape from Duckov** (Steam app `3167020`, game build `2.3.30`).

The game ships with Unity's Input System but **no gamepad bindings at all** — its action asset
(`Duckov Controls`) contains only `<Keyboard>` and `<Mouse>` paths, and the joystick aim path
that exists in `InputManager` is dead code. DuckovPad adds a complete pad layer on top.

---

## What it does

**Easy directional aiming.** Point the right stick toward the target as you see it on screen.
The crosshair rides a circle around the duck measured in *screen* pixels, so it sweeps at the
same speed in every direction — no slow corners, and it never has its reach cut short to stay
on screen. Full deflection still means "point there"; easing off the stick slows the turn rather
than shortening it, which is where fine adjustment comes from. Releasing the stick keeps the
last direction. The old twin-stick and free-cursor styles remain available in the Controller tab.

**Aim assist you can't feel aiming for you.** Two separate halves. *Friction* quietly slows the
turn rate while the crosshair is near an enemy, so targets are easy to stop on without the aim
ever being moved on your behalf. *Pull* adds a small tracking offset on top — capped at about
ten degrees and unwound the moment the target leaves the wedge — enough to hold a strafing
enemy, never enough to point somewhere you didn't. Both step aside during a deliberate flick, so
sweeping across a crowded room doesn't drag. Targets are ranked by where they sit **on screen**
rather than by world angle, which is what makes the help land where you're actually looking.

**Weapon recoil you can see.** The kick the game applies to a mouse player now accumulates over
a burst and settles back afterwards, instead of being wiped out each frame by the absolute aim
point. Turn it off in the Controller tab if you prefer the crosshair pinned to the stick.

**Dynamic controller detection.** The Controller tab displays the active device and how it was
identified. Xbox, PlayStation, Switch and Steam Deck labels update when you use a different
controller. Steam Input identifies a physical controller behind a virtual Xbox pad when the
connection can be matched unambiguously; ambiguous connections are reported rather than guessed.

**Steam Deck through Steam Input.** Sticks, triggers, D-pad and face buttons use gamepad input.
Trackpad and gyro mouse aiming can work alongside controller movement and triggers. All four
rear buttons can be separately mapped through reserved F7–F10 outputs. See the included
[Steam Deck setup guide](dist/DuckovPad/SteamDeckSetup.md) for the required Steam layout.

**Lock-on.** Click **R3** to lock aim onto an enemy; a marker appears over the target and aim
tracks it until you press again, push the stick hard away, or the target dies. The crosshair
*travels* onto the target over about an eighth of a second and hands control back just as
smoothly, rather than teleporting either way. Light stick pressure leads the target for movers,
and a hard flick past the break angle picks the next enemy that way instead of simply dropping
the lock. The locked enemy is drawn with a white outline that fades in and out with the lock, so
you can see who has your aim without reading the crosshair. Optionally it can also snap to the
nearest enemy the moment you pull the trigger.

**Settings inside the game.** One **Controller** tab contains both preferences and button
mapping. Expand a section to see its controls; Aiming starts open. Rows, sliders, switches,
dropdowns, fonts and hover effects come from the game's own settings. Adjust sliders with left/right,
select switches to turn them on or off, and use the right stick to scroll. Under Button mapping,
select a binding and press the new button or chord (hold LB and tap Y for `LB+Y`). B cancels,
Delete clears, and Restore button defaults contains the reset action. Changes save automatically.

**Console-style menu navigation.** The left stick and D-pad move a persistent highlight between
buttons, equipment, inventory slots and hotbar slots. Release the stick and the highlight stays
put; hold a direction to repeat. The pointer is hidden during this navigation. A selects an item,
then A on a destination places it using the game's own stacking, swapping and equipment rules.
X opens item actions, Y quick-moves items, and B cancels a selection or goes back. LT temporarily
enables a visible precision cursor for controls that need one.

**Button prompts.** A contextual prompt bar along the bottom shows what each button does right
now — and holding **LB** turns it into a live cheat-sheet for the whole modifier layer.

**Rumble**, and **keyboard/mouse keep working**. Ordinary controllers use last-input focus;
Deck/Steam Controller precision mode allows mouse movement alongside pad input. **F5** toggles
the pad off or on.

---

## Install

Already installed to your local game folder at:

```
E:\SteamLibrary\steamapps\common\Escape from Duckov\Duckov_Data\Mods\DuckovPad\
```

To enable: in game, **Options → Mods**, accept the modding agreement if prompted, then switch
**DuckovPad** on.

Keep the Workshop mod **EFD.Controller** ("Controller Support") disabled while using DuckovPad;
both replace the same input handler.

To install elsewhere, copy `dist/DuckovPad` into that game's `Duckov_Data/Mods/` directory.

---

## Default layout (Xbox names)

### On foot

| Control | Action |
| --- | --- |
| Left stick | Move (push fully to sprint) |
| Right stick | Aim |
| RT | Fire |
| LT | Aim down sights |
| L3 | Sprint |
| **R3** | **Lock on / release lock** |
| A | Interact |
| B | Dash |
| X | Reload |
| Y | Swap primary / secondary |
| RB | Character skill (hold to aim, release to use) |
| D-pad ↑ / ↓ | Mouse-wheel equivalent (ammo & interaction target, or weapon — follows your in-game Scroll Wheel Behaviour setting) |
| D-pad ← / → | Cycle weapon slots 1 → 2 → 3 |
| Start | Inventory / stash |
| Select | Map |

### Hold LB for the second layer

The prompt bar shows these while LB is held, so there's nothing to memorise.

| Control | Action |
| --- | --- |
| LB + D-pad ↑ → ↓ ← | Quick-use item slots 3, 4, 5, 6 |
| LB + A | Put away |
| LB + X | Stop action |
| LB + Y | Melee weapon |
| LB + B | Quack |
| LB + L3 | Night vision |
| LB + R3 | Toggle camera view |
| LB + Start | Pause menu |
| LB + Select | Quest log |

### In menus and inventory

| Control | Action |
| --- | --- |
| Left stick / D-pad | Select the next UI element; hold to repeat |
| Right stick | Scroll |
| A | Select / confirm; select an item, then press A at its destination to place, stack, swap, equip or assign to the hotbar |
| X | Item actions: use, equip, split, drop, etc. |
| Y | Quick move / take (the game's inventory double-click action) |
| B | Close the item popup, cancel item selection, or go back |
| LT | Hold for a visible precision cursor; A can drag in this mode |
| LB / RB | Previous / next page |
| LB | Rotate (in build mode) |
| Start | Close |

Grab / place / swap (A) and quick move (Y) work on the focused slot while the item
tooltip is up — the tooltip no longer blocks its own slot. `UiUse`, `UiDrop` and
`UiMark` in `Settings.json` add direct controller binds for use, drop and wishlist
mark on the focused item (same rules as their keyboard keys, no hover needed); they
are unbound by default and appear in the prompt bar and tooltip once assigned.
Any pad button passes the boot "Click to Continue" title screen and the loading
curtain, not just A / Start.

On a native options slider, left/right adjusts the value and up/down moves to another control.
The Controller tab uses the same row prefabs as the game's General, Audio and Graphics pages,
including their dropdowns, slider styling, spacing, hover states and value fields. Collapsible
headings keep the single page readable while preserving the game's normal options layout. A
heading is set apart from the settings under it: capitals, an accent colour, a `[+]` marker when
it is closed and `[-]` when it is open, and a count of what it holds ("5 settings", "15 buttons")
where a setting would show its value.
Build placement still uses a free pointer. Moving the mouse restores mouse control and its cursor.
Dropping items is inside **X → Drop**, so Y cannot accidentally discard an item.

Version 1.4.0 rebuilds aim feel on a screen-space model: a crosshair that sweeps at one speed in
every direction, a turn rate that scales with stick deflection instead of a global smoothing lag,
aim assist split into friction and a capped pull, eased lock-on with stick lead and target
cycling, and weapon recoil that survives the absolute aim write. Existing custom bindings and
saved options are retained, except the old *Aim speed* value — the setting it used to store meant
something different, so it starts again at the new default. Reopen the game after installing an
updated DLL.

---

## Configuration

Settings and bindings live together under **Options → Controller**, grouped into expandable
sections. Both are saved in the game's own options file and override
`Settings.json`.

`Settings.json` (next to the mod DLL) still holds defaults for everything plus a few
advanced knobs that are intentionally file-only. Edit it and restart the game; delete it to
regenerate defaults. Changes you make in game are written back to it so the two never disagree.
(The shared `Modifier`
button is file-only: chords name it literally, so changing it alone would break the LB layer.)

Button names accept Xbox (`A`, `B`, `X`, `Y`, `LB`, `RB`, `LT`, `RT`, `L3`, `R3`, `Start`,
`Select`, `DpadUp`…), PlayStation (`Cross`, `Circle`, `Square`, `Triangle`, `L1`, `R1`…), or raw
Unity control names (`buttonSouth`, `leftShoulder`, `dpad/up`…). Chain with `+` for chords
(`LB+DpadUp`, `LB+RB+Start`) — a more specific chord always wins over a less specific one. Set a
binding to `""` to unbind it.

### Settings only in the file

| Key | Default | What it does |
| --- | --- | --- |
| `Aim.OuterDeadzone` | `0.92` | Stick deflection counted as fully pushed. |
| `Aim.ResponseCurve` | `1.35` | Higher puts more of the stick's travel into fine control. |
| `Aim.AdsReachMultiplier` | `1.15` | Crosshair distance multiplier while aiming down sights. |
| `AimAssist.RequireLineOfSight` | `true` | Skip targets behind walls. |
| `AimAssist.StickinessDegrees` | `8` | Extra tolerance for the target already tracked. |
| `AimAssist.FlickDegreesPerSecond` | `420` | Sweep faster than this and assist steps aside. |
| `AimAssist.ReleaseDegreesPerSecond` | `110` | How fast the pull unwinds once the target is gone. |
| `AimSnap.BreakAngleDegrees` | `75` | Stick angle away from the target that breaks a lock. |
| `AimSnap.ReleaseTime` | `0.2` | Seconds for the crosshair to hand back control after a lock. |
| `AimSnap.OutlineColor` | `#FFFFFF` | Colour of the lock-on outline. Any HTML colour string. |
| `AimSnap.OutlineWidth` | `1.0` | Outline thickness, 0–1, relative to the game's own outline width. |
| `Cursor.ScrollUnitsPerNotch` | `120` | Lower if menus scroll too fast on your system. |
| `UiSnap.SnapConeDegrees` | `65` | How far off-axis a D-pad snap will reach. |
| `Hints.PlayStationLabels` | `false` | L1/R1/L2/R2 instead of LB/RB/LT/RT on the prompts. |

---

## If something doesn't feel right

Turn on **Options → Controller → Troubleshooting → Diagnostic overlay**. It reports whether
a pad is seen, whether the pad or the mouse currently has control, whether the options tab
installed, how many hostiles are in range, which one aim assist has picked, the angle to it, and
how much friction and pull are being applied. That turns "aim assist isn't working" into a
specific answer.

For aim feel specifically:

- **Crosshair swings too far off a light touch** — raise *Fine control at part-stick*, or raise
  *Right stick deadzone* if it happens at rest.
- **Aim feels laggy or mushy** — raise *Aim speed*, or *Maximum sweep speed* if only big turns
  feel slow. Lower *Fine control at part-stick* if mid-stick is the problem.
- **Assist feels like it's playing for you** — lower *Most the pull may bend aim* toward zero and
  leave *Slow down near enemies* high. That keeps the help without any of the aiming.
- **Assist doesn't help enough** — raise *Pull toward enemies* and *Assist cone* first; strength
  scales both halves at once.
- **Lock-on snaps too hard** — raise *Time to settle onto the target*.

---

## How it works

The mod prefixes `CharacterInputControl.Update`. When the pad has focus and you're in gameplay,
it takes over the frame and drives the game's own `InputManager` entry points — `SetMoveInput`,
`SetTrigger`, `SetAdsInput`, `Dash`, `Interact` and the rest — which is exactly what the keyboard
path does, so gameplay rules (pause, death, input blocking, skill state) still apply.

**Aiming.** The game keeps a virtual mouse position in `InputManager._aimMousePosCache`,
raycasts it onto a ground plane at the duck's height, and hands the result to the character.
Rather than fake mouse deltas — which fight the game's own per-frame cursor warping and produce
drift — DuckovPad writes that field directly from the stick direction, then calls
`SetAimInputUsingMouse(Vector2.zero)` so the untouched vanilla pipeline (recoil, obstacle sweeps,
headshot targeting) runs on top. The game's existing crosshair follows
`InputManager.AimScreenPoint`, so it tracks the pad with no UI work.

Everything above that write is reasoned about in **screen** space — an angle around the duck and
a radius in pixels. `CameraArm` fixes the view at 55° of pitch and −30° of yaw, so a circle drawn
around the duck in the *world* projects to a tilted ellipse: aim built on world angles and world
reticle distances sweeps at a different rate, and pumps the crosshair in and out by different
amounts, depending on which way the player points. Worse, an eleven-metre reach overshoots the
top and bottom of a 16:9 window, so the old code binary-searched a shorter distance whenever the
crosshair neared those edges and then smoothed the result — the crosshair visibly slowed and
shortened as it swept through the vertical. A constant *pixel* radius removes all of it at once:
one speed in every direction, no search, and only a closed-form trim in the rare corner the
fixed radius cannot cover.

Turning follows an exponential settle with a hard degrees-per-second cap. The exponential alone
is what made big sweeps feel uneven — it covers most of a large turn in the first few
milliseconds and then crawls — so the cap turns the opening of every sweep into constant-rate
motion and leaves only the last few degrees to ease in. Below full deflection the whole rate
scales down rather than the reach, which is where fine adjustment comes from.

**Aim assist** is friction plus a bounded pull, kept as two separate things. Friction damps the
turn rate while the crosshair is near a target and never moves the aim, which turns out to do
most of the work of making enemies easy to stay on. The pull accumulates into a single offset
angle capped at a few degrees and unwound as soon as the target leaves the wedge — enough to
track a strafing enemy, structurally incapable of aiming for the player. Both scale back as the
measured sweep rate rises, so a deliberate flick is never fought. (An earlier version slerped
the aim a fixed fraction of the way onto the target instead; at any strength worth having, that
simply took the aim off the player, which is what made it feel cheap.) Targets come from an
overlap sphere on the damage-receiver layer, queried with `QueryTriggerInteraction.Collide` so
trigger-collider receivers aren't skipped, and are then ranked by screen angle rather than world
angle so the help lands where the player is actually looking.

**The lock-on outline** borrows the game's own. Duckov ships Easy Performant Outline and already
drives it from its URP renderer — `HalfObsticle` outlines the cover you stand behind — so the
mod only has to supply an `Outlinable` pointing at the locked enemy's renderers; the outline
material, dilation and blur come from the `Outliner` already sitting on the render camera. That
`Outlinable` lives on a throwaway child object of the target rather than on the target itself,
so nothing the mod does can disturb an outline the game set up, and releasing the lock is a plain
`Destroy`. Two details the filtering imposes: the holder must sit on a layer the camera draws
(it borrows one from a renderer it outlined), and only mesh and skinned-mesh renderers are
eligible, because EPO reads a submesh count straight off the mesh and would throw on anything
without one.

**Recoil** survives the absolute write by being carried explicitly. The game adds one frame of
kick to whatever sits in the aim cache; writing an absolute point every frame would erase it
before the player saw it. The driver reads the cache back after `SetAimInputUsingMouse`, keeps
the difference, and adds it to the next frame's point, so a burst climbs and settles the way it
does for a mouse.

**The Controller tab** reuses the game's scroll page, spacing and actual option prefabs from
General, Audio and Graphics. Native slider and toggle entries retain their controls and receive
the mod's existing storage keys. Choices use the game's dropdown; remapping and section buttons
reuse its widget artwork and hover states. When a slider or toggle template is unavailable,
a native dropdown supplies its values. Collapsible headings keep less-used controls
out of the way; they carry a `[+]` / `[-]` marker and a row count rather than the "Show" /
"Hide" caption they used to, which was indistinguishable from a setting's own value.
The title is reapplied after localization so it cannot revert to Graphics
Settings. Rebuilding removes old controller tabs, including the former separate Controls tab.
Values retain their existing `OptionsManager` keys, so saved preferences and bindings survive.

**Menus** retain an invisible Input System pointer for the game's hover and tooltip handling.
Controller confirmation uses the existing UI click handlers, placement uses the game's drop
handlers, and quick move uses its inventory transfer handler. Only visible, reachable controls
become navigation targets: view backdrops, disabled entries, masked items and controls obscured
by popups are excluded. Occupied and empty inventory slots each provide one navigation stop.
Item action and split popups keep navigation within the popup until it closes.

**Device arbitration** uses the fact that the cursor is warped to a known point every frame we
are in control: if the real cursor diverges from that point, the player moved the mouse, and
focus hands back.

---

## Building from source

```bash
dotnet build src/DuckovPad/DuckovPad.csproj -c Release
```

If your game is installed elsewhere:

```bash
dotnet build src/DuckovPad/DuckovPad.csproj -c Release -p:GameManaged="D:\Games\Escape from Duckov\Duckov_Data\Managed"
```

Then copy `src/DuckovPad/bin/Release/DuckovPad.dll` over `dist/DuckovPad/DuckovPad.dll` and copy
the folder into `Duckov_Data/Mods/`.

Navigation regression checks (requires .NET 10):

```bash
dotnet run --project tests/NavigationChecks/NavigationChecks.csproj -c Release
```

Aiming and controller-profile checks:

```bash
dotnet run --project tests/ControllerChecks/ControllerChecks.csproj -c Release
```

### Layout

```
src/DuckovPad/
  ModBehaviour.cs     entry point, Harmony patches, device arbitration, diagnostics
  Pad.cs              gamepad abstraction: aliases, N-way chords, deadzones
  AimDriver.cs        screen-aligned aiming, mouse precision, aim assist, lock-on
  AimMath.cs          tested deadzone response and angular smoothing
  ControllerDevice.cs active device, Steam Input identification and labels
  ControllerProfile.cs controller-family classification and button names
  TargetFinder.cs     hostile search shared by assist and lock-on
  GameplayDriver.cs   on-foot actions
  UiDriver.cs         controller focus and hidden pointer for menus
  UiSnap.cs           visible target discovery and persistent focus
  UiNavigation.cs     directional input repeat and slot selection geometry
  UiActions.cs        confirm, select/place and quick-move actions
  HintHud.cs          button prompt bar, lock marker, hover outline (runtime uGUI)
  LockOutline.cs      outlines the locked enemy through the game's own EPO pipeline
  NativeOptionsUi.cs  single Controller tab and game scroll-page integration
  PadSettingsLayout.cs native option rows, expandable sections and binding controls
  PadOptions.cs       which settings appear there and how they map to config
  PadBindings.cs      which button mappings appear and how they persist
  PadBindingRow.cs    press-to-capture binding rows
  PadGui.cs           glyph table and generated textures
  Rumble.cs           haptics
  PadConfig.cs        Settings.json model
  TitleGate.cs        boot Click-to-Continue gate
  LoadingContinue.cs  save-loading curtain gate
  NativeItemPrompts.cs  controller hints inside the item tooltip
  Log.cs              mod log helper
  ViewUtil.cs         View.TryQuit access
lib/0Harmony.dll    patching library, shipped with the mod
dist/DuckovPad/     ready-to-install mod folder
research/           decompiled game sources used to build this (not shipped)
```

---

## Publishing to the Workshop

`info.ini` deliberately has no `publishedFileId`. Use the game's in-game mod uploader
(Options → Mods → Upload) — it writes the ID back into `info.ini` after the first upload.
`preview.png` is already included as the Workshop thumbnail.

---

## Known limitations

- **New UI needs a live controller check.** Automated checks cover direction, repeat,
  release stability and target geometry. The Controller tab, binding capture and the
  inventory actions require verification inside the game.
- Steam Deck gyro, trackpads, rear buttons, touch, rumble and reconnect behavior require a
  live hardware check. Deck support uses Steam Input's gamepad/mouse/keyboard compatibility
  outputs and the setup guide's mappings, not a native Steam Input action manifest.
- The game's own interaction prompts still show keyboard keys; DuckovPad's prompt bar is
  additional rather than a replacement.
- Text entry (naming saves, search boxes) still needs a keyboard.
- A game update that renames `InputManager._aimMousePosCache` or `CharacterInputControl.Update`
  would break aiming; the mod logs a clear error and falls back to keyboard/mouse.
