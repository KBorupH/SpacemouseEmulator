# SpaceMouse Pilot

> Turn your 3Dconnexion SpaceMouse into a virtual Xbox controller.

SpaceMouse Pilot reads your SpaceMouse directly over raw HID and outputs it as a standard Xbox 360 controller — no middleware, no extra drivers beyond ViGEmBus. Any game or application that accepts controller input will see it as a real gamepad.

The 6-axis input maps naturally to flight controls — tilt to bank, twist to yaw, push up and down for throttle — making it a strong fit for flight-heavy games. Pair it with a keyboard, mouse, or any other input device for everything else.

---

## Requirements

- Windows 10/11 x64
- A 3Dconnexion SpaceMouse (USB HID)
- [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases/latest) — free, ~2MB, one-time install

---

## Installation

1. Install the [ViGEmBus driver](https://github.com/nefarius/ViGEmBus/releases/latest)
2. Download the latest `SpaceMousePilot_Setup_x.x.x.exe` from [Releases](../../releases/latest)
3. Run the installer

---

## Getting Started

**Before clicking Start Bridge**, right-click the 3Dconnexion tray icon and choose **Exit**. SpaceMouse Pilot reads the device directly — the 3Dconnexion driver will block it if left running.

1. Launch **SpaceMouse Pilot** from the Start menu or desktop shortcut
2. Click **Start Bridge** — both status indicators should turn green
3. Launch your game and configure it to use controller input

Closing the window minimises to the system tray. Right-click the tray icon to quit.

---

## Calibration

For best results, calibrate to your specific device before playing:

1. Start the bridge first
2. Click **Calibrate axes**
3. Push the device to its physical limits on all axes during the countdown — tilt fully in every direction, twist each way, push up and down
4. Bars turn green as each axis is recorded
5. Settings save automatically when done

---

## Settings

| Tab | What it does |
|---|---|
| **Roll / Pitch / Yaw / Collective** | Per-axis sensitivity, deadzone, response curve, scale, invert |
| **Mapping** | Assign each axis and button to a gamepad output |
| **Performance** | Bridge poll rate, UI refresh rate, calibration duration |

Hit **Save settings** to persist across sessions.

---

## Default Axis Mapping

| SpaceMouse | Gamepad | In-game |
|---|---|---|
| Tilt left/right (Roll) | Right stick X | Bank / Aileron |
| Tilt forward/back (Pitch) | Right stick Y | Pitch / Elevator |
| Twist (Yaw) | Left stick X | Rudder / Yaw |
| Push up/down (Collective) | Left stick Y | Throttle / Collective |

---

## Tips

- **Too twitchy?** Lower sensitivity and raise the curve — higher curve gives more precision near centre
- **Drifting at rest?** Increase deadzone on the affected axis, or recalibrate
- **3DxWare blocking the device?** Exit it fully from the system tray, not just the window
- **Bridge won't start?** Ensure ViGEmBus is installed and SpaceMouse is plugged in

---

## FAQ

**What games does this work with?**
Any game or application that accepts XInput / Xbox controller input on Windows.

**Will this work alongside other controllers?**
Yes — it appears as an additional controller. Other devices continue to work normally.

**Do I need SpaceMouse Pilot running while playing?**
Yes — minimised to tray is fine, but it must be running to maintain the virtual controller.

**Can I use the SpaceMouse for other things at the same time?**
Not while the bridge is running. Click **Stop Bridge** to release the device back to 3DxWare.

**Will this cause issues with anti-cheat?**
Low risk. The virtual controller is identical to a real Xbox 360 controller. ViGEmBus is a signed Windows driver used by Steam Input and millions of accessibility tools.

## Versions

Releases follow this pattern:

| Version | What it means | Recommended for |
|---|---|---|
| `0.x.x-alpha.x` | Early development, expect bugs | Testing only |
| `0.x.x-beta.x` | Feature complete, being stabilised | Adventurous users |
| `0.x.x` | Stable pre-1.0 release | Most users |
| `1.0.0`+ | Production stable | Everyone |

**If you just want it to work** — always pick the latest release without `alpha` or `beta` in the version. If no stable release exists yet, the latest `beta` is the safest pre-release option.

**If you want to help test** — `alpha` builds are the most recent but may have rough edges. Feedback and bug reports are welcome via [Issues](../../issues).

---

## For Developers

### Stack

- **.NET 10 / WPF** — UI
- **CommunityToolkit.Mvvm** — MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **HidSharp** — raw HID device access (no 3DxWare dependency)
- **Nefarius.ViGEm.Client** — official .NET ViGEmBus client
- **MinVer** — version from git tags, no manual version file
- **Inno Setup** — installer

### Project Structure

```
SpaceMousePilot/
├── Models/              — Pure data (AppConfig, AxisConfig)
├── Services/            — BridgeService, ConfigService, Logger (no UI dependency)
├── ViewModels/          — MainViewModel, AxisViewModel, MeterViewModel, ButtonMappingViewModel
├── Views/               — MainWindow.xaml (pure binding), converters, dialogs
├── Controls/            — AxisMeter (custom WPF element)
├── Themes/              — Dark.xaml resource dictionary
├── Assets/              — icon.ico
├── installer/           — spacemouse_pilot.iss
├── App.xaml/cs          — Single instance, named pipe IPC, system tray
├── AppVersion.cs        — Reads version from assembly (set by MinVer via git tag)
├── build.ps1            — dotnet publish + Inno Setup
├── release.ps1          — Prompt for version → tag → build → GitHub release
└── CHANGELOG.md         — Patch notes per version
```

### Building

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
.\build.ps1
```

Output: `dist\installer\SpaceMousePilot_Setup_x.x.x.exe`

### Releasing

```
git tag v0.1.0-alpha.1
git push origin v0.1.0-alpha.1
```

The `.github/workflows/release.yml` workflow triggers on any `v*` tag push. It builds a self-contained exe, compiles the Inno Setup installer, and publishes a GitHub release with the installer attached. No local tooling needed beyond git.

Requires [GitHub CLI](https://cli.github.com/) — run `gh auth login` on first use.

### Versioning

Version is driven entirely by git tags. To release `0.1.0-alpha.2`:

```
git tag v0.1.0-alpha.2
git push origin v0.1.0-alpha.2
```

The GitHub Actions workflow picks up the tag, builds, and publishes the release automatically. No local release script needed.

Before tagging, add an entry to `CHANGELOG.md`:

```markdown
## 0.1.0-alpha.2
- Fixed calibration not saving on exit
- Improved deadzone precision
```

The version header must match the tag exactly (without the `v`). Bullets should be short and user-facing. Newest version goes at the top. The release workflow reads the matching entry and uses it as the GitHub release body automatically.

### Architecture Notes

**HID format detection** — The SpaceMouse Compact and newer devices send all 6 axes in a single 12-byte Report 1 (modern format). Older devices split translation and rotation across Report 1 and Report 2. The bridge auto-detects on first packet and logs which format was chosen.

**MVVM** — `BridgeService` has no WPF dependency and communicates upward via events only. `MainViewModel` subscribes and marshals back to the UI thread via `Dispatcher.Invoke`. All UI state is `[ObservableProperty]`, all user actions are `[RelayCommand]`.

**Single instance** — enforced via a named `Mutex`. A second launch signals the running instance via a named pipe (`SpaceMousePilot_IPC`) to restore its window, then exits.
