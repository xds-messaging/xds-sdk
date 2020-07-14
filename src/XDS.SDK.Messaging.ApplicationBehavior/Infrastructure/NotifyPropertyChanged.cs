using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace XDS.Messaging.SDK.ApplicationBehavior.Infrastructure
{
	[DataContract]
	public class NotifyPropertyChanged : INotifyPropertyChanged
    {
	    public event PropertyChangedEventHandler PropertyChanged;

	    protected void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
	    {
		    if (Equals(storage, value))
		    {
			    return;
		    }

		    storage = value;
		    OnPropertyChanged(propertyName);
	    }

	    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	}
}
