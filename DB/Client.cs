using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Commons
{
    public class Client : NotifyPropertiesModel
    {
        public int ID { get; set; }

        private string _Name = "";
        public required string Name { get => _Name; set => Set(ref _Name, value); }

        public required Guid Guid { get; set; }

        public virtual ICollection<Space> Servers { get; set; } = new ObservableCollection<Space>();
        public virtual ICollection<Chat> Chats { get; private set; } = new ObservableCollection<Chat>();
    }
}
