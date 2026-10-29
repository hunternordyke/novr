# Motion controls and VR projection prototype

This is the first iteration toward the hands-on cockpit interaction described in the
[official VTOL VR manual](https://cdn.steamstatic.com/steam/apps/667970/manuals/VTOL_VR_Manual.pdf).
The stick and throttle use runtime Unity primitives. The shared console uses an
original Blender model. Neither includes VTOL VR code, models, or textures.

## Status

The plugin identifies itself as `NOVR (Motion Prototype) 0.4.5.3` in the BepInEx log.

The mod and patcher compile against Nuclear Option 0.34.3 (Steam build 26272685).
Automated tests cover grab/release behavior, tracking-loss recovery, stick directions,
response curves, throttle handoff, and frame-rate-independent HUD smoothing. A local
metadata check verifies declared Harmony method targets and injected fields against
the installed game. These checks do **not** prove correct rendering or controller
behavior in a live headset. Headset flight testing is still required.

## Install the experimental package

1. Close Nuclear Option.
2. Keep a backup of your current `BepInEx/plugins/NOVR` and `BepInEx/patchers/NOVR` folders.
3. With BepInEx 5 already installed, extract `NOVR-steam-frame-experimental.zip`
   into the game's **BepInEx** directory. The archive contains `plugins/NOVR` and
   `patchers/NOVR`; merge those folders and replace the matching files.
4. Start Nuclear Option through your existing VR launch setup. The patcher deploys
   the updated OpenXR support assembly at startup. Replacing only `NOVR.dll` is not
   sufficient: the controller profiles live in the updated OpenXR assembly.
5. Enter an aircraft cockpit and press **F7**. This toggles motion controls and saves
   the preference. You can also set `Enabled = true` under `[Motion Controls]` in
   `BepInEx/config/deltawing.novr.cfg` while the game is closed.

Restoring both backed-up NOVR folders and restarting restores the previous mod,
including its OpenXR payload. F7 disables flight input immediately, but restoring
the previous package is the way to revert all HUD/runtime changes.

## Grip comfort and control feel

Open **View → Flight controls** to adjust the stick's sensitivity/deadzone and
throttle travel without taking off the headset. The displayed travel is the wrist
angle required for full stick input; increasing it makes the stick less sensitive.
**Reset response** restores response defaults without changing saved seat/handle
positions. The yaw switch lets rudder pedals keep control.

**Hold** remains the default for each hand. Optional **Tap** mode works independently
for stick and throttle: briefly squeeze and release grip to latch the handle, then
squeeze again to let go. Holding longer than 0.3 seconds still releases when you
let go. The handle label explicitly says **LATCHED**. Release a latched handle
before aiming at console controls; combat bindings remain active while latched.

Tracking loss, menus, recentering, mode changes and reaching far from a handle
release the latch. The default breakaway distance is 40 cm, increased to twice the
grab radius if that is larger. A fresh grip release/press is required afterward.
No grab or firing action is synthesized when tracking returns.

The menu uses explicit **Flight actions**, **Cockpit setup** and **Back** buttons;
button backgrounds remain visible instead of receiving the dark theme tint twice.

**View → Controls help** shows the actual configured combat/map/wrist buttons,
using native Frame labels or the active fallback and the selected handedness.
Missing hand tracking and unsuccessful placement now show a message on the console.
F8 also closes the wrist menu before placing handles.

Validation: 62 automated checks and 18 live offline checks with
simulated tracked hands. The live checks exercise native pitch/throttle input,
latching/release, tracking recovery, breakaway, wrist cancellation, settings and
profile-aware binding help. Fixed-camera captures check the wrist layout and
button contrast. A further 18 offline checks cover native braking, separate brake/CM
actions, tracking/menu cancellation, placement protection and the throttle-layout UI.
Four configuration-loading checks cover default and custom-binding preservation.
Physical grip timing and comfort still need headset evaluation.

## Brakes and throttle buttons

While holding or latching the throttle, hold **left D-pad down** on native Frame
or **left thumbstick click** on a Touch-compatible profile to apply the game's
wheel/air brakes. Releasing the button releases braking. These buttons follow
handedness. Brakes remain available when the motion combat-button layer is disabled.

Open **View → Flight controls → Button bindings** for two complete throttle layouts.
**Trigger: CMs** preserves the existing countermeasure-trigger layout and adds a
separate brake button. **Trigger: brakes** puts braking on the trigger and moves
countermeasures to the shoulder (Frame) or primary face button (Touch). The page
shows every resulting binding before you return to flight. Selecting a layout
replaces only the four throttle-hand brake/CM/target bindings; custom settings on
other controls are preserved. Existing custom throttle assignments are preserved
on upgrade; if the new default brake would conflict, it starts unassigned.

Tracking loss or opening a menu cancels braking. Release and press the grip/button
again after returning. The hands-free placement gesture is ignored while either
flight handle is held, so thumbstick-click bindings cannot accidentally relocate
controls while flying.

## Flying

The release behavior below describes the default Hold mode. Tap mode is described
above.

| Interaction | Behavior |
| --- | --- |
| Right hand near stick, then press grip | Grab the stick; the current wrist orientation becomes neutral. |
| Tilt the held stick backward/forward | Pitch up/down. |
| Tilt the held stick right/left | Roll right/left. |
| Move the stick-hand thumbstick horizontally | Yaw, while the stick is held. Disable `Thumbstick Yaw` to use pedals. |
| Left hand near throttle, then press grip | Grab at the current throttle setting without jumping. |
| Move held throttle forward/back | Increase/decrease throttle. Travel is 25 cm by default. |
| Release stick grip | Return to the game's normal pitch/roll input. With neutral physical controls, the stick centers. |
| Release throttle grip | Return to the native throttle accumulator at the last setting. Physical throttle input can take over. |
| F8 | Enable motion controls and place handles when both hands are tracked. Release grips before grabbing again. |
| Both thumbstick clicks held for 0.8 seconds | Release both handles first, then place them while motion controls are enabled. |
| F7 | Toggle the feature. |

Version 0.4.5.0 corrects the reversed pitch input: pulling back sends the game's
negative pitch command (nose-up), and pushing forward sends positive pitch
(nose-down). The visible handle follows the same motion. Roll is unchanged.

For landing gear, open **View → Flight actions → Toggle gear** on Frame.
The same page provides **Eject... → Confirm eject**. Cancel, Close, loss of
tracking/focus, or changing aircraft clears the confirmation. The native quick
wheel does not yet have a motion-controller binding; use these wrist actions.

The models are deliberately simple: a stick with a base, and a throttle sliding along
a rail. Version 0.4.5.3 gives them solid depth-writing surfaces and subtle shading,
so their shape and overlapping parts are easier to read. White spheres show tracked
hand positions. Handles are blue at rest, amber within grabbing distance, and green
while held. Grab feedback includes a short haptic pulse on supported devices.

Captions sit above and outboard of the handles on dark backplates, leaving the
shared console clear at the default positions. They mirror with the hand layout
and respect foreground cockpit
geometry instead of drawing through it. They follow font-atlas rebuilds without
changing the game's shared font material. They show hold/latch state
and the grip action needed to release a latch. Five throttle marks span the chosen
travel range in 25% steps; they are visual guides, not physical detents. The readout
stays above the handle as travel is adjusted. **BRAKES ON** appears in
amber when the aircraft's native brake input is applied, including a keyboard/HOTAS
brake input while the motion models are visible. **BRAKES RELEASED** confirms release.
This reports the brake command, not measured deceleration or a parking-brake latch.

If a handle model cannot load, motion input suspends and reports a recovery message.
Toggle motion controls off and on to retry. Failed or replaced models release their
owned geometry/materials without modifying shared game meshes.

For 0.4.5.3, 17 offline in-game checks verify opaque/shaded surfaces, caption bounds
and font refresh, native brake indication, tracking cleanup, resource disposal and
recovery after a deliberately failed model load. Render inspection also checks
foreground text occlusion and the handles alongside the shared console. These use
temporary simulated input; physical headset reach and comfort remain unverified.

Place the controllers where your hands can rest comfortably before calibrating. The
controls stay in cockpit space as you look around. Placement and seat offsets are now saved per aircraft and handedness. See the
[Steam Frame guide](STEAM_FRAME.md) for the wrist panel and additional controls. `Left Handed` swaps hands and mirrors placement. After
changing handedness, calibrate again if needed. Controllers without thumbstick
clicks can use F8.

## Configuration

All values are in `BepInEx/config/deltawing.novr.cfg`.

| Section/key | Default | Purpose |
| --- | --- | --- |
| Motion Controls / Enabled | false | Opt into the experimental controls. |
| Motion Controls / Left Handed | false | Left-hand stick, right-hand throttle. |
| Motion Controls / Stick Position | (0.22, -0.36, 0.32) | Neutral grip position, meters from the recentered eye. |
| Motion Controls / Throttle Position | (-0.24, -0.36, 0.32) | Center of the throttle rail. |
| Motion Controls / Stick Angle | 25 degrees | Tilt for full deflection. |
| Motion Controls / Stick Deadzone | 0.06 | Suppress small movements around neutral. |
| Motion Controls / Stick Response | 1.5 | Higher values soften response near center. |
| Motion Controls / Throttle Travel | 0.25 m | Travel from zero to full thrust. |
| Motion Controls / Grab Radius | 0.12 m | Distance required for the initial grab. |
| Motion Controls / Thumbstick Yaw | true | Use thumbstick yaw while holding the stick. |
| VR HUD / Head Follow Response | 10 | Head-following text response; higher is faster, 0 follows directly. |

The loader registers Steam Frame, Oculus Touch, Index, Vive, and Microsoft motion-controller
OpenXR profiles. This is profile support in code, not a claim of testing every device
or streaming runtime. Use an OpenXR runtime that exposes tracked controllers.

## HUD and UI changes

- Waypoint/objective icons, labels, and range rings use the VR HUD projection.
- The cursor intersects the UI or map surface so its visual depth matches the target.
- Generic joystick and XR axes no longer feed menu navigation; keyboard/gamepad
  navigation remains available.
- HUD world markers, boresight, and flight-path vector project relative to the HUD
  camera position, avoiding translation being mixed into their direction calculation.
- Head-following status text, target designator placement, and pitch-compass origin
  are updated after pose updates. Status-text smoothing uses elapsed real time,
  giving the same response at different frame rates and during paused menus.
- Controller tracking recovery no longer automatically recenters the headset.
- Native mission-picker and workshop APIs are updated for the installed game version.

The waypoint/map/navigation/binding-display work is incorporated from
[upstream PR #50](https://github.com/InfernoSuperNova/novr/pull/50) by macsalamon, with
the original commit authorship retained. Other changes are implemented in this fork.

## Limits and required flight test

This version supplies pitch, roll, optional yaw, throttle, brakes, configurable combat buttons,
controller pointers, and a wrist panel. See [Steam Frame bindings and setup](STEAM_FRAME.md).
The shared [3D console](UNIVERSAL_COCKPIT_PANEL.md) provides interactive gear, radar,
engine, assist and guarded eject controls. Original aircraft-model switches remain
unchanged. Its two displays receive the existing status widgets and minimized map.
Map icons, pan and zoom work through the controller layer; right-click waypoint orders
and text entry still require the existing input methods.

Controls suspend in maps, menus that disable flight controls, external camera modes,
pause, lost headset tracking, and lost OpenXR focus. A disconnected or untracked
hand releases its own control. Reacquisition requires releasing and pressing grip
again. The pilot hooks preserve the game's flight-assist/filter step and G-LOC
restriction. Multiplayer behavior and aircraft-specific auto-hover/collective
interactions still require live tests.

Before treating this as a playable release, check in a local mission:

1. Both eyes show the models and hand spheres at the same positions, with normal
   cockpit occlusion; labels are readable at the chosen seat position.
2. Pitch/roll/yaw directions and full travel match the aircraft response. Release
   returns to neutral physical controls; HOTAS/pedals take over when expected.
3. Throttle does not jump when grabbed or released, including negative-range and
   inverted-collective binding settings. Try auto-hover separately.
4. Open the map, pause, enter the runtime dashboard, cover/disconnect a controller,
   recenter, eject, and change aircraft. No stale grip or input should survive.
5. Waypoints, flight-path vector, target markers, and pitch ladder remain aligned
   when leaning, turning, rolling, and crossing a floating-origin reset.
6. Point at map icons, empty map areas, and the Select Aircraft button. Cursor depth
   and click target should agree in both eyes.

## Developer verification

```sh
dotnet build NOVR/NOVR.csproj -c Release -p:DeployToGame=false
dotnet build NOVR.Patcher/NOVR.Patcher.csproj -c Release -p:DeployToGame=false
dotnet run --project tests/NOVR.Tests.csproj -c Release
# Optional: also verify patch method/field metadata against a local installation.
dotnet run --project tests/NOVR.Tests.csproj -c Release -- \
  build-output/plugins/NOVR.dll "/path/to/Nuclear Option/NuclearOption_Data/Managed"
```

The test project does not execute Unity or require proprietary game files for its
behavior tests. The optional metadata check requires your own
game and BepInEx installation; it does not execute the game or validate Harmony's
runtime patching. Do not commit game DLLs or decompiled game source.

## Implemented follow-up and remaining work

The controller pointer, combat layer, wrist panel, per-aircraft placement, seat adjustment,
and HUD readability follow-up is described in [STEAM_FRAME.md](STEAM_FRAME.md).

Remaining work should be driven by headset flight testing: verify aircraft-specific
collective/auto-hover behavior, improve labels and control geometry, add direct cockpit
switch interaction, and profile frame pacing and cockpit camera costs. Streaming latency
and controller emulation need validation on Steam Frame; this code does not configure
Steam Link quality, foveation, or networking.
