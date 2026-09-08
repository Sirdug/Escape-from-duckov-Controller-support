# Steam Deck setup

Install DuckovPad in the game's `Duckov_Data/Mods/DuckovPad` folder on your Deck and enable
the mod. The Windows installation on your PC does not automatically copy the mod to a Deck.

In Steam's controller settings for Escape from Duckov, enable Steam Input and apply the
**Gamepad with Mouse Trackpad** template. This template is included in the Steam client.
Keep both sticks, triggers, face buttons, D-pad, Menu and View as gamepad inputs.

Start with **Options → Controller → Aiming → Easy directional (recommended)**. Point the
right stick toward what you see on screen; thumb pressure no longer changes aim distance.
Let go to keep your direction. Aim assist helps with direction and enemy distance. Hold L2
for more controlled adjustments. R3 locks an enemy; press it again or push away to release.

The active controller name appears at the top of the Controller tab. Buttons use Deck labels
such as L1/R1/L2/R2. Connecting or using another supported controller updates the name and
prompts without restarting.

## Trackpads and gyro

- Right trackpad: Mouse. It can aim precisely and move the pointer in menus.
- Right trackpad click: left mouse button for pointing/clicking, or gamepad A if preferred.
- Optional gyro: Mouse, enabled while holding L2. Adjust its sensitivity in Steam, then use
  the mod's **Steam Deck & precision → Trackpad / gyro mouse sensitivity** for fine tuning.
- Left trackpad: leave the template mapping or assign D-pad / mouse scrolling in Steam.
- The mod's **Trackpad / gyro mouse with controller** setting defaults to Auto for a detected
  Deck or Steam Controller. If Steam presents your device only as an unidentified Xbox pad,
  set it to **On (any controller)**.

Mouse precision input takes over aiming while controller movement and triggers remain active.
Moving the right stick returns to directional aiming. Precision mouse aim does not receive
the directional mode's target assistance. Use the touchscreen through the game's normal UI;
Steam + X opens Steam's keyboard for text entry.

## Four rear buttons

In Steam's **Edit Layout → Buttons**, enable rear buttons and set these keyboard outputs:

| Physical button | Steam output | Initial extra action in DuckovPad |
| --- | --- | --- |
| L4 | F7 | Sprint |
| R4 | F8 | Reload |
| L5 | F9 | Interact |
| R5 | F10 | Lock on / release |

These outputs give the four grips independent inputs instead of duplicating face buttons.
F7–F10 are reserved by the mod while a gamepad is connected. Remap L4/R4/L5/R5 to any listed
action in **Options → Controller → Button mapping**. Assigning a grip elsewhere clears its
old binding. The four initial extra actions also appear under **Steam Deck back buttons**.
Save the Steam layout as a personal layout when finished.

## Detection and verification

Direct device metadata identifies Xbox, PlayStation, Switch, Steam Deck and Steam Controller
families. Steam Input can reveal the physical type behind a virtual Xbox controller when the
connection is unambiguous. With multiple virtual pads, the mod reports the exposed device
and the identification limitation; it does not infer the controller from the computer model.

Steam Input supplies trackpad/gyro mouse output, grip keyboard mappings and Deck rumble
emulation. This mod does not install a native Steam Input action manifest or claim raw access
to every Deck sensor. Steam and Quick Access buttons remain managed by Steam.

The build and automated aim-math, controller-label and navigation checks pass on the
development PC. Physical Steam Deck, touch, gyro, rumble and live reconnection tests still
need to be performed on a Deck. Check aiming in all directions, release stability, L2/R2,
all four rear buttons, trackpad clicking/dragging and switching to an external controller.

Valve references: [Steam Input API](https://partner.steamgames.com/doc/api/ISteamInput),
[legacy controller mappings](https://partner.steamgames.com/doc/features/steam_controller/legacy_mode).
