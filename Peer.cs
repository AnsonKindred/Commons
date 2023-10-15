using System;
using System.Collections.Generic;
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
        const string GAME_ID = "NjZLCnplYndpdGhwbGFuQG5vYmxld2hhbGUuY29tCjhRTll4Z2VnS2ZyMDh5ZFI4S1V1dzVNOXVxYm53WkZWeVpyWG04dStNbFJLcW1acVByRCtoZHAzTmJ5ODVBYkRpNURBQiszTlBDa1BxdmR0V3dOQjNRPT0=";

        protected TcpListener? listener;
        protected TcpClient? localClient;
        protected List<TcpClient> connectedClients = new();
        protected NobleConnect.Peer noblePeer;

        internal IPEndPoint NobleEndPoint => noblePeer.RelayEndPoint;

        protected bool isRunning = true;

        // Use this to be notified when a client connects
        internal event Action<TcpClient>? ClientConnected;

        internal Peer()
        {
            string decodedGameID = Encoding.UTF8.GetString(Convert.FromBase64String(GAME_ID));
            string[] parts = decodedGameID.Split('\n');

            IceConfig config = new IceConfig();
            config.origin = parts[0];
            config.username = parts[1];
            config.password = parts[2];
            config.iceServerAddress = "us-east.connect.noblewhale.com";
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
            connectedClients.Add(client);
            await OnClientConnected(client);
            ClientConnected?.Invoke(client);
        }

        internal virtual async Task OnClientConnected(TcpClient client)
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
