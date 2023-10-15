using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Commons
{
    class SpaceNetworker
    {
        public Space Space { get; private set; }

        public ControlPeer ControlPeer { get; private set; }
        public VoipPeer VoipPeer { get; private set; }

        CommonsContext db;

        public SpaceNetworker(CommonsContext db, Space server)
        {
            this.Space = server;
            this.db = db;

            ControlPeer = new ControlPeer(this, db);
            VoipPeer = new VoipPeer();
        }

        public async Task HostSpace()
        {
            await VoipPeer.StartHosting();
            await ControlPeer.StartHosting();

            Space.Address = ControlPeer.NobleEndPoint.Address.ToString();
            Space.Port = ControlPeer.NobleEndPoint.Port;

            db.SaveChanges();
        }

        public async Task JoinSpace()
        {
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(Space.Address), Space.Port);
            await ControlPeer.Connect(serverEndPoint);
        }

        public void Dispose()
        {
            ControlPeer.Dispose();
            VoipPeer.Dispose();
        }
    }
}
