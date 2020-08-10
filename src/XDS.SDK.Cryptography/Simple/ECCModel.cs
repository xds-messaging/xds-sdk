using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace XDS.SDK.Cryptography.Simple
{
	[DataContract]
    public class ECCModel
    {
        [DataMember(Name = "cipherV2Bytes")]
        public string CipherV2Bytes;

        [DataMember(Name = "currentPublicKey")]
        public string CurrentPublicKey { get; set; }

        [DataMember(Name = "authKey")]
        public string AuthKey { get; set; }
    }
}
