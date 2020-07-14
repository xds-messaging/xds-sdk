using System;
using System.Collections.Generic;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public sealed class SimpleContainer : IServiceProvider, IDisposable
    {
        readonly Dictionary<string, Descriptor> _implementationsByKey = new Dictionary<string, Descriptor>();

        bool _isDisposing;
        bool _isDisposed;

        public SimpleContainer(string debugName)
        {
            if (string.IsNullOrWhiteSpace(debugName))
                throw new ArgumentException("Please supply a debug name for this Container.", nameof(debugName));
            this.Name = debugName;
        }

        public string Name { get; }

        public void RegisterType<TKey, TImplementation>(string instanceLabel = null, bool replaceExisting = false) where TImplementation : class
        {
            var key = GetKey<TKey>(instanceLabel);
            if (replaceExisting)
                this._implementationsByKey.Remove(key);
            if (this._implementationsByKey.ContainsKey(key))
                throw new Exception($"{key} is already registered. Use Register(..., replaceExisting = true) to replace.");
            this._implementationsByKey.Add(key, new Descriptor
            {
                ImplementationType = typeof(TImplementation),
                Instance = null,
            });
        }


        public void RegisterObject<TKey>(object instance, string instanceLabel = null, bool replaceExisting = false)
        {
            var key = GetKey<TKey>(instanceLabel);
            if (replaceExisting)
                this._implementationsByKey.Remove(key);
#if DEBUG
            if (this._implementationsByKey.ContainsKey(key))
                throw new Exception($"{key} is already registered. Use Register(..., replaceExisting = true) to replace.");
#endif
            this._implementationsByKey.Add(key, new Descriptor
            {
                ImplementationType = instance.GetType(),
                Instance = instance,
            });
        }
        static string GetKey<TKey>(string instanceLabel)
        {
            return instanceLabel == null
                ? typeof(TKey).FullName
                : $"{typeof(TKey).FullName} - {instanceLabel}";
        }

        public TKey Get<TKey>(string instanceLabel = null) where TKey : class
        {
            if (this._isDisposed)
                throw new ObjectDisposedException("Container is disposed.");

            var key = GetKey<TKey>(instanceLabel);
            if (!this._implementationsByKey.ContainsKey(key))
                throw new Exception($"Container: {key} is not yet registred but required byanother Type. Register this Type/object, or register it before dependend Types ask for it.");


            var descriptor = this._implementationsByKey[key];

            if (descriptor.Instance != null)
                return (TKey)descriptor.Instance;
            try
            {
                descriptor.Instance = (TKey)Activator.CreateInstance(descriptor.ImplementationType);
            }

            catch (Exception e)
            {
                throw new Exception($"Container could not create instance of {descriptor.ImplementationType}. Key: {key}. {e.Message}");
            }
            return (TKey)descriptor.Instance;
        }


        public void Dispose()
        {
            if (this._isDisposing)
                return;
            this._isDisposing = true;

            foreach (var descriptor in this._implementationsByKey)
            {
	            try
	            {
		            var instance = descriptor.Value.Instance as IDisposable;
		            instance?.Dispose();
	            }
	            catch (Exception e)
	            {
					
	            }
            }
            this._implementationsByKey.Clear();
	        this._isDisposed = true;
        }

        public object GetService(Type serviceType)
        {
            throw new NotImplementedException();
        }

        sealed class Descriptor
        {
            public Type ImplementationType;
            public object Instance;
        }
    }


}
