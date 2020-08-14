using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;

namespace XDS.Messaging.SDK.ApplicationBehavior.ViewModels
{
	public class ProfileViewModel : NotifyPropertyChanged
	{
		public const string ProfileFStoreId = "1";
		public const string DefaultUsername = "Anonymous";

		readonly AppRepository _repo;
		readonly IFileService _fileService;

		

		public ProfileViewModel(AppRepository appRepository, IFileService fileService)
		{

			this._repo = appRepository;
			this._fileService = fileService;
		}

		public string Name
		{
			get => this._name;
			private set => Set(ref this._name, value);
		}
		string _name;

        public string NewName
        {
            get => this._newName;
			set => Set(ref this._newName, value);
        }
        string _newName;

        public string RenameError
        {
            get => this._renameError;
            private set => Set(ref this._renameError, value);
        }
        string _renameError;

		public byte[] PictureBytes
		{
			get => this._pictureBytes;
			private set => Set(ref this._pictureBytes, value);
		}
		byte[] _pictureBytes;

		public byte[] PublicKey
		{
			get => this._publicKey;
			private set => Set(ref this._publicKey, value);
		}
		byte[] _publicKey;

		public string ChatId
		{
			get => this._chatId;
			private set => Set(ref this._chatId, value);
		}
		string _chatId;

        public string DefaultAddress
		{
            get => this.defaultAddress;
            private set => Set(ref this.defaultAddress, value);
        }
        string defaultAddress;

		public bool IsIdentityPublished
		{
			get => this._isIdentityPublished;
			private set => Set(ref this._isIdentityPublished, value);
		}
		bool _isIdentityPublished;
		
		public async Task LoadProfile()
		{
			var profile = await this._repo.GetProfile();
			this.Name = profile.Name;
            this.NewName = profile.Name;
            this.DefaultAddress = profile.DefaultAddress;
			this.PublicKey = profile.PublicKey;
			this.ChatId = XDS.SDK.Messaging.CrossTierTypes.ChatId.GenerateChatId(profile.PublicKey);
			this.IsIdentityPublished = profile.IsIdentityPublished;
			if (profile.PictureBytes == null)
				await ResetProfileImage();
			else
				this.PictureBytes = profile.PictureBytes;
		}

		public async Task ResetProfileImage()
		{
			
			this.PictureBytes = await this._fileService.LoadAssetImageBytesAsync("Profile.png");
			await SaveChanges();
		}

		

		public async Task UpdateProfileImage(byte[] pictureBytes)
		{
			if (pictureBytes == null)
			{
				await ResetProfileImage();
				return;
			}
			this.PictureBytes = pictureBytes;
			await SaveChanges();
		}

		async Task SaveChanges()
		{
			var profile = await this._repo.GetProfile();
			profile.Name = this.Name;
			profile.PictureBytes = this._pictureBytes;
			await this._repo.UpdateProfile(profile);
		}

        public bool CanExecuteRenameCommand()
        {
            if (string.IsNullOrEmpty(this.NewName))
            {
                this.RenameError = "Too short!";
                return false;
            }

            if (this.NewName.Length > 50)
            {
                this.RenameError = "Too long!";
                return false;
            }
            if (this.NewName == this.Name)
            {
                this.RenameError = "No Change...";
                return false;
            }
            this.RenameError = "";
            return true;

        }

        public async Task ExecuteRenameCommand()
        {
            var profile = await this._repo.GetProfile();
            profile.Name = this.NewName;
            await this._repo.UpdateProfile(profile);
            this.Name = NewName;
        }

	}
}
