namespace XDS.SDK.Messaging.CrossTierTypes.Photon
{
    public class PhotonOutput : IPhotonOutput
    {
        public int BlockHeight { get; set; }
        public int Index { get; set; }
        public byte[] HashTx { get; set; }
        public long Satoshis { get; set; }
        public UtxoType UtxoType { get; set; }
        public byte[] SpendingTx { get; set; }
        public int SpendingN { get; set; }
        public int SpendingHeight { get; set; }
    }
}
