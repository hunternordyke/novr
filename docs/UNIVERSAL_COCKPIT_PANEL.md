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
interact directly by coming within 5.5 cm of a control and squeezing grip.

| Control | Laser | Direct hand |
| --- | --- | --- |
| Gear lever | Trigger toggles gear | Grip the wheel-shaped handle, then move up/down at least 2.5 cm |
| Radar, engine, flight assist | Trigger toggles | Grip near the toggle |
| Next WPN | Trigger cycles weapon | Grip near the key |
| CM bezel key | Trigger cycles countermeasure type | Grip near the key |
| + / − navigation keys | Trigger changes minimap zoom, centered on the aircraft | Grip near the key |
| MAP | Trigger expands tactical map | Grip near the key |
| Red eject guard | Trigger lifts/closes cover | Grip near the cover |
| Yellow eject handle | After opening the cover and releasing trigger, hold trigger on the handle for 0.75 s | After opening the cover and releasing grip, grab the handle and pull it toward you at least 4.5 cm |

Ejection requires a fresh press after opening the cover. Leaving the handle during
a laser hold cancels the pull. Pausing, menus, tracking/focus loss, recentering and
aircraft changes reset the guard. The game still applies its own flight-input,
G-LOC, gear and radar restrictions. Gear lights show down/locked in green,
transition in amber, and otherwise go dark; they represent the game's aggregate
gear state, not separate sensors for three wheels. The switch positions follow
actual aircraft state. A radar action does nothing if that aircraft has no radar.

The native radial wheel still has no dedicated motion-controller binding. The
console and wrist Flight page supply the supported actions, including ejection.

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
No military/game model or texture is included.

## Validation status

58 automated checks pass, including pitch polarity, diagonal stick input,
guarded-ejection transitions, embedded mesh integrity and 45 Harmony target
metadata checks against Nuclear Option 0.34.3. The game builds with no errors.
22 live checks in a disposable offline FS-12 mission verify initialization, widget
docking/restoration (including fonts and anchors), map zoom/expand/restore, native
weapon and countermeasure selection, radar/engine/assist switches, the ground gear
restriction, pause cancellation and guarded ejection. Ejection also verifies the
camera handoff to the pilot's helmet point after native asynchronous setup.
Live fixed-camera captures verify the model, readable weapon/countermeasure
widgets and minimized map. These captures use a
temporary diagnostic camera; they do not establish stereo comfort or physical
controller reach. In-headset placement, hand interaction and flight feel still
require hands-on validation. The studio preview has blank screens; the displays
populate in the game.

If the embedded model cannot load, NOVR keeps the original HUD and reports one
error instead of retrying every frame. Toggle F6 off/on to retry after resolving
the problem. No diagnostic tool is included in the release package.
