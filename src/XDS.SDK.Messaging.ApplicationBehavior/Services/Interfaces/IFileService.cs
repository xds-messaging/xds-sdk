
using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface IFileService
    {

        string GetLocalFolderPath();
        string GetInstallLocation();
        Task<object> LoadLocalImageAsync(string imagePath);
        Task<object> LoadLocalImageBrushAsync(string imagePath);

        void SetLocalFolderPathForTests(string localFolderPathOverride);

        Task<object> LoadAssetImageAsync(string name);
        Task<object> LoadAssetImageBrushAsync(string name);
		Task<byte[]> LoadAssetImageBytesAsync(string name);

	}
}
