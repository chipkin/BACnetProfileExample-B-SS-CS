# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_No changes yet._

## [1.2.0] - 2026-10-27

Built on CAS BACnet Stack 6.x (`69b6249f`).

### Added

- **`--deviceName <text>`** sets the Device's Object_Name at start-up, like
  `--deviceID` sets its instance (ASHRAE 135-2024 cl. 12.1.1.3, 12.1.1.4).
  Object_Name must be unique on the internetwork, so two units could not share
  the compile-time name. The default is unchanged (`Chipkin Example B-SS`). A name
  must be 1..255 bytes with no control characters, otherwise the example warns and
  keeps the default. The name is not writable over the network. Documented in the
  README, PICS and TUTORIAL.
- **Wire Test** `tests/configuration_test.py` checks `--deviceID` and `--deviceName`
  over Who-Is, ReadProperty and Who-Has, and that `--help` lists exactly the
  options the example reads.

### Changed

- **Updated to CAS BACnet Stack 6.x `69b6249f`.** No change to `Program.cs`.
- **PICS** now cites the CAS BACnet Stack's property profiles for the
  objects-and-properties tables.

### Fixed

- **PICS: Database_Revision is 5**, not 0. The stack adds one per object created at
  start-up (cl. 12.11): the Device and its four objects.

### Compatibility

- **The native stack library is built with the stack's CMake presets.** The stack
  no longer ships Visual Studio or Linux makefile projects. On Windows use
  `cmake --preset windows-x64-dll`, `cmake --build --preset windows-x64-dll-release`
  and `cmake --install build-windows-x64-dll --config Release` (the `-vs2022`
  presets for Visual Studio 2022); on Linux use `linux-x64-release-shared`. The
  install step puts the library in the submodule's `bin/`, where the `.csproj`
  copies it from. See the README.

## [1.1.0] - 2026-10-16

Built on CAS BACnet Stack 6.x (`64b2cfac`, 6.0.23). This is the first tagged
release; it includes everything from the earlier 1.0.x development builds.

The sensor performs the B-SS Profile Function (ANSI/ASHRAE 135-2024 Annex L.4.5,
"Ability to provide values for any of its BACnet objects upon request") with live
readings and a failure mode. The BACnet surface is DS-RP-B, DM-DDB-B and DM-DOB-B
over five objects, matching the C++ example 1.3.0.

### Added

- **First C# B-SS (Smart Sensor) example** for the CAS BACnet Stack, ported from
  the C++ example. It has a Device (instance 389001), Analog Input 1, Binary
  Input 1, Multi-State Input 1 and Network Port 1; ReadProperty, Who-Is / I-Am and
  Who-Has / I-Have; an unsolicited I-Am at start-up; options `--help`, `--version`,
  `--deviceID` and `--port`; and interactive keys `h`, `q` and up/down. The device
  is read-only: WriteProperty is rejected.
- **Native binding.** The stack's C# adapter is compiled in from
  `submodules/cas-bacnet-stack/adapters/csharp/` and calls the native stack library
  by P/Invoke; there is no NuGet package. The `.csproj` copies the library
  (`CASBACnetStack_x64_Release.dll` or `libCASBACnetStack_x64_Release.so`) from the
  submodule's `bin/` into the build output once it has been built.
- **Sensor simulation.** Once a second Analog Input 1 moves 0.5 C between 18.0 and
  26.0, Binary Input 1 toggles every 3 s and Multi-State Input 1 steps 1, 2, 3
  every 4 s. The up/down keys nudge Analog Input 1.
- **Analog Input 1 `Reliability`** reads `no-fault-detected`, or `open-loop` while
  a simulated sensor wire is broken, and the FAULT bit of `Status_Flags` follows
  (cl. 12.2.7, 12.2.9). The TUTORIAL explains how to report a sensor fault from
  real hardware.
- **Wire Tests** under `tests/` (discovery, sensing, failure modes), run against
  the built example.
- **Missing-library error.** If the stack library is not next to the executable,
  the example prints a clear message (what is missing, where it looked, that the
  stack is a licensed Chipkin product) and exits with code 1. `--help` and
  `--version` work without the library.
- README, TUTORIAL and PICS (`docs/PICS.md`).

### Changed

- **Protocol_Revision 30** and **segmentation supported** (`segmented-both`,
  `Max_Segments_Accepted` 16, `APDU_Segment_Timeout` 5000 ms), from the stack.
- **The Device is named "Chipkin Example B-SS"** so examples on the same network
  can be told apart.
- **Releases ship the application without the CAS BACnet Stack library.** The
  Windows and Linux zips contain a self-contained single-file app, `README.md` and
  `LICENSE`. Put your licensed stack library next to the executable.
- `common/` 1.1.0 (see `common/CHANGELOG.md`).

### Fixed

- **Network Port 1 properties a client could not read.** `IP_DNS_Server`,
  `FD_BBMD_Address` and `FD_Subscription_Lifetime` now return values. Fixed by the
  stack update.
- **`Application_Software_Version` and `Firmware_Revision` were stale.** The first
  now follows the example's version, the second is built from the running stack's
  version.
