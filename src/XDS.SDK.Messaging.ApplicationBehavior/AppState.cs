namespace XDS.Messaging.SDK.ApplicationBehavior
{
    public class AppState
    {
		public bool IsMessagesWaiting { get; private set; }
        public bool IsIdentityPublished { get; private set; }
		public bool AreContactsChecked { get; private set; }
		public bool IsUdpConnected { get; internal set; }

        public void SetIsMessagesWaiting(bool isMessagesWaiting)
        {
            this.IsMessagesWaiting = isMessagesWaiting;
        }

		public void SetAreContactsChecked(bool areContactsChecked)
		{
			this.AreContactsChecked = areContactsChecked;
		}

		internal void SetIsIdentityPublished(bool isIdentityPublished)
        {
            this.IsIdentityPublished = isIdentityPublished;
        }


        internal void SetUdpIsConnected(bool v)
        {
            this.IsUdpConnected = v;
        }
    }
}
