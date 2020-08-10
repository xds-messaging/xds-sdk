using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.ViewModels
{
	public class ContactsViewModel : NotifyPropertyChanged
	{
		public ObservableCollection<Contact> SelectedContacts { get; } = new ObservableCollection<Contact>();

		readonly AppRepository repo;
		readonly IMessageBoxService messageBoxService;
		readonly IFileService fileService;
		readonly ChatWorker chatWorker;
        readonly ContactListManager contactListManager;
        readonly ProfileViewModel profileViewModel;


		public ContactsViewModel(AppRepository appRepository, IMessageBoxService messageBoxService, IFileService fileService, ChatWorker chatWorker, ProfileViewModel profileViewModel,  ContactListManager contactListManager)
		{
			this.repo = appRepository;
			this.messageBoxService = messageBoxService;
			this.fileService = fileService;
			this.chatWorker = chatWorker;
            this.profileViewModel = profileViewModel;
			this.contactListManager =contactListManager;
		}

		

		#region Adding a Contact



		public bool CanExecuteSaveAddedContactCommand()
		{
			if (string.IsNullOrEmpty(this.AddedContactId))
			{
				this.CurrentError = "The XDS ID is required.";
				return false;
			}

			var regex = new Regex("^[A-Za-z0-9]+$");
			if (!regex.IsMatch(this.AddedContactId))
			{
				this.CurrentError = "Use only latin letters and numbers";
				return false;
			}

			if (this.AddedContactId.Length < 13)
			{
				this.CurrentError = "This XDS ID is too short!";
				return false;
			}
			if (this.AddedContactId.Length > 14)
			{
				this.CurrentError = "This XDS ID is too long!";
				return false;
			}

			// TODO: Verify Base64 Chars
			if (this.contactListManager.Contacts.Count(c => c.ChatId == this.AddedContactId ) > 0)
			{
				this.CurrentError = "This contact already exists.";
				return false;
			}

			if (this.profileViewModel.ChatId == this.AddedContactId)
			{
				this.CurrentError = "You cannot add yourself.";
				return false;
			}

            try
            {
                ChatId.DecodeChatId(this.AddedContactId);

            }
            catch (InvalidDataException ae)
            {
                this.CurrentError = ae.Message;
                return false;
            }
			this.CurrentError = "Looks good!";
			return true;

		}

		public async Task ExecuteSaveAddedContactCommand()
		{
			try
			{
				var addedContactId = this.AddedContactId;
				await AddContact(addedContactId);
				// await GetAllContacts();
				// var added = Contacts.Single(c => c.Id == addedContactId);
				// CurrentContact = added; // add this point, the contact is not yet usable, we have only tried to send the request for public key and verification to the server but we cannot have a reply yet.
			}
			catch (Exception e)
			{
				await this.messageBoxService.ShowError(e.Message);
			}
		}

		async Task AddContact(string addedContactId)
        {
            var name = $"Added {DateTime.Now.DayOfWeek}";

            if (!string.IsNullOrWhiteSpace(this.NewName) && this.NewName.Trim().Length <= 35)
                name = this.NewName.Trim();

			var addedContact = new Identity
			{
				Id = Guid.NewGuid().ToString(),
				UnverifiedId = addedContactId,
				Name = name,
				ContactState = ContactState.Added
			};
			await this.repo.AddContact(addedContact);

			await this.chatWorker.VerifyContactInAddedStateAsync(addedContact);
		}

		public string AddedContactId
		{
			get => this._addedContactId;
			set => Set(ref this._addedContactId, value);
		}
		string _addedContactId;

		public string CurrentError
		{
			get => this._currentError;
			set => Set(ref this._currentError, value);
		}
		string _currentError;

		#endregion

		public Contact ContactToEdit
		{
			get => this._contactToEdit;
			set
			{
				this.NewName = value.Name;
				Set(ref this._contactToEdit, value);
			}
		}
		Contact _contactToEdit;

		public bool SetContactToEdit(string contactId)
		{
			this.ContactToEdit = this.contactListManager.Contacts.SingleOrDefault(c => contactId == c.Id);
			return this.ContactToEdit != null;
		}

		public string RenameError
		{
			get => this._renameError;
			set => Set(ref this._renameError, value);
		}
		string _renameError;

		public string NewName
		{
			get => this._newName;
			set => Set(ref this._newName, value);
		}
		string _newName;



		public bool CanExecuteRenameContactCommand()
		{
			if (string.IsNullOrEmpty(this.NewName))
			{
				this.RenameError = "Rename: Too short!";
				return false;
			}

			if (this.NewName.Length > 50)
			{
				this.RenameError = "Rename: Too long!";
				return false;
			}
			if (this.NewName == this.ContactToEdit.Name)
			{
				this.RenameError = "Rename: No Change...";
				return false;
			}
			this.RenameError = "Rename:";
			return true;

		}

		public async Task ExecuteRenameContactCommand()
		{
			try
			{
				await this.repo.UpdateContactName(this.ContactToEdit.Id, this.NewName);
				this.ContactToEdit.Name = this.NewName;
			}
			catch (Exception e)
			{
				await this.messageBoxService.ShowError(e.Message);
			}
		}


		

		

		public async Task UpdateContactImage(byte[] pictureBytes)
		{
			this.ContactToEdit.PictureBytes = pictureBytes ?? await this.fileService.LoadAssetImageBytesAsync("Profile.png");
			await this.repo.UpdateContactImage(this.ContactToEdit.Id, this.ContactToEdit.PictureBytes);
		}

		#region Delete a Contact

		public string DeleteList
		{
			get => this._deleteList;
			set => Set(ref this._deleteList, value);
		}
		string _deleteList;

		public async void ExecuteDeleteCommand()
		{
			try
			{
				var contactIds = this.SelectedContacts.Select(x => x.Id).ToArray();
				foreach (var id in contactIds)
				{
					await this.repo.DeleteAllMessages(id);
				}
				await this.repo.DeleteContacts(contactIds);
				this.contactListManager.RemoveContactsAndConversations(contactIds);

				this.SelectedContacts.Clear();
				this.contactListManager.CurrentContact = this.contactListManager.Contacts.FirstOrDefault();

			}
			catch (Exception e)
			{
				await this.messageBoxService.ShowError(e.Message);
			}
		}

		#endregion
	}


}
