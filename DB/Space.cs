using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Commons.DB;

namespace Commons
{
    public class Space : NotifyingSerializable
    {
        [Key]
        [Column(TypeName = "BLOB")]
        public Guid ID { get; set; }

        private string _Name = "";
        public string Name { get => _Name; set => Set(ref _Name, value); }

        public string? Address { get; set; }
        public int Port { get; set; }
        public bool IsLocal { get; set; }

        public virtual ICollection<Channel> Channels { get; set; } = new ObservableCollection<Channel>();
        public virtual ICollection<Client> Clients { get; set; } = new ObservableCollection<Client>();

        [NotMapped]
        public Channel? CurrentChannel { get; set; }

        [NotMapped]
        internal SpaceNetworker? SpaceNetworker { get; set; }

        public Space() : base() { }
        public Space(byte[] buffer) : base(buffer) { }
        public Space(byte[] buffer, ref int offset) : base(buffer, ref offset) { }

        override public int Serialize(byte[] buffer, ref int offset)
        {
            int startingOffset = offset;

            ID.TryWriteBytes(new Span<byte>(buffer, offset, buffer.Length - offset));
            offset += GUID_LENGTH;

            int numNameBytes = Encoding.UTF8.GetBytes(Name, new Span<byte>(buffer, offset, buffer.Length - offset));
            offset += numNameBytes;

            return offset - startingOffset;
        }

        override public void Deserialize(byte[] buffer, ref int offset)
        {
            ID = new Guid(new Span<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;

            Name = Encoding.UTF8.GetString(buffer, offset, buffer.Length - offset);
            offset += buffer.Length - offset;

            IsLocal = false;
        }
    }
}
