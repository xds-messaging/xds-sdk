using System.Collections.Generic;
using System.Runtime.Serialization;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Settings
{
    [DataContract]
    public class ChatSettings : NotifyPropertyChanged
    {
		[DataMember]
        public int Interval
		{
			get => this._interval;
			set => Set(ref this._interval, value);
		}
	    int _interval;

	    [DataMember]
	    public List<HostRecord> Hosts
	    {
		    get => this._hosts;
		    set => Set(ref this._hosts, value);
		}
		List<HostRecord> _hosts;

	}
}
