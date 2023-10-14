using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using NobleConnect;
using NobleConnect.Ice;

namespace Commons
{
    using IPTuple = System.Tuple<IPEndPoint, IPEndPoint>;

    class ServerNetworker
    {
        public enum Command : byte
        {
            GET_CLIENTS = 1,
            ADD_CLIENT = 2,
            GET_CHATS = 3,
            ADD_CHAT = 4,
            ADD_SERVER = 5
        }
        Server server;
        TcpListener? listener;
        TcpClient? localClient;

        List<TcpClient> connectedClients = new();
        CommonsContext db;
        bool isRunning = true;
        Peer noblePeer;

        const string GAME_ID = "NjZLCnplYndpdGhwbGFuQG5vYmxld2hhbGUuY29tCjhRTll4Z2VnS2ZyMDh5ZFI4S1V1dzVNOXVxYm53WkZWeVpyWG04dStNbFJLcW1acVByRCtoZHAzTmJ5ODVBYkRpNURBQiszTlBDa1BxdmR0V3dOQjNRPT0=";

        public ServerNetworker(CommonsContext db, Server server)
        {
            this.server = server;
            this.db = db;

            Logger.logger = (s) => Trace.WriteLine(s);
            Logger.logLevel = Logger.Level.Info;

            string decodedGameID = Encoding.UTF8.GetString(Convert.FromBase64String(GAME_ID));
            string[] parts = decodedGameID.Split('\n');

            IceConfig config = new IceConfig();
            config.origin = parts[0];
            config.username = parts[1];
            config.password = parts[2];
            config.iceServerAddress = "us-east.connect.noblewhale.com";
            config.icePort = 3478;
            config.protocolType = ProtocolType.Tcp;

            noblePeer = new Peer(config);

            PeerProcess();
        }

        public async void StartHosting()
        {
            listener = new TcpListener(IPAddress.Any, server.Port);
            listener.Start();
            server.Address = ((IPEndPoint)listener.LocalEndpoint).Address.ToString();
            server.Port = ((IPEndPoint)listener.LocalEndpoint).Port;

            noblePeer.InitializeHosting((IPEndPoint)listener.LocalEndpoint, OnPrepared);

            while (isRunning)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                connectedClients.Add(client);
                ReceiveFromClient(client);
                await SendServer(client);
            }
        }

        async Task SendServer(TcpClient client)
        {
            Trace.WriteLine("Send server: " + server.Name);
            byte[] payload = Encoding.UTF8.GetBytes(server.Name);
            await SendCommand(Command.ADD_SERVER, client, payload);
        }

        public void OnPrepared(string ipAddress, ushort port)
        {
            if (listener == null) return;
            server.Address = ipAddress;
            server.Port = port;
            db.SaveChanges();
        }

        public async void PeerProcess()
        {
            while (isRunning)
            {
                noblePeer?.Update();
                await Task.Delay(10);
            }

            noblePeer?.Dispose();
        }

        public async Task JoinServer()
        {
            localClient = new TcpClient(new IPEndPoint(IPAddress.Any, 0));
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(server.Address), server.Port);

