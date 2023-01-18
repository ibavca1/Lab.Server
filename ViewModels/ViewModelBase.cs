using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lab.Server.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected void SetValue<T>(ref T backingFiled, T value, [CallerMemberName] string? propertyName = null)
        {
            if(EqualityComparer<T>.Default.Equals(backingFiled, value)) return;
            backingFiled = value;
            OnPropertyChanged(propertyName);
        }
    }
}
