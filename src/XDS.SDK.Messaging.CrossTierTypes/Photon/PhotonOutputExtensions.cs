using System;
using System.Collections.Generic;
using System.Text;

namespace XDS.SDK.Messaging.CrossTierTypes.Photon
{
    static class PhotonOutputExtensions
    {
        public static byte[] SerializeCore(this IPhotonOutput o)
        {
            byte[] serialized = PocoSerializer.Begin()
                .Append(o.HashTx)
                .Append((ushort)o.Index)
                .Append(o.BlockHeight)
                .Append(o.Satoshis)
                .Append((byte)o.UtxoType)
                .Append(o.SpendingTx)
                .Append(o.SpendingN)
                .Append(o.SpendingHeight)
                .Finish();
            return serialized;
        }

        public static PhotonOutput DeserializePhotonOutput(this byte[] photonOutput)
        {
            var output = new PhotonOutput();

            var ser = PocoSerializer.GetDeserializer(photonOutput);

            output.HashTx= ser.MakeByteArray(0);
            output.Index = ser.MakeUInt16(1);
            output.BlockHeight = ser.MakeInt32(2);

            output.Satoshis = ser.MakeInt64(3);
            output.UtxoType = (UtxoType)ser.MakeByte(4);

            output.SpendingTx = ser.MakeByteArray(5);
            output.SpendingN = ser.MakeUInt16(6);
            output.SpendingHeight = ser.MakeInt32(7);


            return output;
        }
    }
}
