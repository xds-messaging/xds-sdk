using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
	public interface INotificationService
    {
		Task CreateDeviceNotification();
		void PlaySound();
	}
}
