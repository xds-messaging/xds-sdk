using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public class NotificationManager
    {
		INotificationService _notificationService;

		public NotificationManager(INotificationService notificationService)
		{
			this._notificationService = notificationService;
		}

		public async Task NotifyMessageReceived(Message message)
		{
			this._notificationService.CreateDeviceNotification();
			this._notificationService.PlaySound();
		}
	}
}
