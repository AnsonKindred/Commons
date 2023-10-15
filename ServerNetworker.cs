using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Commons
{
    class ServerNetworker
    {
        public Server Server { get; private set; }

        public ControlPeer ControlPeer { get; private set; }
        public VoipPeer VoipPeer { get; private set; }

        CommonsContext db;

        public ServerNetworker(CommonsContext db, Server server)
        {
            this.Server = server;
            this.db = db;

            ControlPeer = new ControlPeer(this, db);
            VoipPeer = new VoipPeer();
        }

        public async Task StartHosting()
        {
            await VoipPeer.StartHosting();
            await ControlPeer.StartHosting();

            Server.Address = ControlPeer.NobleEndPoint.Address.ToString();
            Server.Port = ControlPeer.NobleEndPoint.Port;

            db.SaveChanges();
        }

        public async Task JoinServer()
        {
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(Server.Address), Server.Port);
            await ControlPeer.Connect(serverEndPoint);
        }

        public void Dispose()
        {
            ControlPeer.Dispose();
            VoipPeer.Dispose();
        }
    }
}
