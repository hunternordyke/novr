# FS-20 Vortex: integrated VR cockpit

Available in 0.4.7.0. The FS-20 retains its original cockpit cluster, instruments,
tactical display, target/landing camera behavior and warning lights. There is no
movable replacement panel on this aircraft. Seat adjustment and saved console
placements do not move the cluster. Other aircraft retain the shared console.

Release the flight-control grip on the hand you want to use. Either free hand can
point at an enabled control and press trigger, or reach within 3.5 cm and press
trigger or grip. White contextual hints identify the function and current state.
Release before moving to another control. Button presses, grabs and eject actions
use the existing sound and haptic settings.

In 0.4.7.1, interaction hints and the eject-cover label are explicitly white.
The font material also handles alpha-only font atlases correctly, including
after the dynamic atlas rebuilds; these could previously make the text black
despite its light tint. This changes the VR interaction text, not the aircraft's
original painted legends.

## Control locations

Bezel button numbers below run left to right along the **upper row beneath the
wide screen**, or along the **single row immediately beneath each small screen**.
They exclude the rectangular lamps below those rows.

| Existing cockpit part | VR function |
| --- | --- |
| Left gear wheel/lever | Point + trigger toggles; directly grab and move up/down 2.5 cm. Ground gear lock remains active. |
| Upper-left PYLON dial | Next weapon |
| Left PARK switch | Hold for wheel brakes; release to release brakes. It does not latch. |
| Right APU toggle | Engine ignition on/off; the game does not expose a separate APU simulation here. |
| Toggle immediately left of APU | Radar on/off |
| Center AUTO TRIM toggle | Native flight assist |
| Bottom center toggle | Native hover assist; availability/ground restrictions still apply. The original decorative legend remains. |
| Wide-screen upper bezel 1 / 2 | Tactical display span larger / smaller, 5–160 km. Rescales existing radar/optical plot contacts, not the full-screen map. |
| Wide-screen upper bezel 3 | Open full tactical map |
| Wide-screen upper bezel 5 | Navigation lights |
| Wide-screen upper bezel 7 / 8 | AOTS helmet view on/off / return AOTS to head aiming |
| Lower-left screen buttons 1 / 2 | Previous / next weapon |
| Lower-left screen buttons 5 / 6 | Next countermeasure type / toggle linked guns |
| Lower-right screen buttons 1 / 2 | Unlock last target / clear all selected targets |

Unmapped controls remain decorative. JETTISON, ENG FIRE, ground power and IFF are
not assigned unrelated actions. Native gauges/screens retain the game's content;
this build does not add generic seven-page MFDs over them.

## Ejection

A small guarded pull loop sits beneath the existing center pedestal. Lift its
cover with a fresh trigger/grip press, **release**, then grab the loop and pull
toward you at least 4.5 cm. With the pointer, open the guard, release, then press
trigger on the exposed handle. There is no added hold timer. Menus, tracking loss
and leaving the cockpit cancel the interaction. The native JETTISON switch is
not the ejection control. View → Flight actions retains its separate confirmation.

## Setup

View → Cockpit setup → FS-20 cockpit contains the interaction toggle and a short
guide. F6 toggles FS-20 interactions, leaving the original cockpit visible. Edit
handle layout still adjusts the ergonomic stick/throttle positions; it cannot
select or move the aircraft's cluster. Flight bindings, twist yaw, VTOL throttle
thumbstick input, right-bumper magnifier and helmet night-vision gesture remain.

## Implementation and validation

The installed game's FS-20 definition is `SmallFighter1` (`Multirole1` is the
KR-67). Interactions are scoped to the local living pilot in that aircraft's VR
cockpit. Gear, engine, radar, lights, weapons, gun linking and flight-assist actions
use the native input path, preserving its validation and multiplayer commands.

Most FS-20 switches are static islands within a combined native mesh. At runtime,
NOVR creates an instance of that mesh and separates only the mapped moving pieces,
retaining original materials, UVs, vertex colors, normals and tangents. The white
gear stem moves with its black wheel. Actual aircraft state drives toggle/lever
positions; bezel keys depress on press. Native screens and warning components are
never suppressed or paused for the FS-20. Original mesh state restores on exit.
If mesh extraction fails, the original geometry remains with static click targets.

No game meshes/textures are distributed. Coordinates were checked against the
installed 0.34.3 cockpit: 19 distinct controls, 1,586 animated triangles out of
13,292 cockpit triangles; no overlapping selection regions. Private offline
renders checked the native layout and gear travel. Release compilation, input/
ejection checks, private-field compatibility and menu layout checks passed.
These are offline checks, not in-headset or live aircraft validation. The new
switches, grip reach, foreground stereo pass and restoration need the user's test.
