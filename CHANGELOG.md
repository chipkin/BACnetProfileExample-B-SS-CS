# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
