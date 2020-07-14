using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;

namespace XDS.Messaging.SDK.AppSupport.NetStandard
{
	public class NotificationService : INotificationService
	{
		public NotificationService()
		{

		}

		public async Task CreateDeviceNotification()
		{
			await Task.CompletedTask;
		}

		public void PlaySound()
		{
			
		}
	}
}