namespace Enigma.Licensing.Keys;

/// <summary>
/// Which half of a key pair a key file holds.
/// </summary>
/// <remarks>
/// The distinction is load-bearing rather than descriptive: a private key signs and must be kept
/// secret — it is the half that may be password-encrypted — while a public key only verifies and is
/// meant to be shipped inside an application. The two halves of a pair share a
/// <see cref="LicensingKey.KeyId"/>, so the kind is what tells them apart.
/// </remarks>
public enum KeyKind
{
    /// <summary>The verification half, shipped with the application. Wire value <c>"public"</c>.</summary>
    Public,

    /// <summary>The signing half, kept by the issuer. Wire value <c>"private"</c>.</summary>
    Private,
}
