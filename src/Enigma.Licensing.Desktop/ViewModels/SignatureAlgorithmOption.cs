using System;
using System.Collections.Generic;

namespace Enigma.Licensing.Desktop.ViewModels;

/// <summary>
/// One entry of the signing-algorithm picker: a <see cref="LicenseSignatureAlgorithm"/>, the words offering
/// it, and whether choosing it brings the RSA key-size picker with it.
/// </summary>
/// <remarks>
/// <para>
/// A wrapper rather than binding the enum directly, on the same grounds as
/// <see cref="ThemeOption"/>: <c>MLDsa65</c> is a member name, not a label, and the choice deserves a line
/// saying what it costs and what it buys.
/// </para>
/// <para>
/// <b>The labels repeat the format's wire identifiers, deliberately.</b> They are what a key file records in
/// its <c>algorithm</c> field and what an error message will quote, so a picker showing anything else would
/// leave the user translating. The library's own enum ↔ string table is internal to it; this is a
/// user-interface string that happens to agree with it, and the round-trip test that generates a real pair
/// per member is what keeps the two honest.
/// </para>
/// </remarks>
public sealed class SignatureAlgorithmOption
{
    private SignatureAlgorithmOption(LicenseSignatureAlgorithm algorithm, string label, string description, bool isRsa)
    {
        Algorithm = algorithm;
        Label = label;
        Description = description;
        IsRsa = isRsa;
    }

    /// <summary>
    /// Gets every scheme on offer — the three post-quantum parameter sets first, then the three RSA digests.
    /// </summary>
    /// <remarks>
    /// The order is the recommendation: a new pair generated today should be post-quantum unless the
    /// application that will check the licence cannot be given a verifier for one.
    /// </remarks>
    public static IReadOnlyList<SignatureAlgorithmOption> All { get; } =
    [
        new(LicenseSignatureAlgorithm.MLDsa44, "ML-DSA-44",
            "Post-quantum (FIPS 204), the smallest parameter set and the smallest signatures.", false),
        new(LicenseSignatureAlgorithm.MLDsa65, "ML-DSA-65",
            "Post-quantum (FIPS 204). The balanced parameter set, and the one to pick without a reason to pick another.", false),
        new(LicenseSignatureAlgorithm.MLDsa87, "ML-DSA-87",
            "Post-quantum (FIPS 204), the strongest parameter set and the largest keys and signatures.", false),
        new(LicenseSignatureAlgorithm.RsaSha256, "RSA-SHA256",
            "Classical RSA, PKCS#1 v1.5 over a SHA-256 digest. Choose RSA only when the application checking the licence cannot verify a post-quantum signature.", true),
        new(LicenseSignatureAlgorithm.RsaSha384, "RSA-SHA384",
            "Classical RSA, PKCS#1 v1.5 over a SHA-384 digest.", true),
        new(LicenseSignatureAlgorithm.RsaSha512, "RSA-SHA512",
            "Classical RSA, PKCS#1 v1.5 over a SHA-512 digest.", true),
    ];

    /// <summary>Gets the scheme a new pair is generated for when the user chooses nothing.</summary>
    public static SignatureAlgorithmOption Default { get; } = For(LicenseSignatureAlgorithm.MLDsa65);

    /// <summary>Gets the scheme this option selects.</summary>
    public LicenseSignatureAlgorithm Algorithm { get; }

    /// <summary>Gets the words shown in the picker — the format's own identifier for the scheme.</summary>
    public string Label { get; }

    /// <summary>Gets the line explaining what the scheme is, shown beneath the picker.</summary>
    public string Description { get; }

    /// <summary>
    /// Gets a value indicating whether this is one of the RSA schemes, and therefore whether a modulus size
    /// has to be chosen alongside it. The ML-DSA parameter sets fix their own key sizes.
    /// </summary>
    public bool IsRsa { get; }

    /// <summary>Returns the option for <paramref name="algorithm"/>.</summary>
    /// <param name="algorithm">The scheme to look up.</param>
    /// <returns>The matching option.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="algorithm"/> is not an offered scheme.</exception>
    public static SignatureAlgorithmOption For(LicenseSignatureAlgorithm algorithm)
    {
        foreach (SignatureAlgorithmOption option in All)
        {
            if (option.Algorithm == algorithm)
            {
                return option;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(algorithm));
    }

    /// <inheritdoc/>
    public override string ToString() => Label;
}
