# Steam Frame PC streaming prototype

NOVR Motion Prototype **0.4.5.7**, targeting Nuclear Option **0.34.3**, Steam build
26272685. Nuclear Option runs on the PC; SteamVR supplies OpenXR tracking and
controller input while the headset streams the game. This is an experimental build,
compiled and checked against a local game installation. A live Steam Frame session
now starts through SteamVR/OpenXR using DirectX 11 on the Linux/Proton test host.
Live logs confirm 67 single-player missions, controller clicks through mission
selection, the airbase map and Reserve/Fly, and entry into an aircraft. Motion
flight controls, HUD comfort and the other controller bindings still need hands-on
validation.

## Install and start

1. Close the game. Back up `BepInEx/plugins/NOVR` and `BepInEx/patchers/NOVR`.
2. With BepInEx 5 already installed, extract `NOVR-steam-frame-experimental.zip`
   into the game's **BepInEx directory**, replacing the matching files in both folders.
3. Use SteamVR as the PC's OpenXR runtime and connect Frame through your PC streaming
   setup. Establish the VR session first, then launch Nuclear Option. On the tested
   Linux/Proton host, use the normal DirectX 11 launch, not the Vulkan launch option.
   An explicit `-force-d3d11` launch succeeded; the Vulkan launch exited during
   OpenXR initialization.
4. In a cockpit, press the throttle-hand **View** button (the thin button at the top
   of the left controller in the default layout) to open the wrist panel. Choose
   **Place handles (3s)**; this also enables motion controls. The panel closes;
   place both hands comfortably and wait three seconds. A short haptic pulse confirms
   placement when the controllers support it. Release both grips before grabbing.
5. Use **Seat up/down/left/right/forward/back** for 2 cm adjustments. Placements and
   seat offsets save automatically for the aircraft and handedness.

The keyboard fallbacks are F6 to toggle the console, F7 to toggle motion controls,
F8 to enable motion controls and place handles, and F9
to open the wrist panel. Release both handles first; holding both thumbstick clicks for 0.8 seconds also places
the handles. The console shows a placement countdown and confirmation.
Motion flight controls remain off initially; menu pointers and the console are on.
Restore both backed-up folders and restart to revert the experimental build.

The package must include the updated patcher payload, not just `NOVR.dll`: it contains
the OpenXR controller profiles. The existing upstream installer installs upstream NOVR,
not this development branch. This package does not include BepInEx itself.

## If the headset shows a flat screen inside a black border

A desktop stream is not a VR session. NOVR can load and create its world-space menu
while OpenXR initialization fails, leaving that menu visible only in the flat game
window. Check `Player.log` for `XR_ERROR_RUNTIME_UNAVAILABLE` and SteamVR for a
connected, tracked headset before changing HUD/menu size.

On Frame, use **Streaming Status → host PC → Advanced → Stream VR without game**
to establish the VR connection first, then launch Nuclear Option. If the host is
advertised as “ready for 2D streaming only,” resolve the host's VR streaming setup
first. Restart the game after establishing the connection; this build does not retry
failed OpenXR initialization automatically.