            var t = new TaskCompletionSource<IPTuple>();
            noblePeer.InitializeClient(serverEndPoint, (v4, v6) => OnPreparedToConnect(t, v4, v6));
            await t.Task;
        }

        void OnPreparedToConnect(TaskCompletionSource<IPTuple> t, IPEndPoint bridgeEndPointV4, IPEndPoint bridgeEndPointV6)
        {
            if (localClient != null)
            {
                localClient.Connect(bridgeEndPointV4);
                ReceiveFromClient(localClient);
            }
            t.SetResult(new IPTuple(bridgeEndPointV4, bridgeEndPointV6));
        }

        public async void ReceiveFromClient(TcpClient client)
        {
            try
            {
                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

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

                    await DoCommand(client, (Command)commandByte[0], payloadBytes);
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }

            connectedClients.Remove(client);
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


        private async Task DoCommand(TcpClient tcpClient, Command command, byte[]? payload)
        {
            Trace.WriteLine("[" + Process.GetCurrentProcess().Id + "] Doing command " + command);
            switch (command)
            {
                case Command.GET_CLIENTS: await GetClients(tcpClient); break;
                case Command.ADD_CLIENT: AddClient(payload); break;
                case Command.GET_CHATS: await GetChats(tcpClient, payload); break;
                case Command.ADD_CHAT: await AddChat(tcpClient, payload); break;
                case Command.ADD_SERVER: AddServer(payload); break;
            }
        }

        private void AddServer(byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));
            string name = Encoding.UTF8.GetString(payload, 0, payload.Length);
            Trace.WriteLine("Add server " + name);
            server.Name = name;
            db.SaveChanges();
            //if (MainWindow.TheServerList != null)
            //{
            //    MainWindow.TheServerList.Items.Refresh();
            //}
        }

        private async Task GetClients(TcpClient tcpClient)
        {
            Trace.WriteLine("Sending clients");
            foreach (Client client in server.Clients)
            {
                Trace.WriteLine("Sending client: " + client.Name);
                await SendClient(client, tcpClient);
            }
        }

        private void AddClient(byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));

            Guid guid = new Guid(new ReadOnlySpan<byte>(payload, 0, 16));
            string name = Encoding.UTF8.GetString(payload, 16, payload.Length - 16);
            Trace.WriteLine("received client: " + name);
            Client? client = server.Clients.Where(c => c.Guid.Equals(guid)).FirstOrDefault();
            if (client == null)
            {
                Client newClient = new Client { Guid = guid, Name = name };
                server.Clients.Add(newClient);
            }
            else
            {
                client.Name = name;
            }
            db.SaveChanges();
        }

        private async Task GetChats(TcpClient client, byte[]? payload)
        {
            long timestamp = BitConverter.ToInt64(payload);
            var chats = server.Chats.Where(c => c.Timestamp > timestamp);
            foreach (Chat chat in chats)
            {
                await SendChat(chat, client);
            }
        }

        public async Task SendChat(Chat chat, TcpClient? tcpClient = null, TcpClient? excludeClient = null)
        {
            int length = 16 + sizeof(long) + Encoding.UTF8.GetByteCount(chat.Content);
            byte[] bytes = new byte[length];
            chat.Client.Guid.TryWriteBytes(bytes);
            byte[] timestampBytes = BitConverter.GetBytes(chat.Timestamp);
            Array.Copy(timestampBytes, 0, bytes, 16, sizeof(long));
            Encoding.UTF8.GetBytes(chat.Content, 0, chat.Content.Length, bytes, 16 + sizeof(long));
            await SendCommand(Command.ADD_CHAT, tcpClient, bytes, excludeClient);
        }

        private async Task AddChat(TcpClient tcpClient, byte[]? payload)
        {
            if (payload == null) throw new NullReferenceException(nameof(payload));

            Guid clientGuid = new Guid(new ReadOnlySpan<byte>(payload, 0, 16));
            long timestamp = BitConverter.ToInt64(payload, 16);
            string content = Encoding.UTF8.GetString(payload, 16 + sizeof(long), payload.Length - 16 - sizeof(long));
            Client? client = server.Clients.Where(c => c.Guid.Equals(clientGuid)).FirstOrDefault();
            if (client == null)
            {
                client = new Client { Guid = clientGuid, Name = "?????" };
                server.Clients.Add(client);
            }
            Chat newChat = new Chat {
                ClientID = client.ID,
                Client = client,
                Server = server,
                ServerID = server.ID,
                Content = content,
                Timestamp = timestamp
            };
            client.Chats.Add(newChat);
            db.SaveChanges();

            if (listener != null)
            {
                await SendChat(newChat, null, tcpClient);
            }
        }

        internal async Task SendClient(Client client, TcpClient? tcpClient = null, TcpClient? excludeClient = null)
        {
            int length = 16 + Encoding.UTF8.GetByteCount(client.Name);
            byte[] bytes = new byte[length];
            client.Guid.TryWriteBytes(bytes);
            Encoding.UTF8.GetBytes(client.Name, 0, client.Name.Length, bytes, 16);
            await SendCommand(Command.ADD_CLIENT, tcpClient, bytes, excludeClient);
        }

        public async Task SendCommand(Command command, TcpClient? tcpClient = null, byte[]? payload = null, TcpClient? excludeClient = null)
        {
            if (tcpClient == null)
            {
                if (listener == null && localClient != null)
                {
                    await SendCommandToStream(command, localClient.GetStream(), payload);
                }
                else if (listener != null)
                {
                    foreach (var client in connectedClients)
                    {
                        if (client == excludeClient) continue;
                        await SendCommandToStream(command, client.GetStream(), payload);
                    }
                }
            }
            else
            {
                await SendCommandToStream(command, tcpClient.GetStream(), payload);
            }
        }

        public async Task SendCommandToStream(Command command, NetworkStream stream, byte[]? payload = null)
        {
            await stream.WriteAsync(new byte[] { (byte)command }, 0, 1);
            int payloadLength = payload?.Length ?? 0;
            await stream.WriteAsync(BitConverter.GetBytes(payloadLength), 0, sizeof(int));
            if (payload != null)
            {
                await stream.WriteAsync(payload, 0, payload.Length);
            }
            Trace.WriteLine("[" + Process.GetCurrentProcess().Id + "] Really sending command " + command);
        }

        public void Dispose()
        {
            isRunning = false;
        }
    }
}
