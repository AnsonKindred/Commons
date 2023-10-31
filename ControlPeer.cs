using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Commons.Audio;

namespace Commons
{
    using IPTuple = Tuple<IPEndPoint, IPEndPoint>;

    internal class ControlPeer : Peer
    {
        public const int GUID_LENGTH = 16;

        internal enum Command : byte
        {
            ADD_SPACE,
            GET_CLIENTS, ADD_CLIENT,
            GET_CHATS, ADD_CHAT,
            GET_CHANNELS, ADD_CHANNEL,
            CLIENT_IS_READY,
            CONNECT_TO_VOIP
        }

        protected TcpListener? listener;
        protected TcpClient? localClient;
        protected NetworkStream? localClientStream => localClient?.GetStream();
        protected List<NetworkStream> connectedClients = new();

        public bool IsConnected => localClient?.Connected ?? false;

        // Use this to be notified when a client connects
        protected event Action<NetworkStream>? ClientConnected;

        public SpaceNetworker spaceNetworker { get; private set; }
        public Space? ControlledSpace => spaceNetworker.NetworkedSpace;
        CommonsContext db;

        IPEndPoint? hostRelayEndpoint;

        TaskCompletionSource? joinSpaceTaskCompletionSource;

        byte[] serializationBuffer = new byte[128];

        internal ControlPeer(SpaceNetworker spaceNetworker, CommonsContext db) : base(1024, ProtocolType.Tcp) 
        {
            this.spaceNetworker = spaceNetworker;
            this.db = db;
        }

        internal override async Task<IPEndPoint> StartHosting(IPEndPoint localEndPoint)
        {
            listener = new TcpListener(localEndPoint.Address, localEndPoint.Port);
            listener.Start();
            localEndPoint = (IPEndPoint)listener.LocalEndpoint;

            AcceptClients();

            return await base.StartHosting(localEndPoint);
        }

        internal virtual async void AcceptClients()
        {
            if (listener == null) return;

            while (isRunning)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                await _OnClientConnected(client);
                ReceiveFromClient(client);
            }
        }

        private async Task _OnClientConnected(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            connectedClients.Add(stream);
            await OnClientConnected(stream);
            ClientConnected?.Invoke(stream);
        }

        internal async Task OnClientConnected(NetworkStream client)
        {
            await SendSpace(client);
            await SendClients(client);
            await SendChannels(client);
            Trace.WriteLine("Client connected, sending IS READY");
            await SendClientIsReady(client);
            // Send voip endpoint
            ///await SendVoipEndpoint(client);
        }

        internal override async Task<IPTuple?> Connect(IPEndPoint remoteEndPoint)
        {
            if (db == null) return null;
            if (db.LocalClient == null) return null;

            hostRelayEndpoint = remoteEndPoint;
            localClient = new TcpClient(new IPEndPoint(IPAddress.Any, 0));

            noblePeer.SetLocalEndPoint((IPEndPoint)localClient.Client.LocalEndPoint);

            IPTuple? bridgeEndPoints = await base.Connect(hostRelayEndpoint);
            
            if (bridgeEndPoints == null) throw new Exception("Bad bridges");

            localClient.Connect(bridgeEndPoints.Item1);

            ReceiveFromClient(localClient);

            joinSpaceTaskCompletionSource = new TaskCompletionSource();
            await joinSpaceTaskCompletionSource.Task;

            Trace.WriteLine("Connected, please send client thank you jesus");
            await SendClient(db.LocalClient);

            return bridgeEndPoints;
        }

