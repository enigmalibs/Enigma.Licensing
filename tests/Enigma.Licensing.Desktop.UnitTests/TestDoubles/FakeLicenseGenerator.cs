using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.TestDoubles;

/// <summary>
/// Stands in for the library's generator: it keeps every argument the page handed it, hands back an envelope
/// it was configured with, and can be made to fail or to pause in the middle.
/// </summary>
/// <remarks>
/// <para>
/// Only the path overload is implemented — the one the generate page uses. The rest throw, so a page that
/// started calling another fails loudly in a test rather than quietly doing nothing.
/// </para>
/// <para>
/// The envelope's payload is a real, complete licence document rather than opaque bytes, because reading the
/// licence back out of it is something the page actually does — through
/// <see cref="LicenseEnvelope.ReadLicense"/>, which deserializes the whole record and so needs every required
/// field present, not just the identifier. <see cref="PayloadJson"/> is settable so a test can hand over a
/// payload that says nothing useful and check that the issue is still reported as a success — the licence has
/// been written by then, and not being able to name it is not a failure to write it.
/// </para>
/// </remarks>
internal sealed class FakeLicenseGenerator : ILicenseGenerator
{
    /// <summary>Gets or sets the identifier the returned envelope's payload carries.</summary>
    internal string LicenseId { get; set; } = "01JYQ7ZK3MEXAMPLEEXAMPLE00";

    /// <summary>
    /// Gets or sets the payload the returned envelope carries. Defaults to a complete licence naming
    /// <see cref="LicenseId"/>.
    /// </summary>
    internal string? PayloadJson { get; set; }

    /// <summary>Gets or sets the fingerprint the returned envelope is stamped with.</summary>
    internal string? KeyId { get; set; } = "378A86040C38330F";

    /// <summary>Gets or sets a failure to raise instead of signing.</summary>
    internal Exception? Fail { get; set; }

    /// <summary>
    /// Gets or sets work to run inside the call, after its arguments have been recorded — the seam a test uses
    /// to look at the page while the operation is still running.
    /// </summary>
    internal Func<Task>? DuringGenerate { get; set; }

    /// <summary>Gets how many licences were asked for.</summary>
    internal int Generations { get; private set; }

    /// <summary>Gets the request the last call was handed.</summary>
    internal LicenseRequest? Request { get; private set; }

    /// <summary>Gets the key file the last call was told to sign with.</summary>
    internal string? PrivateKeyPath { get; private set; }

    /// <summary>Gets where the last call was told to write the licence.</summary>
    internal string? OutputPath { get; private set; }

    /// <summary>
    /// Gets the password array the last call was handed — the array itself, so a test can watch what the
    /// caller does to it afterwards.
    /// </summary>
    internal char[]? Password { get; private set; }

    /// <summary>Gets what the password said while the call could still read it.</summary>
    internal string? PasswordText { get; private set; }

    /// <inheritdoc/>
    public async Task<LicenseEnvelope> GenerateAndSaveAsync(
        LicenseRequest request,
        string privateKeyPath,
        char[]? password,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        Generations++;
        Request = request;
        PrivateKeyPath = privateKeyPath;
        OutputPath = outputPath;
        Password = password;
        PasswordText = password is null ? null : new string(password);

        if (DuringGenerate is not null)
        {
            await DuringGenerate().ConfigureAwait(false);
        }

        if (Fail is not null)
        {
            throw Fail;
        }

        return new LicenseEnvelope
        {
            Format = LicenseEnvelope.CurrentFormat,
            Algorithm = "ML-DSA-65",
            KeyId = KeyId,
            Payload = Encoding.UTF8.GetBytes(PayloadJson ?? DefaultPayloadJson(request)),
            Signature = [0x01, 0x02, 0x03],
        };
    }

    /// <summary>
    /// The three fields a <c>License</c> requires, in the format's camelCase shape, echoing back the product
    /// the request asked for. Fixed timestamp: nothing here should vary between runs.
    /// </summary>
    /// <param name="request">The request being signed.</param>
    /// <returns>The payload JSON.</returns>
    private string DefaultPayloadJson(LicenseRequest request) =>
        $"{{\"id\":\"{LicenseId}\",\"createdAt\":\"2026-08-07T09:00:00+00:00\"," +
        $"\"productId\":{JsonSerializer.Serialize(request.ProductId)}}}";

    /// <inheritdoc/>
    public LicenseEnvelope Generate(LicenseRequest request, LicensingKey privateKey)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<LicenseEnvelope> GenerateAndSaveAsync(
        LicenseRequest request,
        LicensingKey privateKey,
        Stream output,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
