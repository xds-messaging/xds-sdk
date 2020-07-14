using Microsoft.Extensions.DependencyInjection;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.Messaging.SDK.ApplicationBehavior.ViewModels;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Cryptography.E2E;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.Resources.Strings;

namespace XDS.Messaging.SDK.ApplicationBehavior
{
    public static class BootstrapperCommon
    {
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<AppState>();
            services.AddSingleton<ResourceWrapper>();
            services.AddSingleton<NotificationManager>();
            services.AddSingleton<AppRepository>();

            services.AddSingleton<UnreadManager>();
            services.AddSingleton<IChatEncryptionService,PortableChatEncryptionService>();
            services.AddSingleton<E2ERatchet>();

            services.AddSingleton<IChatClient,ChatClient>();
            services.AddSingleton<INetworkClient, NoTLSClient>();
            services.AddSingleton<ChatWorker>();
            services.AddSingleton<DeviceVaultService>();
            services.AddSingleton<ProfileViewModel>();


            services.AddSingleton<ContactListManager>();
            services.AddSingleton<ContactsViewModel>();
            services.AddSingleton<OnboardingViewModel>();
		}
      
    }
}
