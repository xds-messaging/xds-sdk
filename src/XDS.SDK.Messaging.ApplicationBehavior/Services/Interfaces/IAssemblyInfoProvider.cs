using System.Reflection;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface IAssemblyInfoProvider
    {
        string AssemblyProduct { get; }
        string AssemblyDescription { get; }
        string AssemblyVersion { get; }
        string AssemblyCompany { get; }
        string AssemblyCopyright { get; }
        Assembly Assembly { set; }
    }
}
