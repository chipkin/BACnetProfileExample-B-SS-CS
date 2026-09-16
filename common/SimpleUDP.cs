// SPDX-License-Identifier: CC0-1.0
// Public-domain example code (CC0) - see ../LICENSE.

// SimpleUDP.cs
// =============================================================================
// A minimal UDP wrapper for the BACnet examples - the C# edition of the C++
// examples' common/SimpleUDP.{h,cpp} (and the Node edition's
// common/SimpleUDP.ts), with the same responsibilities:
//
//   - own ONE datagram socket bound to the BACnet/IP port,
//   - let the stack PULL inbound datagrams one at a time (TryReceive) from its
//     receive callback (the stack never owns the socket - the application does),
//   - send outbound datagrams where the stack's send callback points.
//
// Unlike the Node edition, this is NOT queue-based: .NET's Socket exposes a
// synchronous, non-blocking "how many bytes are waiting" check (Available), so
// TryReceive() polls the OS socket directly on every stack tick rather than
// draining a background-thread-fed queue. That keeps this whole class
// single-threaded, matching the C++ edition's synchronous model - there is no
// lock anywhere in this file. The BACnet examples series' shared convention
// (see common/README.md) is one call to TryReceive() per stack Tick(); this
// class assumes that, not concurrent access from multiple threads.
//
// The class never sees a BACnet "connection string" - it deals in host-order
// ip/port pairs. Packing the 6-byte connection string (4 IP octets + 2 port
// bytes, port BIG-endian) is CASExampleHelper's job, exactly as in the C++ and
// Node editions.
// =============================================================================

using System;
using System.Net;
using System.Net.Sockets;

namespace BACnetProfileExampleBSSCS.Common
{
    /// <summary>One received datagram, handed to the stack's receive callback.</summary>
    public sealed class ReceivedDatagram
    {
        public byte[] Message;
        public string FromIp;
        public int FromPort;
    }

    public sealed class SimpleUDP
    {
        private Socket socket;

        /// <summary>
        /// Bind the socket. Throws SocketException on bind failure (for example:
        /// another BACnet device already owns the port exclusively).
        /// </summary>
        public void Setup(int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.EnableBroadcast = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        /// <summary>The next queued inbound datagram, or false if none arrived.</summary>
        public bool TryReceive(out ReceivedDatagram datagram)
        {
            datagram = null;
            if (socket == null || socket.Available <= 0)
            {
                return false;
            }
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[2048]; // comfortably larger than the BACnet/IP APDU max (1497)
            int received;
            try
            {
                received = socket.ReceiveFrom(buffer, ref remote);
            }
            catch (SocketException ex)
            {
                Console.Error.WriteLine("Error: UDP receive failed: " + ex.Message);
                return false;
            }
            IPEndPoint remoteEndPoint = (IPEndPoint)remote;
            byte[] message = new byte[received];
            Array.Copy(buffer, message, received);
            datagram = new ReceivedDatagram
            {
                Message = message,
                FromIp = remoteEndPoint.Address.ToString(),
                FromPort = remoteEndPoint.Port
            };
            return true;
        }

        /// <summary>
        /// Send one datagram. Fire-and-forget by design: UDP gives no delivery
        /// guarantee anyway, so a send error is logged, not thrown - same
        /// behaviour as the C++/Node SimpleUDP.Send.
        /// </summary>
        public void Send(byte[] message, string toIp, int toPort)
        {
            if (socket == null)
            {
                return;
            }
            try
            {
                socket.SendTo(message, new IPEndPoint(IPAddress.Parse(toIp), toPort));
            }
            catch (SocketException ex)
            {
                Console.Error.WriteLine("Error: UDP send to " + toIp + ":" + toPort + " failed: " + ex.Message);
            }
        }

        /// <summary>Close the socket.</summary>
        public void Shutdown()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
        }
    }
}
