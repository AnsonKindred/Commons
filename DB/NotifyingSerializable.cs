using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Commons.DB
{
    public abstract class NotifyingSerializable : INotifyPropertyChanged
    {
        public const int GUID_LENGTH = 16;

        public event PropertyChangedEventHandler? PropertyChanged;

        public NotifyingSerializable() { }

        public NotifyingSerializable(byte[] buffer)
        {
            int offset = 0;
            Deserialize(buffer, ref offset);
        }

        public NotifyingSerializable(byte[] buffer, ref int offset)
        {
            Deserialize(buffer, ref offset);
        }

        public int Serialize(byte[] buffer)
        {
            int offset = 0;
            return Serialize(buffer, ref offset);
        }

        public abstract int Serialize(byte[] buffer, ref int offset);
        public abstract void Deserialize(byte[] buffer, ref int offset);

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
