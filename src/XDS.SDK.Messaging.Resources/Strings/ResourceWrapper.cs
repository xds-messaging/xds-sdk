using System.ComponentModel;

namespace XDS.SDK.Messaging.Resources.Strings
{
    /// <summary>
    /// How to update/add a Resource:
    /// 1. Enter the new string in the Resource designer.
    /// 2. Build the XDSSec.Applications project. This will trigger to run BuildTools, which generates the necessary code.
    /// </summary>
    public partial class ResourceWrapper : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Info Info { get { return this._info; } }
        readonly Info _info;
        public ResourceWrapper()
        {
            this._info = new Info();
            this.Info.CultureChanged += (s, e) =>
            {
                if (this._info.IsEN)
                    this._generatedResource = new Generated_en();
                if (this._info.IsDE)
                    this._generatedResource = new Generated_de();
                if (this._info.IsRU)
                    this._generatedResource = new Generated_ru();
                if (this._info.IsFR)
                    this._generatedResource = new Generated_fr();
                if (this._info.IsIT)
                    this._generatedResource = new Generated_it();

                var p = PropertyChanged;
                if (p != null)
                    p(this, new PropertyChangedEventArgs(null));
            };
        }

        IGeneratedResource _generatedResource = new Generated_en();

       


    }
}
