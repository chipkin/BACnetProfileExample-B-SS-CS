// SPDX-License-Identifier: CC0-1.0
// Public-domain example code (CC0) - see ../LICENSE.

// CASExampleHelper.cs
// =============================================================================
// Shared plumbing for the BACnet examples - the C# edition of the C++
// examples' common/CASExampleHelper.{h,cpp} (and the Node edition's
// common/CASExampleHelper.ts), same responsibilities, same names:
//
//   - RegisterCommonCallbacks(): the three transport/time callbacks every
//     example needs (receive, send, system time). This is where the 6-byte
//     IPv4 connection string lives: 4 IP octets then the port in BIG-endian
//     byte order - written here ONCE so no example re-derives it.
//   - SendIAm(): the unsolicited I-Am every example transmits on start-up.
//   - GetLocalIPv4(): the primary interface's address/netmask/broadcast (the
//     OS-level plumbing the Network Port object reports).
//   - CLI helpers (--help/--version/--deviceID/--port) + PrintVersion().
//
// The stack PULLS datagrams: its receive callback asks for one queued datagram
// per call and the stack never touches the socket. The application (SimpleUDP)
// owns the socket - keep that split in your own project.
//
// PORT-KEYED TRANSPORT (this example's stack pin, branch `6.x`). A link is
// identified by the Network Port object's INSTANCE, not by a transport-type
// enumeration: BACnetStack_RegisterCallbackReceiveMessageForPort /
// BACnetStack_RegisterCallbackSendMessageForPort take/report a
// networkPortInstance, and BACnetStack_SendIAm's fourth argument is that same
// instance. This example has exactly one Network Port (instance 1,
// "Vermilion" - see NETWORK_PORT_INSTANCE in Program.cs), so every call below
// names it directly rather than looping over a port table.
//
// UNSAFE / POINTERS. Unlike the Node adapter (which marshals every out-param
// as a Buffer), the C# adapter's delegates are declared with raw pointers
// (byte*, uint*) matching the native C ABI 1:1 - see
// submodules/cas-bacnet-stack/adapters/csharp/CASBACnetStackAdapterBindings.cs.
// This file (and Program.cs) are therefore `unsafe` throughout, and the
// project sets <AllowUnsafeBlocks>true</AllowUnsafeBlocks>.
// =============================================================================

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using CASBACnetStack;

namespace BACnetProfileExampleBSSCS.Common
{
    public static class CASExampleHelper
    {
        // The version of the vendored common/ helper itself (NOT the example's
        // version). Bump it whenever anything in common/ changes, and record the
        // change in common/CHANGELOG.md.
        public const string COMMON_VERSION = "1.1.0";

        // The delegate instances passed to BACnetStack_RegisterCallback* must be
        // kept alive for as long as the native stack may call them - a local
        // variable would be eligible for GC as soon as RegisterCommonCallbacks()
        // returns, which would crash the process the next time the stack invokes
        // a collected delegate's native thunk. Static fields keep them rooted for
        // the life of the process.
        private static CASBACnetStackAdapter.FPCallbackReceiveMessageForPort receiveDelegate;
        private static CASBACnetStackAdapter.FPCallbackSendMessageForPort sendDelegate;
        private static CASBACnetStackAdapter.FPCallbackGetSystemTime systemTimeDelegate;

        // ---------------------------------------------------------------------
        // Local IPv4 discovery
        // ---------------------------------------------------------------------

        public struct LocalIPv4
        {
            public string Address;  // e.g. "192.168.1.20"
            public string Netmask;  // e.g. "255.255.255.0"
            public string Broadcast; // e.g. "192.168.1.255" (address | ~netmask)
        }

