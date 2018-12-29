using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace Sound_Track_Win
{
    namespace NetworkAudio
    {
        public class MessageData
        {
            public string ID;
            public enum MessageType
            {
                ping,               // "you there?"
                acknowledge,        // Generic receipt or yes
                rxFailed,           // Request to resend
                deny,               // Generic no
                requestTracking,    // Device requests permission to be tracking target
                statusRequest,      // Speaker requests settings details from server
                startPlayback,      // Tell speaker to start output
                suspendPlayback,    // Tell speaker to stop output
                requestID,          // Requests server to create and send ID
                // Below requires message string (above doesn't)
                giveID,             // Server gives ID to client
                playbackCommand,    // Server giving playback adjustment commands (temporary) to speaker
                playbackSettings,   // Either server giving speaker its base parameters for playback or controlling device giving server overall settings
                reportProximity,    // Tracking device gives beacons in proximity or other tracking factor
                mediaInfo,          // Information on the current playback
                errorMessage,       // Text message to be displayed as error
                displayMessage      // Text message to display
            }
            public string Message { get; set; }
        }

        public class ServerResource
        {
            public string Name { get; set; }
            public IPAddress IP { get; set; }
            public string ID { get; private set; }
            public int StreamPort { get; set; }
            public int CommPort { get; set; }
            public int RestPort { get; set; }

            public ServerResource(string serverName, IPAddress serverIP, string serverID, int serverStreamPort, int serverCommPort, int serverRestPort)
            {
                Name = serverName;
                IP = serverIP;
                ID = serverID;
                StreamPort = serverStreamPort;
                CommPort = serverCommPort;
                RestPort = serverRestPort;

                ID.Trim();
                if (ID != "")
                {
                    if (ID.Length != 10)
                    {
                        ID = "";
                        throw new ArgumentException("Invalid ID: must be 10 characters long");
                    }

                    Regex IDCheck = new Regex(@"[^a-z,A-Z,0-9](?n)", RegexOptions.Compiled);
                    if (IDCheck.Matches(ID).Count > 0)
                    {
                        ID = "";
                        throw new ArgumentException("Invalid ID: invalid characters");
                    }
                }
            }
        }

        class ProbeData
        {
            public string Key { get; set; }
            public int ResponsePort { get; set; }

            public int ByteLength
            {
                get
                {
                    List<byte> data = new List<byte>();
                    data.AddRange(Encoding.UTF8.GetBytes(Key));
                    data.AddRange(BitConverter.GetBytes(ResponsePort));
                    return data.Count;
                }
            }
            public static int FixedByteLength
            {
                get
                {
                    List<byte> data = new List<byte>();
                    data.AddRange(Encoding.UTF8.GetBytes("soundtrack"));
                    data.AddRange(BitConverter.GetBytes(new int()));
                    return data.Count;
                }
            }

            public ProbeData()
            {
                Key = "";
                ResponsePort = 0;
            }

            public ProbeData(byte[] data) { FromBytes(data); }

            public ProbeData(int port)
            {
                Key = "soundtrack";
                ResponsePort = port;
            }

            public bool IsGoodData { get { return Key == "soundtrack"; } }

            public byte[] ToBytes()
            {
                List<byte> data = new List<byte>();
                data.AddRange(Encoding.UTF8.GetBytes(Key));
                data.AddRange(BitConverter.GetBytes(ResponsePort));
                return data.ToArray();
            }

            public void FromBytes(byte[] data)
            {
                try
                {
                    Key = Encoding.UTF8.GetString(data, 0, 10);
                    ResponsePort = BitConverter.ToInt32(data, 10);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                    Key = "";
                    ResponsePort = 0;
                }
            }
        }

        class DeviceData
        {
            public string Key { get; set; }
            public DeviceType ThisDevice { get; set; }
            public string ID { get; set; }
            public int StreamPort { get; set; }
            public int CommPort { get; set; }
            public int RestPort { get; set; }
            public string DeviceName { get; set; }

            public int ByteLength
            {
                get
                {
                    List<byte> data = new List<byte>();
                    data.AddRange(Encoding.UTF8.GetBytes(Key));
                    data.Add((byte)ThisDevice);
                    data.AddRange(BitConverter.GetBytes(StreamPort));
                    data.AddRange(BitConverter.GetBytes(CommPort));
                    data.AddRange(BitConverter.GetBytes(RestPort));
                    return data.Count + 265; // 10 for ID, 255 for name
                }
            }
            public static int FixedByteLength
            {
                get
                {
                    List<byte> data = new List<byte>();
                    data.AddRange(Encoding.UTF8.GetBytes("soundtrack"));
                    data.Add(new byte());
                    data.AddRange(BitConverter.GetBytes(new int()));
                    data.AddRange(BitConverter.GetBytes(new int()));
                    data.AddRange(BitConverter.GetBytes(new int()));
                    return data.Count + 265; // 10 for ID, 255 for name
                }
            }

            public enum DeviceType
            {
                unknown,
                dedicatedOutput,
                smartOutput,
                server
            }

            public DeviceData()
            {
                Key = "";
                ThisDevice = DeviceType.unknown;
                ID = "";
                StreamPort = 0;
                CommPort = 0;
                RestPort = 0;
                DeviceName = "";
            }

            public DeviceData(DeviceType device, string id, int streamPort, int updatePort, int restPort, string deviceName)
            {
                Key = "soundtrack";
                ThisDevice = device;
                ID = id;
                this.StreamPort = streamPort;
                this.CommPort = updatePort;
                this.RestPort = restPort;
                this.DeviceName = deviceName;
            }

            public DeviceData(ServerResource server)
            {
                Key = "soundtrack";
                ThisDevice = DeviceType.server;
                ID = server.ID;
                StreamPort = server.StreamPort;
                CommPort = server.CommPort;
                RestPort = server.RestPort;
                DeviceName = server.Name;

            }

            public DeviceData(byte[] data) { FromBytes(data); }

            public byte[] ToBytes()
            {
                if (DeviceName.Length < 255) { DeviceName = DeviceName.PadRight(255); }
                else { DeviceName = DeviceName.Substring(0, 255); }

                if (ID.Length != 10)
                {
                    byte[] nullChars = new byte[10];
                    ID = Encoding.UTF8.GetString(nullChars);
                }

                List<byte> data = new List<byte>();
                data.AddRange(Encoding.UTF8.GetBytes(Key));
                data.Add((byte)ThisDevice);
                data.AddRange(Encoding.UTF8.GetBytes(ID));
                data.AddRange(BitConverter.GetBytes(StreamPort));
                data.AddRange(BitConverter.GetBytes(CommPort));
                data.AddRange(BitConverter.GetBytes(RestPort));
                data.AddRange(Encoding.UTF8.GetBytes(DeviceName));
                return data.ToArray();
            }

            public void FromBytes(byte[] data)
            {
                try
                {
                    Key = Encoding.UTF8.GetString(data, 0, 10);
                    ThisDevice = (DeviceType)data[10];
                    ID = Encoding.UTF8.GetString(data, 11, 10);
                    StreamPort = BitConverter.ToInt32(data, 21);
                    CommPort = BitConverter.ToInt32(data, 25);
                    RestPort = BitConverter.ToInt32(data, 29);
                    DeviceName = Encoding.UTF8.GetString(data, 33, 255);
                    DeviceName = DeviceName.Trim();
                }
                catch
                {
                    Key = "";
                    ThisDevice = DeviceType.unknown;
                    ID = "";
                    StreamPort = 0;
                    CommPort = 0;
                    RestPort = 0;
                    DeviceName = "";
                }
            }

            public void FromBytes(byte[] data, int offset)
            {
                try
                {
                    Key = Encoding.UTF8.GetString(data, 0 + offset, 10);
                    ThisDevice = (DeviceType)data[10 + offset];
                    ID = Encoding.UTF8.GetString(data, 11 + offset, 10);
                    StreamPort = BitConverter.ToInt32(data, 21 + offset);
                    CommPort = BitConverter.ToInt32(data, 25 + offset);
                    RestPort = BitConverter.ToInt32(data, 29 + offset);
                    DeviceName = Encoding.UTF8.GetString(data, 33 + offset, 255);
                    DeviceName = DeviceName.Trim();
                }
                catch
                {
                    Key = "";
                    ThisDevice = DeviceType.unknown;
                    ID = "";
                    StreamPort = 0;
                    CommPort = 0;
                    RestPort = 0;
                    DeviceName = "";
                }
            }

            public void SetValues(DeviceType device, int streamPort, int updatePort, string deviceName)
            {
                Key = "soundtrack";
                ThisDevice = device;
                this.StreamPort = streamPort;
                this.CommPort = updatePort;
                this.DeviceName = deviceName;
            }

            public ServerResource ToServerResource(IPAddress serverIP)
            {
                if (ThisDevice == DeviceType.server) return new ServerResource(DeviceName, serverIP, ID, StreamPort, CommPort, RestPort);
                else return null;
            }

            public bool IsGoodData { get { return Key == "soundtrack"; } }

        }

        class AudioReceiver
        {
            Socket StreamSocket;
            Socket MulticastSocket;
            Socket MessageSocket;
            MulticastOption Multicast;
            readonly IPAddress MulticastIP = IPAddress.Parse("239.205.205.205");

            //public IPAddress ServerIPAd { get; }
            //public int ServerCommPort { get; }
            //public int ServerStreamPort { get; }
            public ServerResource ConnectedServer { get; }
            public int MulticastPort { get; set; } = 2250;
            public int StreamPort { get; set; } = 2251;
            public int CommPort { get; set; } = 2252;
            public string DeviceName { get; set; }
            public bool ConnectedToServer { get; } = false;
            public bool ReceivingStream { get; } = false;

            public AudioReceiver(string name = "")
            {
                DeviceName = name;

                StreamSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                MessageSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Multicast = new MulticastOption(MulticastIP);


                try
                {
                    MulticastSocket.Bind((EndPoint)new IPEndPoint(IPAddress.Any, MulticastPort));
                    MulticastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, Multicast);

                    MessageSocket.Bind((EndPoint)new IPEndPoint(IPAddress.Any, CommPort));
                }
                catch (Exception e)
                {
                    if (MulticastSocket != null) MulticastSocket.Close();
                    if (MessageSocket != null) MessageSocket.Close();
                    MessageBox.Show(e.ToString());
                }

            }

            public bool Connect(ServerResource server)
            {

                return false;
            }

            public List<ServerResource> PollServers(int attempts = 3, int timeout_ms = 1000)
            {
                List<ServerResource> servers = new List<ServerResource>();
                ServerResource server;
                DeviceData serverData = new DeviceData();
                EndPoint remoteEP = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                IPEndPoint remoteIPEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] commData = new byte[DeviceData.FixedByteLength];
                DateTime startTime;
                int elapsedTime;
                int bytesReceived = 0;

                // Make sure poll attempts is at least 1
                if (attempts < 1) attempts = 1;

                string message = "";
                // Perform sending probe and waiting for data for the specified timeout "attempts" times
                for (int i = 0; i < attempts; i++)
                {
                    SendProbe();

                    // Reset the start time for the overall timeout timer
                    startTime = DateTime.Now;
                    elapsedTime = 0;

                    // Must repeatedly receive the datagrams during the time before timeout in case multiple servers respond
                    while (elapsedTime < timeout_ms)
                    {
                        bytesReceived = 0;
                        elapsedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
                        message += elapsedTime.ToString() + ", ";
                        // Set the receive timeout to the amount remaining before the overall timeout finishes
                        if ((timeout_ms - elapsedTime) > 2)
                            MessageSocket.ReceiveTimeout = timeout_ms - elapsedTime;
                        else
                            break;

                        try
                        {
                            bytesReceived = MessageSocket.ReceiveFrom(commData, ref remoteEP);
                            remoteIPEP = (IPEndPoint)remoteEP;

                        }
                        catch (SocketException e)
                        {
                            if (e.SocketErrorCode != SocketError.TimedOut)
                            {
                                // Something real happened
                                MessageBox.Show(e.ToString(), "ERROR!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                // No problem, just a timeout
                            }
                        }

                        // Process data if anything was received
                        if (bytesReceived >= 278)
                        {
                            serverData.FromBytes(commData);
                            // Add the server to the list if the data is good and an item with the IP doesn't already exist
                            if (serverData.IsGoodData)
                            {
                                server = serverData.ToServerResource(remoteIPEP.Address);
                                if (!servers.Any(item => item.IP == server.IP))
                                    servers.Add(server);
                            }
                        }
                    }

                }
                return servers;
            }

            void SendProbe()
            {
                EndPoint multicastEP = (EndPoint)new IPEndPoint(MulticastIP, MulticastPort);

                ProbeData testData = new ProbeData(CommPort);
                byte[] message = testData.ToBytes();

                MulticastSocket.SendTo(message, message.Length, SocketFlags.None, multicastEP);
            }
        }

        class AudioServer
        {
            Socket StreamSocket; // Socket for sending stream
            Socket MulticastSocket; // Socket for receiving multicasts
            Socket MessageSocketTx; // Socket for transmitting messages
            Socket MessageSocketRx; // Socket for receiving messages

            readonly MulticastOption Multicast;
            readonly IPAddress MulticastIP = IPAddress.Parse("239.205.205.205");

            IPAddress ClientIPAd;
            int ClientCommPort;
            public int MulticastPort { get; set; } = 2250;
            public int OutboundStreamPort { get; set; } = 2251;
            public int CommPortTx { get; set; } = 2252;
            public int CommPortRx { get; set; } = 2253;
            public string DeviceName { get; set; }

            byte[] MulticastData = new byte[ProbeData.FixedByteLength];
            byte[] MessageData = new byte[ProbeData.FixedByteLength];
            readonly byte[] ServerDataBytes;


            public AudioServer(string name = "", string id = "")
            {
                DeviceName = name;

                StreamSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                MulticastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                MessageSocketTx = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                MessageSocketRx = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Multicast = new MulticastOption(MulticastIP);

                ServerDataBytes = new DeviceData(new ServerResource(name, null, id, OutboundStreamPort, CommPortTx, 2249)).ToBytes();

                EndPoint localEP;

                // Bind the multicast socket and subscribe to the multicast
                try
                {
                    localEP = (EndPoint)new IPEndPoint(IPAddress.Any, MulticastPort);
                    MulticastSocket.Bind(localEP);
                    MulticastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, Multicast);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

                // Bind the message transmit socket
                try
                {
                    localEP = (EndPoint)new IPEndPoint(IPAddress.Any, CommPortTx);
                    MessageSocketTx.Bind(localEP);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

                // Bind the message receive socket
                try
                {
                    localEP = (EndPoint)new IPEndPoint(IPAddress.Any, CommPortRx);
                    MessageSocketRx.Bind(localEP);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

                try
                {
                    EndPoint multicastEP = (EndPoint)new IPEndPoint(MulticastIP, MulticastPort);
                    MulticastSocket.BeginReceiveFrom(MulticastData, 0, MulticastData.Length, SocketFlags.None, ref multicastEP, new AsyncCallback(ReceivedMulticastMessage), null);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
            }

            void ReceivedMulticastMessage(IAsyncResult result)
            {
                EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
                MulticastSocket.EndReceiveFrom(result, ref clientEP);
                IPEndPoint clientIPEP = (IPEndPoint)clientEP;
                ProbeData data = new ProbeData(MulticastData);

                if (data.IsGoodData)
                {
                    ClientIPAd = clientIPEP.Address;
                    ClientCommPort = data.ResponsePort;
                    MessageSocketTx.SendTo(ServerDataBytes, (EndPoint)new IPEndPoint(ClientIPAd, ClientCommPort));
                }
                else
                {
                }

                EndPoint multicastEP = (EndPoint)new IPEndPoint(MulticastIP, MulticastPort);
                MulticastSocket.BeginReceiveFrom(MulticastData, 0, MulticastData.Length, SocketFlags.None, ref multicastEP, new AsyncCallback(ReceivedMulticastMessage), null);
            }

            void ReceivedDirectMessage(IAsyncResult result)
            {
                EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
                MessageSocketRx.EndReceiveFrom(result, ref clientEP);

            }


            public string GenerateID()
            {
                byte[] randomID = new byte[10];
                Random rnd = new Random();

                for (int i = 0; i < randomID.Length; i++)
                {
                    randomID[i] = (byte)rnd.Next(48, 109);
                    if (randomID[i] > 57) { randomID[i] += 7; }
                    if (randomID[i] > 90) { randomID[i] += 6; }
                }

                return Encoding.UTF8.GetString(randomID);
            }

        }
    }
}
