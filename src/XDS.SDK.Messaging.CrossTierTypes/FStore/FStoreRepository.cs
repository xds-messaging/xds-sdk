using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
	public class FStoreRepository<T> : IAsyncRepository<T> where T : class, IId
	{
		static FSTable Table => FStoreTables.TableConfig[typeof(T)];

		readonly IFStore _fStore;
		readonly Func<T, byte[]> _serialize;
		readonly Func<byte[], T> _deserialize;

		public FStoreRepository(IFStore fstore, Func<T, byte[]> serialize, Func<byte[], T> deserialize)
		{
			this._fStore = fstore;
			this._serialize = serialize;
			this._deserialize = deserialize;
		}

		public object Store => this._fStore;

		public async Task Add(T item, string page = null)
		{
			if (Table.IdMode == IdMode.Auto && item.Id != null)
				throw new InvalidOperationException();
			if (Table.IsPaged && page == null)
				throw new InvalidOperationException();
			item.Id = await this._fStore.InsertFile(Table, item.Id, UpdateIdAndSerialize, this._serialize, item, page);
		}

		byte[] UpdateIdAndSerialize(string itemId, object serFun, object entity)
		{
			Func<T, byte[]> fun = (Func<T, byte[]>)serFun;
			T actualEntity = (T)entity;
			actualEntity.Id = itemId;
			return fun(actualEntity);
		}

		public async Task<T> Get(string id, string page = null)
		{
			var file = await this._fStore.FindFile(Table, id, page);
			if (file == null)
				return null;
			return this._deserialize(file.Contents);
		}

		public async Task Delete(string id, string page = null)
		{
			await this._fStore.DeleteFileIfExists(Table, id, page);
		}

		public async Task Update(T item, string page)
		{
			var contents = this._serialize(item);
			await this._fStore.UpdateFile(Table, new FSFile(contents, item.Id), page);
		}

		public async Task<uint> Count(string page = null)
		{
			return await this._fStore.CountFiles(Table, page);
		}

		public async Task<IReadOnlyList<T>> GetAll(string page = null)
		{
			int i;
			IReadOnlyList<FSFile> files = await this._fStore.GetAll(Table, page);
			var entities = new T[files.Count];
			for (i = 0; i < files.Count; i++)
			{
				var contents = files[i].Contents;
				if (contents.Length == 0) // the file was broken, e.g. because of a crash while writing to it
				{
					await Delete(files[i].Id, page); // delete the broken file, it's gone
					return await GetAll(page); // restart
				}
				entities[i] = this._deserialize(contents);
			}

			return entities;
		}

		public async Task<IReadOnlyList<T>> GetRange(uint startIndex, uint maxCount, string page = null)
		{
			var files = await this._fStore.GetRange(Table, startIndex, maxCount, page);
			var entities = new T[files.Count];
			for (var i = 0; i < files.Count; i++)
				entities[i] = this._deserialize(files[i].Contents);
			return entities;
		}

		public async Task DeletePage(string page)
		{
			if (!Table.IsPaged)
				throw new InvalidOperationException();
			await this._fStore.DeleteTableIfExists(Table, page);
		}
	}
}
