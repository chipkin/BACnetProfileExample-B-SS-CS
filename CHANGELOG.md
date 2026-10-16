# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - unreleased

Profile-functional (series bar): a faithful port of
`BACnetProfileExample-B-SS-CPP` 1.3.0 (tag `v1.3.0`; `0743e75` plus a CI-only commit).
The sensor performs the B-SS Profile Function - ANSI/ASHRAE 135-2024 Annex
L.4.5, "Ability to provide values for any of its BACnet objects upon request" -
with readings that change, and shows a Failure Mode. The BACnet Surface is
unchanged (DS-RP-B, DM-DDB-B, DM-DOB-B; the same five objects).

### Added

- **Sensor Simulation** (`Program.cs` section 2, `SensorSimulationTick()`,
  labelled Simulation in the README): once a second Analog Input 1 moves 0.5 C
  between 18.0 and 26.0, Binary Input 1 toggles every 3 s, Multi-State Input 1
  steps 1 -> 2 -> 3 -> 1 every 4 s. Before, all three were constants (Analog
  Input 1 changed only on an arrow key).
- **Analog Input 1 `Reliability`** (optional property, now enabled):
  `no-fault-detected`, or `open-loop` while the simulated sensor wire is
  broken; the stack derives the FAULT bit of `Status_Flags` from it (cl. 12.2.7,
  12.2.9). While broken the reading freezes. Test-only hook: the environment
  variable `CAS_EXAMPLE_TEST_SENSOR_FAULT_FILE` names a file, and the wire is
  broken while that file exists.
- **Wire Tests** (`tests/`, copied from the C++ reference) and the shared
  `wiretest/` 0.7.0 harness, vendored verbatim. `tests/bss_setup.py` adds
  `launch_command()`: `--exe` defaults to this build's output and may be a
  command line (e.g. `dotnet <dll>`). Windows, 3 runs in a row: 36/36 checks
  each (discovery 11/11, sensing 11/11, failure modes 14/14), no `[RETRY]`
  lines. Mutation-checked: removing the Simulation tick, the wire break, the
  Reliability branch, the 3 s toggle, or enabling WriteProperty each fails a
  check.
- The `.csproj` copies `CASBACnetStack_x64_Release.dll` /
  `libCASBACnetStack_x64_Release.so` from the submodule's `bin/` into the build
  output when it has been built.

### Changed

