# AGENTS.md

Guidance for AI coding agents working in this repository. See
<https://agents.md/> for the format. Human contributors should read
[README.md](README.md) first, then [TUTORIAL.md](TUTORIAL.md).

## What this project is

A **tutorial** C#/.NET example that implements the BACnet **B-SS** (Smart
Sensor) profile using the CAS BACnet Stack's C# P/Invoke adapter: a complete
minimal, **read-only** BACnet/IP device (DS-RP-B, DM-DDB-B, DM-DOB-B). Part of
the BACnet profile example series, and the **first C# example** in it - the
C++ sibling is `BACnetProfileExample-B-SS-CPP` and the Node sibling is
`BACnetProfileExample-B-SS-Node`; this example keeps the same objects, names,
and section layout as both, but the binding mechanism is new: native
P/Invoke, not an embedded C++ link or an N-API addon. The top priority is
that the code reads like a tutorial a customer can learn from and
copy-paste. Favour clarity over cleverness.

## Layout

This repository is self-contained:

- `Program.cs` - the whole application, three numbered sections (§1
  configuration, §2 get callbacks, §3 main). There is no §2b/§2c - a B-SS
  registers no `Set*` callback and no `DeviceCommunicationControl` handler.
- `common/` - the shared C# helper (SimpleUDP, CASExampleHelper, constants),
  vendored in. Never edit here alone: once a second C# example exists, a
  change must be swept to it too and `COMMON_VERSION` bumped with a
  `common/CHANGELOG.md` entry.
- `README.md` - what this example is. Keep it short and about THIS example
  only.
- `TUTORIAL.md` - how to extend and review the example. Long-form material
  that would bloat the README belongs here.
- `docs/PICS.md` - the Protocol Implementation Conformance Statement. Its
  objects-and-properties section is GENERATED from `docs/objects.json`; do
  not hand-edit between the `OBJECTS-PROPERTIES` markers.
- `docs/objects.json` - the input to that generator. Update it in the same
  change as any `Program.cs` change that adds an object or a `Get*` branch.
- `submodules/cas-bacnet-stack` - the **CAS BACnet Stack** as a git submodule
  (private; tracks `6.x`). Its `adapters/csharp/` is compiled directly into
  this project via `<Compile Include>` in the `.csproj` (no NuGet package);
  its `source/`/MSVC build produce the separately-built native shared library
  this example loads via P/Invoke at run time - see README.md
  "Build the native CAS BACnet Stack library". After cloning, run
  `git submodule update --init --recursive`.

The `PROFILE-TABLE` block in README.md is also generated, from the
example-series repository's `docs/profile-table.md`. Edit it there, not here.

## Build

```bash
git submodule update --init --recursive   # once, if not cloned with --recursive
dotnet build -c Release                   # compiles this example + the vendored adapter
```

`dotnet build` does **not** build the native CAS BACnet Stack library the
adapter P/Invokes into - that is a separate native C++ build (MSBuild on
Windows against `submodules/cas-bacnet-stack/projects/msvs/BuildCASBACnetStack.sln`
`/p:Configuration=ReleaseDll /p:Platform=x64`; g++ on Linux). See README.md
for the exact commands. Do not try to fold that into the `.csproj` as a
pre-build target unless you also make it a no-op when the DLL/.so is already
present - re-running the native build on every `dotnet build` would make the
inner loop painfully slow.

**Release only.** The adapter's DllImport filename is
`CASBACnetStack_x64_Debug` under `#if DEBUG`, else `CASBACnetStack_x64_Release`;
the stack's MSVC solution only has a `ReleaseDll` configuration. Build and run
this example in Release. Do not "fix" this by adding a Debug DLL step here -
that belongs in the stack submodule, not this example.

## Run

```bash
dotnet run -c Release -- --port 47821 --deviceID 12345   # run
dotnet build -c Release                                   # compile-check (no separate typecheck step in .NET)
```

Interactive keys while running (only when stdin is a real console - see
TUTORIAL.md "Interactive keys are a simplification"): `h` help, `q` quit,
up/down nudge Analog Input 1.

