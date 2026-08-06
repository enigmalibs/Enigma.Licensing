namespace Enigma.Licensing.Keys;

/// <summary>
/// A freshly generated key pair: the public half to ship with an application, and the private half the
/// issuer keeps and signs with.
/// </summary>
/// <param name="PublicKey">The verifying half. Never password-encrypted when written to a file.</param>
/// <param name="PrivateKey">The signing half. Optionally password-encrypted when written to a file.</param>
/// <remarks>
/// The two halves are returned together because that is the only moment they are ever together: after
/// <see cref="IKeyService.SaveKeyPairAsync"/> has written them they live in separate files with
/// separate lifetimes, and only their shared <see cref="LicensingKey.KeyId"/> still says they belong
/// to each other.
/// </remarks>
public sealed record LicensingKeyPair(LicensingKey PublicKey, LicensingKey PrivateKey);
