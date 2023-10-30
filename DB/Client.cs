using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Commons.DB;

namespace Commons
{
    public class Client : NotifyingSerializable
    {
        [Key]
        [Column(TypeName = "BLOB")]
        public Guid ID { get; set; }

        private string _Name = "";
        public string Name { get => _Name; set => Set(ref _Name, value); }

        public virtual ICollection<Space> Spaces { get; set; } = new ObservableCollection<Space>();

        public Client() : base() { }
        public Client(byte[] buffer) : base(buffer) { }
        public Client(byte[] buffer, ref int offset) : base(buffer, ref offset) { }

        public override int Serialize(byte[] buffer, ref int offset)
        {
            int startingOffset = offset;
            ID.TryWriteBytes(buffer);
            offset += GUID_LENGTH;
            int numNameBytes = Encoding.UTF8.GetBytes(Name, 0, Name.Length, buffer, GUID_LENGTH);
            offset += numNameBytes;
            return offset - startingOffset;
        }

        public override void Deserialize(byte[] buffer, ref int offset)
        {
            ID = new Guid(new ReadOnlySpan<byte>(buffer, offset, GUID_LENGTH));
            offset += GUID_LENGTH;
            Name = Encoding.UTF8.GetString(buffer, offset, buffer.Length - GUID_LENGTH);
            offset += buffer.Length - GUID_LENGTH;
        }
    }
}