Linux host support remains experimental. On the local test PC, SteamVR beta and the
OpenXR runtime were already configured, but Steam was started without its
`-vrlinkforceenable` option. The host launcher was updated to retain `-pipewire`,
`PRESSURE_VESSEL_IMPORT_OPENXR_1_RUNTIMES=1`, and enable VR streaming. This is a host
configuration change, not a setting included in this ZIP. See Valve's
[platform support guidance](https://help.steampowered.com/en/faqs/view/2668-0B6E-79E0-A85B)
and the [Linux streaming issue report](https://github.com/ValveSoftware/SteamVR-for-Linux/issues/962).

Version **0.4.4.3** fixed repeat-start payload installation: identical runtime
DLLs are left untouched when Unity has already loaded them. MultiPass is now set
explicitly before OpenXR initialization, including on the first install. Neither
change replaces the need for an active VR streaming connection.

The test headset initially could not connect directly over local Wi-Fi while its
Tailscale connection was enabled. With Tailscale disabled on Frame, the direct Wi-Fi
connection and SteamVR session worked. The wireless adapter has not been independently
validated; successful home Wi-Fi streaming does not establish adapter health.

Version **0.4.4.4** fixes a menu pointer exception: Unity UI's graphic collection
supports indexed access but throws when enumerated. That exception interrupted the
controller update before the cursor/laser could be drawn. Frame detection also now
recognizes the runtime-generated layouts derived from Valve's controller profile.

Version **0.4.4.5** restores automatic mission tags before the VR menu filters
missions. Without this step, valid missions could all be hidden from Single Player
and multiplayer hosting. Both lists refresh when reopened to include missions added
later during startup. Mission header errors skip only the affected entry.

Version **0.4.4.6** uses OpenXR session focus for controller input instead of
requiring focus on the desktop mirror. Visible native VR menus also keep their
pointer active independently of the desktop cursor flag after loading transitions.
The game runs in the background while VR is enabled. Opening the runtime dashboard
still cancels VR input; the mouse fallback requires desktop focus. `Player.log` now
reports pointer state changes, tracking/focus information and controller UI presses.

Version **0.4.4.8** uses the active player-pilot state and local aircraft ownership
instead of the unused legacy `Pilot.playerControlled` field. Cockpit checks run
before querying scene menus, and the radial-menu query tolerates scene startup.
A pending handle placement survives temporary readiness/tracking loss for up to ten
seconds after its countdown, then logs a reason if it cannot complete. The wrist
panel and game log show motion-control readiness status.

Version **0.4.5.0** corrects inverted motion-stick pitch, matching the game's
negative-pitch/nose-up convention and its cockpit stick animation. Pulling back
commands nose-up; pushing forward commands nose-down. Roll is unchanged. The
wrist Flight page now includes an ejection action with a separate confirmation.
The shared 3D console adds physical controls, live weapon/status displays and the
minimap. It also adds readable countermeasure selection, minimap zoom keys, live
placement adjustments and a weapon SAFE indicator. An offline 22-check exercise
covers console actions, HUD restoration and the ejection camera handoff; physical
controller reach and stereo comfort still need headset testing. See [the universal cockpit console guide](UNIVERSAL_COCKPIT_PANEL.md).

Version **0.4.5.1** adds optional tap-to-latch flight grips, separately for stick
and throttle. Hold-to-grip remains the default. Open **View → Flight controls**
for grip mode, sensitivity, deadzone, throttle travel and yaw settings. **Controls
help** shows the bindings currently in use, including Frame/Touch fallback and
handedness. Missing tracking and failed handle placement have visible feedback.

Version **0.4.5.2** adds wheel/air-brake input and a throttle-layout page:
**View → Flight controls → Button bindings**. Braking uses D-pad down on native
Frame, or the throttle-hand thumbstick click on Touch fallback. Hold the throttle
(or latch it) while using that button. The optional **Trigger: brakes** layout
moves the four throttle actions together to keep their buttons distinct:

| Throttle action | Native Frame, trigger-brake layout | Touch fallback, trigger-brake layout |
| --- | --- | --- |
| Brake, hold | Left trigger | Left trigger |
| Countermeasures, hold | Left shoulder | Left X |
| Select target, release | Left D-pad down | Left Y |
| Next countermeasure, release | Left D-pad right | Left thumbstick click |

**Trigger: CMs** restores the default table below. Custom bindings survive an
upgrade; an added brake binding starts unassigned if its default button is already
used by a custom throttle action. The wrist page shows the active bindings and warns
about shared buttons. Neither layout changes the flight stick, map or wrist controls.

Version **0.4.5.3** makes the stick/throttle solid and shaded, moves their captions
clear of the handles, and adds readable backplates. Captions now respect cockpit
occlusion. Throttle markings follow the
selected travel distance. The throttle readout shows **BRAKES ON** in amber while
the game's brake input is applied, then **BRAKES RELEASED**. It also shows native
keyboard/HOTAS braking while motion models are visible. These are command indicators;
the flight controls still use the same bindings and grip modes.

Version **0.4.5.4** restores the game’s normal target-camera zoom, zoom-out after
losing a target, and wide landing-camera view. The instrument magnification now
uses the same field of view as the rendered camera. This does not change headset
zoom or add a new control binding.

## Shared 3D console

The console appears automatically in the cockpit. Its layout and placement are
shared by every aircraft. Release a flight-control grip, then use the laser and
trigger, or reach to a nearby control and use trigger or grip. The gear lever supports grab-and-move;
ejection requires lifting the red guard and a separate deliberate pull/hold.
Weapon/countermeasure status and the minimap move to its displays.

Use **View → Flight actions → Console setup** to adjust its position, tilt and
size. **F6** shows/hides it and restores the previous HUD layout when hidden.
Version 0.4.5.5 keeps the other free hand’s pointer visible when one hand is near
the console. Nearby controls take priority for their own hand; hints follow the
control being used, including eject progress. Release a held control before
operating another. Version 0.4.5.6 keeps direct gear/eject travel consistent at
all console sizes. Ejection feedback distinguishes a request from the aircraft’s
confirmation. If it is not accepted, release, reopen the guard and pull again;
there is no automatic retry. Console interactions and display fitting still
require in-headset validation.

Version 0.4.5.7 adds **HOV** below the CM key. Its lamp follows native hover assist.
The same action is available from **View → Flight actions → Hover assist**. It
requires a compatible aircraft and at least 1 m ground clearance. During hover,
**HOVER INPUT** shows the pilot’s command rather than automatic engine power;
letting go and regrabbing preserves that command. The normal caption is
**COLLECTIVE** or **THROTTLE**, depending on the aircraft.

## Default bindings

Right-handed layout: right hand = stick/pointer; left hand = throttle/wrist panel.
The handedness setting swaps these roles. Native Frame physical labels below describe
the default right-handed layout. In a mirrored layout, use the same *button position*
on the opposite controller; the labels differ between hands.

| Action | Native Steam Frame | Touch-compatible fallback | Context |
| --- | --- | --- | --- |
| Show/hide console | Wrist Console setup | F6 | Cockpit |
| Grab flight control | Grip near handle | Grip near handle | Cockpit, motion controls enabled |
| Fire selected weapon | Right trigger | Right trigger | Hold stick grip; release trigger before firing after a mode change |
| Wheel / air brake | Left D-pad down | Left thumbstick click | Hold button while holding or latching throttle |
| Countermeasures | Left trigger | Left trigger | Hold throttle grip |
| Next weapon | Right shoulder | Right A | Hold stick; activates on button release |
| Select target | Left shoulder | Left X | Hold throttle; selects under the head-directed target designator on release |
| Next countermeasure | Left D-pad right | Left Y | Hold throttle; activates on release |
| Clear targets | Right Y | Wrist panel | Hold stick for the direct binding |
| Open/close map | Right B | Right B | Cockpit; works with grips released |
| Open/close wrist panel | Left View | Left menu | Cockpit; works with motion controls off |
| Click / drag menus | Right trigger / hold trigger | Right trigger / hold trigger | Menu or map visible |
| Scroll a menu | Right thumbstick vertical | Same | Point at scrollable content |
| Zoom map | Right thumbstick vertical | Same | Point at the map |
| Pan map | Left thumbstick | Same | Point at the map with right controller |
| Yaw | Right thumbstick horizontal | Same | Hold stick, flight mode |

The menu pointer uses the stick hand. If that hand is untracked, the mouse remains
available. Controller raycasting uses actual world-space UI surfaces, including
normal button, dropdown, drag and scroll events. Losing tracking cancels a press
without sending a click or drop. Trigger presses are disarmed across menu/flight
transitions until released. Physical keyboard/HOTAS bindings remain available.

No binding uses the system button. The headset/runtime may reserve some buttons;
F9 remains available if the runtime does not deliver the menu/view input.

Bindings are editable in `BepInEx/config/deltawing.novr.cfg` under
`[VR Controller Bindings]` while the game is closed. Button values include `Trigger`,
`Primary`, `Secondary`, `Menu`, `Shoulder`, `Top`, `Outside`, `StickClick`, and `None`.
`ShoulderOrPrimary` and `OutsideOrSecondary` select the native Frame button when
available and the ordinary Touch button otherwise. `Primary` means Frame A/D-pad down;
`Secondary` means Frame X/D-pad right. `PrimaryOrStickClick`, `PrimaryOrSecondary`
and `SecondaryOrStickClick` adapt the throttle presets to Frame/Touch input.
Avoid assigning conflicting actions to the same
button. Combat bindings are distinct from the fixed trigger/menu pointer interaction.

## Wrist panel and cockpit profiles

The panel follows the throttle hand and faces the headset. It holds still during a
trigger press. **Flight actions** and **Cockpit setup** switch between pages;
subpages have an explicit Back button. The wrist panel provides:

- Motion enable, delayed handle placement, view recentering, handedness and seat adjustment.
- Gear, radar, engine, flight assist, weapon/countermeasure cycling, target clearing and map access.
- HUD size, depth, backplate contrast, declutter and reset controls.

Flight actions close the panel and enter the game's existing input path on the next
frame. The game retains its G-LOC, weapon safety, gear/radar altitude restrictions and
multiplayer command handling. For landing gear, choose **View → Flight actions →
Toggle gear**. For ejection, choose **Eject...**, then **Confirm eject**. **Cancel**
returns to the flight page; closing the panel, losing tracking/focus, or changing
aircraft clears the confirmation. Ejection uses the game's normal input handling.
The native radial quick wheel does not yet have a motion-controller binding;
the wrist panel provides the listed actions instead.

Profiles use the aircraft definition's stable key, with separate left/right-handed
layouts. The config section's hash avoids invalid configuration characters; its
comments identify the aircraft. Default positions in `[Motion Controls]` seed new
profiles. Reset aircraft setup returns that profile's seat to zero and handles to the
configured defaults. Recenter adjusts tracking; it does not overwrite saved profiles.

## HUD readability

`[VR HUD]` includes:

| Setting | Default | Range / effect |
| --- | --- | --- |
| Status Panel Scale | 1.15 | 0.8–1.6; status panels only |
| Status Panel Distance | 4 m | 2–10 m; preserves angular size while changing stereo depth |
| Status Panel Backplates | true | Dark backgrounds over bright terrain |
| Declutter Status Panels | false | Fades the secondary lower-left panel to 15% |
| Head Follow Response | 10 | Existing head-follow smoothing; 0 follows immediately |

World target markers, boresight and flight-path symbology are excluded from status
panel scaling/depth adjustments. Weapon and countermeasure panels remain visible
when decluttering. The new backplates and panel spacing still need visual evaluation
in each cockpit and both eyes.

## Native Frame profile and fallback

The fork registers Valve's
[`XR_VALVE_frame_controller_interaction` profile](https://github.com/ValveSoftware/Unity/blob/7e842352310fc2cb458ac6b127ce1afc233173a1/com.valvesoftware.openxr.utils/Documentation~/index.md)
before initializing OpenXR. The profile exposes the additional shoulder/face buttons
and the native aim pose. If the runtime does not enable that extension, the profile is
skipped and the existing Touch-compatible profile remains available. Both controllers are now detected with native Steam Frame bindings in the live
SteamVR streaming session; physical button behavior still needs hands-on validation.
The BepInEx log reports each controller and whether NOVR selected native Steam Frame
bindings or the generic fallback. See [third-party notices](THIRD_PARTY_NOTICES.md) for the Valve source and license.

## Verification and live test pass

Automated checks cover grip/trigger transitions, tracking recovery, input curves,
throttle handoff, pointer surface geometry, HUD smoothing and map zoom rates. An
optional metadata check verifies Harmony methods, explicit overloads and injected
fields against the locally installed game and Rewired assemblies. It does not run
Unity, load OpenXR hardware, or prove the patches execute correctly in game.

Before using this as a regular flight setup, check a local mission:

1. Verify the default binding table with native Frame input and, if available, Touch
   emulation. Test left-handed mode and the View/menu fallback.
2. Click a main-menu button, scroll, use a dropdown and drag a scrollbar. Hold trigger,
   open the runtime dashboard, then return: no click or weapon fire should occur.
3. Select map icons, pan and zoom while pointing at the map. Zoom should retain the
   location under the laser. Confirm depth alignment in both eyes.
4. Fire, cycle weapons, select/clear targets and deploy/cycle countermeasures. Check
   wrist gear/radar/engine/assist actions and native game restrictions.
5. Save different seat/handle placements in two aircraft; change aircraft and restart
   the game. Test recentering, pause, ejection and controller disconnection.
6. Check status panel depth, contrast and clutter settings against bright terrain.
   Aim/world markers must retain alignment during head motion and aircraft maneuvers.

Right-click map orders/waypoint construction and text entry still use the existing
input methods. Aircraft-specific collective/auto-hover, multiplayer, streaming latency,
render performance and physical Steam Frame behavior remain unverified. Streaming
quality, eye tracking and foveation are controlled by the runtime and are not configured
by this change.
