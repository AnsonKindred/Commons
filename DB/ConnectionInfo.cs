using System;
using System.Collections.Generic;
using System.Text;
using Commons.DB;

namespace Commons
{
    public class ConnectionInfo : NotifyingSerializable
    {
        public string? ipAddress { get; set; }
        public ushort? Port { get; set; }

        required public Client Client { get; set; }

        public ConnectionInfo() : base() { }
        public ConnectionInfo(byte[] buffer) : base(buffer) { }
        public ConnectionInfo(byte[] buffer, ref int offset) : base(buffer, ref offset) { }

        public override int Serialize(byte[] buffer, ref int offset)
        {
            int startingOffset = offset;

            return offset - startingOffset;
        }

        public override void Deserialize(byte[] buffer, ref int offset)
        {
        }
    }
}
