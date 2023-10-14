using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Commons
{
    public class NotifyPropertiesModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

    }
}
