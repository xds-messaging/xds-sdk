
using System;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Chat.MessageCollection.Framework
{
    public class ItemIndexRange
    {
        public readonly int FirstIndex;
        public readonly int Length;

        public ItemIndexRange(int firstIndex, int length)
        {
            if (firstIndex < 0)
                throw new ArgumentOutOfRangeException("firstIndex");
            if (length < 0)
                throw new ArgumentOutOfRangeException("length");
            this.FirstIndex = firstIndex;
            this.Length = length;
        }
    }
}
