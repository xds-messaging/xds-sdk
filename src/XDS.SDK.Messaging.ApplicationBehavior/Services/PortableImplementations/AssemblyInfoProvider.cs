using System;
using System.Linq;
using System.Reflection;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public class AssemblyInfoProvider : IAssemblyInfoProvider
    {
        Assembly _assembly;
        string _product;
        string _description;
        string _version;
        string _company;
        string _copyright;


        public string AssemblyProduct
        {
            get
            {
                if (this._product != null)
                    return this._product;
                this._product = GetAttribute<AssemblyProductAttribute>().Product;
                return this._product;
            }
        }

        public string AssemblyDescription
        {
            get
            {
                if (this._description != null)
                    return this._description;
                this._description = GetAttribute<AssemblyDescriptionAttribute>().Description;
                return this._description;
            }
        }

        public string AssemblyVersion
        {
            get
            {
                if (this._version != null)
                    return this._version;
                var ver = this.Assembly.GetName().Version;

                this._version = string.Format("{0}.{1}.{2}", ver.Major, ver.Minor, ver.Build);
                return this._version;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                if (this._company != null)
                    return this._company;
                this._company = GetAttribute<AssemblyCompanyAttribute>().Company;
                return this._company;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                if (this._copyright != null)
                    return this._copyright;
                this._copyright = GetAttribute<AssemblyCopyrightAttribute>().Copyright;
                return this._copyright;
            }
        }

        public Assembly Assembly
        {
            get
            {
                if (this._assembly == null)
                    this._assembly = typeof(AssemblyInfoProvider).GetTypeInfo().Assembly;
                return this._assembly;
            }
            set
            {
                this._assembly = value;
            }
        }

        T GetAttribute<T>() where T : Attribute
        {
            return (T)(this.Assembly.GetCustomAttributes(typeof(T))).Single();
        }
    }
}
