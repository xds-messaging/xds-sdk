using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Chat
{
	public class Contact : INotifyPropertyChanged
	{

		public Contact()
		{

		}
		string _id;
		public string Id
		{
			get => this._id;
			set { Set(ref this._id, value); }
		}

		public string ChatId => XDS.SDK.Messaging.CrossTierTypes.ChatId.GenerateChatId(this.StaticPublicKey);

		string _name;
		public string Name
		{
			get => this._name;
			set { Set(ref this._name, value); }
		}

		byte[] _pictureBytes;
		public byte[] PictureBytes
		{
			get => this._pictureBytes;
			set { Set(ref this._pictureBytes, value); }
		}

		int _unreadMessages;
		public int UnreadMessages
		{
			get => this._unreadMessages;
			set => Set(ref this._unreadMessages, value);
		}

		bool _hasMessages;
		public bool HasMessages
		{
			get => this._hasMessages;
			set { Set(ref this._hasMessages, value); }
		}

		ContactState _contactState;
		public ContactState ContactState
		{
			get => this._contactState;
			set { Set(ref this._contactState, value); }
		}

		string _contactStateText;
		public string ContactStateText
		{
			get => this._contactStateText;
			set { Set(ref this._contactStateText, value); }
		}

		string _chatPreview;
		public string ChatPreview
		{
			get => this._chatPreview;
			set { Set(ref this._chatPreview, value); }
		}

		DateTime _lastMessageDateLocalTime;
		public DateTime LastMessageDateLocalTime
		{
			get => this._lastMessageDateLocalTime;
			set { Set(ref this._lastMessageDateLocalTime, value); }
		}

		byte[] _staticPublicKey;
		public byte[] StaticPublicKey
		{
			get => this._staticPublicKey;
			set { Set(ref this._staticPublicKey, value); }
		}

		bool _lastMessageSideIsMe;
		public bool LastMessageSideIsMe
		{
			get => this._lastMessageSideIsMe;
			set => Set(ref this._lastMessageSideIsMe, value);
		}

		bool _isLastMessageUnread;
		public bool IsLastMessageUnread
		{
			get => this._isLastMessageUnread;
			set => Set(ref this._isLastMessageUnread, value);
		}

		SendMessageState _sendMessageState;
		public SendMessageState SendMessageState
		{
			get => this._sendMessageState;
			set => Set(ref this._sendMessageState, value);
		}

		string _unverfiedId;
		public string UnverfiedId
		{
			get => this._unverfiedId;
			set { Set(ref this._unverfiedId, value); }
		}

		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;
		void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
		{
			if (Equals(storage, value))
			{
				return;
			}

			storage = value;
			OnPropertyChanged(propertyName);
		}
		void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
