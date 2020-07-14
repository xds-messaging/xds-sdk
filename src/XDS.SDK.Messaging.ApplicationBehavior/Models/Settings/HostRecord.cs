using System.Runtime.Serialization;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Settings
{
	[DataContract]
	public class HostRecord
	{
		[DataMember]
		public string DnsIp;
		[DataMember]
		public int Port;
		[DataMember]
		public string Label;
		[DataMember]
		public bool IsSelected;

		public override bool Equals(object obj)
		{
			if (obj == null || !(obj is HostRecord other))
				return false;
			return this.DnsIp == other.DnsIp && this.Port == other.Port;
		}

		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		public override string ToString()
		{
			return $"{ this.DnsIp ?? "null"}:{this.Port}";
		}
	}
}
