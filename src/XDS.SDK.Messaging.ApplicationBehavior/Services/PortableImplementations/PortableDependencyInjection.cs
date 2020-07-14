using System;
using Microsoft.Extensions.DependencyInjection;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public interface IDependencyInjection
    {
        IServiceCollection ServiceCollection { get; }
        IServiceProvider ServiceProvider { get; }
        void AddServiceProvider(IServiceProvider serviceProvider);
    }

    public sealed class PortableDependencyInjection : IDependencyInjection
    {
        public PortableDependencyInjection(IServiceCollection serviceCollection)
        {
            this.ServiceCollection = serviceCollection;
        }

        public IServiceCollection ServiceCollection { get; }

        public IServiceProvider ServiceProvider { get; private set; }

        public void AddServiceProvider(IServiceProvider provider)
        {
            this.ServiceProvider = provider;
        }
    }
}
