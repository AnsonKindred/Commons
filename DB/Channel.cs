using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Commons.DB;

namespace Commons
{
    public class Channel : NotifyingSerializable
    {
        [Key]
        [Column(TypeName = "BLOB")]
        public Guid ID { get; set; }

        [Column(TypeName = "BLOB")]
        public Guid SpaceID { get; set; }

        public virtual Space? Space { get; set; }

        private string _Name = "";
        public string Name { get => _Name; set => Set(ref _Name, value); }

        public virtual ICollection<Chat> Chats { get; set; } = new ObservableCollection<Chat>();

        public Channel() : base() { }
        public Channel(byte[] buffer) : base(buffer) { }
        public Channel(byte[] buffer, ref int offset) : base(buffer, ref offset) { }

        override public int Serialize(byte[] data, ref int offset)
        {
            int startingOffset = offset;

            ID.TryWriteBytes(new Span<byte>(data, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            SpaceID.TryWriteBytes(new Span<byte>(data, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            int numNameBytes = Encoding.UTF8.GetBytes(Name, 0, Name.Length, data, offset);
            offset += numNameBytes;

            return offset - startingOffset;
        }

        override public void Deserialize(byte[] data, ref int offset)
        {
            ID = new Guid(new Span<byte>(data, offset, GUID_LENGTH));
            offset += GUID_LENGTH;

            SpaceID = new Guid(new Span<byte>(data, offset, GUID_LENGTH));
            offset += GUID_LENGTH;

            Name = Encoding.UTF8.GetString(data, offset, data.Length - offset);
            offset += data.Length - offset;
        }
    }
}
