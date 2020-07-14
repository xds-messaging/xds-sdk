using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XDS.SDK.Messaging.CrossTierTypes.FStore
{
    public interface IFStore
    {
        Task<string> InsertFile(FSTable table, string itemId, Func<string, object, object, byte[]> doSerialize,
            object serFun, object entity, string page = null, int currentAttmept = 1);

        Task<FSFile> FindFile(FSTable table, string id, string page);

        Task DeleteFileIfExists(FSTable table, string id, string page);

        Task UpdateFile(FSTable table, FSFile file, string page);

        Task<IReadOnlyList<FSFile>> GetRange(FSTable table, uint startIndex, uint maxCount, string page);

        Task<IReadOnlyList<FSFile>> GetAll(FSTable table, string page);

        Task<uint> CountFiles(FSTable table, string page);

		Task DeleteAndRecreateStore();

		Task DeleteTableIfExists(FSTable table, string page);

	    Task<byte[]> LoadSpecialFile(string filename);

	    Task WriteSpecialFile(string filename, byte[] buffer);

    }
}
