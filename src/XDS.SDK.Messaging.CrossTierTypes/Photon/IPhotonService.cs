using System;

namespace XDS.SDK.Messaging.CrossTierTypes.Photon
{
    /// <summary>
    /// Photon is a service similar to Neutrino, but lighter. While neutrinos carry a tiny mass leading to the
    /// phenomenon of neutrino oscillations, photons are strictly zero mass particles.
    /// </summary>
    public interface IPhotonService
    {
        (long balance, int height, byte[] hashBlock, PhotonError photonError) GetPhotonBalance(string address, PhotonFlags photonFlags);
        (long balance, int height, byte[] hashBlock, IPhotonOutput[] outputs, PhotonError photonError)  GetPhotonOutputs(string address, PhotonFlags photonFlags);
    }

    public interface IPhotonOutput
    {
        int BlockHeight { get; set; }
        int Index { get; set; }
        byte[] HashTx { get; set; }
        long Satoshis { get; set; }
        UtxoType UtxoType { get; set; }
        byte[] SpendingTx { get; set; }

        /// <summary>
        /// Value is only valid if SpendingTx is not null.
        /// </summary>
        int SpendingN { get; set; }

        /// <summary>
        /// Value is only valid if SpendingTx is not null.
        /// </summary>
        int SpendingHeight { get; set; }
    }

    [Flags]
    public enum PhotonFlags : int
    {
        None = 0,
        Confirmed = 2,
        Spendable = 4,
        Staking = 8,
        IncludeSpentOutputs = 16
    }

    public enum PhotonError
    {
        NotSet = 0,
        /// <summary>
        /// The operation was successful and the result is valid.
        /// </summary>
        Success = 1,

        /// <summary>
        /// The supplied bech32 string did not match any known address.
        /// </summary>
        UnknownAddress = 2,

        /// <summary>
        /// The service is not yet fully initialized (can retry later).
        /// </summary>
        ServiceInitializing = 4,

        /// <summary>
        /// The arguments were incomplete or not valid for the given context.
        /// </summary>
        InvalidArguments = 8,

        NotImplemented = 16,
    }

    public enum UtxoType : byte
    {
        NotSet = 0,
        Mined = 1,
        Staked = 2,
        Received = 3
    }
}
