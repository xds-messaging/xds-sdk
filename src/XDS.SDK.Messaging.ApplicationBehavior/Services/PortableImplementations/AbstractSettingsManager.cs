using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Settings;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public abstract class AbstractSettingsManager
    {
        protected const string SettingsFilename = "Settings.txt";
        protected readonly IAssemblyInfoProvider Aip;
        protected readonly IDependencyInjection DependencyInjection;
        protected readonly ILogger Logger;

        protected AbstractSettingsManager(IDependencyInjection dependencyInjection, ILoggerFactory loggerFactory)
        {
            this.Logger = loggerFactory.CreateLogger<AbstractSettingsManager>();
            this.Aip = dependencyInjection.ServiceProvider.Get<IAssemblyInfoProvider>();
            this.DependencyInjection = dependencyInjection;
            InitForBinding();
        }

        public ChatSettings ChatSettings { get; protected set; }
        public CryptographySettings CryptographySettings { get; protected set; }
        public UpdateSettings UpdateSettings { get; protected set; }

        public abstract string CurrentDirectoryName { get; set; }




        public abstract void FactorySettings();

        protected abstract string ReadSettingsFile();

        protected abstract void WriteSettingsFile(string settingsFile);

        void InitForBinding()
        {
            var success = TryLoadSettings();
            if (!success || IsMigrationNesessary())
            {
                FactorySettings();
                TrySaveSettings();
            }
            this.ChatSettings.PropertyChanged += (s, e) => TrySaveSettings();
            this.CryptographySettings.PropertyChanged += (s, e) => TrySaveSettings();
            this.UpdateSettings.PropertyChanged += (s, e) => TrySaveSettings();
        }

        bool IsMigrationNesessary()
        {
            return (this.ChatSettings == null
                    || this.CryptographySettings == null
                    || this.UpdateSettings == null);
        }

        bool TrySaveSettings()
        {
            try
            {
                // Collect the Settings
                var settings = new UserSettings
                {
                    ChatSettings = this.ChatSettings,
                    CryptographySettings = this.CryptographySettings,
                    UpdateSettings = this.UpdateSettings
                };

                // Serialize, save
                var serializedSettings = Serialize(settings);
                WriteSettingsFile(serializedSettings);

                return true;
            }
            catch (Exception e)
            {
                this.Logger.LogError($"Could not save settings: {e.Message}");
                return false;
            }
        }

        bool TryLoadSettings()
        {
            try
            {
                // Load, deserialize
                string serializedSettings = ReadSettingsFile();
                if (string.IsNullOrWhiteSpace(serializedSettings))
                    return false;
                var settings = Deserialize(serializedSettings);

                // Distribute
                this.ChatSettings = settings.ChatSettings;

                if (string.IsNullOrWhiteSpace(this.ChatSettings?.Hosts?.FirstOrDefault()?.DnsIp))
                    return false;
                this.CryptographySettings = settings.CryptographySettings;
                this.UpdateSettings = settings.UpdateSettings;
                if (this.CryptographySettings == null ||
                    this.UpdateSettings == null)
                    return false;
                return true;
            }
            catch (Exception e)
            {
                this.Logger.LogWarning($"Could not load settings: {e.Message}");
                return false;
            }
        }

        string Serialize(UserSettings settings)
        {
            using (var stream = new MemoryStream())
            {
                var ser = new DataContractSerializer(typeof(UserSettings));
                ser.WriteObject(stream, settings);
                var data = stream.ToArray();
                var serialized = Encoding.UTF8.GetString(data, 0, data.Length);
                return serialized;
            }
        }

        UserSettings Deserialize(string data)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
            {
                var ser = new DataContractSerializer(typeof(UserSettings));
                var settings = (UserSettings)ser.ReadObject(stream);
                return settings;
            }
        }
    }
}





