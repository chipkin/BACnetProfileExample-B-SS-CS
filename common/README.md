# `common/` - shared example plumbing (C# edition)

The helpers every C# example in the BACnet profile example series will share.
**Vendored**: this directory is a *copy* in each example repo, not a package -
change it in one repo and you must sweep the same change to every sibling and
bump `COMMON_VERSION` (in `CASExampleHelper.cs`) + add a `CHANGELOG.md` entry.
This is the *first* C# example in the series, so there is no sibling to sweep
to yet - the next C# example in the series starts by copying this folder.

## Versioning

`COMMON_VERSION` in `CASExampleHelper.cs`, changelog in `common/CHANGELOG.md`.
The C# common versions independently of the C++ and Node `common/` (each at
its own version) - same rules, separate lineage. This copy starts at
**1.0.0**: the first C# `common/`, built against the
`submodules/cas-bacnet-stack` `6.x` branch's current adapter/callback API. See
`common/CHANGELOG.md` for what's in it and how the C# binding mechanism
differs from the C++/Node editions.

## What's here

| File | What it is |
|---|---|
| `SimpleUDP.cs` | The UDP socket the application owns: bind, poll for inbound datagrams (`Socket.Available`, no background thread), send. The stack never touches the socket - it pulls datagrams through the receive callback. |
| `CASExampleHelper.cs` | `RegisterCommonCallbacks()` (receive/send/system-time + the 6-byte IPv4 connection string, port big-endian, written here ONCE), `SendIAm()`, `GetLocalIPv4()`, and CLI helpers. |
| `CASBACnetStackExampleConstants.cs` | The handful of BACnet enumeration values this example needs that the vendored C# adapter does not already define itself - see the file's header comment for the full mapping against `CASBACnetStack.CASBACnetStackAdapter`'s own enumerations. |

## How Program.cs uses it

```csharp
var udp = new SimpleUDP();
udp.Setup(port);
CASExampleHelper.RegisterCommonCallbacks(udp, NETWORK_PORT_INSTANCE); // before AddDevice
// ... AddDevice, objects, services ...
CASExampleHelper.SendIAm(deviceInstance, port, NETWORK_PORT_INSTANCE); // announce on start-up
while (running) { CASBACnetStackAdapter.BACnetStack_Tick(); Thread.Sleep(1); }
```

Unlike the C++/Node `common/`, there is no `RestartKind`/`RequestRestart`/
`RestartDue` deferred-restart pattern here: B-SS does not implement DM-RD-B
(ReinitializeDevice), so this copy of `common/` does not carry code for a
capability no example using it needs yet. Add it back (port from a sibling
that has it, once one exists) if you build a C# profile example that requires
DM-RD-B.

## Why this file is `unsafe`

The C# adapter's `Get*Property`/`Set*Property`/transport delegates are
declared with raw pointers (`byte*`, `uint*`, `float*`), matching the native
CAS BACnet Stack's C ABI directly rather than marshaling into managed buffers.
`CASExampleHelper.cs` and `Program.cs` are therefore `unsafe` throughout, and
the project sets `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`. See
`CASBACnetStackAdapterBindings.cs` in the vendored adapter for the full
delegate declarations.
