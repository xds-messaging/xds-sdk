using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Settings
{
    [DataContract]
    public class CryptographySettings : INotifyPropertyChanged
    {
        [DataMember]
        public byte LogRounds
        {
            get { return this._logRounds; }
            set
            {
                if (this._logRounds != value)
                {
                    this._logRounds = value;
                    OnPropertyChanged();
                }
            }
        }

        byte _logRounds;

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}