        protected async void ReceiveFromClient(TcpClient client)
        {
            NetworkStream? stream = null;

            try
            {
                // Get a stream object for reading and writing
                stream = client.GetStream();

                // Loop to receive all the data sent by the client.
                while (client.Connected)
                {
                    // Read the byte that represents the command type
                    byte[] commandByte = new byte[1];
                    int numBytesRead = await stream.ReadAsync(commandByte, 0, commandByte.Length);
                    if (numBytesRead == 0) break;

                    Trace.WriteLine("[" + Process.GetCurrentProcess().Id + "] Received command " + (Command)commandByte[0]);

                    // Read the bytes that represent the length of the payload
                    byte[]? lengthBytes = await ReceiveBytes(stream, sizeof(int));
                    if (lengthBytes == null) break;
                    int payloadLength = BitConverter.ToInt32(lengthBytes, 0);

                    // Read the payload bytes
                    byte[]? payloadBytes = null;
                    if (payloadLength != 0)
                    {
                        payloadBytes = await ReceiveBytes(stream, payloadLength);
                        if (payloadBytes == null) break;
                    }

                    await DoCommand(stream, (Command)commandByte[0], payloadBytes);
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }

            if (stream != null)
            {
                connectedClients.Remove(stream);
            }
        }

        // Receive a specific length of bytes from a network stream, even if it takes multiple reads.
        private async Task<byte[]?> ReceiveBytes(NetworkStream stream, int numBytesToReceive)
        {
            byte[] bytes = new byte[numBytesToReceive];
            int totalBytesRead = 0;
            while (totalBytesRead < bytes.Length)
            {
                int numBytesRead = await stream.ReadAsync(bytes, totalBytesRead, bytes.Length - totalBytesRead);
                if (numBytesRead == 0) break;
                totalBytesRead += numBytesRead;
            }
            if (totalBytesRead != bytes.Length) return null;
            return bytes;
        }

        internal async Task SendCommand(Command command, NetworkStream? tcpClient = null, NetworkStream? excludeClient = null)
        {
            await SendCommand(command, new ArraySegment<byte>(), tcpClient, excludeClient);
        }

        internal async Task SendCommand(Command command, ArraySegment<byte> payload, NetworkStream? tcpClient = null, NetworkStream? excludeClient = null)
        {
            Trace.WriteLine("Sending command " + command);
            if (tcpClient == null)
            {
                if (listener == null && localClient != null)
                {
                    Trace.WriteLine("Sending command A" + command);
                    await SendCommandToStream(command, localClient.GetStream(), payload);
                }
                else if (listener != null)
                {
                    Trace.WriteLine("Sending command B" + command);
                    foreach (var client in connectedClients)
                    {
                        if (client == excludeClient) continue;
                        await SendCommandToStream(command, client, payload);
                    }
                }
            }
            else
            {
                Trace.WriteLine("Sending command C" + command);
                await SendCommandToStream(command, tcpClient, payload);
            }
        }

        internal async Task SendCommandToStream(Command command, NetworkStream stream, ArraySegment<byte> payload)
        {
            await stream.WriteAsync(new byte[] { (byte)command }, 0, 1);
            await stream.WriteAsync(BitConverter.GetBytes(payload.Count), 0, sizeof(int));
            if (payload.Count > 0)
            {
                await stream.WriteAsync(payload.Array.AsMemory(payload.Offset, payload.Count));
            }
            Trace.WriteLine("[" + Process.GetCurrentProcess().Id + "] Really sending command " + command);
        }

        #region Commands

        private async Task DoCommand(NetworkStream tcpClient, Command command, byte[]? payload)
        {
            Trace.WriteLine("[" + Process.GetCurrentProcess().Id + "] Doing command " + command);
            switch (command)
            {
                case Command.ADD_SPACE: AddSpace(payload); break;
                case Command.CONNECT_TO_VOIP: await DoConnectToVoipHost(payload); break;

                case Command.GET_CLIENTS: await SendClients(tcpClient); break;
                case Command.ADD_CLIENT: AddClient(payload); break;

                case Command.GET_CHANNELS: await SendChannels(tcpClient); break;
                case Command.ADD_CHANNEL: AddChannel(payload); break;

                case Command.GET_CHATS: await SendChats(tcpClient, payload); break;
                case Command.ADD_CHAT: await AddChat(tcpClient, payload); break;

                case Command.CLIENT_IS_READY: SetClientReady(); break;
            }
        }

        async Task SendClientIsReady(NetworkStream client)
        {
            await SendCommand(Command.CLIENT_IS_READY, client);
        }

        void SetClientReady()
        {
            if (joinSpaceTaskCompletionSource == null) throw new NullReferenceException(nameof(joinSpaceTaskCompletionSource));
            joinSpaceTaskCompletionSource.SetResult();
        }

        async Task SendSpace(NetworkStream client)
        {
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));

