using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the library's validator: it keeps every argument the page handed it, hands back the verdict it
/// was primed with, and can be made to throw or to pause in the middle.
/// </summary>
/// <remarks>
/// <para>
/// Only the path overload is implemented — the one the validate page uses. The other two throw, so a page that
/// started calling one of them fails loudly in a test rather than quietly doing nothing.
/// </para>
/// <para>
/// <b>Throwing is as important as answering here.</b> The library's contract splits down the middle: nothing a
/// licence can be makes it throw, but a key file that is not a key does, and so does a missing file. Those are
/// the cases the page has to turn into a notification rather than a verdict, and <see cref="Fail"/> is how a
/// test asks for one.
/// </para>
/// </remarks>
internal sealed class FakeLicenseValidator : ILicenseValidator
{
    /// <summary>Gets or sets the verdict the next call hands back.</summary>
    internal LicenseValidationResult Result { get; set; } = LicenseSamples.Valid();

    /// <summary>Gets or sets a failure to raise instead of reaching a verdict.</summary>
    internal Exception? Fail { get; set; }

    /// <summary>
    /// Gets or sets work to run inside the call, after its arguments have been recorded — the seam a test uses
    /// to look at the page while the check is still running.
    /// </summary>
    internal Func<Task>? DuringValidate { get; set; }

    /// <summary>Gets how many checks were asked for.</summary>
    internal int Validations { get; private set; }

    /// <summary>Gets the licence the last call was told to check.</summary>
    internal string? LicensePath { get; private set; }

    /// <summary>Gets the key the last call was told to check it against.</summary>
    internal string? PublicKeyPath { get; private set; }

    /// <summary>Gets the product the last call was told to expect, if any.</summary>
    internal string? ProductId { get; private set; }

    /// <summary>Gets the device the last call was told to expect, if any.</summary>
    internal string? DeviceId { get; private set; }

    /// <inheritdoc/>
    public async Task<LicenseValidationResult> ValidateAsync(
        string licensePath,
        string publicKeyPath,
        string? productId = null,
        string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        Validations++;
        LicensePath = licensePath;
        PublicKeyPath = publicKeyPath;
        ProductId = productId;
        DeviceId = deviceId;

        if (DuringValidate is not null)
        {
            await DuringValidate().ConfigureAwait(false);
        }

        if (Fail is not null)
        {
            throw Fail;
        }

        return Result;
    }

    /// <inheritdoc/>
    public LicenseValidationResult Validate(
        LicenseEnvelope envelope,
        LicensingKey publicKey,
        string? productId = null,
        string? deviceId = null)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<LicenseValidationResult> ValidateAsync(
        Stream licenseInput,
        Stream publicKeyInput,
        string? productId = null,
        string? deviceId = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
