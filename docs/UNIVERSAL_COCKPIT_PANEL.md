# Universal cockpit console

NOVR 0.4.5.0 adds one shared 3D console for every aircraft. It appears low in front
of the seated pilot and stays fixed in cockpit space as the headset moves. Its
layout, position, tilt and size are shared across aircraft. Existing per-aircraft
seat offsets still apply.

## Displays

The left display receives the game's live weapon, countermeasure, power and
grid-reference widgets. The right display receives its live minimized map.
These are the existing UI objects, not simulated readings or a second map render.
The original transforms are restored when the console hides, when leaving the
cockpit, and before expanding the tactical map. Aim markers, flight-path vector,
pitch ladder and other world-aligned symbology stay in the forward view. Incoming
missile alerts also remain forward-facing and override HUD declutter while active.
The weapon display explicitly shows **SAFE** when the native weapon safety lock
is engaged; it never changes the aircraft’s actual safety state.

## Controls

Release the flight-control grip on the hand you want to use. The default right
hand can aim a laser at the console and use its trigger. Either free hand can
interact directly by coming within 5.5 cm of a control and pressing **trigger or grip**.
Use grip to hold the motion flight stick and throttle.

Version 0.4.5.5 gives a nearby console control priority for that hand. The other
free hand can keep pointing, and both hovered controls highlight. Once one hand
holds a control, it owns console input until released. Release the buttons before
moving to another control; arriving with a held press does not activate it.
If squeezing trigger and grip together, release both before a new grab.

Hints describe the current interaction: pinch/grab instructions when close,
trigger instructions when pointing, and progress during a laser eject pull.
These hints take priority over entry messages and tracking notices for the other
hand. Result messages such as **GEAR LOCKED ON GROUND** remain visible for the
control that produced them. Point at the front of the console to use its laser controls.

| Control | Laser | Direct hand |
| --- | --- | --- |
| Gear lever | Trigger toggles gear | Hold trigger or grip on the wheel-shaped handle, then move up/down at least 2.5 cm |
| Radar, engine, flight assist | Trigger toggles | Trigger or grip near the toggle |
| Next WPN | Trigger cycles weapon | Trigger or grip near the key |
| HOV bezel key | Trigger toggles native hover assist | Trigger or grip near the key |
| CM bezel key | Trigger cycles countermeasure type | Trigger or grip near the key |
| + / − navigation keys | Trigger changes minimap zoom, centered on the aircraft | Trigger or grip near the key |
| MAP | Trigger expands tactical map | Trigger or grip near the key |
| Red eject guard | Trigger lifts/closes cover | Trigger or grip near the cover |
| Yellow eject handle | After opening the cover and releasing trigger, hold trigger on the handle for 0.75 s | After opening the cover and releasing your press, hold trigger or grip on the handle and pull it toward you at least 4.5 cm |

Version 0.4.5.6 measures direct gear and eject travel in physical centimeters,
independent of console size or tilt. Pull the handle out toward you, perpendicular
to the panel face; sideways motion and pushing away do not count as an eject pull.

A completed pull shows **EJECT REQUESTED** while the game processes it, then
**EJECTION STARTED** only after the aircraft confirms it. The console reserves
input during that request so another hand cannot replace it with a different
action. If the game does not accept it, the guard closes and the hint briefly
shows **EJECT NOT CONFIRMED**. Release, reopen the guard and make another deliberate
pull to retry. It never retries automatically.

Ejection requires a fresh press after opening the cover. Leaving the handle during
a laser hold cancels the pull. Pausing, menus, tracking/focus loss, recentering and
aircraft changes reset the guard. Aircraft without switchable ordinary flight assist show an explicit unavailable
hint and a centered ASSIST lever; the wrist action is disabled with an explanation.
The game still applies its own flight-input,
G-LOC, gear and radar restrictions. Gear lights show down/locked in green,
transition in amber, and otherwise go dark; they represent the game's aggregate
gear state, not separate sensors for three wheels. The switch positions follow
actual aircraft state. A radar action does nothing if that aircraft has no radar.

The native radial wheel still has no dedicated motion-controller binding. The
console and wrist Flight page supply the supported actions, including ejection.

## Hover assist

Version 0.4.5.7 adds a **HOV** key below CM, with a green lamp that follows the
actual native hover-assist state. It is separate from the ordinary ASSIST toggle.
Point and press trigger, or reach and press trigger/grip. You can also use
**View → Flight actions → Hover assist**. The wrist button shows the current
state, or explains **not fitted** / **ground locked** when unavailable.

Hover assist must be fitted to the aircraft and requires at least 1 m of ground
clearance. The game still applies its native input restrictions. Hover behavior,
automatic thrust-vector changes and restoration of ordinary flight assist remain
under the game’s control.

