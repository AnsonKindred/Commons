using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using Commons.DB;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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

        public virtual ObservableCollection<Channel> Channels { get; set; } = new ObservableCollection<Channel>();
        public virtual ObservableCollection<Client> Clients { get; set; } = new ObservableCollection<Client>();

        [NotMapped]
        private Channel? _CurrentChannel = null;
        [NotMapped]
        public Channel? CurrentChannel { get => _CurrentChannel; set => Set(ref _CurrentChannel, value); }

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
