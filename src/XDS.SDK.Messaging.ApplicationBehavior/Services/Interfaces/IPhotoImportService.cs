using System.Threading.Tasks;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces
{
    public interface IPhotoImportService
    {
        Task<byte[]> ImportPhoto(string pathAndFilename);
        Task<string> GetPhotoFutureAccessPath();

        Task<byte[]> GetProfilePhotoBytes(object context = null);


		Task<byte[]> GetProfilePhotoBytesFromThumbnailImage();
        Task<object> ConvertPhotoBytesToPlatformImage(byte[] photoBytes);
        Task<object> ConvertPhotoBytesToPlatformImageBrush(byte[] photoBytes);
		


	}
}
