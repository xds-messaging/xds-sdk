using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XDS.Messaging.SDK.ApplicationBehavior.Infrastructure
{
    public static class ObservableCollectionAddRange
    {
        public static void AddRange<T>(this ObservableCollection<T> observableCollection, IEnumerable<T> items)
        {
            foreach (var i in items)
                observableCollection.Add(i);
        }

    }
}
