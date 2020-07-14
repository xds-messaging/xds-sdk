using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Settings
{
    [DataContract]
    public class UpdateSettings : INotifyPropertyChanged
    {
        [DataMember]
        public string Version
        {
            get { return this._version; }
            set
            {
                if (this._version != value)
                {
                    this._version = value;
                    OnPropertyChanged();
                }
            }
        }
        string _version;

        [DataMember]
        public string SKU
        {
            get { return this._sku; }
            set
            {
                if (this._sku != value)
                {
                    this._sku = value;
                    OnPropertyChanged();
                }
            }
        }
        string _sku;

        [DataMember]
        public DateTime Date
        {
            get { return this._date; }
            set
            {
                if (this._date != value)
                {
                    this._date = value;
                    OnPropertyChanged();
                }
            }
        }
        DateTime _date;

        [DataMember]
        public bool Notify
        {
            get { return this._notify; }
            set
            {
                if (this._notify != value)
                {
                    this._notify = value;
                    OnPropertyChanged();
                }
            }
        }
        bool _notify;

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}