- **Stack Pin** `6.x` @ `ec60c718` -> `6.x` @ `64b2cfac5` (CAS BACnet Stack
  6.0.23), loaded as the `ReleaseDll` native library through the pin's own
  `adapters/csharp/` (no source change was needed for the regenerated
  bindings). **Protocol_Revision 24 -> 30**; segmentation `segmented-both`
  (`Max_Segments_Accepted` 16, `APDU_Segment_Timeout` 5000 ms), from the stack.
  Network Port 1's `IP_DNS_Server`, `FD_BBMD_Address` and
  `FD_Subscription_Lifetime` now read as values (B-SS-CPP #20-#22).
- `common/` 1.0.0 -> 1.1.0 (see `common/CHANGELOG.md`): the C++ `common/`
  3.2.0 / 3.4.0 broadcast conventions, `PollKey()` (also reads `q` from a
  redirected stdin, which is how the Wire Tests stop the example), and the
  three-line version banner.
- The up/down keys nudge the simulated Analog Input 1 reading; the Simulation
  carries on from the new value.
- README: the scenario, the Simulation, Wire Tests, the version line; TUTORIAL:
  replacing the Simulation with hardware and reporting a sensor fault; PICS:
  Reliability row, segmentation, Protocol_Revision 30, Application_Software_Version
  1.1.0. APP_VERSION 1.0.2 -> 1.1.0.
- **Releases ship the app without the CAS BACnet Stack library.**
  `.github/workflows/release.yml` publishes the app self-contained as one file
  (`dotnet publish -r win-x64` / `linux-x64`) and releases
  `BACnetProfileExampleBSSCS-windows-x64.zip` / `-linux-x64.zip` (the app, its
  `.pdb`, `README.md`, `LICENSE`), plus `README.md`, `LICENSE` and
  `metrics-*.json`. It never ships `CASBACnetStack_x64_Release.dll` /
  `libCASBACnetStack_x64_Release.so`: the `.csproj` marks them
  `CopyToPublishDirectory="Never"`, and guard steps (the published app, the
  package, and the release assets including inside the zips) fail on any
  `CASBACnetStack*` or `.node` file. Before, the workflow copied the stack DLL
  / `.so` into the release. CI also runs the published app without the library
  and asserts the first line of the missing-library message and exit code 1,
  and runs the Wire Tests on Linux after the smoke test, as the reference does.
- **Missing-library error.** Before any stack call, `Main()` looks for the
  library in the folder that holds the executable (`AppContext.BaseDirectory`).
  If it is not there, `Main()` prints the series' shared message to stderr
  ("ERROR: This example cannot run without the CAS BACnet Stack library.",
  the missing file, where it looked, that the stack is a commercially
  licensed (paid) library from Chipkin that is not in the release, the
  contact URL, and where licensed customers put it) and exits 1. A
  `DllNotFoundException` / `BadImageFormatException` on the first stack call
  prints the same message plus a `Details:` line. `--help` and `--version`
  work without the library (`common/` `PrintVersion()` then says the stack
  version is not available).
- `.gitignore` covers every stack binary name. README: what a release
  contains, why it will not run without the licensed library, where to put the
  library; build it from the submodule into `submodules/cas-bacnet-stack/bin/`
  (the Linux `g++` line now writes there too). The stack's C# adapter is
  described as compiled in place from the submodule, not vendored. TUTORIAL:
  troubleshooting rows for the message.

## [1.0.2] - unreleased

### Changed

- **Device renamed from the series' colour placeholder "Rainbow" to "Chipkin
  Example B-SS"** so devices from different examples in the series are
  distinguishable from each other on the same BACnet network - every example
  previously announced the identical Object_Name "Rainbow", which made two
  examples on one subnet indistinguishable by name. Sub-object names are
  unchanged. `docs/colour-table.md` (series root) updated to match.
  APP_VERSION bumped 1.0.1 -> 1.0.2.

## [1.0.1] - unreleased

### Fixed

- **`Application_Software_Version` (12) and `Firmware_Revision` (44) were
  hardcoded to `"1.0.0"` and never updated as the example's real version
  advanced** - the same issue found and fixed in
  [BACnetProfileExample-B-SCHUB-CPP](https://github.com/chipkin/BACnetProfileExample-B-SCHUB-CPP)
  v1.1.13 via a real device read with CAS BACnet Explorer.
  `Application_Software_Version` now returns `APP_VERSION` directly (one
  source of truth, can't drift from `--version`'s own banner again).
  `Firmware_Revision` is now built once at start-up, right after
  `CASExampleHelper.PrintVersion()` makes the first successful native stack
  call, from the CAS BACnet Stack's own
  `BACnetStack_GetAPIMajorVersion()`/`GetAPIMinorVersion()`/
  `GetAPIPatchVersion()`/`GetAPIBuildVersion()` (the same 4 calls
  `PrintVersion()` already uses for the start-up banner) - it names the
  underlying platform, not this app. Verified with a real ReadProperty
  (`bacpypes3`) against the running device:
  `Application_Software_Version = "1.0.1"`,
  `Firmware_Revision = "6.0.21.0"`.

## [1.0.0] - unreleased

### Added

- The **first B-SS (Smart Sensor) C# implementation** in this example series,
  and the first C# example in the series overall - ported from
  [BACnetProfileExample-B-SS-CPP](https://github.com/chipkin/BACnetProfileExample-B-SS-CPP)
  v1.2.0 (same device model, same object/property split between "app" and
  "stack", same section layout) with the documentation skeleton carried over
  from
  [BACnetProfileExample-B-SS-Node](https://github.com/chipkin/BACnetProfileExample-B-SS-Node).
  The binding mechanism is new to this series: native P/Invoke against the
  vendored C# adapter (`submodules/cas-bacnet-stack/adapters/csharp/`), not
  the C++ direct-link or the Node N-API addon - see `common/CHANGELOG.md` for
  what that changes.
- The complete B-SS example application (`Program.cs`): device 389001
  ("Rainbow"), the series' three read-only input objects (Analog Input
  "Bronze", Binary Input "Emerald", Multi-State Input "Hot Pink") plus the
  required Network Port ("Vermilion"), DS-RP-B (ReadProperty), DM-DDB-B /
  DM-DOB-B (Who-Is/I-Am, Who-Has/I-Have), unsolicited I-Am on start-up,
  series-standard CLI (`--help`/`--version`/`--deviceID`/`--port`) and
  interactive keys (`h`/`q`/arrows, via non-blocking `Console.KeyAvailable`
  polling - skipped automatically when stdin is redirected, e.g. the CI smoke
  test). There are no `Set*` callbacks and `SERVICE_WRITE_PROPERTY` is never
  enabled - this device is read-only end to end, matching the B-SS profile
  boundary.
- Vendored C# `common/` v1.0.0: `SimpleUDP.cs`, `CASExampleHelper.cs`
  (transport callbacks + the 6-byte IPv4 connection string, I-Am, local-IP
  discovery, CLI helpers), `CASBACnetStackExampleConstants.cs`. See
  `common/CHANGELOG.md` for the full list of what targeting the
  `submodules/cas-bacnet-stack` `6.x` branch's C# adapter means (trailing
  `errorCode` on every `GetProperty*` callback, the folded
  `AddNetworkPortObject()`, the `*ForPort` transport callbacks keyed by
  Network Port instance, and - unique to C# - no `LoadBACnetFunctions()` step
  and raw-pointer callback signatures instead of marshaled buffers).
- Direct source inclusion of the vendored adapter's three files
  (`CASBACnetStackAdapter.cs`, `CASBACnetStackAdapterBindings.cs`,
  `PropertyBufferHelper.cs`) via `<Compile Include>` in the `.csproj` - there
  is no NuGet package for this adapter; this follows the precedent in
  `submodules/cas-bacnet-stack/adapters/csharp/selftest/PropertyBufferHelperSelfTest.csproj`.
- Repository scaffold: CAS BACnet Stack submodule (`submodules/cas-bacnet-stack`,
  tracking `6.x`), CC0-1.0 licence, README, TUTORIAL, `docs/PICS.md` +
  `docs/objects.json`, AGENTS.md, changelog.

Verified on the wire: Who-Is → I-Am; every required property of every object
reads back; `State_Text[1..3]` reads `On`/`Off`/`Auto` and `State_Text[4]`
errors `invalid-array-index`; WriteProperty is rejected on every object (no
`Set*` callback registered).
