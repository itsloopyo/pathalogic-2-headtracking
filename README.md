# Pathologic 2 Head Tracking

![Pathologic 2 running with this mod](https://raw.githubusercontent.com/itsloopyo/pathalogic-2-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for Pathologic 2 that moves the view with your head while your mouse or controller keeps aiming, driven by a webcam, phone, or any OpenTrack compatible tracker, with no VR headset required.

## Features

- **Decoupled look and aim** - head tracking moves the view; weapons and interactions stay on your mouse or controller
- **6DOF positional tracking** - lean, peek and duck with head position, on top of yaw, pitch and roll
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android

## Requirements

- [Pathologic 2](https://store.steampowered.com/app/505230/Pathologic_2/) on Steam. The mod is built and verified against the Steam copy's 64-bit `Pathologic.exe`, and Steam is the only store the installer finds by itself. A copy from another store has not been tested; to try one, pass its install folder to the installer as shown under [Standalone Installer](#standalone-installer).
- A head tracking source that sends the OpenTrack UDP protocol to port 4242: [OpenTrack](https://github.com/opentrack/opentrack) driven by a webcam, a VR headset through SteamVR, or a phone app that speaks the same protocol.
- Windows 10 or 11, 64-bit.

## Installation

### Standalone Installer

No release has been published yet. Until one is, `pixi run package` builds the
same installer ZIP from source (see [Building from Source](#building-from-source)).

1. Download `Pathologic2HeadTracking-vX.Y.Z-installer.zip` from the [Releases page](https://github.com/itsloopyo/pathalogic-2-headtracking/releases).
2. Extract it anywhere.
3. Double-click `install.cmd`. It finds your Steam copy of Pathologic 2, extracts the bundled BepInEx 5 (x64) into the game folder if it is not already there, and places the three mod DLLs in `BepInEx\plugins\`.
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`. See [Setting Up OpenTrack](#setting-up-opentrack).
5. Launch the game. The mod writes `BepInEx\config\com.cameraunlock.pathologic2.headtracking.cfg` on its first run.

If the installer cannot find your game, point it at the install folder yourself.
Either set the environment variable:

```powershell
$env:PATHOLOGIC_2_PATH = "D:\Games\Pathologic"
.\install.cmd
```

or pass the path as the first argument:

```powershell
.\install.cmd "D:\Games\Pathologic"
```

### Manual Installation

To place the files by hand:

1. Install BepInEx 5, x64. Take `BepInEx_win_x64_5.4.23.5.zip` from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases), or use the copy at `vendor\bepinex\BepInEx_win_x64.zip` inside the installer ZIP. Extract it into the game folder so that `winhttp.dll`, `doorstop_config.ini` and a `BepInEx` folder sit next to `Pathologic.exe`.
2. Launch the game once and quit, so BepInEx creates `BepInEx\plugins` and `BepInEx\config`.
3. Copy the three DLLs from the installer ZIP's `plugins\` folder into `BepInEx\plugins\`: `Pathologic2HeadTracking.dll`, `CameraUnlock.Core.dll` and `CameraUnlock.Core.Unity.dll`.
4. Configure your tracker to output UDP to `127.0.0.1:4242`.
5. Launch the game.

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimeters, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### VR Headset Setup

1. Connect the headset to the PC over Air Link, Virtual Desktop or a link cable.
2. Start SteamVR.
3. In OpenTrack, set **Input** to the SteamVR tracker.
4. Leave **Output** on **UDP over network**, host `127.0.0.1`, port `4242`.

### Webcam Setup

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam, with no
markers to wear and no IR hardware to buy. Select it under **Input**, pick your
camera in its settings, and use the output settings above. How well it tracks
depends on your camera and your lighting, so try it before buying anything.

### Phone App Setup

The mod accepts one thing: the OpenTrack UDP protocol on port `4242`. A phone
tracker is usable here if it sends that protocol itself, or ships a PC-side
companion that does. Check your app against that before anything else.

For an app that does send it, what decides the wiring is how much filtering the
app does before the packet leaves the phone. An app that filters on-device can
point straight at this PC's LAN address (run `ipconfig` to find it) on port
`4242`. A raw or lightly filtered feed sent direct will jitter, because the
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, and that app should go through OpenTrack so its filters and
curves can clean the feed up first. The test is quicker than the theory: try
direct, hold your head still, and if the view drifts or shakes, route it through
OpenTrack's **UDP over network** *input* on another port, say `5252`, and let
OpenTrack forward to `127.0.0.1:4242`. Route it through OpenTrack anyway if you
want its curve mapping.

I made [Headcam](https://headcam.app) so decent tracking was free for anybody
with a phone already in their pocket. It filters on-device, so it can send
direct. Any app that filters enough noise works exactly the same way.

Smoothing is chosen per connection from the packet's source address. A phone on
WiFi is a remote connection and gets `RemoteSmoothing`, and so does a tracker
running on this same PC if it sends to the machine's LAN address instead of
`127.0.0.1`, because the classifier sees a transport and not a machine.

### Centering

Centering is done in the tracker: OpenTrack's **Center** bind, SteamVR's reset,
or the CENTER button in your phone app. The mod applies the pose it receives
exactly as it arrives, so zeroing the tracker's output leaves the view where the
game itself puts it.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |
| Toggle aim dot      | `Insert`    | `Ctrl+Shift+U`  |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

`Page Down` / `Ctrl+Shift+H` switches yaw between world-locked (horizon-stable,
the default) and camera-local, which follows the camera's own up axis.

`Insert` / `Ctrl+Shift+U` switches the mod's aim dot on and off. The game's own
interaction prompt keeps following the aim line either way - it is the game's
marker, and leaving it in the middle of a decoupled view would point it at the
wrong thing.

## Configuration

Settings live in `BepInEx/config/com.cameraunlock.pathologic2.headtracking.cfg`,
written the first time the game runs with the mod installed. Every entry carries
its description, its type and its default above it, and its accepted range where
it has one. The file is read once at startup, so restart the game after editing
it. Delete it to reset to defaults. An entry left out of the file falls back to
its default, so an older config keeps working after an update. BepInEx writes
the sections in alphabetical order, which is the order they appear in below.

```ini
[Aim]
## Physics layers a shot is treated as stopping on, by name. This sets the
## depth the reticle is drawn at, so it is an allow-list rather than a
## blacklist.
AimLayers = Default,Doors,Buildings,Indoor_Transparant_Object,Dynamic,Target,Indoor,Indoor Isolated,Dynamic_Sunless,Ragdoll,Key Building,Unimportant Object,Pond,Terrain
## Layers whose trigger colliders also stop a shot - the NPC hit boxes.
AimNpcHitLayers = Npc Hit Colliders

[Collision]
## Cut a lean back to whatever the level leaves room for, so the view never
## ends up inside a wall. No effect in rotation-only mode. Off by default until
## the sweep has been seen engaging on a real wall in this game.
CollisionEnabled = false
## Distance in meters the eye is held off a surface. Floored at runtime at one
## and a half times the camera's near clip plane, which is 0.025 m here, so
## this value is what decides the standoff. Maximum 0.6.
CollisionMargin = 0.15
## How fast the lean opens back up once an obstruction clears. 0.9 is a 200ms
## time constant. Tightening is never smoothed.
CollisionReleaseSmoothing = 0.9
## Physics layers the lean sweep treats as solid, by name
CollisionLayers = Default,Doors,Buildings,Indoor,Indoor Isolated,Key Building,Terrain,InvisibleWall

[Diagnostics]
## One line a second carrying the applied pose, lean, aim distance and reticle
## offset together
LogAimGeometry = false
## Peak frame gaps, mod callback durations and garbage collections every ten seconds
LogFrameTiming = false
## The camera set, the physics layer table and the HUD element the prompt is
## moved through, once per world load
LogSceneSurvey = false

[General]
## Whether head tracking is enabled when the game starts
EnabledOnStartup = true
## Whether to show a notification when the plugin initializes
ShowStartupNotification = true
## Yaw mode: true = horizon-locked yaw (default), false = camera-local
WorldSpaceYaw = true

[Keybindings]
## Unity KeyCode names, so PageUp rather than Page Up
ToggleKey = End
CycleTrackingModeKey = PageUp
YawModeKey = PageDown
ToggleReticleKey = Insert

[Network]
## UDP port to listen for OpenTrack data (1024-65535)
UDPPort = 4242

[Position]
## Positional tracking: lean in and out, peek side to side, duck
PositionEnabled = true
PositionSensitivityX = 1
PositionSensitivityY = 1
PositionSensitivityZ = 1
## Movement envelope in meters
PositionLimitX = 0.3
PositionLimitY = 0.2
PositionLimitZ = 0.4
PositionLimitZBack = 0.1
## Distance from the neck pivot to the tracked point on your face, in meters.
## Leave at 0 unless your tracker sends a neck-pivoted position it does not
## already correct itself.
TrackerPivotForward = 0

[Sensitivity]
## Multipliers for head rotation (0.1-3.0)
YawSensitivity = 1
PitchSensitivity = 1
RollSensitivity = 1

[Smoothing]
## Tracker running on this machine (loopback). 0 = none, 1 = heavy.
## 0.0 is a 20ms time constant and 1.0 is a 10s one. Both hold at any frame rate.
LocalSmoothing = 0
## Tracker on a remote network device. 0 = none, 1 = heavy.
RemoteSmoothing = 0.15

[UI]
## Toast when the OpenTrack connection is lost or restored
ShowConnectionNotifications = true
## Draw a dot where the line of the shot stops. Pathologic 2 draws no crosshair
## of its own, so with the view decoupled from aim there is otherwise nothing
## marking where a revolver, rifle or shotgun is pointed.
ShowReticle = true
```

### Field of view

Set the field of view with the game's own slider in its Settings screen, which
covers 40 to 100 degrees with 60 as the default. The mod reads it every frame,
so a change takes effect straight away without a restart.

The game also narrows and widens the camera on its own during scripted moments.
A narrower view magnifies everything on screen, head tracking included, so
without correction the same head movement would swing the picture further for as
long as one of those is running. The mod scales the pose to cancel that out,
which keeps head movement worth the same amount of screen at any field of view.
Head tilt is left alone, because a tilt rotates the picture by its own angle
whatever the field of view is.

## Troubleshooting

BepInEx's log is the first thing to read: `BepInEx\LogOutput.log` in the game
folder, written whenever `Enabled` is `true` under `[Logging.Disk]` in
`BepInEx\config\BepInEx.cfg`.

**Mod not loading:**

- Confirm `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder sit next to `Pathologic.exe`, in the game root.
- Confirm you took the **x64** BepInEx 5 build. The x86 one silently fails to load into a 64-bit process, and the BepInEx 6 IL2CPP builds are for a runtime this game does not use.
- Confirm all three DLLs are in `BepInEx\plugins\`: `Pathologic2HeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`.

**No tracking response:**

- Check OpenTrack is running with output set to UDP `127.0.0.1:4242`, and that the port matches `UDPPort`.
- The log writes `No tracker data on UDP port 4242` when nothing has reached the mod in the fifteen seconds after it takes the port. That line means the mod is listening and the packets are not arriving, so look at the tracker, the port and the firewall rather than at the game.
- Press `End` (or `Ctrl+Shift+Y`) to toggle tracking on.
- Check your firewall is not blocking UDP port `4242`.
- Tracking applies in ordinary gameplay only. It stands down in the menus, the inventory, the map, the mind map, trade, lock picking, sleep, dialogue, cutscenes and while dead, and it starts about a second and a half after a level loads.
- If the view sits off to one side, center it in your tracker.

**The dot is not where the bullet lands:**

- The dot marks the surface a straight ray from the aim camera stops on, and it is drawn at that surface's true depth rather than at a fixed range. Firearms in this game scatter their shots by weapon condition, so a worn revolver will not put every round on it.

**The game window moved when I launched:**

- By design, and only once per launch. If the game opens windowed and off-center, the mod centers it on the work area of the monitor it is already on. A fullscreen game, a window that already fills the screen, and a window the game placed sensibly itself are all left alone.

**Jittery or unstable tracking:**

- Raise `RemoteSmoothing` for a phone or other network device, or `LocalSmoothing` for a tracker running on this PC. Each value covers rotation and position alike.
- Route a phone app through OpenTrack so its filters and curves clean the feed up before it reaches the game.
- Improve your lighting for webcam tracking.

**Yaw feels wrong when looking up or down at extreme angles:**

- Toggle between world-locked and camera-local yaw with `Page Down` (or `Ctrl+Shift+H`). World-locked, the default, is horizon-stable; camera-local follows the camera's current up axis.

**Leaning still puts the view through a wall:**

- `CollisionEnabled` ships `false`. Set it to `true` to have the mod cut a lean back to whatever the level leaves room for. The log lists the collision layer names it matched in this build and warns about any it could not, and writes one line the first time the sweep runs.
- Raise `CollisionMargin`.

## Updating

Download the new release and run `install.cmd` again. Your config is preserved.

## Uninstalling

Run `uninstall.cmd`. This removes the mod DLLs. BepInEx is only removed if this
mod's installer put it there. To remove it anyway:

```powershell
.\uninstall.cmd /force
```

## Building from Source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (any recent version)
- [pixi](https://pixi.sh) task runner

The build compiles its Unity and BepInEx references from checked-in stub
sources, so it needs no game install.

```bash
git clone --recurse-submodules https://github.com/itsloopyo/pathalogic-2-headtracking.git
cd pathalogic-2-headtracking

pixi run build      # build the mod (Release)
pixi run install    # build and deploy into the game folder
pixi run package    # create the installer release ZIP
```

## Community & Support

- [Discord](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch of head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your phone into a head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

The MIT license covers the code in this repository. It does not cover the
bundled third-party components, or any Pathologic 2 footage in `assets/`, which
belongs to the game's developer. See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the full breakdown.

## Credits

- [Ice-Pick Lodge](https://ice-pick.com/) - developer of Pathologic 2
- [tinyBuild](https://tinybuild.com/) - publisher of Pathologic 2
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity mod loader
- [OpenTrack](https://github.com/opentrack/opentrack) - head tracking software and UDP protocol
- [cameraunlock-core](https://github.com/itsloopyo/cameraunlock-core) - shared head tracking pipeline

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Ice-Pick Lodge or
tinyBuild. Use at your own risk.
