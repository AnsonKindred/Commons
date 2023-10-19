using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Commons.Audio;

namespace Commons
{
    class SpaceNetworker
    {
        public Space Space { get; private set; }

        public ControlPeer ControlPeer { get; private set; }
        public VoipPeer VoipPeer { get; private set; }

        CommonsContext db;

        public SpaceNetworker(CommonsContext db, Space space)
        {
            this.Space = space;
            this.db = db;

            space.SpaceNetworker = this;

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
            IPEndPoint spaceEndPoint = new IPEndPoint(IPAddress.Parse(Space.Address), Space.Port);
            await ControlPeer.Connect(spaceEndPoint);
        }

        public void Dispose()
        {
            ControlPeer.Dispose();
            VoipPeer.Dispose();
        }
    }
}