        /// <summary>
        /// The primary non-loopback IPv4 interface. The Network Port object
        /// reports these values, and SendIAm() targets the derived subnet
        /// broadcast.
        /// </summary>
        public static LocalIPv4 GetLocalIPv4()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }
                    byte[] ipBytes = addr.Address.GetAddressBytes();
                    byte[] maskBytes = addr.IPv4Mask != null
                        ? addr.IPv4Mask.GetAddressBytes()
                        : new byte[] { 255, 255, 255, 0 };
                    byte[] broadcastBytes = new byte[4];
                    for (int i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(ipBytes[i] | (~maskBytes[i] & 0xFF));
                    }
                    return new LocalIPv4
                    {
                        Address = addr.Address.ToString(),
                        Netmask = new IPAddress(maskBytes).ToString(),
                        Broadcast = new IPAddress(broadcastBytes).ToString()
                    };
                }
            }
            // No network: loopback keeps the example runnable on an offline machine.
            return new LocalIPv4 { Address = "127.0.0.1", Netmask = "255.0.0.0", Broadcast = "127.255.255.255" };
        }

        // ---------------------------------------------------------------------
        // The three common callbacks
        // ---------------------------------------------------------------------

        /// <summary>
        /// Register the receive/send/system-time callbacks against one
        /// SimpleUDP, for the given Network Port object instance. Call once,
        /// before AddDevice.
        /// </summary>
        /// <param name="udp">the socket this example owns.</param>
        /// <param name="networkPortInstance">the instance passed to
        /// BACnetStack_AddNetworkPortObject for this socket (this example:
        /// NETWORK_PORT_INSTANCE, 1).</param>
        public static unsafe void RegisterCommonCallbacks(SimpleUDP udp, uint networkPortInstance)
        {
            receiveDelegate = (byte* message, ushort maxMessageLength,
                                byte* sourceConnectionString, byte* sourceConnectionStringLength,
                                byte* destinationConnectionString, byte* destinationConnectionStringLength,
                                byte maxConnectionStringLength, uint* outNetworkPortInstance) =>
            {
                // The out-params arrive UNINITIALIZED - write every one we do not
                // fill with real data, or the stack reads garbage.
                *sourceConnectionStringLength = 0;
                *destinationConnectionStringLength = 0;
                if (maxConnectionStringLength < 6)
                {
                    return (ushort)0; // cannot even fit an IPv4 connection string
                }
                if (!udp.TryReceive(out ReceivedDatagram datagram))
                {
                    return (ushort)0; // nothing waiting this tick
                }
                if (datagram.Message.Length > maxMessageLength)
                {
                    Console.Error.WriteLine(
                        "Error: dropping " + datagram.Message.Length + "-byte datagram from " +
                        datagram.FromIp + ":" + datagram.FromPort + " - larger than the stack's " +
                        maxMessageLength + "-byte receive buffer.");
                    return (ushort)0;
                }
                Marshal.Copy(datagram.Message, 0, (IntPtr)message, datagram.Message.Length);
                // 6-byte IPv4 connection string: 4 IP octets, then the port BIG-endian.
                string[] octets = datagram.FromIp.Split('.');
                sourceConnectionString[0] = byte.Parse(octets[0]);
                sourceConnectionString[1] = byte.Parse(octets[1]);
                sourceConnectionString[2] = byte.Parse(octets[2]);
                sourceConnectionString[3] = byte.Parse(octets[3]);
                sourceConnectionString[4] = (byte)((datagram.FromPort >> 8) & 0xFF);
                sourceConnectionString[5] = (byte)(datagram.FromPort & 0xFF);
                *sourceConnectionStringLength = 6;
                // Which Network Port object this datagram arrived on.
                *outNetworkPortInstance = networkPortInstance;
                return (ushort)datagram.Message.Length;
            };
            CASBACnetStackAdapter.BACnetStack_RegisterCallbackReceiveMessageForPort(receiveDelegate);

            // A NULL / length-0 connection string is the stack saying "broadcast on
            // this data link" (BACnetStack_RegisterCallbackSendMessageForPort's
            // contract in CASBACnetStackDLL.h): send it to the local subnet
            // broadcast on this socket's port (C++ common/ 3.2.0). A broadcast
            // addressed to THIS socket's own address means "this subnet" too - the
            // stack copies its own looped-back I-Am address for a Device_Address_Binding
            // Who-Is (stack issue #3933) - so it also goes to the local subnet
            // broadcast (C++ common/ 3.4.0). Every other address is sent to as given.
            // A connection string of 1-5 octets is not a BACnet/IP address: refused.
            int boundPort = udp.Port;
            LocalIPv4 local = GetLocalIPv4(); // read once, not on every send
            sendDelegate = (byte* message, ushort messageLength,
                             byte* connectionString, byte connectionStringLength,
                             uint sendNetworkPortInstance, bool broadcast) =>
            {
                string toIp;
                int toPort;
                if (connectionString == null || connectionStringLength == 0)
                {
                    toIp = local.Broadcast;
                    toPort = boundPort;
                }
                else if (connectionStringLength < 6)
                {
                    return (ushort)0;
                }
                else
                {
                    toIp = connectionString[0] + "." + connectionString[1] + "." +
                           connectionString[2] + "." + connectionString[3];
                    toPort = (connectionString[4] << 8) | connectionString[5];
                    if (broadcast && toPort == boundPort && toIp == local.Address)
                    {
                        toIp = local.Broadcast;
                    }
                }
                byte[] buffer = new byte[messageLength];
                Marshal.Copy((IntPtr)message, buffer, 0, messageLength);
                udp.Send(buffer, toIp, toPort);
                return messageLength;
            };
            CASBACnetStackAdapter.BACnetStack_RegisterCallbackSendMessageForPort(sendDelegate);

            systemTimeDelegate = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // unix epoch SECONDS
            CASBACnetStackAdapter.BACnetStack_RegisterCallbackGetSystemTime(systemTimeDelegate);
        }

        // ---------------------------------------------------------------------
        // I-Am
        // ---------------------------------------------------------------------

        /// <summary>
        /// Broadcast an unsolicited I-Am announcing this device - every example
        /// sends one on start-up. Targets the LOCAL subnet broadcast (the
        /// device's own network) rather than the global 255.255.255.255 /
        /// network 0xFFFF: the broadcast is ip | ~mask of the primary IPv4
        /// interface - the same network the Network Port object reports.
        /// </summary>
        /// <param name="networkPortInstance">the Network Port object to send on
        /// (this example: NETWORK_PORT_INSTANCE, 1) - the fourth
        /// BACnetStack_SendIAm argument identifies the LINK by Network Port
        /// instance, not by a transport-type enumeration.</param>
        public static unsafe bool SendIAm(uint deviceInstance, ushort udpPort, uint networkPortInstance)
        {
            LocalIPv4 local = GetLocalIPv4();
            string[] octets = local.Broadcast.Split('.');
            byte[] connectionString = new byte[6];
            connectionString[0] = byte.Parse(octets[0]);
            connectionString[1] = byte.Parse(octets[1]);
            connectionString[2] = byte.Parse(octets[2]);
            connectionString[3] = byte.Parse(octets[3]);
            connectionString[4] = (byte)((udpPort >> 8) & 0xFF);
            connectionString[5] = (byte)(udpPort & 0xFF);
            fixed (byte* connectionStringPtr = connectionString)
            {
                return CASBACnetStackAdapter.BACnetStack_SendIAm(
                    deviceInstance, connectionStringPtr, 6, networkPortInstance,
                    true /* broadcast */, 0 /* local network */, null, 0);
            }
        }

        // ---------------------------------------------------------------------
        // CLI helpers (--help / --version / --deviceID / --port)
        // ---------------------------------------------------------------------

        /// <summary>
        /// The native CAS BACnet Stack library's file name on this OS, as the adapter's
        /// [DllImport] resolves it: CASBACnetStack_x64_Release.dll on Windows,
        /// libCASBACnetStack_x64_Release.so on Linux (..._Debug in a Debug build).
        /// </summary>
        public static string StackLibraryFileName()
        {
            string name = CASBACnetStackAdapter.BACNET_API_DLL_FILENAME;
            if (OperatingSystem.IsWindows())
            {
                return name + ".dll";
            }
            return "lib" + name + (OperatingSystem.IsMacOS() ? ".dylib" : ".so");
        }

        public static void PrintVersion(string appName, string appVersion)
        {
            Console.WriteLine(appName + " v" + appVersion);
            // --help and --version must work without the native CAS BACnet Stack
            // library (a release does not include it): if it cannot be loaded, say
            // so on the stack-version line instead of throwing.
            string stackVersion;
            try
            {
                stackVersion =
                    CASBACnetStackAdapter.BACnetStack_GetAPIMajorVersion() + "." +
                    CASBACnetStackAdapter.BACnetStack_GetAPIMinorVersion() + "." +
                    CASBACnetStackAdapter.BACnetStack_GetAPIPatchVersion() + "." +
                    CASBACnetStackAdapter.BACnetStack_GetAPIBuildVersion();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException ||
                                       ex is EntryPointNotFoundException)
            {
                stackVersion = "not available (the native library " + StackLibraryFileName() +
                    " could not be loaded)";
            }
            Console.WriteLine("CAS BACnet Stack version: " + stackVersion);
            // The vendored common/ helper has its own version (common/CHANGELOG.md)
            // so it is easy to tell whether this example's copy is stale.
            Console.WriteLine("Common helper (common/) version: " + COMMON_VERSION);
        }

        /// <summary>The interactive key list (the 'h' key and --help both print it).</summary>
        public static void PrintInteractiveCommands()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  h     - show this help (version + commands)");
            Console.WriteLine("  q     - quit");
            Console.WriteLine("  up    - increase Analog Input 1 by 1.1");
            Console.WriteLine("  down  - decrease Analog Input 1 by 1.1");
        }

        /// <summary>The 'h' key: version banner, then the key list.</summary>
        public static void PrintHelp(string appName, string appVersion)
        {
            PrintVersion(appName, appVersion);
            PrintInteractiveCommands();
        }

        // ---------------------------------------------------------------------
        // Interactive keys
        // ---------------------------------------------------------------------

        public enum KeyCommand { None, Help, Quit, ArrowUp, ArrowDown }

        // Keys typed on a redirected stdin (a pipe - how the Wire Tests' harness
        // stops an example, by writing 'q'). Read on a background thread, because
        // a pipe has no non-blocking "is a key waiting" check; the main loop only
        // ever reads this queue. A real console uses Console.KeyAvailable instead.
        private static readonly System.Collections.Concurrent.ConcurrentQueue<KeyCommand> pipedKeys =
            new System.Collections.Concurrent.ConcurrentQueue<KeyCommand>();
        private static System.Threading.Thread pipedKeyReader;

        /// <summary>
        /// The key pressed since the last call, or KeyCommand.None - never blocks.
        /// Same keys as the C++ common/ PollKey(): h, q, up/down arrows. On a
        /// redirected stdin only the letter keys exist (a pipe carries no arrows).
        /// </summary>
        public static KeyCommand PollKey()
        {
            if (!Console.IsInputRedirected)
            {
                if (!Console.KeyAvailable)
                {
                    return KeyCommand.None;
                }
                ConsoleKeyInfo key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.H: return KeyCommand.Help;
                    case ConsoleKey.Q: return KeyCommand.Quit;
                    case ConsoleKey.UpArrow: return KeyCommand.ArrowUp;
                    case ConsoleKey.DownArrow: return KeyCommand.ArrowDown;
                    default: return KeyCommand.None;
                }
            }
            if (pipedKeyReader == null)
            {
                pipedKeyReader = new System.Threading.Thread(() =>
                {
                    int character;
                    while ((character = Console.In.Read()) >= 0)
                    {
                        if (character == 'q' || character == 'Q')
                        {
                            pipedKeys.Enqueue(KeyCommand.Quit);
                        }
                        else if (character == 'h' || character == 'H')
                        {
                            pipedKeys.Enqueue(KeyCommand.Help);
                        }
                    }
                });
                pipedKeyReader.IsBackground = true; // never keeps the process alive on exit
                pipedKeyReader.Start();
            }
            return pipedKeys.TryDequeue(out KeyCommand pipedKey) ? pipedKey : KeyCommand.None;
        }

        /// <summary>Prints help/version and returns true if the caller should exit.</summary>
        public static bool HandleHelpAndVersionArgs(string[] args, string appName, string appVersion)
        {
            foreach (string arg in args)
            {
                if (arg == "--help" || arg == "-h")
                {
                    PrintVersion(appName, appVersion);
                    Console.WriteLine();
                    Console.WriteLine("Options:");
                    Console.WriteLine("  --help              Show this help and exit");
                    Console.WriteLine("  --version           Show version information and exit");
                    Console.WriteLine("  --deviceID <inst>   BACnet Device instance (0..4194302)");
                    Console.WriteLine("  --port <udp>        BACnet/IP UDP port (default 47808)");
                    Console.WriteLine();
                    PrintInteractiveCommands();
                    return true;
                }
            }
            foreach (string arg in args)
            {
                if (arg == "--version")
                {
                    PrintVersion(appName, appVersion);
                    return true;
                }
            }
            return false;
        }

        private static int ParseNumberArg(string[] args, string name, int min, int max, int fallback)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0)
            {
                return fallback;
            }
            string raw = (index + 1) < args.Length ? args[index + 1] : null;
            int value = 0;
            if (raw == null || !int.TryParse(raw, out value) || value < min || value > max)
            {
                Console.Error.WriteLine("Error: " + name + " expects an integer " + min + ".." + max +
                    ", got \"" + (raw ?? "") + "\".");
                Environment.Exit(1);
            }
            return value;
        }

        public static ushort ParsePortArg(string[] args, ushort fallback)
        {
            return (ushort)ParseNumberArg(args, "--port", 1, 65535, fallback);
        }

        public static uint ParseDeviceIdArg(string[] args, uint fallback)
        {
            // 4194303 is the BACnet "unconfigured" sentinel - a real device may not use it.
            return (uint)ParseNumberArg(args, "--deviceID", 0, 4194302, (int)fallback);
        }
    }
}
