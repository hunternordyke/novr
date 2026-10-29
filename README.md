# [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR

NOVR is a reworked version of [UUVR](https://github.com/Raicuparta/uuvr) designed and optimized for Nuclear Option.

## Experimental motion controls fork

NOVR Motion Prototype **0.4.6.3** adds a tracked-controller stick and throttle,
laser pointers, combat bindings, a wrist menu and saved seat/handle positions.
The shared 3D console combines seven selectable MFD pages with physical gear,
radar, engine, flight-assist and guarded eject controls. View → Edit cockpit layout
lets either hand move the panel, stick and throttle independently. Native aircraft
handle models replace the stationary originals while active; helicopter collective
defaults are lower than the stick. A compact forward HUD reduces visual clutter.

VTOL-style bindings use stick trigger/action for fire/weapon, and throttle
trigger/action for brakes/countermeasures. Target select, unlock-last and clear-all
are grip-gated. Physical right bumper opens a fixed-depth inspection magnifier
with either grip released or held; it does not change headset stereo projection.

Optional tap-to-latch grips, adjustable control response, a live binding guide,
wheel/air brakes and readable handle captions support flying without a keyboard.
The **HOV** key and wrist action operate native hover assist; its lamp follows
actual aircraft state. Throttle/collective readouts preserve the pilot’s command
while hover assist adjusts engine power. Unavailable controls explain why.
Native target/landing-camera zoom and magnification are preserved.

Version 0.4.6.1 adds per-aircraft dashboard anchoring and clearance fitting,
a MIL detent, native VTOL nozzle-angle control, left-stick MFD map panning and a
brow-trigger night-vision gesture. Rigged Valve gloves and locally reused pilot
sleeves add visible hands, with adjustable wrist alignment and slimmer arms.
Interaction sounds and haptics accompany physical controls. Either hand can use
the menu/panel pointer. Default throttle travel is 12 cm; the VT-7 panel starts
25% larger before clearance fitting. Seat changes leave cockpit controls in place.
Hand, sleeve, HUD and feedback settings live under **View → Comfort & display**.

Version 0.4.6.2 makes the View menu steady by default, keeps its buttons clear
of the opaque console, and separates the controller guide by hand. In Console
setup an outline previews placement. Resetting saved seat/handle positions now
requires a specific confirmation. Press either MFD's **PAGES** title to choose a display immediately;
unsupported pages are marked **NOT FITTED**, and choosing the other MFD's current
page swaps them. The physical previous/next keys still work.

Version 0.4.6.3 reduces full throttle/collective travel to **12 cm**, including the
MIL shelf, with 1 cm adjustment steps. Free hands default to palms vertical and
thumbs up. Held gloves wrap around and stay attached to the moving stick, throttle
or collective; the pilot sleeves solve toward those wrists. Releasing a handle
restores controller tracking. View opens a Home page with grouped settings and
consistent Back/Home/Close navigation. **Comfort & display → Hands & arms → Hand
alignment** adjusts the free-hand pose; **Flight controls** adjusts travel.

Steam Frame **PC streaming through SteamVR** is the binding target, with a
Touch-compatible fallback. The build targets Nuclear Option **0.34.3**.
Offline input and display checks are documented in the guides below; physical
controller reach, stereo comfort and flight feel still need headset evaluation.

- **F6** shows/hides the console; hiding it restores the original status HUD.
- **F7** enables/disables motion flight controls (off initially).
- Hold grip near the stick, then tilt your wrist for pitch/roll. Twist it for rudder/nosewheel steering; horizontal stick-hand thumbstick yaw is also available.
- Hold grip near the throttle, then move forward/back. In helicopters, lift/lower the hinged collective instead. Releasing retains the command with neutral physical throttle input.
- Hold the throttle and squeeze its trigger to brake with the VTOL-style preset. Choose throttle layouts from **View → Flight controls → Button bindings**.
- **F8** enables motion controls and places the handles at your hands. Release both handles first; holding both thumbstick clicks for 0.8 seconds also places them when motion controls are enabled.
- **View** on the throttle hand, or **F9**, opens the wrist panel. Enable motion controls there without a keyboard.
- [Universal cockpit console: controls, displays and positioning](docs/UNIVERSAL_COCKPIT_PANEL.md).
- [Steam Frame bindings, installation, and testing](docs/STEAM_FRAME.md).
- [Motion-control setup and projection details](docs/MOTION_CONTROLS.md).

Use the experimental package built from this branch for these features. The upstream
installer and upstream release links below install the original mod.

Build the mod and patcher without changing your game installation:

```sh
dotnet build NOVR/NOVR.csproj -c Release -p:DeployToGame=false
dotnet build NOVR.Patcher/NOVR.Patcher.csproj -c Release -p:DeployToGame=false
dotnet run --project tests/NOVR.Tests.csproj -c Release
```

These individual projects and tests can use .NET SDK 8; the full solution's packaging
project targets .NET 9 and requires a compatible SDK. Build output is staged under
`build-output/game/BepInEx`. Game assemblies are read from your own installation.

## User Installation

### Recommended: GUI installer

1. Close Nuclear Option before installing or updating the mod.
2. Download the latest installer from the [NOVR releases page](https://github.com/InfernoSuperNova/novr/releases/latest):
    - **Windows:** `NOVR.Installer-Win.exe`
    - **Linux/Proton:** `NOVR.Installer-Linux`
3. Run the installer.
    - On Linux, you may need to make it executable first: `chmod +x NOVR.Installer-Linux`.
4. If Nuclear Option is not found automatically, choose the game folder manually.
    - The selected folder must contain `NuclearOption_Data/Managed`.
5. Click **Install**.
6. Launch Nuclear Option from Steam.

The installer can also update, repair, or uninstall NOVR after it is installed.

### What the installer does

The installer:

- Finds your Nuclear Option install.
- Installs BepInEx 5.x if it is missing.
- Downloads the latest NOVR release zip.
- Installs NOVR into:
    - `BepInEx/plugins/NOVR`
    - `BepInEx/patchers/NOVR`
- Writes the installed NOVR version to `BepInEx/plugins/NOVR/version.txt`.
- On Linux/Proton, attempts to configure the `winhttp` Wine override needed by BepInEx.

Use BepInEx 5.x only. Do not use BepInEx 6.x unless the project explicitly says it is supported.

### Manual zip install

Use this only if the installer does not work for your setup.

1. Close Nuclear Option.
2. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases/latest) into the Nuclear Option game folder.
    - After installing BepInEx, this folder should exist: `Nuclear Option/BepInEx/core`.
3. Download `NOVR.zip` from the [latest NOVR release](https://github.com/InfernoSuperNova/novr/releases/latest).
4. Extract the contents of `NOVR.zip` into `Nuclear Option/BepInEx`.
5. Confirm these files exist:

    ```text
    Nuclear Option/BepInEx/plugins/NOVR/NOVR.dll
    Nuclear Option/BepInEx/patchers/NOVR/NOVR.Patcher.dll
    ```

6. Launch Nuclear Option from Steam.

`NOVR.zip` contains `plugins` and `patchers` folders. Extract it into the existing `BepInEx` folder, not directly into the game root.

### First launch behavior

On game startup, the NOVR BepInEx patcher copies required XR support files into `NuclearOption_Data`. These files may be overwritten every time the game starts.

If the game is already running while installing or rebuilding, Windows may prevent those files from being replaced. Close Nuclear Option before installing, updating, or building the mod.

### Linux/Proton notes

The installer tries to set the required `winhttp` override automatically. If BepInEx does not load under Proton, configure the game's Wine prefix manually so `winhttp` uses `native,builtin`.

See the [BepInEx running under Proton guide](https://docs.bepinex.dev/articles/advanced/proton_wine.html) for the manual setup.

## Building

These steps are for developers building NOVR from source.

1. Install a .NET/MSBuild toolchain that can build SDK-style .NET Framework 4.8 projects.
    - **Windows:** Visual Studio 2022 or Build Tools for Visual Studio 2022 with the **.NET Framework 4.8 targeting pack** installed.
    - **Linux:** the .NET SDK plus Mono/MSBuild and .NET Framework reference assemblies. Distro package names vary, but you usually want packages such as `dotnet-sdk`, `mono`, `msbuild`/`mono-msbuild`, and `mono-reference-assemblies`/`.NET Framework 4.8 reference assemblies`.
2. Have Nuclear Option installed at:
    - **Linux:** `~/.steam/steam/steamapps/common/Nuclear Option` OR `~/.steam/debian-installation/steamapps/common/Nuclear Option` OR `~/.local/share/Steam/steamapps/common/Nuclear Option`
    - **Windows:** `C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option` OR `C:\Program Files\Steam\steamapps\common\Nuclear Option` OR `D:\SteamLibrary\steamapps\common\Nuclear Option`

    If none of these options work for you, set the `NUCLEAR_OPTION_GAME_DIR` environment variable or pass `/p:NuclearOptionGameDir="path\to\Nuclear Option"` when building.
3. Ensure BepInEx 5.x is installed inside the Nuclear Option directory.
4. Build the `Release` configuration from your IDE of choice. JetBrains Rider is tested; Visual Studio should work too. To build from the command line, run this from the project root:

    ```bash
    dotnet build NuclearOptionVirtualRealityMod.sln -c Release
    ```

    The build output is written under `build-output`, as well as copied directly to the BepInEx directory.

## Installer development

The GUI installer is built with Avalonia. Building it in `Release` automatically publishes single-file launchers for Linux and Windows into `dist/`:

```bash
dotnet build NOVR.Installer/NOVR.Installer.csproj -c Release
```

Outputs:

- `dist/NOVR.Installer-Linux`
- `dist/NOVR.Installer-Win.exe`

Building the full solution in `Release` also creates `dist/NOVR.zip`, ready to upload as the mod release asset.

## License

    Nuclear Option VR Mod
    Copyright (C) 2026 InfernoSuperNova (DeltaWing)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

# Original README below

> # Universal Unity VR
>
> [![Raicuparta's VR mods](https://raicuparta.com/img/badge.svg)](https://raicuparta.com)
>
> Use [Rai Pal](https://pal.raicuparta.com) to install this mod.
>
> ## License
>
>     Rai Pal
>     Copyright (C) 2024  Raicuparta
>
>     This program is free software: you can redistribute it and/or modify
>     it under the terms of the GNU General Public License as published by
>     the Free Software Foundation, either version 3 of the License, or
>     (at your option) any later version.
>
>     This program is distributed in the hope that it will be useful,
>     but WITHOUT ANY WARRANTY; without even the implied warranty of
>     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
>     GNU General Public License for more details.
>
>     You should have received a copy of the GNU General Public License
>     along with this program.  If not, see <https://www.gnu.org/licenses/>.
