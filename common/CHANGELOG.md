# Changelog - `common/` (C# edition)

All notable changes to the vendored C# `common/` helpers. The format is
based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-09-16

### Added

- `SimpleUDP.cs` - application-owned UDP socket. Unlike the Node edition (which
  queues datagrams pushed by an event handler), this is poll-based:
  `TryReceive()` checks `Socket.Available` and reads synchronously, so the
  whole class stays single-threaded, closer to the C++ edition's model.
- `CASExampleHelper.cs` - `RegisterCommonCallbacks()` (receive/send/system-time
  callbacks; the 6-byte IPv4 connection string - 4 octets + big-endian port -
  packed/unpacked here once), `SendIAm()` targeting the local subnet broadcast,
  `GetLocalIPv4()` (via `System.Net.NetworkInformation.NetworkInterface`), and
  CLI helpers (`--help`/`--version`/`--deviceID`/`--port`).
- `CASBACnetStackExampleConstants.cs` - only the handful of BACnet enumeration
  values the vendored C# adapter (`submodules/cas-bacnet-stack/adapters/csharp/`)
  does not already define itself; everything the adapter already has
  (`OBJECT_TYPE_*`, `PROPERTY_IDENTIFIER_*`, `SERVICES_SUPPORTED_*`,
  `NETWORK_PORT_OBJECT_NETWORK_TYPE_IPV4`, `PROTOCOL_LEVEL_BACNET_APPLICATION`,
  `NETWORK_NUMBER_QUALITY_UNKNOWN`, `ERROR_INVALID_ARRAY_INDEX`) is reused
  directly from `CASBACnetStack.CASBACnetStackAdapter` instead of being
  duplicated - see the comment at the top of that file for the full mapping.

### First C# `common/` in this series - what makes it different from the C++/Node editions

This is the first C# `common/` in the BACnet profile example series, so
there is no prior C# pin to diff against. The systematic difference versus
the C++ and Node editions is the **binding mechanism**, not the stack's
callback API (both target the same `submodules/cas-bacnet-stack` `6.x`
branch, and every `GetProperty*` callback already carries the trailing
`errorCode` out-parameter, the folded `AddNetworkPortObject()`, and the
`*ForPort` transport callbacks keyed by Network Port instance - none of
that is new here):

- **No `LoadBACnetFunctions()` step.** The C++ adapter (in DLL mode) and the
  Node adapter both resolve the native library's symbols explicitly, at a
  point the application controls. The C# adapter's `BACnetStack_*` methods
  are plain `[DllImport] static extern` declarations - the .NET runtime
  resolves the native library the first time one of them is actually
  called, not up front. `Program.cs` treats the first such call
  (`PrintVersion()`, which calls `BACnetStack_GetAPIMajorVersion()`) as that
  check, wrapped in a `try`/`catch` for `DllNotFoundException` /
  `BadImageFormatException`.
- **Raw pointers, not marshaled buffers.** The Node adapter hands every
  `Get*Property` callback `Buffer` out-parameters; the C# adapter's delegates
  are declared with `byte*`/`uint*`/`float*` matching the native C ABI
  directly. This `common/` (and `Program.cs`) are therefore `unsafe`
  throughout.
- **Delegates must be rooted.** A callback registered with the stack must
  not be garbage-collected while native code may still call back into its
  marshalling thunk. Every delegate this file registers is stored in a
  `static readonly` field for that reason - see the comment on
  `receiveDelegate` in `CASExampleHelper.cs`.
