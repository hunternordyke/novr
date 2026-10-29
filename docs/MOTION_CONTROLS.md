# Motion controls and VR projection prototype

This is the first iteration toward the hands-on cockpit interaction described in the
[official VTOL VR manual](https://cdn.steamstatic.com/steam/apps/667970/manuals/VTOL_VR_Manual.pdf).
The stick and throttle reuse isolated meshes and materials from the installed
aircraft at runtime, with primitive fallbacks for unsupported aircraft. Stationary
original handles are hidden only while the replacements are visible, and restored
when disabled or leaving the cockpit. No native game assets are exported or shipped.
The shared console is an original Blender model; no VTOL VR assets are included.

## Status

The plugin identifies itself as `NOVR (Motion Prototype) 0.4.6.2` in the BepInEx log.

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

## Layout editing and helicopter defaults

Open **left View → Edit cockpit layout**. Release grip first; either hand can
reach and grip the stick, throttle/collective, or panel independently. You can also
point at a handle or panel and squeeze grip. Move the hand to translate the item;
turn your wrist while holding the panel to rotate it. The panel becomes an outline
so handles behind it remain visible. Flight and console actions are suspended
while editing. Open View again and choose **Save layout** or **Cancel layout**.

Handle positions save per aircraft and handedness; the panel pose is shared across
aircraft. Tracking loss or menus release a drag; a new grip press is required.
Leaving the cockpit cancels an unfinished edit. The older **Place handles (3s)**
option is under **View → Flight controls**; F8 remains available.

Helicopter collective travel is centered **15 cm below the default stick**
(default grip coordinates: stick y −0.36 m, collective center y −0.51 m).
With the default 22 cm travel, the top of the collective range stays slightly below
the stick. Untouched old helicopter defaults migrate once; custom placements remain.
**Reset aircraft setup** uses this lower default and also resets the seat position.
Jets and the VT-7 keep their existing throttle height.

## VTOL-style buttons and inspection view

The preset follows VTOL VR's trigger/action-button division, while using Nuclear
Option's native target and weapon logic. **View → Flight controls → Button bindings
→ VTOL-style controls** applies it. Existing stock bindings migrate automatically;
custom bindings are retained. Held or tap-latched handles both enable their buttons.

| Action | Default Frame button | Required context |
| --- | --- | --- |
| Fire | Right trigger | Stick held |
| Next weapon | Right A, release | Stick held |
| Wheel/air brakes | Left trigger, hold | Throttle held |
| Countermeasures | Left D-pad down, hold | Throttle held |
| Select target under head-directed cue | Right thumbstick click, release | Stick held |
| Remove latest selected target | Right X, release | Stick held |
| Clear all selected targets | Right Y, release | Stick held |
| Next countermeasure type | Left D-pad right, release | Throttle held |
| Inspection magnifier | Physical right bumper, hold | Cockpit, with either grip held or released |
| Map / wrist menu | Right B / left View | Cockpit |

Target selection does not force a weapon lock: seeker, range, radar and weapon
restrictions still apply. X removes the most recently selected target; Y clears
all selected targets. Both actions are also on the wrist Flight actions page.
For Touch fallback: A changes weapon, X deploys CMs, right thumbstick click selects,
and Y changes CM type; use wrist actions for unlock/clear. A controller without a
shoulder button has no default inspection binding.

The right bumper is reserved even when using a custom binding or mirrored controls.
It shows a **mono magnified image on a panel 3 m ahead**, with the normal stereo
periphery preserved. Release hides it. Full-view XR zoom was removed after it caused
uncomfortable convergence during headset testing. Headset projection and eye
separation are unchanged by the replacement. The window is for visual inspection;
it does not alter the target camera's zoom or provide MFD cursor control.

The magnifier renders at 1024×640 only while held. Its additional camera has a GPU
cost that still needs streaming/flight evaluation. Menus, pause, tracking loss,
layout editing and aircraft changes cancel it; release before reactivating.

## Grip comfort and control feel

Open **View → Flight controls** to adjust the stick's sensitivity/deadzone and
throttle travel without taking off the headset. The displayed travel is the wrist
angle required for full stick input; increasing it makes the stick less sensitive.
**Reset response** restores response defaults without changing saved seat/handle
positions. Turn both Stick twist yaw and Thumbstick yaw off to let rudder pedals keep control.

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

Validation: 71 automated checks and 18 live offline checks with
simulated tracked hands. The live checks exercise native pitch/throttle input,
latching/release, tracking recovery, breakaway, wrist cancellation, settings and
profile-aware binding help. Fixed-camera captures check the wrist layout and
button contrast. A further 18 offline checks cover native braking, separate brake/CM
actions, tracking/menu cancellation, placement protection and the throttle-layout UI.
Four configuration-loading checks cover default and custom-binding preservation.
Physical grip timing and comfort still need headset evaluation.

Version 0.4.5.7 captures throttle/collective input before native hover filtering.
The virtual handle follows the pilot’s command, not the engine power produced by
the hover controller, so release/regrab does not adopt automatic power output.
Its caption changes to **HOVER INPUT** during hover assist, otherwise **COLLECTIVE**
on aircraft using collective input or **THROTTLE**. Use the console **HOV** key or
**View → Flight actions → Hover assist**;
unavailable aircraft and the native 1 m ground-clearance restriction are explained
in the UI.

Version 0.4.5.8 gives helicopters such as the SAH-46 a **hinged collective**:
grip the handle and lift to increase the command, or lower it to decrease.
Sideways and forward/back motion does not change the collective command.
The model and grab point follow the same arc. This follows the lift/lower
convention described in the [FAA Helicopter Flying Handbook](https://www.faa.gov/sites/faa.gov/files/helicopter_flying_handbook.pdf).
The VT-7 and fixed-wing aircraft keep their forward/back throttle rail.
The motion direction does not change when hover assist is toggled.

**View → Flight controls** calls this setting **Collective lift** in helicopters,
with **Shorter lift / Longer lift** buttons. The default full range is 22 cm.
Saved positions remain the middle of the range. **Place handles** aligns the
visible grips with your hands at the current command, including a fully raised
or lowered collective, and saves separately for each aircraft and handedness.
Existing helicopter positions are retained; use placement again if the new lift
range needs more clearance around your chair.

Validation for 0.4.5.8: 76 controller-driven checks in the SAH-46 and the same
76 checks in the VT-7 pass. They exercise the actual native command with both
handedness layouts, reject motion on other axes, preserve release/regrab, check
placement at 0/50/100% over 5/25/60 cm travel, and cover native axis conventions,
tap latching, breakaway, tracking recovery and label fitting. The aircraft are
held stationary and controller poses are simulated; these are not flight tests.
Rendered inspection verifies the new lever at nominal placement.

Version 0.4.5.9 makes the handle captions 35% smaller and brings them closer to
the pilot, just outside each grip. This reduces sidewall clipping and keeps them
clear of the shared console at nominal placement. Collective captions sit above
the complete lift range. Captions still obey cockpit depth; saved seat, handle
and console positions remain adjustable. Custom placements and headset
readability still need hands-on testing.

Validation for 0.4.5.9: 71 automated checks and 17 in-game visual/recovery
checks pass. Installed-plugin renders check both handedness layouts in the
VT-7 and SAH-46 at nominal placement using the complete cockpit/HUD camera
composition. These captures do not establish stereo comfort or readability
through the headset.

## Brakes and throttle buttons

The default VTOL-style layout uses the **left trigger** for wheel/air brakes while
holding or latching the throttle. The older **Trigger: CMs** layout uses left
D-pad down on Frame, or left thumbstick click on Touch fallback. Releasing the button releases braking. These buttons follow
handedness. Brakes remain available when the motion combat-button layer is disabled.

Open **View → Flight controls → Button bindings** for the VTOL-style preset and two older throttle layouts.
**Trigger: CMs** preserves the existing countermeasure-trigger layout and adds a
separate brake button. **Trigger: brakes** puts braking on the trigger and moves
countermeasures to the shoulder (Frame) or primary face button (Touch). The page
shows every resulting binding before you return to flight. Selecting a layout
changes the throttle brake/CM bindings and the target-select hand/button. The
VTOL preset puts selection on the stick; the older presets put it on the throttle.
Other controls are preserved when selecting an older throttle preset. Existing custom throttle assignments are preserved
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
| Twist the held/latched stick left or right | Rudder in flight and native wheel steering on the ground; 30° twist gives full input by default. |
| Move the stick-hand thumbstick horizontally | Optional additional yaw, summed with twist and clamped. Turn both `Stick Twist Yaw` and `Thumbstick Yaw` off to use pedals. |
| Left hand near throttle, then press grip | Grab at the current throttle setting without jumping. |
| Move held throttle forward/back | Increase/decrease throttle. Travel is 22 cm by default. |
| Lift/lower the held helicopter collective | Increase/decrease collective command. Lift is 22 cm by default; sideways/forward motion is ignored. |
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

The models are deliberately simple: a stick with a base, and either a throttle
sliding along a rail or a hinged helicopter collective. Version 0.4.5.3 gives them solid depth-writing surfaces and subtle shading,
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
| Motion Controls / Throttle Travel | 0.22 m | Travel from zero to full thrust. |
| Motion Controls / Grab Radius | 0.12 m | Distance required for the initial grab. |
| Motion Controls / Stick Twist Yaw | true | Twist for native rudder/nosewheel steering while holding or latching the stick. |
| Motion Controls / Stick Yaw Angle | 30° | Twist needed for full yaw; uses stick deadzone and response. |
| Motion Controls / Thumbstick Yaw | true | Also allow horizontal thumbstick yaw while holding the stick. |
| VR HUD / Head Follow Response | 10 | Head-following text response; higher is faster, 0 follows directly. |

The loader registers Steam Frame, Oculus Touch, Index, Vive, and Microsoft motion-controller
OpenXR profiles. This is profile support in code, not a claim of testing every device
or streaming runtime. Use an OpenXR runtime that exposes tracked controllers.

## Instrument camera projection

Version 0.4.5.4 restores native target-camera zoom, lost-target zoom-out and the
wide landing-camera view. Instrument magnification reads the same field of view
that drives the rendered image. The previous independent projection workaround
has been removed.

The field-of-view guard now applies only to cameras that may render to the
headset. Render textures, reflection/preview cameras and cameras explicitly
excluded from XR retain their normal FOV setters. Both target-camera components
opt out of URP XR rendering. Headset projection remains runtime-controlled; this
change adds no headset zoom or new button binding.

21 checks in an offline FS-12 mission verify the actual camera setters, projection
matrices and native target/landing behavior, alongside the headset guard and URP
camera classification. The guard cases temporarily control the XR-enabled flag;
the instrument checks run with the real active XR runtime. The diagnostic code
is excluded from the package. Physical stereo comfort remains unverified.

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

## 0.4.6.0 validation

76 automated checks pass, including 45 native patch-target checks. In-game
checks using simulated controller input passed: 53 in the VT-7 for layout,
native handle restoration, MFD pages and focused HUD; 50 in the SAH-46 for
the same core behavior before the final lower-default adjustment; and 32 in
the VT-7 for configuration migration, grip-bound native actions, target selection,
unlock/clear and the replacement inspection magnifier. The latter verify both
actual OpenXR eye projections remain unchanged while the image is visible.
The helicopter's final lower default is compiled; its ergonomics remain for
headset testing. Captures are inspection views, not stereo comfort validation.

## 0.4.6.1 targeting, tracking and yaw

The VTOL preset now selects on the **stick-hand thumbstick click**, matching the
hand used by VTOL VR for SOI selection. Press and release while holding/latching
the stick. Native Frame reads its explicit thumbstick-click control. The complete
stock 0.4.6.0 preset migrates automatically; custom/legacy throttle presets and an
explicit saved hand preference remain. The old config key `Select Target - throttle
hand` is retained for compatibility; `Select Target On Stick Hand` selects its hand.

Head, hand-grip and pointer positions now use the same recentered rotation and
translation. Previously recentering rotated orientations but left positions in
the old room axes. Saved seat, panel and handle placements are not rewritten.
Use **View → Recenter view** while seated facing forward after updating, and
check hand-marker alignment in the headset.

Stick twist feeds the native yaw input, which also drives steerable landing gear.
Each grab captures a neutral orientation. Pitch/roll swing is separated from twist;
release, lost tracking and menus cancel motion input. The native stick model twists
visually too. Both yaw options are available under **View → Flight controls**.

## 0.4.6.1 throttle, dashboard and MFD additions

- Grip the throttle and move its thumbstick forward/back to command VTOL angle
  toward forward flight/hover. Native nozzle limits and hover assist retain priority.
  On tilt-wing aircraft this selects manual nacelle tilt; toggling native Flight
  Assist restores automatic tilt. Aircraft without a VTOL system ignore this axis.
- Afterburning jets have a MIL shelf at their actual engine threshold (the installed
  FS-12 uses 90%; some workshop jets differ). The physical shelf spans 1.6 cm of
  extra hand travel. Push through for afterburner. A notch/MIL mark and haptic cues
  identify it. Release/regrab preserves power. Helicopter collectives and engines
  without afterburners do not receive a detent. Disable `MIL Throttle Detent` to
  restore linear travel.
- Press **L STK** under either MFD to bring up Map and slave the physical left
  thumbstick to it. Move to pan, click to recenter, press **SLAVED** again to return
  to flight control. This takes precedence over that same stick's nozzle, yaw and
  target-click bindings. Tactical/Sensor feeds are display-only in this version.
- Dashboard anchoring and saved manual panel positions are per aircraft. Moving
  the seat does not move the panel or flight controls. **Anchor to dashboard**
  restores the automatic placement. The clearance audit and exceptions are in
  `UNIVERSAL_COCKPIT_PANEL.md`.
- Physical eject pulls have no added timer; an armed pointer press now acts
  immediately too. Release after opening the guard, then make a separate pull/press.
  The game still runs its canopy and ejection-seat animation.
- `Interaction Haptic Strength` controls short grab, release, button, lever and
  throttle-detent cues; 0 disables them.

Build checks: 91 automated checks pass; all 46 declared Harmony targets resolve. Live offline checks
cover target-click release, native rudder/nosewheel steering, recenter transforms,
seat-independent controls, dashboard/manual placement, native nozzle movement,
map slaving and MIL power. See the packaged NOVR-validation-0.4.6.1.txt for run scope and limitations.

## Pilot hands, helmet gesture and interaction feedback — 0.4.6.1

Rigged gloves use Valve's BSD-licensed SteamVR slim meshes and reference poses.
They keep proper UV seams and normal details, with a tint sampled from the installed
pilot. The pilot's own sleeve geometry/materials remain local. A private texture
copy (up to 2048 square) adds mild sharpening and subtle fabric detail once on
aircraft entry; the original atlas is untouched. Native pilot animations and other players are untouched.
Grip curls three fingers and thumb; trigger curls index. This is controller-driven
posing, not optical finger tracking. The two gloves total 9,798 triangles.

Sleeves default to 82% of native thickness with unchanged lengths and wrist targets.
View → Flight controls → Button bindings → Hands & feedback includes Slimmer/Wider
sleeves, Gloves/Sleeves toggles, wrist-angle and hand-offset adjustments. Sleeves hide beyond their reachable envelope. Config
`Glove Wrist Offset` / `Glove Wrist Rotation` adjust visuals only; interaction points
continue using the runtime's grip pose. These values need a headset fit check.

Bring either free hand to the brow, then make a fresh trigger press to toggle native
night vision. A small cue identifies the gesture area. Held-trigger arrival and
tracking recovery cannot toggle it; menus, map, layout and occupied hands suppress it.

`Interaction Sound Volume` (default 0.25) controls original spatial grip/release,
button, detent, guard and helmet cues. The Hands & feedback page has sound-volume buttons.
Zero mutes sound independently of `Interaction Haptic Strength`. Six pooled voices
and seven pre-generated clips bound the audio cost; the aircraft mixer is reused
where available.

Runtime controller reads are shared per hand per frame, with immediate invalidation
on calibration/seat changes. Panel state and MFD labels only refresh when changed.
Pilot geometry reads and glove setup happen on aircraft entry, not every frame.
No global game/SteamVR quality or streaming settings are changed. See NOVR-validation-0.4.6.1.txt
for final checks; no headset FPS or stereo-comfort claim is made.

## Final comfort adjustments

- Either hand can operate menu and cockpit-panel pointers. Aim at a control and
  make a fresh trigger press to take the pointer. Its owning hand retains a drag
  through release; switching hands, entering a menu or recovering tracking cannot
  turn a held trigger into a new click. Direct panel touch still works with either
  free hand. Release a latched flight handle before using that hand on the panel.
- Default throttle/collective travel is now 22 cm (previously 25 cm). Existing
  untouched defaults migrate once; custom travel is preserved. Flight controls
  still has Shorter/Longer travel buttons.
- The default glove pitch is corrected downward by 25 degrees. In Hands & feedback,
  Wrist tips down/up changes it in 5-degree steps; nearer/farther and lower/higher
  move the visible wrist in 5 mm steps. These adjustments leave controller hit
  positions and flight inputs unchanged. Physical comfort remains a headset check.
- VT-7 dashboard fitting starts 25% larger, then checks surrounding clearance.
  Existing manually placed VT-7 panels enlarge once (within the scale limit) and
  move slightly toward the pilot to allow for the thicker scaled panel. Other
  aircraft scales are unchanged. Edit layout and Console setup remain available.
- The diagnostic input helper has been removed. The final game restart loads only
  the production mod; no automatic aircraft switches or controller tests run.

## 0.4.6.2 — steadier menus and direct MFD selection

The View menu opens about 72 cm in front of you and stays there while you point.
Either controller can operate it. Losing the usual menu hand does not close it
while the other hand can still point. Close and reopen View to center it again.
If you prefer the older hand-following behavior, use **View → Flight controls →
Button bindings → View menu: STEADY / FOLLOW HAND**. The adjacent **Bring menu
forward** button places the menu again using the selected mode.

The opaque console hides while View is open so it cannot cover menu buttons.
**Console setup** retains an outline preview for position, tilt and scale changes;
close View to inspect the actual displays. Layout editing keeps its existing
transparent preview and Save/Cancel behavior. The menu laser uses the game's
available particle shader and a finer tip.

Each page has a clear title, motion/backplate/declutter toggles show their state,
and **Controls help** separates stick-hand and throttle-hand bindings into two
columns. **Hands & feedback** is also available directly from **Flight controls**.
Resetting the seat and handles requires **Reset seat & handles… → Reset seat &
handles**; **Keep my setup** returns without changing saved placement. This reset
applies only to the current aircraft and handedness; panel placement is retained.

Touch or point at the **PAGES** title above either MFD, then trigger/grip a page.
The picker highlights the current page and marks unavailable aircraft instruments
**NOT FITTED**. Selecting the other MFD's existing page swaps the two native
widgets rather than duplicating them. **BACK**, or pressing the title again,
closes the picker without changing the page. Only one picker opens at a time.
The existing previous/next bezel buttons remain available.

Page choices use the rectangles actually drawn on the display for direct-touch
and ray hit testing. The ray dot follows the actual hit on the button. Existing
fresh-press gates apply: holding a trigger while entering a menu, moving between
choices or recovering tracking cannot activate another choice. Opening View,
hiding the console, leaving the cockpit or entering layout editing closes pickers.