While hover assist is active, the motion handle reads **HOVER INPUT**. Its position
and percentage follow the pilot’s climb/descent command, independently of the engine power
chosen by the hover controller. Moving it forward increases the command; releasing
and regrabbing preserves that command. The ordinary readout is **COLLECTIVE** on
aircraft using collective input, or **THROTTLE** otherwise. The current handle
still moves along the same forward/back rail in every aircraft.

## Position and fallback

Open **View → Flight actions → Console setup** to adjust position in 2 cm steps,
tilt in 5-degree steps, or size in 5% steps. Adjustments preview live on the console;
its flight controls are inactive until the wrist menu closes.
**Reset console** restores the shared defaults. **F6** shows/hides the console;
hiding it restores the previous forward status UI and minimap. The console is
independent of F7's motion-stick/throttle enable setting.

Defaults: 77 cm wide, 36 cm tall, centered 32 cm below and 65 cm forward of the
recentered seat, tilted upward 20 degrees. Settings live in
`[Universal Cockpit Panel]` in the NOVR configuration.

## Model and implementation

The original model is authored by `tools/build_cockpit_panel.py` in Blender. It
exports GLB, a Blender source file, a preview, and the mesh data embedded in NOVR.
The game needs neither Blender nor a runtime GLB importer. Geometry batches by
material and animated group; it adds no physics colliders or extra map-rendering
camera. The panel uses NOVR's projected UI layer, so it stays visible over the
underlying aircraft dashboard; it is not a replacement for the aircraft model.

The design takes general inspiration from the grouped cockpit controls in the
[USAF F-16A/B flight manual](https://www.aahs-online.org/resources/e-library/fm/USAF-F16A_B-Flight-Manual.pdf)
and the striped emergency handles described in
[NASA's F-111 cockpit reference](https://www.nasa.gov/image-article/f-111-advanced-fighter-technology-integration-2/).
It is an original, simplified universal layout, not a replica of either aircraft.
No military/game model or texture is included. Direct trigger interaction and
contextual hints follow the approach described in the
[VTOL VR manual](https://cdn.steamstatic.com/steam/apps/667970/manuals/VTOL_VR_Manual.pdf?t=1649333292).

## Validation status

68 automated checks pass, including pitch polarity, diagonal stick input,
guarded-ejection transitions, embedded mesh integrity and 45 Harmony target
metadata checks against Nuclear Option 0.34.3. The game builds with no errors.
22 live checks in a disposable offline FS-12 mission verify initialization, widget
docking/restoration (including fonts and anchors), map zoom/expand/restore, native
weapon and countermeasure selection, radar/engine/assist switches, the ground gear
restriction, pause cancellation and guarded ejection. Ejection also verifies the
camera handoff to the pilot's helmet point after native asynchronous setup.
For 0.4.5.5, 32 further checks drive the production panel with simulated controller
poses and button values. They cover both hands, direct trigger/grip input, native
weapon/CM/engine/assist actions, the visible pointer, control hints, tracking
recovery, handedness, scaled/tilted positioning, map transitions, the ground gear
restriction and a full direct-trigger guarded eject pull. The previous build
reproduced six interaction failures; all six pass with these changes.
For 0.4.5.6, 38 additional controller-driven checks exercise fixed physical gear
and eject travel across five size/tilt combinations, reject sideways motion and
pushes away from the pilot, verify pending/unconfirmed/accepted feedback, protect a request from a
simultaneous other-hand press, and complete native ejection. The preceding build
failed 15 assertions in this sequence; the updated build passes all 38. These
checks wait for an established cockpit before beginning the interactions. The
32 earlier controller-interaction checks were also repeated and passed on 0.4.5.6.
For 0.4.5.7, 81 checks across the SAH-46 (31), VT-7 (30) and FS-12 (20) verify
widget docking/restoration, map transitions, available/unavailable hover and
ordinary-assist controls, native hover activation through direct, laser and wrist
input, actual-state lamps, stable command readouts, release/regrab and all four
negative-range/inverted-collective setting combinations. Full-scale captions fit
their backplates and the wrist capability labels fit their buttons. These tests
use simulated hand and altitude conditions with the aircraft held stationary;
they validate input and UI behavior, not hover flight handling. The prior 18
flight-grip/menu and 32 controller-console checks also passed again on 0.4.5.7.
Live fixed-camera captures verify the model, readable weapon/countermeasure
widgets and minimized map. These captures use a
temporary diagnostic camera; they do not establish stereo comfort or physical
controller reach. In-headset placement, hand interaction and flight feel still
require hands-on validation. The studio preview has blank screens; the displays
populate in the game.

If the embedded model cannot load, NOVR keeps the original HUD and reports one
error instead of retrying every frame. Toggle F6 off/on to retry after resolving
the problem. No diagnostic tool is included in the release package.
