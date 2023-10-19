using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NobleConnect.Ice;

namespace Commons
{
    using IPTuple = Tuple<IPEndPoint, IPEndPoint>;

    internal abstract class Peer
    {
        const string GAME_ID = "MTAKemViQG5vYmxld2hhbGUuY29tCiFjW3JyM3NSb21jXi54ViU8VGlEXFZ0bjlFeDMwWjFE";

        protected TcpListener? listener;
        protected TcpClient? localClient;
        protected NetworkStream? localClientStream => localClient?.GetStream();
        protected List<NetworkStream> connectedClients = new();
        protected NobleConnect.Peer noblePeer;

        internal IPEndPoint NobleEndPoint => noblePeer.RelayEndPoint;

        public bool IsConnected => localClient?.Connected ?? false;

        protected bool isRunning = true;

        // Use this to be notified when a client connects
        internal event Action<NetworkStream>? ClientConnected;
        protected int bufferSize;

        internal Peer(int bufferSize)
        {
            this.bufferSize = bufferSize;

            string decodedGameID = Encoding.UTF8.GetString(Convert.FromBase64String(GAME_ID));
            string[] parts = decodedGameID.Split('\n');

            IceConfig config = new IceConfig();
            config.origin = parts[0];
            config.username = parts[1];
            config.password = parts[2];
            config.bufferSize = bufferSize;
            config.forceRelayOnly = true;
            config.iceServerAddress = "159.203.136.135";// us -east.connect.noblewhale.com";
            config.icePort = 3478;
            config.protocolType = ProtocolType.Tcp;

            noblePeer = new NobleConnect.Peer(config);

            PeerProcess();
        }

        protected virtual async void PeerProcess()
        {
            while (isRunning)
            {
                noblePeer?.Update();
                await Task.Delay(10);
            }

            noblePeer?.Dispose();
        }

        internal virtual async Task<IPEndPoint> StartHosting()
        {
            listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();

            AcceptClients();

            var completionSource = new TaskCompletionSource<IPEndPoint>();
            noblePeer.InitializeHosting((IPEndPoint)listener.LocalEndpoint, (ip, port) => completionSource.SetResult(new IPEndPoint(IPAddress.Parse(ip), port)) );
            return await completionSource.Task;
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

        internal virtual async Task OnClientConnected(NetworkStream client)
        {
            // Child classes can override this or other classes can use the ClientConnected event
        }

        abstract protected void ReceiveFromClient(TcpClient client);

        internal virtual async Task<IPTuple> Connect(IPEndPoint remoteEndPoint)
        {
            localClient = new TcpClient(new IPEndPoint(IPAddress.Any, 0));

            var t = new TaskCompletionSource<IPTuple>();
            noblePeer.InitializeClient(remoteEndPoint, (v4, v6) => OnPreparedToConnect(t, v4, v6));
            return await t.Task;
        }

        protected virtual void OnPreparedToConnect(TaskCompletionSource<IPTuple> t, IPEndPoint bridgeEndPointV4, IPEndPoint bridgeEndPointV6)
        {
            if (localClient != null)
            {
                localClient.Connect(bridgeEndPointV4);
                ReceiveFromClient(localClient);
            }
            t.SetResult(new IPTuple(bridgeEndPointV4, bridgeEndPointV6));
        }

        internal virtual void Dispose()
        {
            isRunning = false;
        }
    }
}
