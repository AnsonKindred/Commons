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

        protected NobleConnect.Peer noblePeer;

        internal IPEndPoint NobleEndPoint => noblePeer.RelayEndPoint;

        protected bool isRunning = true;
        protected int bufferSize;

        internal Peer(int bufferSize, ProtocolType protocol)
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
            config.protocolType = protocol;

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

        internal virtual async Task<IPEndPoint> StartHosting() { return await StartHosting(new IPEndPoint(IPAddress.IPv6Any, 0)); }

        internal virtual async Task<IPEndPoint> StartHosting(IPEndPoint localEndpoint)
        {
            var completionSource = new TaskCompletionSource<IPEndPoint>();
            noblePeer.InitializeHosting(localEndpoint, (ip, port) => completionSource.SetResult(new IPEndPoint(IPAddress.Parse(ip), port)));
            return await completionSource.Task;
        }

        internal virtual async Task<IPTuple?> Connect(IPEndPoint remoteEndPoint)
        {
            var t = new TaskCompletionSource<IPTuple>();
            noblePeer.InitializeClient(remoteEndPoint, (v4, v6) => t.SetResult(new IPTuple(v4, v6)));
            return await t.Task;
        }

        internal virtual void Dispose()
        {
            isRunning = false;
        }
    }
}
