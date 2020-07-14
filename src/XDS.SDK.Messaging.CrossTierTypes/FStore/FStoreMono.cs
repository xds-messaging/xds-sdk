using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
	public class FStoreMono : IFStore
	{
		readonly string _storeName; // e.g. "FStore"
		readonly DirectoryInfo _storeLocation;  // e.g. new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Personal)); -> /e.g. data/user/0/com.xdssec/files/
		readonly Action _initFStore;
		StorageFolder _storeFolder; // e.g. "/data/user/0/com.xdssec/files/FStore"

		public FStoreMono(FStoreConfig fStoreConfig)
		{
			this._storeName = fStoreConfig.DefaultStoreName;
			this._storeLocation = fStoreConfig.StoreLocation;
			this._initFStore = fStoreConfig.Initializer;
			var storeFolder = Path.Combine(this._storeLocation.FullName, this._storeName);
			if (Directory.Exists(storeFolder))
				this._storeFolder = new StorageFolder(storeFolder);
		}

		public bool StoreExists()
		{
			return this._storeFolder != null;
		}

		public void CreateStore()
		{
			var storeFolder = Path.Combine(this._storeLocation.FullName, this._storeName);
			if (Directory.Exists(storeFolder))
				throw new Exception("The folder already exists!");
			Directory.CreateDirectory(storeFolder);
			this._storeFolder = new StorageFolder(storeFolder);
		}

		public void CreateTable(FSTable table)
		{
			var store = this._storeFolder;
			store.CreateFolderAsync(table.TableFolderName(), CreationCollisionOption.FailIfExists);
		}

		public async Task<string> InsertFile(FSTable table, string itemId, Func<string, object, object, byte[]> doSerialize, object serFun, object entity, string page = null, int currentAttmept = 1)
		{
			if (table == null || doSerialize == null)
				throw new ArgumentException();
			var tableFolder = FindTableFolder(table, page);

			if (table.IdMode == IdMode.UserGenerated)
			{
				StorageFile newFile = tableFolder.CreateFile(itemId, CreationCollisionOption.FailIfExists);
				FileIO.WriteBytes(newFile, doSerialize(newFile.Name, serFun, entity));
				return newFile.Name;
			}

			string nextId = GetNextId(tableFolder);
			try
			{
				StorageFile newFile = tableFolder.CreateFile(nextId, CreationCollisionOption.FailIfExists);
				FileIO.WriteBytes(newFile, doSerialize(newFile.Name, serFun, entity));
				return newFile.Name;
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.Message);
				Debug.WriteLine($"Insert Attempt {currentAttmept} failed.");
				uint dummy;
				FStoreTables.IdCache.TryRemove(tableFolder.Name, out dummy);
				if (currentAttmept > 10)
					throw;
				return await InsertFile(table, itemId, doSerialize, serFun, entity, page, currentAttmept + 1);
			}
		}

		public async Task<FSFile> FindFile(FSTable table, string id, string page)
		{
			if (table == null || id == null)
				throw new ArgumentException();
			var tableFolder = FindTableFolder(table, page);
			StorageFile storageFile = tableFolder.TryGetFile(id);
			if (storageFile == null)
				return null;
			byte[] buffer = FileIO.ReadBuffer(storageFile);
			return new FSFile(buffer.ToArray(), id);
		}

		public async Task DeleteFileIfExists(FSTable table, string id, string page)
		{
			if (table == null || id == null)
				throw new ArgumentException();
			var tableFolder = FindTableFolder(table, page);
			StorageFile storageFile = tableFolder.TryGetFile(id);
			if (storageFile == null)
				return;
			storageFile.Delete();
			await Task.CompletedTask;
		}

		public async Task UpdateFile(FSTable table, FSFile file, string page)
		{
			if (table == null || file?.Id == null)
				throw new ArgumentException();
			var tableFolder = FindTableFolder(table, page);
			var storageFile = tableFolder.TryGetFile(file.Id);
			FileIO.WriteBytes(storageFile, file.Contents);
			await Task.CompletedTask;
		}

		public async Task<IReadOnlyList<FSFile>> GetRange(FSTable table, uint startIndex, uint maxCount, string page)
		{
			var tableFolder = FindTableFolder(table, page);
			var tableFiles = tableFolder.GetFilesOrderedByName((int)startIndex, (int)maxCount);
			return await ReadFiles(table, tableFiles);
		}

		public async Task<IReadOnlyList<FSFile>> GetAll(FSTable table, string page)
		{
			var tableFolder = FindTableFolder(table, page);
			StorageFile[] tableFiles = tableFolder.GetFilesOrderedByName();
			return await ReadFiles(table, tableFiles);
		}

		async Task<List<FSFile>> ReadFiles(FSTable table, IReadOnlyList<StorageFile> tableFiles)
		{
			var files = new List<FSFile>();
			if (!table.ReadListInReverseOrder)
				for (var i = 0; i < tableFiles.Count; i++)
				{
					var buffer = FileIO.ReadBuffer(tableFiles[i]);
					files.Add(new FSFile(buffer.ToArray(), tableFiles[i].Name));
				}
			else
			{
				for (var i = tableFiles.Count - 1; i >= 0; i--)
				{
					var buffer = FileIO.ReadBuffer(tableFiles[i]);
					files.Add(new FSFile(buffer.ToArray(), tableFiles[i].Name));
				}
			}
			return files;
		}
		public async Task<uint> CountFiles(FSTable table, string page)
		{
			var tableFolder = FindTableFolder(table, page);
			return tableFolder.CountFiles();

		}

		string GetNextId(StorageFolder tableFolder)
		{
			uint lastIdFromCache;
			if (FStoreTables.IdCache.TryGetValue(tableFolder.Name, out lastIdFromCache))
			{
				var newId = lastIdFromCache - 1;
				FStoreTables.IdCache[tableFolder.Name] = newId;
				return newId.ToString("d6");
			}
			StorageFile[] tableFiles = tableFolder.GetFilesOrderedByName(0, 1);

			uint lastId;
			if (tableFiles.Length == 0)
				lastId = 999999;
			else
			{
				var lastTableFile = tableFiles[0];
				if (!uint.TryParse(lastTableFile.Name, NumberStyles.None, null, out lastId))
				{
					// we delete everything that spoils our system!
					tableFiles[0].Delete();
					GetNextId(tableFolder);
				}
			}
			uint nextId = lastId - 1; // we'll never return 999999, the fist id will be 999998
			FStoreTables.IdCache[tableFolder.Name] = nextId;
			var nextFileName = nextId.ToString("d6");
			return nextFileName;
		}



		public bool TableExists(FSTable table, string page)
		{
			var tableFolder = FindTableFolder(table, page);
			return tableFolder != null;
		}

		public async Task DeleteAndRecreateStore()
		{
			if (this._storeFolder != null)
			{
				this._storeFolder.DeleteIfExists();
			}
			this._initFStore();
			await Task.CompletedTask;
		}

		public async Task DeleteTableIfExists(FSTable table, string page)
		{
			StorageFolder tableFolder = FindTableFolder(table, page);
			if (tableFolder == null)
				return;
			tableFolder.DeleteIfExists();
			await Task.CompletedTask;
		}

		StorageFolder FindTableFolder(FSTable table, string page)
		{
			StorageFolder tableFolder = this._storeFolder.TryGetItemAsync(table.TableFolderName());
			if (tableFolder == null)
				return null;
			if (!table.IsPaged || page == null)
				return tableFolder;
			StorageFolder pageFolder = tableFolder.CreateFolderAsync(page, CreationCollisionOption.OpenIfExists);
			return pageFolder;
		}

		public async Task<byte[]> LoadSpecialFile(string filename)
		{
            var fStoreFolder = this._storeFolder;//await FindStoreFolder();
            if (fStoreFolder == null)
                return null;
            var specialFile = fStoreFolder.TryGetFile(filename);
            if (specialFile == null)
                return null;
            //if (!specialFile.IsOfType(StorageItemTypes.File))
                //throw new Exception();
            var buffer = FileIO.ReadBuffer((StorageFile)specialFile);
            return buffer.ToArray();
		}

        public async Task WriteSpecialFile(string filename, byte[] buffer)
        {
            var fStoreFolder = this._storeFolder;
            if (fStoreFolder == null)
                throw new Exception("FStore folder not present.");

            var specialFile = fStoreFolder.CreateFile(filename, CreationCollisionOption.ReplaceExisting);
            FileIO.WriteBytes(specialFile, buffer);
	        await Task.CompletedTask;
        }

		class StorageFolder
		{
			public readonly string Name;
			public StorageFolder(string name)
			{
				this.Name = name;
			}

			public StorageFolder TryGetItemAsync(string tableFolderName)
			{
				if (tableFolderName == null)
					return null;
				var subdirectory = Path.Combine(this.Name, tableFolderName);
				if (!Directory.Exists(subdirectory))
					return null;

				return new StorageFolder(subdirectory);
			}

			public StorageFolder CreateFolderAsync(string name, CreationCollisionOption creationCollisionOption = CreationCollisionOption.FailIfExists)
			{
				if (string.IsNullOrWhiteSpace(name))
					throw new InvalidOperationException("Invalid folder name.");
				var fullpath = Path.Combine(this.Name, name);
				var exists = Directory.Exists(fullpath);

				if (creationCollisionOption == CreationCollisionOption.FailIfExists)
				{
					if (exists)
						throw new InvalidOperationException("Folder already exists.");
					Directory.CreateDirectory(fullpath);

				}
				else if (creationCollisionOption == CreationCollisionOption.OpenIfExists)
				{
					if (!exists)
						Directory.CreateDirectory(fullpath);
				}
				else
					throw new NotSupportedException();

				return new StorageFolder(fullpath);
			}

			internal void DeleteIfExists()
			{
				if (Directory.Exists(this.Name))
					Directory.Delete(this.Name, true);
			}

			public StorageFile[] GetFilesOrderedByName(int startIndex, int maxItemsToRetrieve)
			{
				return Directory.GetFiles(this.Name, "*", SearchOption.TopDirectoryOnly)
					.OrderBy(x => x).Skip(startIndex).Take(maxItemsToRetrieve).
					Select(x => new StorageFile(Path.GetFileName(x), x)).ToArray();

			}

			public StorageFile[] GetFilesOrderedByName()
			{
				return Directory.GetFiles(this.Name, "*", SearchOption.TopDirectoryOnly)
					.OrderBy(x => x)
					.Select(x => new StorageFile(Path.GetFileName(x), x)).ToArray();
			}

			public uint CountFiles()
			{
				return (uint)Directory.GetFiles(this.Name, "*", SearchOption.TopDirectoryOnly).Length;
			}

			public StorageFile TryGetFile(string fileName)
			{
				var fullPath = Path.Combine(this.Name, fileName);
				if (File.Exists(fullPath))
					return new StorageFile(fileName, fullPath);
				return null;
			}

			public StorageFile CreateFile(string itemId, CreationCollisionOption creationCollisionOption)
			{
				var filePath = Path.Combine(this.Name, itemId);
				var exists = File.Exists(filePath);

                if (exists)
                {
                    if (creationCollisionOption == CreationCollisionOption.FailIfExists)
                    {
                        throw new InvalidOperationException("File already exists.");
                    }
                    if (creationCollisionOption == CreationCollisionOption.ReplaceExisting)
                    {
                        File.Delete(itemId);
                    }
                }

				return new StorageFile(itemId, filePath);
			}
		}

		class StorageFile
		{
			public readonly string Name;
			public readonly string FullPath;

			public StorageFile(string name, string fullPath)
			{
				this.Name = name;
				this.FullPath = fullPath;
			}

			public void Delete()
			{
				File.Delete(this.FullPath);
			}
		}

		static class FileIO
		{
			internal static byte[] ReadBuffer(StorageFile storageFile)
			{
				return File.ReadAllBytes(storageFile.FullPath);
			}

			internal static void WriteBytes(StorageFile storageFile, byte[] data)
			{
				File.WriteAllBytes(storageFile.FullPath, data);
			}
		}
		enum CreationCollisionOption
		{
			FailIfExists,
			OpenIfExists,
            ReplaceExisting
		}


	}

}
