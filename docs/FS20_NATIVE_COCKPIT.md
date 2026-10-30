# FS-20 Vortex: integrated VR cockpit

Version 0.4.7.3 keeps the FS-20's original instrument cluster and adds raised,
labelled physical controls over its existing switch rows. There is no movable
replacement panel. Seat adjustments never move the cluster or flight handles.
Other aircraft retain the shared console.

## Physical interaction

Release the flight handle with the hand you want to use. Either hand works.
Approach a button with the pointing fingertip, touch it, then withdraw before
pressing again. No trigger or grip is needed for buttons. For a switch, reach
close, grip to attach the thumb/index pinch, and flick up or down at least 1.2 cm.
Release to detach. Controller rays cannot operate these FS-20 controls.
Raised housings, chamfered caps and metal stems use cockpit lighting. The caps
reference a plain paint/normal-map region of the installed FS-20 cockpit atlas,
with restrained surface detail; white legends use a separate text material.
Touch, press, grab, switch travel and release have distinct sound/haptic cues.

| Location | Control and operation |
| --- | --- |
| Separate PREV / NEXT keys above each MFD | Touch the desired key; page name appears on the fixed nameplate above |
| Lower switch row, left to right | Gear flick switch; touch-and-hold brake; radar flick switch; AOTS HUD flick switch |
| Between the displays | MAP PAN: slaves physical left thumbstick to the minimap; click stick to recenter; touch again to release |
| Left side tray, below shoulder | THROTTLE POS: unlock throttle, grab/move it, release, then touch again to save and lock |
| Right side tray, below shoulder | STICK POS: independently unlock/move/save the stick |
| Left side tray, six arrow keys | Seat up/down/left/right/forward/back, 2 cm per touch |
| Striped handles beside the seat | Fresh grip, then pull UP at least 4.5 cm to eject; no hold timer |

The position buttons follow handedness. Ground gear/weapon restrictions remain.
Unassigned original switches are decorative; engine, lights, flight assist,
weapon selection and target clearing remain available in View → Flight actions
and through the controller bindings. No decorative JETTISON switch triggers eject.
The View menu retains its separate confirmed ejection action.

## MFDs and cockpit presentation

Both integrated MFDs cycle Weapons, Map, Flight, Engine/Fuel, Systems, Tactical and
Sensor. The left starts on the live minimap, the right on Weapons. The native
widgets/camera feeds are reused; unavailable feeds report NOT FITTED. Selecting a
page already owned by the other display swaps the pages. MAP PAN owns the left
thumbstick while active, preventing accidental VTOL-angle or target input.

The upper native wide display and cockpit geometry remain intact. Duplicate
floating status panels are hidden while the integrated displays are active.
Compact missile/radar warnings, feed and damage information sit off the right
side of the cluster. MFDs and physical controls stay visible while moving handles,
adjusting the seat or opening the setup menu. Flight actions suspend during setup.
The original small caps, knobs and fasteners intersecting the new housings are
hidden using a private copy of the cockpit index buffer. Only 44 measured
decorative islands are omitted; the panel structure, seat and displays remain.
Disabling the integrated controls restores the original mesh. A geometry mismatch
leaves the native mesh untouched. No seat/headrest masking is installed.

## Flight handles and gloves

The FS-20 throttle's rendered copy is 68% of the former size (approximately 16 cm
across). Travel remains independently adjustable, defaulting to 12 cm. The MIL
shelf resolves from the aircraft's runtime engine registry: 90% command on FS-20.
Push through its shelf for afterburner. There are no floating throttle/stick status
captions. A small physical MIL mark remains. The bottom 4.5 cm of the metal stick
shaft and throttle arm fades smoothly to transparent, marking their floating
ends. The upper handles stay opaque. The geometry is prepared once per handle;
original cockpit meshes and shared materials are untouched.

The FS-20 has separate authored joint poses for its throttle and stick. The
throttle palm rests above the handle, fingers curl independently around its front,
and the thumb can reach the side hat. The stick hand wraps below the head with
its heel clear of the palm shelf. The cuff and sleeve align toward the forearm.
Gloves stay attached to the control while held; free hands follow tracking.

Steam Frame capacitive thumbstick, face-button, trigger and grip presence drive
visual finger motion. On the stick, touching A rests the thumb on the red release
button; pressing A moves its pad inward slightly. Thumbstick touch uses a separate
hat pose. Touch alone never becomes a button press. Controllers without capacitive
signals still animate actual presses. These are input-driven poses, not optical
hand tracking. Other aircraft retain their existing grip poses.

## Right-handed weapon bindings

- Trigger: guns, including when a missile/bomb is selected.
- A: selected non-gun weapon release. It cannot fire a selected gun.
- B: next weapon on release while holding the stick; map when the stick is free.
- Right stick click: target selection; X: unlock latest; Y: clear all.
- Right bumper: magnifier, regardless of grip.

Native pilot/menu/cockpit checks, station safety, reloads and launch/lock/salvo
behavior remain in the firing path. Fixed guns can fire alongside a selected
non-gun weapon without changing the selected station. Existing keyboard/HOTAS
inputs retain their ordinary behavior when no VR firing command is active.
Reapply the preset under View → Flight controls → Button bindings if needed.

## Scope and validation

The installed game's FS-20 definition is `SmallFighter1` (not `Multirole1`).
The implementation targets Nuclear Option 0.34.3 and Steam Frame PC streaming.
No proprietary game meshes, textures or assemblies are distributed. Handle meshes
and pilot sleeves are reused from the local installation; authored poses are only
joint rotations and offsets. No per-frame mesh collision solver is added.

Release compilation, input/asset checks, UI lifecycle checks and menu layout
checks accompany the build. Private geometry checks sample vertices, triangle
centers and edge midpoints against the installed control meshes, including thumb
transitions. Offline mission checks use synthetic tracked inputs and inspection
cameras; physical reach, haptic feel and stereo comfort still require headset use.
