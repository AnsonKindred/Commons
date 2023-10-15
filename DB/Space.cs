using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Commons
{
    public class Space : NotifyPropertiesModel
    {
        public int ID { get; set; }

        private string _Name = "";
        public required string Name { get => _Name; set => Set(ref _Name, value); }

        public required string Address { get; set; }
        public required int Port { get; set; }
        public required bool IsLocal { get; set; }

        public virtual ICollection<Chat> Chats { get; private set; } = new ObservableCollection<Chat>();
        public virtual ICollection<Client> Clients { get; set; } = new ObservableCollection<Client>();

        [NotMapped]
        internal SpaceNetworker? SpaceNetworker { get; set; }

    }
}