## Conventions

- Device is named "Rainbow"; objects use the series' colour names; vendor id
  389.
- Implement **only** the services and objects the B-SS profile requires -
  but expose **every required property** of each object.
- **This device is read-only.** There is no `RegisterCallbackSetProperty*`
  call and no `SERVICES_SUPPORTED_WRITE_PROPERTY` anywhere in `Program.cs`;
  do not add Set-side plumbing here - that belongs in a different profile
  example (B-SA/B-ASC).
- **No `LoadBACnetFunctions()` call** - unlike the C++/Node editions, the C#
  adapter's `BACnetStack_*` methods are plain `[DllImport] static extern`;
  the runtime resolves the native library on first call. `Program.cs` treats
  the `PrintVersion()` call at start-up as that check (wrapped in
  `try`/`catch` for `DllNotFoundException`/`BadImageFormatException`). Do not
  add a `LoadBACnetFunctions()`-shaped call that does not exist in this
  adapter.
- Every `BACnetStack_*` setup call's return value is checked; failures print
  which call failed and exit non-zero.
- The stack PULLS: callbacks serve values through **raw pointers**
  (`float* value`, `uint* value`, `byte* value`), not managed buffers - this
  project is `unsafe` throughout (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`).
  Every `GetProperty*` callback ends with a trailing `uint* errorCode`
  out-param (stack issue #974) - leave it alone on a catch-all `return false`;
  set it only where this device knows the read is wrong (see the
  `State_Text` branch in `GetPropertyCharacterString`).
- Every delegate passed to a `BACnetStack_RegisterCallback*` call is stored in
  a `static readonly` field, never a bare lambda/method-group expression
  passed inline - the CLR can collect an unrooted delegate while native code
  may still call back into its marshalling thunk.
- The 6-byte IPv4 connection string (4 octets + BIG-endian port) is packed in
  `common/CASExampleHelper.cs` only - never re-derive it.
- Links are identified by **Network Port object instance**, not by a
  transport-type enumeration: `RegisterCommonCallbacks()` and `SendIAm()` both
  take a `networkPortInstance` parameter, and the transport callbacks are
  `BACnetStack_RegisterCallbackReceiveMessageForPort()` /
  `BACnetStack_RegisterCallbackSendMessageForPort()`.
- Reuse enumeration constants already defined in the vendored adapter
  (`CASBACnetStack.CASBACnetStackAdapter.OBJECT_TYPE_*`,
  `PROPERTY_IDENTIFIER_*`, `SERVICES_SUPPORTED_*`, ...) rather than
  redeclaring them in `common/CASBACnetStackExampleConstants.cs` - that file
  only adds the handful the adapter does not already have (see its header
  comment for the full mapping).
- Present tense only: no comment or doc references a previous version of this
  example or of the stack.
- **Never edit `common/` in this repo alone once a sibling C# example
  exists** - it will be a vendored copy shared across the series, with its
  own version (`COMMON_VERSION`) and changelog (`common/CHANGELOG.md`).

## How to verify a change

There are no unit tests; verification is behavioural:

1. `dotnet build -c Release` with 0 warnings, 0 errors.
2. Smoke: with the native library built and copied next to the output (see
   README.md), `dotnet run -c Release -- --port 47821` stays up past the
   ready banner (every failed setup call exits 1, so "still running" proves
   registration).
3. Read back what you changed with a BACnet client (Who-Is, ReadProperty of
   every required property; confirm a WriteProperty is rejected).
4. If you changed the objects or their properties, regenerate
   `docs/PICS.md` (`python tools/gen-objects-properties.py
   BACnetProfileExample-B-SS-CS` from the series root) and confirm no row
   comes out flagged with ⚠.

## Releasing

Bump `APP_VERSION` in `Program.cs` and add an entry to
[CHANGELOG.md](CHANGELOG.md), then tag `vX.Y.Z`. The GitHub Actions workflow
builds the native library, builds + smoke-tests the .NET project, and
publishes a release.

## License

See [LICENSE](LICENSE). The CAS BACnet Stack is a separate, commercially
licensed product and is not covered by it.
