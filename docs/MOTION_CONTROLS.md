# Motion controls and VR projection prototype

This is the first iteration toward the hands-on cockpit interaction described in the
[official VTOL VR manual](https://cdn.steamstatic.com/steam/apps/667970/manuals/VTOL_VR_Manual.pdf).
The implementation is original NOVR code and uses runtime Unity primitives; it does not
include VTOL VR code, models, or textures.

## Status

The plugin identifies itself as `NOVR (Motion Prototype) 0.4.4.1` in the BepInEx log.

The mod and patcher compile against Nuclear Option 0.34.1 (Steam build 26272685).
Automated tests cover grab/release behavior, tracking-loss recovery, stick directions,
response curves, throttle handoff, and frame-rate-independent HUD smoothing. A local
metadata check verifies declared Harmony method targets and injected fields against
the installed game. These checks do **not** prove correct rendering or controller
behavior in a live headset. Headset flight testing is still required.

## Install the experimental package

1. Close Nuclear Option.
2. Keep a backup of your current `BepInEx/plugins/NOVR` and `BepInEx/patchers/NOVR` folders.
3. With BepInEx 5 already installed, extract `NOVR-motion-controls-experimental.zip`
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

## Flying

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
| F8 or both thumbstick clicks held for 0.8 seconds | Place controls at your hands. Release grips before grabbing again. |
| F7 | Toggle the feature. |

The models are deliberately simple: a stick with a base, and a throttle sliding along
a rail. White spheres show tracked hand positions. Handles are blue at rest, amber
within grabbing distance, and green while held. Grab feedback includes a short
haptic pulse on devices that support it.

Place the controllers where your hands can rest comfortably before calibrating. The
controls stay in cockpit space as you look around. Placement is shared across
aircraft in this iteration. `Left Handed` swaps hands and mirrors placement. After
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

The loader registers Oculus Touch, Index, Vive, and Microsoft motion-controller
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

This version supplies pitch, roll, optional yaw, and throttle. Weapons, targeting,
countermeasures, gear, and menu clicks still use the game's normal bindings. Fully
clickable cockpit switches and motion-controller menu pointers are future work.

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

## Recommended next improvements

1. **Controller laser pointers and trigger clicks for all menus and the tactical map.**
   This removes the mouse dependency during aircraft selection, mission setup, and
   mid-flight map use. Use one shared ray for cursor placement and actual hit tests.
2. **A compact combat-control layer on the controllers.** Add configurable trigger
   fire, weapon selection, countermeasures, targeting, and a small wrist menu for
   gear/radar/engine actions, with tooltips and deliberate ejection confirmation.
3. **Per-aircraft seat and control placement profiles.** Save the preferred stick,
   throttle, handedness, and eye position for each cockpit. Add in-VR adjustment so
   users can tune it without editing a file or reaching for the keyboard.
4. **Separate HUD readability controls.** Keep aiming and world markers aligned,
   while allowing adjustable angular text size, contrast, panel distance, and
   decluttering for secondary information. Include a quick reset-to-defaults action.
5. **Predictable frame pacing and cockpit display performance.** Profile render
   camera stacks and allocations with a headset; reduce redundant UI/layout work
   and throttle nonessential display updates where measurements justify it.
