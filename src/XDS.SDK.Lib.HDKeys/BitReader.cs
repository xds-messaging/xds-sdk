using System.Collections;
using System.Text;

namespace XDS.SDK.Lib.HDKeys
{
    class BitReader
    {
        BitArray array;
        public BitReader(byte[] data, int bitCount)
        {
            BitWriter writer = new BitWriter();
            writer.Write(data, bitCount);
            this.array = writer.ToBitArray();
        }

        public BitReader(BitArray array)
        {
            this.array = new BitArray(array.Length);
            for (int i = 0; i < array.Length; i++)
                this.array.Set(i, array.Get(i));
        }

        public bool Read()
        {
            var v = this.array.Get(this.Position);
            this.Position++;
            return v;
        }

        public int Position
        {
            get;
            set;
        }

        public uint ReadUInt(int bitCount)
        {
            uint value = 0;
            for (int i = 0; i < bitCount; i++)
            {
                var v = Read() ? 1U : 0U;
                value += (v << i);
            }
            return value;
        }

        public int Count
        {
            get
            {
                return this.array.Length;
            }
        }

        public BitArray ToBitArray()
        {
            BitArray result = new BitArray(this.array.Length);
            for (int i = 0; i < this.array.Length; i++)
                result.Set(i, this.array.Get(i));
            return result;
        }

        public BitWriter ToWriter()
        {
            var writer = new BitWriter();
            writer.Write(this.array);
            return writer;
        }

        public void Consume(int count)
        {
            this.Position += count;
        }

        public bool Same(BitReader b)
        {
            while (this.Position != this.Count && b.Position != b.Count)
            {
                var valuea = Read();
                var valueb = b.Read();
                if (valuea != valueb)
                    return false;
            }
            return true;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(this.array.Length);
            for (int i = 0; i < this.Count; i++)
            {
                if (i != 0 && i % 8 == 0)
                    builder.Append(' ');
                builder.Append(this.array.Get(i) ? "1" : "0");
            }
            return builder.ToString();
        }
    }
}