# BACnet B-SS (Smart Sensor) - C# example

A minimal, copy-paste-friendly example showing how to implement the BACnet
**B-SS (Smart Sensor)** device profile in C# / .NET, using the
[CAS BACnet Stack](https://store.chipkin.com/services/stacks/bacnet-stack). It
listens on **BACnet/IP (UDP 47808)**, answers **ReadProperty**, and is
discoverable via **Who-Is / I-Am**. Its readings follow a sensor
**Simulation** (labelled below), and it reports a broken sensor the BACnet way.

This is a faithful C# port of the reworked
[BACnetProfileExample-B-SS-CPP](https://github.com/chipkin/BACnetProfileExample-B-SS-CPP)
(v1.3.0): the same Device, objects, properties, Simulation, Failure Modes,
test-only hook and command-line options, proven by the same
[Wire Tests](#wire-tests), which cannot tell the two apart on the wire.

This is the **first C# example** in the BACnet profile example series. There
is no prior C# example repo to copy structurally - this one was built from the
C++ edition's device model and the Node edition's documentation skeleton, with
a binding mechanism new to the series: native **P/Invoke** against the
vendored C# adapter, not an embedded C++ link or a Node N-API addon.

- **[TUTORIAL.md](TUTORIAL.md)** - how to extend this example and how to review
  it for conformance. Read it when you start turning this into your own device.
- **[docs/PICS.md](docs/PICS.md)** - the Protocol Implementation Conformance
  Statement: every object, every property, and who answers it.

> **Versions:** this document describes **example v1.1.0**, built and verified
> against **CAS BACnet Stack 6.0.23** (`6.x` @ `64b2cfac5`, loaded as the
> `ReleaseDll` native library through the stack's own `adapters/csharp/`), at
> **Protocol_Revision 30**, with the vendored C# `common/` helper at **v1.1.0**
> and `wiretest/` at **0.7.0**. Running the example prints all three versions -
> if what it prints disagrees with this line, trust the program and check
> `CHANGELOG.md`.

## What is the B-SS (Smart Sensor) profile?

**B-SS (BACnet Smart Sensor)**, defined in Annex L of ANSI/ASHRAE 135, is the
simplest BACnet device profile - the standard describes it as *"a simple
sensing device with very limited resources."* It is meant for inexpensive,
fixed-function sensors (temperature, humidity, occupancy, a contact, ...) that
mostly just need to **report what they measure** when asked.

A B-SS device answers **ReadProperty** and is discoverable. It does not have
to support **WriteProperty** (a smart sensor is read-only), **alarming /
event reporting**, **scheduling**, or **trending**, and this example
implements none of them on purpose.

**But it is still a full BACnet device.** Even the simplest profile must
present the standard object model - a **Device** object, a **Network Port**
object (every device needs one), and its sensor objects - and each object
must expose all of its **required properties**. The CAS BACnet Stack
generates most of those automatically (Object_Identifier, Object_Type,
Status_Flags, Object_List, Protocol_*, ...); this example supplies the
handful that are application-specific. The result is conformant for
**Protocol_Revision 30**. [docs/PICS.md](docs/PICS.md) lists every property
and who answers it.

## The device this example creates

```
Device 389001  "Chipkin Example B-SS"   (Vendor 389 - Chipkin Automation Systems)
    │
    ├── Analog Input 1       "Bronze"      Present_Value  21.5 -> ramps 18.0 .. 26.0  (REAL, degrees Celsius)
    ├── Binary Input 1       "Emerald"     Present_Value  toggles every 3 s     (0 = inactive / 1 = active)
    ├── Multi-State Input 1  "Hot Pink"    Present_Value  1 -> 2 -> 3 every 4 s (state, 1..3)
    └── Network Port 1       "Vermilion"   the BACnet/IP port     (required on every device)
```

## The scenario: a sensor a controller reads

What a B-SS is for, in one sentence, from Annex L.4.5 of ANSI/ASHRAE 135-2024:
*"Ability to provide values for any of its BACnet objects upon request."* A
controller or workstation (the **Peer** - any BACnet client, or the Python
client in [`tests/`](tests/README.md)) reads the sensor and gets what it is
measuring **now**:

1. The Peer finds the sensor with **Who-Is** and reads its `Object_List`.
2. It reads Analog Input 1 `Present_Value` - say `22.0` - and again a few
   seconds later - `23.5`. The reading follows the (simulated) room temperature,
   0.5 C a second. Binary Input 1 and Multi-State Input 1 change too.
3. **Failure Mode - a broken sensor.** When the sensor wire breaks, Analog
   Input 1 `Reliability` reads `open-loop` (4) and the **FAULT** bit of
   `Status_Flags` is TRUE (clause 12.2.7 derives FAULT from Reliability). The
   reading stops changing. Repair the wire and `Reliability` returns to
   `no-fault-detected`, FAULT clears, and the reading moves again.
4. **Failure Mode - a request it cannot serve.** Reading an object or property
   the sensor does not have, or an array element past the end, gets the error
   clause 15.5.1.3.1 names (`unknown-object`, `unknown-property`,
   `invalid-array-index`); a WriteProperty gets a **Reject**
   `unrecognized-service`, because a B-SS is read-only.

### The sensor Simulation

A real smart sensor samples a thermistor, a contact and a selector switch. This
example has none of them, so it **simulates** them - non-BACnet scaffolding,
outside the BACnet Surface, in section 2 of [`Program.cs`](Program.cs)
(`SensorSimulationTick()`, called beside `BACnetStack_Tick()`). Once a second:

- the **temperature** (Analog Input 1) moves 0.5 C, ramping up to 26.0 and back
  down to 18.0;
- the **contact** (Binary Input 1) toggles every 3 s;
- the **selector** (Multi-State Input 1) steps 1 -> 2 -> 3 -> 1 every 4 s.

The **broken wire** is a test-only hook, not a feature: if the environment
variable `CAS_EXAMPLE_TEST_SENSOR_FAULT_FILE` names a file, the wire counts as
broken for as long as that file exists. The Wire Tests create and delete it.
Nothing on the network can break the sensor.

To port the example to hardware, replace `SensorSimulationTick()` with your own
sampling (on a timer or another thread - never block the BACnet callbacks on
slow I/O), and set `Reliability` from your hardware's own diagnostics.

## What this example supports

The example implements exactly the capabilities below - and nothing more,
which is the point of a profile example.

### BIBBs (BACnet Interoperability Building Blocks)

| BIBB | Description | Supported |
|------|-------------|:---------:|
| DS-RP-B | Data Sharing - ReadProperty - B | ✅ |
| DM-DDB-B | Device Management - Dynamic Device Binding - B | ✅ |
| DM-DOB-B | Device Management - Dynamic Object Binding - B | ✅ |

### Services (executed / B-side)

| Service | Notes |
|---------|-------|
| ReadProperty | Responds to property reads (DS-RP-B). |
| Who-Is / I-Am | Answers Who-Is with I-Am, and broadcasts an I-Am on start-up (DM-DDB-B). |
| Who-Has / I-Have | Answers Who-Has with I-Have (DM-DOB-B). |

### Object types

| Object type | Instance | Name |
|-------------|:--------:|------|
| Device | 389001 | Chipkin Example B-SS |
| Analog Input | 1 | Bronze |
| Binary Input | 1 | Emerald |
| Multi-state Input | 1 | Hot Pink |
| Network Port | 1 | Vermilion |

Every required property of every object, and who answers it, is in
[docs/PICS.md](docs/PICS.md).

## Requires the CAS BACnet Stack (licensed product)

This example **builds against the CAS BACnet Stack, which is a commercial
Chipkin product** - it is not free or open source, and there is no
public/trial build. The stack is referenced here as the **private** git
submodule `submodules/cas-bacnet-stack` (tracking the `6.x` branch). Unlike
the Node edition (npm workspace, compiles a native addon) or the C++ edition
(compiles the stack's own source straight into the executable), the C#
edition links against a **separately built native shared library**
(`CASBACnetStack_x64_Release.dll` on Windows / `libCASBACnetStack_x64_Release.so`
on Linux) through P/Invoke - see
[Build the native CAS BACnet Stack library](#build-the-native-cas-bacnet-stack-library)
below. **To get the CAS BACnet Stack (and access to build this example),
contact Chipkin:** <https://store.chipkin.com/services/stacks/bacnet-stack> or
sales@chipkin.com.

## What's in this repository

This is a **self-contained** project. It ships:

- `Program.cs` - the example device (section 2 is the sensor Simulation).
- `common/` - the shared C# helper (UDP, callbacks, CLI, keys) vendored in.
- `tests/` - the Wire Tests ([tests/README.md](tests/README.md)), copied from
  the C++ edition; `wiretest/` - the series' shared Wiretest Harness 0.7.0,
  vendored verbatim.
- `BACnetProfileExampleBSSCS.csproj` - the .NET build (`net8.0`), the same on
  Windows and Linux.
- `docs/PICS.md` - the conformance statement.
- `docs/objects.json` - the input to the objects-and-properties generator.
- `submodules/cas-bacnet-stack/` - the **CAS BACnet Stack as a git submodule**
  (private; requires a license - see above). Its `adapters/csharp/` is
  included directly into this project (`<Compile Include>` in the .csproj,
  no NuGet package); its `source/` and MSVC/CMake build produce the separate
  native shared library this example loads at run time.

## Prerequisites

- **.NET 8 SDK or later** (verified with SDK 10.0.402 building `net8.0`) and
  the .NET 8 runtime.
- **Python 3** (standard library only) to run the Wire Tests.
- Git (to fetch the stack submodule).
- **To build the native library** (see below): on Windows, Visual Studio 2022
  Build Tools (MSBuild) building the stack's `projects/msvs/CASBACnetStack/CASBACnetStack.vcxproj`;
  on Linux, a C++17 toolchain (g++/clang++).

## Build

```bash
git clone --recursive https://github.com/chipkin/BACnetProfileExample-B-SS-CS.git
cd BACnetProfileExample-B-SS-CS

dotnet build -c Release
```

Already cloned without `--recursive`? Run `git submodule update --init --recursive`
first - the build needs the stack submodule's `adapters/csharp/` sources.

`dotnet build` compiles this example and the vendored C# adapter - it does
**not** build the native CAS BACnet Stack library the adapter calls into via
P/Invoke. That is a separate step (below), because it is a native C++ build,
not a .NET one.

### Build the native CAS BACnet Stack library

The C# adapter's P/Invoke declarations resolve
`CASBACnetStack_x64_Release.dll` (Windows) / `libCASBACnetStack_x64_Release.so`
(Linux) from the process's own directory (or the system library search path)
at run time - there is no bundled copy and no NuGet package. Build it once
from the stack submodule, then copy it next to this project's build output.

**Windows** - the stack's MSVC project has a `ReleaseDll|x64` configuration
that builds exactly this DLL, under exactly this name (a customer-interface
build: no test-tool exports):

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  submodules\cas-bacnet-stack\projects\msvs\CASBACnetStack\CASBACnetStack.vcxproj `
  /p:Configuration=ReleaseDll /p:Platform=x64

dotnet build -c Release   # copies submodules\cas-bacnet-stack\bin\CASBACnetStack_x64_Release.dll next to the .exe
```

The `.csproj` copies the DLL (or `libCASBACnetStack_x64_Release.so`) from the
submodule's `bin/` into the build output whenever it exists there, so build the
native library first and `dotnet build` second.

(Adjust the MSBuild path to your Visual Studio / Build Tools install; `vswhere`
or the "Developer Command Prompt for VS 2022" both resolve it for you if you
have Visual Studio Installer set up.)

**Linux** - the stack does not currently ship a ready-made shared-library
CMake target (its CMake `adapters/cpp/` builds a static adapter; its Linux
`projects/linux/` build is for the gtest suite). Build one directly from the
stack's own sources with g++:

```bash
g++ -shared -fPIC -std=c++17 -O2 \
    -I submodules/cas-bacnet-stack/source \
    submodules/cas-bacnet-stack/source/*.cpp \
    -o bin/Release/net8.0/libCASBACnetStack_x64_Release.so
```

Either way, **the DLL/.so must end up in the same directory as
`BACnetProfileExampleBSSCS.dll`/`.exe`** (`bin/Release/net8.0/` by default) -
the .NET native-library resolver looks there first.

### Why only a Release build

The vendored adapter selects the native library filename with `#if DEBUG` /
`#else`: `CASBACnetStack_x64_Debug` in a Debug build,
`CASBACnetStack_x64_Release` in Release. The stack's MSVC solution only has a
`ReleaseDll` configuration (see above) - there is no matching `DebugDll` that
would produce `CASBACnetStack_x64_Debug.dll`. So build and run this example in
**Release**; a Debug build starts but fails to load the native library the
first time it calls into the stack (see
[Troubleshooting](TUTORIAL.md#troubleshooting)) unless you first add a
`DebugDll` configuration to the stack's solution yourself.

### Why `PropertyBufferHelper.cs` is here but unused

The `.csproj` includes all three of the adapter's files for parity with its
expected set (see `submodules/cas-bacnet-stack/adapters/csharp/selftest/PropertyBufferHelperSelfTest.csproj`
for the precedent), but `PropertyBufferHelper.cs` packs buffer-shaped `Send*`
calls (ReadProperty/WriteProperty/CreateObject **as a client**) that this
read-only B-SS example never makes - a Smart Sensor only ever *answers*
ReadProperty, it never *initiates* one. It compiles in unused; that is a
documented, deliberate choice, not an oversight.

## Run

```bash
dotnet run -c Release -- --port 47821 --deviceID 12345
# or, after `dotnet build -c Release`, run the built executable directly:
./bin/Release/net8.0/BACnetProfileExampleBSSCS --port 47821
```

Expected output:

```
BACnet B-SS (Smart Sensor) Example - C# v1.1.0
CAS BACnet Stack version: 6.0.23.0
Common helper (common/) version: 1.1.0
FYI: listening on <your-ip>:47808 (broadcast <your-subnet-broadcast>)
FYI: Device 389001 ("Chipkin Example B-SS") ready. Vendor ID 389. Press 'h' for help.
```

The device listens on UDP **47808** (BACnet/IP) by default. Allow that port
through your firewall. To use a different port, pass `--port`.

### Command-line options

| Option | Default | Meaning |
|--------|---------|---------|
| `--port <n>` | `47808` | UDP port to listen on (BACnet/IP). |
| `--deviceID <n>` | `389001` | The device's BACnet instance number (BACnet requires this to be configurable). |
| `--help`, `-h` | - | Show usage and exit. |
| `--version` | - | Print the example and stack versions, then exit. |

### Interactive commands

While the example runs **in a real console** (not redirected - see
[TUTORIAL.md](TUTORIAL.md#interactive-keys-are-a-simplification) for what that
means), these keys are available:

| Key | Action |
|-----|--------|
| `h` | Show the version information and this command list. |
| `q` | Quit. |
| up arrow | Nudge the simulated Analog Input 1 (`Bronze`) reading up by 1.1. |
| down arrow | Nudge the simulated Analog Input 1 (`Bronze`) reading down by 1.1. |

The up/down keys move the simulated temperature; the Simulation carries on
ramping from the new value, and a client re-reading it sees the change. Ctrl+C
also shuts the device down cleanly. With stdin redirected (a pipe, as the Wire
Tests run it) the arrows do not exist, but a `q` written to stdin still quits.

## Verify

Use a BACnet client such as the
[**CAS BACnet Explorer**](https://store.chipkin.com/products/tools/cas-bacnet-explorer):

1. **Discover** - send a **Who-Is**. The device replies with **I-Am** from
   instance **389001** (vendor **389**). It also broadcasts an I-Am at
   start-up.
2. **Browse the object model** - the device shows five objects: the Device
   (`Chipkin Example B-SS`), the three sensors, and the Network Port (`Vermilion`). Reading
   the Device's `Object_List` returns all five.
3. **Read the Device** - ReadProperty `389001` -> `Object_Name` returns
   `"Chipkin Example B-SS"`; `Protocol_Revision` returns `30`; `Description` returns the
   profile description string.
4. **Read a sensor** - ReadProperty Analog Input `1` -> `Present_Value` returns
   the current simulated temperature (`21.5` at start-up, then 0.5 higher each
   second); read it again and it has moved. `Units` returns `degrees-Celsius`;
   `Reliability` returns `no-fault-detected`; `Status_Flags` returns all FALSE;
   `Out_Of_Service` returns `false`; `Object_Name` returns `"Bronze"`. Repeat for Binary Input `1`
   (`"Emerald"`, has `Polarity`) and Multi-State Input `1` (`"Hot Pink"`, has
   `Number_Of_States` = 3). Every required property of every object is
   readable.
5. **Confirm the profile boundary** - a **WriteProperty** to any object is
   rejected. That is correct: a B-SS Smart Sensor is read-only.

For a property-by-property review against the conformance statement, see
[TUTORIAL.md](TUTORIAL.md).

## Wire Tests

The checks above, automated: a Python BACnet client (the Peer) drives the running
example over BACnet/IP from outside it. Python 3 standard library only. The
scripts are the C++ edition's `tests/`, unchanged except for one addition in
`tests/bss_setup.py`: `--exe` may be a whole command line (split with `shlex`),
so the example can also be started as `dotnet <dll>`.

| Script | What it proves |
|---|---|
| `discovery_test.py` | Who-Is, the Device's `Object_List`, then for every object its `Property_List` and a ReadProperty of every property it lists - every read comes back as a value. |
| `sensing_test.py` | **The Profile Function.** Reads the three sensors again and again for 12 s; the readings follow the Simulation (0.5 C steps, a toggle every 3 s, states in order). Fails against a sensor that serves constants. |
| `failure_modes_test.py` | **The Failure Modes.** Breaks the simulated sensor wire: `Reliability` `open-loop`, `Status_Flags` FAULT, a frozen reading; repairs it: all clear, the reading moves. Then the errors for an unknown object / property / array index, and the Reject for a WriteProperty. |

Run them all (after `dotnet build -c Release`, with the native library in the
build output):

```bash
python tests/run_wire_tests.py                     # default --exe bin/Release/net8.0/BACnetProfileExampleBSSCS(.exe)
python tests/run_wire_tests.py --port 47821 --exe "dotnet bin/Release/net8.0/BACnetProfileExampleBSSCS.dll"
```

It ends with `ALL WIRE TESTS PASSED` (exit 0); about 25 seconds.
[tests/README.md](tests/README.md) says what each check proves. **Verified on
Windows only** (3 runs in a row, 36/36 checks each, no `[RETRY]` lines); the
port has not been run on Linux.

## How the C# binding differs from the C++/Node editions

If you have read either sibling example, the object model and callback shapes
are identical - this section is only about what is mechanically different
because C# talks to the stack through P/Invoke instead of an embedded link or
an N-API addon. See `common/CHANGELOG.md` for the full list; the highlights:

- **No `LoadBACnetFunctions()`.** The C# adapter's `BACnetStack_*` methods are
  plain `[DllImport] static extern` declarations; the .NET runtime resolves
  the native library the first time one is called, not up front. `Program.cs`
  treats that first call (inside `PrintVersion()`) as the load check.
- **Raw pointers, not managed buffers.** Every `Get*Property` callback takes
  `byte*`/`uint*`/`float*` out-parameters matching the native C ABI directly -
  this project is `unsafe` throughout (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`).
- **A separately built native library.** There is no "compile the stack into
  the executable" mode for C# (unlike the C++ edition's default SOURCE link
  mode) and no addon build step baked into the package install (unlike Node's
  `npm install`). You build the native shared library once, explicitly (see
  above), and it has to be found next to the executable at run time.
- **Delegates must be rooted** in `static readonly` fields so the CLR cannot
  collect them while native code may still call back into their marshalling
  thunks - see the comments in `common/CASExampleHelper.cs` and `Program.cs`.

## The BACnet profile example series

<!-- PROFILE-TABLE:BEGIN (generated from cas-bacnet-stack-examples/docs/profile-table.md - do not edit here) -->
The CAS BACnet Stack supports every standardized device profile in ASHRAE 135-2024 Annex L, and there is one example repository per profile. Pick the profile your device claims, then the language you build in. "Ask" means the example hasn't been built yet for that language - [contact Chipkin](https://store.chipkin.com/contact-us) if you need one.

### Controllers (Annex L.4)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-SS** Smart Sensor | [B-SS-CPP](https://github.com/chipkin/BACnetProfileExample-B-SS-CPP) | [B-SS-Node](https://github.com/chipkin/BACnetProfileExample-B-SS-Node) | [B-SS-CS](https://github.com/chipkin/BACnetProfileExample-B-SS-CS) | [B-SS-Rust](https://github.com/chipkin/BACnetProfileExample-B-SS-Rust) | [B-SS-Python](https://github.com/chipkin/BACnetProfileExample-B-SS-Python) | [B-SS-Go](https://github.com/chipkin/BACnetProfileExample-B-SS-Go) |
| **B-SA** Smart Actuator | [B-SA-CPP](https://github.com/chipkin/BACnetProfileExample-B-SA-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-ASC** Application Specific Controller | [B-ASC-CPP](https://github.com/chipkin/BACnetProfileExample-B-ASC-CPP) | [B-ASC-Node](https://github.com/chipkin/BACnetProfileExample-B-ASC-Node) | Ask | Ask | Ask | Ask |
| **B-AAC** Advanced Application Controller | [B-AAC-CPP](https://github.com/chipkin/BACnetProfileExample-B-AAC-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-BC** Building Controller | [B-BC-CPP](https://github.com/chipkin/BACnetProfileExample-B-BC-CPP) | Ask | Ask | Ask | Ask | Ask |

### Life safety controllers (Annex L.5)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-LSC** Life Safety Controller | [B-LSC-CPP](https://github.com/chipkin/BACnetProfileExample-B-LSC-CPP) 🚧 | Ask | Ask | Ask | Ask | Ask |
| **B-ALSC** Advanced Life Safety Controller | [B-ALSC-CPP](https://github.com/chipkin/BACnetProfileExample-B-ALSC-CPP) | Ask | Ask | Ask | Ask | Ask |

### Access control controllers (Annex L.6)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-ACC** Access Control Controller | [B-ACC-CPP](https://github.com/chipkin/BACnetProfileExample-B-ACC-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-AACC** Advanced Access Control Controller | [B-AACC-CPP](https://github.com/chipkin/BACnetProfileExample-B-AACC-CPP) | Ask | Ask | Ask | Ask | Ask |

### Lighting controllers (Annex L.11)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-LD** Lighting Device | [B-LD-CPP](https://github.com/chipkin/BACnetProfileExample-B-LD-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-LS** Lighting Supervisor | [B-LS-CPP](https://github.com/chipkin/BACnetProfileExample-B-LS-CPP) | Ask | Ask | Ask | Ask | Ask |

### Elevator controllers (Annex L.13)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-EM** Elevator Monitor | [B-EM-CPP](https://github.com/chipkin/BACnetProfileExample-B-EM-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-EC** Elevator Controller | [B-EC-CPP](https://github.com/chipkin/BACnetProfileExample-B-EC-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-AEC** Advanced Elevator Controller | [B-AEC-CPP](https://github.com/chipkin/BACnetProfileExample-B-AEC-CPP) | Ask | Ask | Ask | Ask | Ask |

### Authentication and authorization (Annex L.14)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-AS** Authorization Server | [B-AS-CPP](https://github.com/chipkin/BACnetProfileExample-B-AS-CPP) | Ask | Ask | Ask | Ask | Ask |

### Miscellaneous (Annex L.7, combinable with any one family)

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-BBMD** Broadcast Management Device | [B-BBMD-CPP](https://github.com/chipkin/BACnetProfileExample-B-BBMD-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-ACDC** Access Control Door Controller | [B-ACDC-CPP](https://github.com/chipkin/BACnetProfileExample-B-ACDC-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-ACCR** Access Control Credential Reader | [B-ACCR-CPP](https://github.com/chipkin/BACnetProfileExample-B-ACCR-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-RTR** Router | [B-RTR-CPP](https://github.com/chipkin/BACnetProfileExample-B-RTR-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-GW** Gateway | [B-GW-CPP](https://github.com/chipkin/BACnetProfileExample-B-GW-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-DAP** Device Address Proxy | [B-DAP-CPP](https://github.com/chipkin/BACnetProfileExample-B-DAP-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-SCHUB** BACnet/SC Hub | [B-SCHUB-CPP](https://github.com/chipkin/BACnetProfileExample-B-SCHUB-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-GENERAL** General device (Annex L.8) | *(satisfied by every example above)* | — | — | — | — | — |

### Operator interfaces and workstations (Annex L.1–L.3, L.9–L.10, L.12)

Client-side profiles.

| Profile | C++ | Node.js | C# | Rust | Python | Go |
|---|---|---|---|---|---|---|
| **B-OD** Operator Display | [B-OD-CPP](https://github.com/chipkin/BACnetProfileExample-B-OD-CPP) | Ask | Ask | Ask | Ask | Ask |
| **B-OWS** Operator Workstation | planned | — | — | — | — | — |
| **B-AWS** Advanced Operator Workstation | planned | — | — | — | — | — |
| **B-XAWS** Extended Advanced Operator Workstation | planned | — | — | — | — | — |
| **B-LSAP** Life Safety Annunciator Panel | planned | — | — | — | — | — |
| **B-LSWS** Life Safety Workstation | planned | — | — | — | — | — |
| **B-ALSWS** Advanced Life Safety Workstation | planned | — | — | — | — | — |
| **B-ACSD** Access Control Security Display | planned | — | — | — | — | — |
| **B-ACWS** Access Control Workstation | planned | — | — | — | — | — |
| **B-AACWS** Advanced Access Control Workstation | planned | — | — | — | — | — |
| **B-LOD** Lighting Operator Display | planned | — | — | — | — | — |
| **B-ALWS** Advanced Lighting Workstation | planned | — | — | — | — | — |
| **B-LCS** Lighting Control Station | planned | — | — | — | — | — |
| **B-ALCS** Advanced Lighting Control Station | planned | — | — | — | — | — |
| **B-ED** Elevator Display | planned | — | — | — | — | — |
| **B-EWS** Elevator Workstation | planned | — | — | — | — | — |
| **B-AEWS** Advanced Elevator Workstation | planned | — | — | — | — | — |

🚧 = in progress. "Ask" = not yet built for that language; contact Chipkin if you need it. Profile definitions: ANSI/ASHRAE 135-2024 Annex L. BIBB definitions: Annex K. Get the stack: <https://store.chipkin.com/services/stacks/bacnet-stack>.
<!-- PROFILE-TABLE:END -->

## References

- **ANSI/ASHRAE Standard 135** (BACnet) - the protocol standard. Object model
  (Clause 12), services (Clause 15), BACnet/IP (Annex J), device profiles
  (Annex L). Purchase / preview via the [ASHRAE store](https://www.ashrae.org/technical-resources/standards-and-guidelines).
- **What is BACnet?** - Chipkin's introduction:
  <https://docs.chipkin.com/protocols/bacnet/>.
- **CAS BACnet Stack** - product page and documentation:
  <https://store.chipkin.com/services/stacks/bacnet-stack>.
- **CAS BACnet Explorer** - client for testing this device:
  <https://store.chipkin.com/products/tools/cas-bacnet-explorer>.
- **Shared helper used by this example** - [`common/README.md`](common/README.md).

See also [TUTORIAL.md](TUTORIAL.md), [docs/PICS.md](docs/PICS.md),
[CHANGELOG.md](CHANGELOG.md), and [AGENTS.md](AGENTS.md).
