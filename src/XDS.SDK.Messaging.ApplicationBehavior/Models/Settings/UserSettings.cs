using System.Runtime.Serialization;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Settings
{
    [DataContract]
    public class UserSettings
    {

        [DataMember]
        public CryptographySettings CryptographySettings { get; set; }

        [DataMember]
        public UpdateSettings UpdateSettings { get; set; }

        [DataMember]
        public ChatSettings ChatSettings { get; set; }
    }
}
