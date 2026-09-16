# Tutorial - extending and reviewing the B-SS example

[README.md](README.md) says what this example *is*. This document is the
*how*: how to extend it into your own device, who serves which property, how
to review the result for conformance, and what goes wrong when you get it
subtly right.

Read this once before you start changing `Program.cs`. The most expensive
mistake in this example is silent, and the section it lives in is
[Add a second analog input](#add-a-second-analog-input).

- [Extending the example](#extending-the-example)
- [What each object type needs you to serve](#what-each-object-type-needs-you-to-serve)
- [Who serves what: the application or the stack?](#who-serves-what-the-application-or-the-stack)
- [Interactive keys are a simplification](#interactive-keys-are-a-simplification)
- [Reviewing your device](#reviewing-your-device)
- [Troubleshooting](#troubleshooting)

## Extending the example

The example is intentionally small so it's easy to change.

**Change a sensor's value or name** - edit the constants / callbacks in
`Program.cs` (e.g. `g_analogInput1Value`, or the `"Bronze"` string in
`GetPropertyCharacterString`).

**Change the device identity before you ship** - vendor ID, vendor name,
model name, description, firmware revision and device name are all in the
`CHANGE ALL OF THIS BEFORE YOU SHIP` block at the top of `Program.cs` §1, with
a per-field note on each saying what to change it to. That block is the
authoritative checklist; it is in the source rather than here so it cannot be
skipped by someone who only reads the code. Keep the application-owned socket
+ `CASExampleHelper.RegisterCommonCallbacks()` shape, the habit of checking
every `BACnetStack_*` return value, and the delegates-rooted-in-static-fields
pattern - those are part of the shape, not the identity, and every sibling
example (C++ or Node) keeps the first two of those too.

### Add a second analog input

Read this whole recipe before starting - the last step is the one that is
easy to miss and the one BTL will fail you for.

> **Why there are four edits, not three - and why skipping one is SILENT.**
> Most of the `GetProperty*` callbacks match on **both** object type *and*
> instance (`objectInstance == ANALOG_INPUT_INSTANCE`), so a new instance
> falls through every one of them. `GetPropertyBool`'s `Out_Of_Service` branch
> is the exception: it matches on `propertyIdentifier` (and `objectType`)
> alone, so `Out_Of_Service` works for a new instance for free.
>
> Here is the part that matters, and it is the opposite of what most people
> assume. Falling through a callback does **not** reliably produce an error.
> The stack errors only for the few properties it refuses to invent -
> `Present_Value`, `Number_Of_States`, `Relinquish_Default`, `Local_Date`,
> `Local_Time`, and a Network Port's `APDU_Length`. For everything else it
> **silently substitutes a default**:
>
> | Property | If you forget to serve it | Loud? |
> |---|---|:--:|
> | `Present_Value` | Error (`value-not-initialized`) | yes |
> | `Object_Name` | reads back as the literal string **`"undefined"`** | **no** |
> | `Units` | reads back as **`no-units` (95)** | **no** |
> | `Out_Of_Service` | served on property + type alone - works by accident | n/a |
>
> **Doesn't the `errorCode` out-parameter fix this?** Only if you use it, and
> only where it is right to. Each `GetProperty*` callback receives a trailing
> `uint* errorCode` that the stack pre-seeds with `success` and reads only
> when you return `false`, so you *can* turn any decline into a chosen BACnet
> error. But writing an error code on the catch-all breaks the device: the
> stack's decline-and-fabricate path is what answers required properties an
> application is not expected to serve - the Device's
> `Max_APDU_Length_Accepted`, `APDU_Timeout` and `Number_Of_APDU_Retries`
> among them. Write `*errorCode` only where *this device* knows the read is
> wrong; `Program.cs` does it in exactly one place - `State_Text` with an
> out-of-range array index (`CASBACnetStackAdapter.ERROR_INVALID_ARRAY_INDEX`).
>
> It is worse than "wrong value": the object's `Property_List` **still
> advertises `Units` (117)**. So the object actively claims to have the
> property, and then answers with a default. Nothing on the wire says you
> forgot anything.
>
> So a half-added object does not look broken; it looks **healthy**. Add two
> of them and both report `Object_Name "undefined"` - duplicate object names
> inside one device, which is a spec violation and a hard BTL failure that
> every scan tool will render as a perfectly good object. **"It scanned OK"
> is exactly the failure mode, not evidence against it.**

```csharp
// 1) a new instance number (in §1 Configuration).
//    Naming: a second object of a type is "<Colour> 2" - so Analog Input 2 is
//    "Bronze 2", NOT a new colour. Each object TYPE owns one colour series-wide.
private const uint ANALOG_INPUT_2_INSTANCE = 2; // "Bronze 2"
private static float g_analogInput2Value = 23.1f; // its live value

// 2) add the object (in Main(), next to the other BACnetStack_AddObject calls).
//    Check the return, like every other stack call in this file.
if (!CASBACnetStackAdapter.BACnetStack_AddObject(g_deviceInstance, CASBACnetStackAdapter.OBJECT_TYPE_ANALOG_INPUT, ANALOG_INPUT_2_INSTANCE))
{
    Console.WriteLine("Error: Failed to add Analog Input 2 (Bronze 2).");
    return 1;
}

// 3) serve its Present_Value + Object_Name:
//    GetPropertyReal:            AI/2 + Present_Value -> *value = g_analogInput2Value
//    GetPropertyCharacterString: AI/2 + Object_Name    -> ReturnCharacterString("Bronze 2", ...)

// 4) DO NOT SKIP: serve its Units, in GetPropertyEnumerated.
//    Units is a REQUIRED property of an Analog Input. The existing check reads
//    `objectInstance == ANALOG_INPUT_INSTANCE`, which is instance 1 - so
//    without this, reading Analog Input 2's Units returns no-units(95) instead
//    of erroring, and the object is NON-CONFORMANT. It will still appear in
//    Object_List and its Present_Value will read back perfectly, so the device
//    looks healthy right up until BTL certification.
//    GetPropertyEnumerated: AI/2 + Units -> *value = CASBACnetStackExampleConstants.ENGINEERING_UNITS_DEGREES_CELSIUS
```

Then re-run the README's Verify steps **against Analog Input 2**, not just
Analog Input 1 - read every required property and **diff it against Analog
Input 1**. Any property that comes back `"undefined"`, `no-units`, or `0`
where object 1 returns something real is a step you missed. Because the
failure is silent (see the table above), this diff is the only thing that
catches it.

### Who serves what: applying it to a new object

There is no commandable-output section in this tutorial - **B-SS has no
outputs**: every object in this profile is a read-only input, so there is no
priority array, no `Relinquish_Default`, no `Set*` callback anywhere in
`Program.cs`. If you need a commandable object (Analog Output, Binary Output,
Multi-State Output, or a Value object with `Priority_Array` enabled), that is
a *different* profile (B-SA or B-ASC) - see
[BACnetProfileExample-B-ASC-Node](https://github.com/chipkin/BACnetProfileExample-B-ASC-Node)
for the commandable-output model (`Commandable`, `CommandWrite`,
`CommandRelinquish`) and the `Set*` callback shape it requires (there is no C#
edition of that example yet - port the shape from Node or C++ using this
example's P/Invoke conventions). Bolting that model onto this example without
also enabling `WriteProperty` (`SERVICES_SUPPORTED_WRITE_PROPERTY`) and the
object's `Priority_Array`/`Relinquish_Default` properties would not make the
object writable anyway - review
[§4 Application services supported](docs/PICS.md#4-application-services-supported)
in the PICS before you reach for it.

The recipe that *is* relevant to a Smart Sensor - adding another read-only
input of a **different** type - follows the same four-step shape as
[Add a second analog input](#add-a-second-analog-input) above: a new instance
number, `BACnetStack_AddObject`, `Object_Name` + the type's other required
value property (`Polarity` for a Binary Input, `Number_Of_States` for a
Multi-State Input), and a diff against the existing object of that type.

## What each object type needs you to serve

The application must serve every REQUIRED property the stack does not
generate. It differs per type - this is the checklist, so you do not have to
infer it:

| Object type | You must serve | Plus |
|---|---|---|
| Analog Input | `Present_Value` (Real), `Object_Name`, `Units` | `Out_Of_Service` |
| Binary Input | `Present_Value` (Enumerated), `Object_Name` | `Polarity`, `Out_Of_Service` |
| Multi-State Input | `Present_Value` (Unsigned), `Object_Name` | `Number_Of_States`, `Out_Of_Service` |

`Out_Of_Service` is served once, in `GetPropertyBool`, matched on
`propertyIdentifier` alone across all three input types plus the Network
Port - it is the one callback in this file that does NOT also match on
object type/instance, which is exactly why it is safe to leave unmatched: a
read-only sensor is never out of service, so the answer is always `false`.

## Who serves what: the application or the stack?

The single most common question when reading this file is "who answers this
property?" For Analog Input 1, the whole picture:

| Property | Served by | How |
|---|---|---|
| `Object_Identifier` | **stack** | generated from the object you added |
| `Object_Type` | **stack** | generated |
| `Object_List` | **stack** | generated (Device object) |
| `Property_List` | **stack** | generated |
| `Status_Flags` | **stack** | generated |
| `Event_State` | **stack**, sort of | no intrinsic alarming here, so nothing serves it - it reads `normal` only because `normal` is the enumeration's zero value and the stack substitutes a datatype default. Correct by coincidence, not design. |
| `Out_Of_Service` | **you** | `GetPropertyBool` - matched on property (+ type) only |
| `Present_Value` | **you** | `GetPropertyReal` |
| `Object_Name` | **you** | `GetPropertyCharacterString` |
| `Units` | **you** | `GetPropertyEnumerated` |

Every object, not just this one, is in [docs/PICS.md](docs/PICS.md).

Two more traps worth calling out explicitly, both silent:

- **Two `networkType`/`networkPortInstance` identifiers, easy to confuse.**
  The Network Port object's `NETWORK_PORT_OBJECT_NETWORK_TYPE_IPV4` (5, a
  *property* enumeration passed to `BACnetStack_AddNetworkPortObject`) is a
  *different* thing from the *link* identifier every transport call and
  `SendIAm` use - the Network Port object's own **instance**
  (`NETWORK_PORT_INSTANCE`, 1 in this example), which
  `common/CASExampleHelper.cs`'s `RegisterCommonCallbacks()` and `SendIAm()`
  both take as a parameter. Mixing them up compiles (both are plain integers)
  and can silently misroute a multi-port device; a single-port example like
  this one is forgiving of the mistake, which is exactly why it is worth
  naming.
- **The stack's own errorCode default is NOT "no error."** `GetPropertyCharacterString`'s
  `State_Text` branch is the one place this file writes `*errorCode` - it is
  pre-seeded with `success`, so a `return false` there without setting it
  would put "Error Class PROPERTY, Error Code success" on the wire instead of
  `invalid-array-index`. Every other `return false` in this file leaves
  `*errorCode` untouched on purpose (see the callout in
  [Add a second analog input](#add-a-second-analog-input) above).

Going beyond this (WriteProperty, COV, alarms, scheduling) means implementing
a richer profile - see the series table in [README.md](README.md).

## Interactive keys are a simplification

The C++ edition polls raw console keys (including arrow-key escape sequences)
every millisecond in its own `while` loop. The Node edition sets stdin raw
mode via `readline.emitKeypressEvents`. This C# edition uses .NET's built-in
non-blocking `Console.KeyAvailable` + `Console.ReadKey(true)`, which .NET
already decodes into a `ConsoleKey` enum (`ConsoleKey.UpArrow`, etc.) without
any manual escape-sequence parsing - genuinely simpler here, not a cut corner.

The one thing this approach cannot do is read keys when stdin is **not** a
real, non-redirected console (a background process, a CI smoke test, output
piped to a file): `Console.KeyAvailable` throws `InvalidOperationException`
in that case. `Program.cs` checks `Console.IsInputRedirected` once at start-up
and skips key polling entirely when it is true - the device still runs
(`BACnetStack_Tick()` keeps ticking, and Ctrl+C or `SIGTERM` still work via
`Console.CancelKeyPress`), it just cannot be nudged from the keyboard. This
mirrors the Node edition's `process.stdin.isTTY` check and the CI smoke test
in `.github/workflows/release.yml`, which never touches the keyboard.

## Reviewing your device

After you have changed anything, review it against the conformance statement
rather than against "it looked fine in the explorer":

1. Regenerate [docs/PICS.md](docs/PICS.md) after editing `docs/objects.json`
   (see [Keeping the PICS honest](#keeping-the-pics-honest) below). A ⚠ row is
   a required property nothing serves.
2. Read **every** property listed for **every** object with a BACnet client,
   and compare the value against the PICS. `"undefined"`, `no-units` and `0`
   are the three shapes a missed callback takes.
3. Diff a new object of a type against the existing one of that type. Anything
   that differs and shouldn't is a callback that matched on instance.
4. Send a **WriteProperty** to any object and confirm it is rejected - a B-SS
   Smart Sensor is read-only, and this example never registers a `Set*`
   callback, so the stack has nothing to consult and declines on its own.
5. Read `State_Text[4]` on Multi-State Input 1 (which only has 3 states) and
   confirm `invalid-array-index`, not an empty string.

### Keeping the PICS honest

`docs/PICS.md` is partly generated. `docs/objects.json` describes each object
and who serves which property; the series tool regenerates the object tables
from it plus the stack's own `docs/property-profile-reference.md` at the
pinned commit:

```bash
python tools/gen-objects-properties.py BACnetProfileExample-B-SS-CS            # rewrite
python tools/gen-objects-properties.py BACnetProfileExample-B-SS-CS --check    # fail if stale
```

(That tool lives in the example-series repository, not in this one. If you
only have this repository, edit the generated block by hand and keep it
matching the callbacks in `Program.cs`.)

When you add an object or a property to `Program.cs`, update
`docs/objects.json` in the same change and regenerate. The `app` list is what
the callbacks serve; `accepted` is for a required property you deliberately
leave to the stack's default, and each one needs a justification. Anything
required, not in `app` and not in `accepted`, comes out as a ⚠ row - that is a
defect, not a feature.

## Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `DllNotFoundException: Unable to load DLL 'CASBACnetStack_x64_Release'` | The native library is missing from the build output directory. See README.md [Build the native CAS BACnet Stack library](README.md#build-the-native-cas-bacnet-stack-library) - it is a separate step from `dotnet build`, and must land next to `BACnetProfileExampleBSSCS.dll`/`.exe`. |
| `DllNotFoundException` mentioning `CASBACnetStack_x64_Debug` | You built/ran in `Debug` configuration. The stack's MSVC solution only ships a `ReleaseDll` configuration - see README.md [Why only a Release build](README.md#why-only-a-release-build). Build and run with `-c Release`. |
| `BadImageFormatException` | The native library's architecture does not match this process. This example targets `x64` (`<PlatformTarget>x64</PlatformTarget>`) - make sure the DLL/.so you built is also x64. |
| `System.InvalidOperationException` from `Console.KeyAvailable` | Should not happen - `Program.cs` checks `Console.IsInputRedirected` first and skips key polling when true. If you see this after a local edit, you likely removed that check; see [Interactive keys are a simplification](#interactive-keys-are-a-simplification). |
| App prints *"could not bind UDP port 47808"* | Another BACnet program is already using 47808. Stop it, or run with `--port <n>`. |
| Client sends Who-Is but sees no I-Am | Firewall is blocking UDP 47808, or the client and device are on different subnets (Who-Is is a broadcast). Allow the port; test on the same subnet first. |
| A WriteProperty you sent is rejected | Correct behaviour - this device is read-only. There is no `RegisterCallbackSetProperty*` call anywhere in `Program.cs`; the stack declines every write without consulting the application. |
