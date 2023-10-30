using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Commons.DB;

namespace Commons
{
    public class Chat : NotifyingSerializable
    {
        [Key]
        [Column(TypeName = "BLOB")]
        public Guid ID { get; set; }
        
        [Column(TypeName = "BLOB")]
        public Guid ChannelID { get; set; }
        
        [Column(TypeName = "BLOB")]
        public Guid ClientID { get; set; }

        public ulong Timestamp { get; set; }
        public string Content { get; set; } = "";

        public virtual Client? Client { get; set; }
        public virtual Channel? Channel { get; set; }

        public Space? Space => Channel?.Space;

        public Chat() : base() { }
        public Chat(byte[] buffer) : base(buffer) { }
        public Chat(byte[] buffer, ref int offset) : base(buffer, ref offset) { }

        public override void Deserialize(byte[] buffer, ref int offset)
        {
            ID = new Guid(new ReadOnlySpan<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            ClientID = new Guid(new ReadOnlySpan<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            ChannelID = new Guid(new ReadOnlySpan<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            Timestamp = BitConverter.ToUInt64(buffer, offset);
            offset += sizeof(ulong);
            Content = Encoding.UTF8.GetString(buffer, offset, buffer.Length - offset);
            offset += buffer.Length - offset;
        }

        public override int Serialize(byte[] buffer, ref int offset)
        {
            int startingOffset = offset;
            ID.TryWriteBytes(new Span<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            ClientID.TryWriteBytes(new Span<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            ChannelID.TryWriteBytes(new Span<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            BitConverter.TryWriteBytes(new Span<byte>(buffer, offset, sizeof(long)), Timestamp);
            offset += sizeof(ulong);
            int numContentBytes = Encoding.UTF8.GetBytes(Content, 0, Content.Length, buffer, offset);
            offset += numContentBytes;

            return offset - startingOffset;
        }
    }
}