            Trace.WriteLine("Send space: " + ControlledSpace.Name);
            int numBytesWritten = ControlledSpace.Serialize(serializationBuffer);
            await SendCommand(Command.ADD_SPACE, new ArraySegment<byte>(serializationBuffer, 0, numBytesWritten), client);
        }

        private void AddSpace(byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));
            if (db.LocalClient == null) throw new NullReferenceException(nameof(db.LocalClient));
            if (hostRelayEndpoint == null) throw new NullReferenceException(nameof(hostRelayEndpoint));

            Space newSpace = new Space(payload);
            db.Spaces.Add(newSpace);
            db.LocalClient.Spaces.Add(newSpace);
            db.SaveChanges();
            newSpace.SpaceNetworker = spaceNetworker;
            spaceNetworker.NetworkedSpace = newSpace;

            Trace.WriteLine("Add space" + newSpace.Name);
        }

        private async Task SendVoipEndpoint(NetworkStream client)
        {
            IPEndPoint voipEndPoint = spaceNetworker.VoipPeer.NobleEndPoint;
            IPAddress voipAddress = voipEndPoint.Address;
            ushort voipPort = (ushort)voipEndPoint.Port;

            byte[] payload = new byte[GUID_LENGTH + sizeof(ushort)];
            voipAddress.TryWriteBytes(payload, out int bytesWritten);
            payload[bytesWritten] = (byte)voipPort;
            payload[bytesWritten + 1] = (byte)(voipPort >> 8);
            var realPayload = new ArraySegment<byte>(payload, 0, bytesWritten + sizeof(ushort));

            // Connect to VOIP host
            await SendCommand(Command.CONNECT_TO_VOIP, realPayload, client);
        }

        private async Task SendChannels(NetworkStream tcpClient)
        {
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));

            Trace.WriteLine("Sending channels");
            foreach (Channel channel in ControlledSpace.Channels)
            {
                Trace.WriteLine("Sending channel: " + channel.Name);
                await SendChannel(channel, tcpClient);
            }
        }

        internal async Task SendChannel(Channel channel, NetworkStream? tcpClient = null, NetworkStream? excludeClient = null)
        {
            int numChannelBytes = channel.Serialize(serializationBuffer);
            await SendCommand(Command.ADD_CHANNEL, new ArraySegment<byte>(serializationBuffer, 0, numChannelBytes), tcpClient, excludeClient);
        }

        private void AddChannel(byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));

            Channel deserializedChannel = new Channel(payload);

            Channel? existingChannel = ControlledSpace.Channels.Where(c => c.ID.Equals(deserializedChannel.ID)).FirstOrDefault();
            if (existingChannel == null)
            {
                db.Channels.Add(deserializedChannel);
                ControlledSpace.Channels.Add(deserializedChannel);
            }
            else
            {
                existingChannel.Name = deserializedChannel.Name;
            }
            db.SaveChanges();
        }

        private async Task DoConnectToVoipHost(byte[]? payload)
        {
            if (payload == null) return;

            IPAddress voipAddress;
            ushort voipPort;

            if (payload.Length == 6)
            {
                voipAddress = new IPAddress(new ReadOnlySpan<byte>(payload, 0, 4));
                voipPort = (ushort)(payload[4] + (payload[5] << 8));
            }
            else if (payload.Length == 18)
            {
                voipAddress = new IPAddress(new ReadOnlySpan<byte>(payload, 0, GUID_LENGTH));
                voipPort = (ushort)(payload[GUID_LENGTH] + (payload[GUID_LENGTH + 1] << 8));
            }
            else
            {
                throw new Exception("Bad voip endpoint");
            }

            IPEndPoint voipEndPoint = new IPEndPoint(voipAddress, voipPort);

            await spaceNetworker.VoipPeer.Connect(voipEndPoint);

            AudioController.StartMonitoringDecoding();
        }

        private async Task SendClients(NetworkStream tcpClient)
        {
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));

            Trace.WriteLine("Sending clients");
            foreach (Client client in ControlledSpace.Clients)
            {
                await SendClient(client, tcpClient);
            }
        }

        internal async Task SendClient(Client client, NetworkStream? tcpClient = null, NetworkStream? excludeClient = null)
        {
            int numClientBytes = client.Serialize(serializationBuffer);
            await SendCommand(Command.ADD_CLIENT, new ArraySegment<byte>(serializationBuffer, 0, numClientBytes), tcpClient, excludeClient);
        }

        private void AddClient(byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));

            Client deserializedClient = new Client(payload);
            Trace.WriteLine("received client: " + deserializedClient.Name);

            Client? existingClient = ControlledSpace.Clients.Where(c => c.ID.Equals(deserializedClient.ID)).FirstOrDefault();
            if (existingClient == null)
            {
                db.Clients.Add(deserializedClient);
                ControlledSpace.Clients.Add(deserializedClient);
            }
            else
            {
                existingClient.Name = deserializedClient.Name;
            }
            db.SaveChanges();
        }

        internal async Task RequestChats(ulong latestTime, Channel channel)
        {
            byte[] payload = new byte[GUID_LENGTH + sizeof(long)];
            channel.ID.TryWriteBytes(payload);
            BitConverter.TryWriteBytes(new Span<byte>(payload, GUID_LENGTH, sizeof(ulong)), latestTime);
            await SendCommand(Command.GET_CHATS, payload);
        }

        private async Task SendChats(NetworkStream client, byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));
            if (ControlledSpace.CurrentChannel == null) throw new Exception("Trying to send chats with new current channel");

            Guid channelID = new Guid(new Span<byte>(payload, 0, GUID_LENGTH));
            ulong timestamp = BitConverter.ToUInt64(payload);

            Channel channel = ControlledSpace.Channels.Where(c => c.ID == channelID).First();

            var chats = channel.Chats.Where(c => c.Timestamp > timestamp);
            foreach (Chat chat in chats)
            {
                await SendChat(chat, client);
            }
        }

        internal async Task SendChat(Chat chat, NetworkStream? tcpClient = null, NetworkStream? excludeClient = null)
        {
            int numChatBytes = chat.Serialize(serializationBuffer);
            await SendCommand(Command.ADD_CHAT, new ArraySegment<byte>(serializationBuffer, 0, numChatBytes), tcpClient, excludeClient);
        }

        private async Task AddChat(NetworkStream tcpClient, byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));
            if (ControlledSpace == null) throw new NullReferenceException(nameof(ControlledSpace));

            Chat deserializedChat = new Chat(payload);

            Client? client = ControlledSpace.Clients.Where(client => client.ID.Equals(deserializedChat.ClientID)).FirstOrDefault();
            if (client == null) throw new Exception("No client for chat " + deserializedChat.ClientID);

            Channel? channel = ControlledSpace.Channels.Where(channel => channel.ID.Equals(deserializedChat.ChannelID)).FirstOrDefault();
            if (channel == null) throw new Exception("No channel for chat " + deserializedChat.ChannelID);

            db.Chats.Add(deserializedChat);
            channel.Chats.Add(deserializedChat);
            db.SaveChanges();

            if (listener != null)
            {
                await SendChat(deserializedChat, null, tcpClient);
            }
        }

        #endregion Commands
    }
}
