using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace XDS.SDK.Messaging.Resources.Strings
{
    public class Info : INotifyPropertyChanged
    {
        public readonly List<string> AvailableCultures = new List<string> {"en", "de", "fr", "it", "ru" };

        public void SwitchCulture(string cultureString)
        {
            var culture = new CultureInfo(cultureString);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            OnCultureChanged();
            OnPropertyChanged(nameof(this.IsEN));
            OnPropertyChanged(nameof(this.IsDE));
            OnPropertyChanged(nameof(this.IsFR));
            OnPropertyChanged(nameof(this.IsIT));
            OnPropertyChanged(nameof(this.IsRU));
        }


        public bool IsEN { get { return IsCurrentCulture("en"); } }
        public bool IsDE { get { return IsCurrentCulture("de"); } }
        public bool IsFR { get { return IsCurrentCulture("fr"); } }
        public bool IsIT { get { return IsCurrentCulture("it"); } }
        public bool IsRU { get { return IsCurrentCulture("ru"); } }


        bool IsCurrentCulture(string cultureString)
        {
            if (CultureInfo.CurrentUICulture.Name.StartsWith(cultureString.ToLowerInvariant()))
                return true;
            return false;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }


        public event EventHandler CultureChanged;
        void OnCultureChanged()
        {
            var handler = CultureChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
