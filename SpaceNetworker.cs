using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Commons
{
    class SpaceNetworker
    {
        public Space? NetworkedSpace { get; set; }

        public ControlPeer ControlPeer { get; private set; }
        public VoipPeer VoipPeer { get; private set; }

        CommonsContext db;

        public SpaceNetworker(CommonsContext db)
        {
            this.db = db; 

            ControlPeer = new ControlPeer(this, db);
            VoipPeer = new VoipPeer();
        }

        public async Task HostSpace(Space space)
        {
            this.NetworkedSpace = space;
            this.NetworkedSpace.SpaceNetworker = this;

            await VoipPeer.StartHosting();
            await ControlPeer.StartHosting();

            NetworkedSpace.Address = ControlPeer.NobleEndPoint.Address.ToString();
            NetworkedSpace.Port = ControlPeer.NobleEndPoint.Port;

            db.SaveChanges();
        }

        public async Task<Space> ConnectToSpace(IPEndPoint spaceHostCoastToCoast)
        {
            await ControlPeer.Connect(spaceHostCoastToCoast);
            if (NetworkedSpace == null) throw new NullReferenceException(nameof(NetworkedSpace));
            return NetworkedSpace;
        }

        public void Dispose()
        {
            ControlPeer.Dispose();
            VoipPeer.Dispose();
        }
    }
}
