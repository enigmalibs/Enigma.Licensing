using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Models;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The three pages, chained, against the <b>real</b> library: keys generated on the keys page sign a licence on
/// the generate page, and the validate page reaches a verdict about it.
/// </summary>
/// <remarks>
/// <para>
/// This is where the phase's acceptance criterion actually lives. Only the platform seams are doubles — the
/// dialogs, the overlay, the info bar and the clipboard — so what runs here is the whole path an issuer walks:
/// a real key pair on disk, a real <c>enigma-license/1</c> document, and a verdict from
/// <see cref="LicenseValidator"/> rather than from a fake. The two schemes in the headline theory are the two
/// families the format supports, so the page is shown to be indifferent to which one signed.
/// </para>
/// <para>
/// The tampering cases are the ones worth reading. A flipped payload byte, a substituted algorithm and a
/// swapped key all have to come out as the <i>same</i> verdict — the signature does not verify — while the
/// document card keeps showing what the file claims. That pairing is the diagnosis a support conversation needs:
/// a fingerprint that is not the one being checked against means the wrong key was picked, not that a customer
/// forged anything.
/// </para>
/// <para>
/// ML-DSA-44 unless a test is about RSA, and RSA at 2048 when it is: these tests are about the pages and the
/// documents, not about the arithmetic, and the keys page's own 4096-bit default would be paid for on every run.
/// </para>
/// </remarks>
public sealed class ValidateLicenseRoundTripTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _directory =
        Directory.CreateTempSubdirectory("enigma-licensing-verdicts-").FullName;

    private readonly IKeyService _keys = new KeyService();

    private readonly FakeFileDialogService _files = new();
    private readonly FakeBusyOverlayService _busy = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeClipboardService _clipboard = new();

    private readonly string _publicKeyPath;
    private readonly string _privateKeyPath;
    private readonly string _licensePath;

    /// <summary>Initializes a new instance of the <see cref="ValidateLicenseRoundTripTests"/> class.</summary>
    public ValidateLicenseRoundTripTests()
    {
        _publicKeyPath = Path.Combine(_directory, "acme.public.key.json");
        _privateKeyPath = Path.Combine(_directory, "acme.private.key.json");
        _licensePath = Path.Combine(_directory, "acme.license.json");
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// The whole chain, driven through the pages themselves: generate a pair on the keys page, sign a licence
    /// with it on the generate page, and check that licence on the validate page.
    /// </summary>
    /// <param name="algorithm">The scheme the pair is generated for.</param>
    /// <returns>A task that completes when the verdict has been asserted.</returns>
    [Theory]
    [InlineData(LicenseSignatureAlgorithm.MLDsa44)]
    [InlineData(LicenseSignatureAlgorithm.RsaSha256)]
    public async Task TheWholeChain_FromTheKeysPage_ThroughAnIssue_ToAVerdict(LicenseSignatureAlgorithm algorithm)
    {
        SignatureAlgorithmOption option = SignatureAlgorithmOption.For(algorithm);

        // The keys page, through its own dialogs.
        GenerateKeysViewModel keysPage = new(_keys, _files, _busy, _notifications, _clipboard)
        {
            SelectedAlgorithm = option,
            SelectedRsaKeySize = 2048,
        };

        _files.Answer(_publicKeyPath);
        await keysPage.BrowsePublicKeyPathCommand.ExecuteAsync(null);
        _files.Answer(_privateKeyPath);
        await keysPage.BrowsePrivateKeyPathCommand.ExecuteAsync(null);

        await keysPage.GenerateKeyPairCommand.ExecuteAsync(null);

        Assert.True(keysPage.HasResult);
        string fingerprint = Assert.IsType<string>(keysPage.LastKeyId);

        // The generate page, signing with the pair that page just wrote.
        GenerateLicenseViewModel generatePage = IssuePage();
        generatePage.ProductId = "Acme Suite 2.*";
        generatePage.Owner = "Contoso Ltd";
        generatePage.ValidUntilDate = DateTime.Today.AddDays(30);

        await generatePage.GenerateLicenseCommand.ExecuteAsync(null);

        string licenseId = Assert.IsType<string>(generatePage.LastLicenseId);

        // The validate page, checking it.
        ValidateLicenseViewModel validatePage = ValidatePage();
        validatePage.ExpectedProductId = "Acme Suite 2.7";

        await validatePage.ValidateLicenseCommand.ExecuteAsync(null);

        // Nothing anywhere in the chain reported a failure.
        Assert.All(_notifications.Notifications, notification => Assert.False(notification.IsError));

        LicenseVerdict verdict = Assert.IsType<LicenseVerdict>(validatePage.Verdict);
        Assert.Equal(LicenseVerdictSeverity.Success, verdict.Severity);
        Assert.Equal("Valid", verdict.Headline);

        // The document says what signed it, and it agrees with the pair the keys page produced.
        Assert.True(validatePage.HasDocumentDetails);
        Assert.Equal(option.Label, validatePage.DocumentAlgorithm);
        Assert.Equal(fingerprint, validatePage.DocumentKeyId);

        // And the licence says what was typed into the generate page.
        LicenseSummary summary = Assert.IsType<LicenseSummary>(validatePage.Summary);
        Assert.Equal(licenseId, summary.Id);
        Assert.Equal("Acme Suite 2.*", summary.ProductId);
        Assert.Equal("Contoso Ltd", summary.Owner);
        Assert.Equal("Any machine", summary.DeviceId);
    }

    /// <summary>
    /// An expired licence is amber, not red, and it is still readable — which is the whole difference between
    /// "your licence ran out on the 3rd" and "invalid licence".
    /// </summary>
    [Fact]
    public async Task AnExpiredLicence_IsAWarning_AndIsStillReadable()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page =>
        {
            page.ProductId = "Acme Suite";
            page.ValidUntilDate = DateTime.Today.AddDays(-1);
        });

        ValidateLicenseViewModel page = ValidatePage();
        page.ExpectedProductId = "Acme Suite";

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        LicenseVerdict verdict = Assert.IsType<LicenseVerdict>(page.Verdict);
        Assert.Equal(LicenseVerdictSeverity.Warning, verdict.Severity);
        Assert.Equal("Expired", verdict.Headline);
        Assert.True(verdict.HasDetail);

        LicenseSummary summary = Assert.IsType<LicenseSummary>(page.Summary);
        Assert.Equal("Acme Suite", summary.ProductId);
        Assert.NotEqual("Never expires", summary.ValidUntil);
    }

    [Fact]
    public async Task ALicenceThatHasNotStartedYet_IsAWarning()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page =>
        {
            page.ProductId = "Acme Suite";
            page.ValidFromDate = DateTime.Today.AddDays(1);
        });

        ValidateLicenseViewModel page = ValidatePage();

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("Not yet valid", Assert.IsType<LicenseVerdict>(page.Verdict).Headline);
        Assert.Equal(LicenseVerdictSeverity.Warning, page.Verdict!.Severity);
    }

    /// <summary>
    /// One byte changed inside the signed payload. The verdict is red and the licence is unreadable — nothing
    /// past the signature check runs — but the document's own fields are still shown, because they are what tells
    /// an issuer which key this was supposed to be checked with.
    /// </summary>
    [Fact]
    public async Task ATamperedPayload_IsASignatureFailure_AndTheDocumentIsStillShown()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite");

        LicenseEnvelope issued = await ReadLicenseAsync();
        byte[] payload = [.. issued.Payload];
        payload[^1] ^= 0xFF;

        await WriteLicenseAsync(issued with { Payload = payload });

        ValidateLicenseViewModel page = ValidatePage();

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        LicenseVerdict verdict = Assert.IsType<LicenseVerdict>(page.Verdict);
        Assert.Equal(LicenseVerdictSeverity.Error, verdict.Severity);
        Assert.Equal("The signature does not verify", verdict.Headline);

        Assert.False(page.HasSummary);
        Assert.True(page.HasDocumentDetails);
        Assert.Equal(issued.KeyId, page.DocumentKeyId);
    }

    /// <summary>
    /// The format's algorithm-substitution claim, from the application's side: the field that names the scheme is
    /// what <i>selects</i> the verifier, so rewriting it makes the signature fail rather than letting a weaker
    /// check through. The document card shows the substituted value, which is the point — it reports the file,
    /// not the key.
    /// </summary>
    [Fact]
    public async Task ASubstitutedAlgorithm_IsASignatureFailure()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite");

        LicenseEnvelope issued = await ReadLicenseAsync();
        Assert.Equal("ML-DSA-44", issued.Algorithm);

        await WriteLicenseAsync(issued with { Algorithm = "ML-DSA-87" });

        ValidateLicenseViewModel page = ValidatePage();

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("The signature does not verify", Assert.IsType<LicenseVerdict>(page.Verdict).Headline);
        Assert.Equal("ML-DSA-87", page.DocumentAlgorithm);
    }

    /// <summary>A scheme nothing here implements is its own verdict, not a signature failure.</summary>
    [Fact]
    public async Task AnAlgorithmNothingImplements_IsReportedAsUnknown()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite");

        await WriteLicenseAsync((await ReadLicenseAsync()) with { Algorithm = "ROT13" });

        ValidateLicenseViewModel page = ValidatePage();

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        LicenseVerdict verdict = Assert.IsType<LicenseVerdict>(page.Verdict);
        Assert.Equal("Unknown signature scheme", verdict.Headline);
        Assert.Equal(LicenseVerdictSeverity.Error, verdict.Severity);
        Assert.Equal("ROT13", page.DocumentAlgorithm);
        Assert.False(page.HasSummary);
    }

    /// <summary>
    /// The most common real failure: a good licence checked against the wrong public key. The fingerprint on the
    /// document card is the first pair's, which is how the issuer sees that the key is what is wrong.
    /// </summary>
    [Fact]
    public async Task TheWrongKey_IsASignatureFailure_AndTheFingerprintSaysWhy()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite");

        string otherPublicKeyPath = Path.Combine(_directory, "other.public.key.json");
        LicensingKeyPair other = _keys.GenerateKeyPair(LicenseSignatureAlgorithm.MLDsa44);
        await _keys.SaveKeyPairAsync(
            other,
            otherPublicKeyPath,
            Path.Combine(_directory, "other.private.key.json"),
            password: null,
            Token);

        ValidateLicenseViewModel page = ValidatePage();
        page.PublicKeyPath = otherPublicKeyPath;

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("The signature does not verify", Assert.IsType<LicenseVerdict>(page.Verdict).Headline);
        Assert.NotEqual(other.PublicKey.KeyId, page.DocumentKeyId);

        LicensingKey signer = await _keys.LoadKeyAsync(_publicKeyPath, cancellationToken: Token);
        Assert.Equal(signer.KeyId, page.DocumentKeyId);
    }

    /// <summary>
    /// A genuine licence for another product. The verdict is amber and the card names the product the licence is
    /// actually for — the answer to the question, rather than a restatement of it.
    /// </summary>
    [Fact]
    public async Task AWrongProduct_IsAWarning_AndTheCardNamesTheRealProduct()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite 1.*");

        ValidateLicenseViewModel page = ValidatePage();
        page.ExpectedProductId = "Acme Suite 2.0";

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal("Wrong product", Assert.IsType<LicenseVerdict>(page.Verdict).Headline);
        Assert.Equal("Acme Suite 1.*", Assert.IsType<LicenseSummary>(page.Summary).ProductId);
    }

    /// <summary>
    /// The wildcard is honoured on the licence's side only, which is what makes one licence cover a range of
    /// versions — and what the page's hint says.
    /// </summary>
    [Fact]
    public async Task AWildcardLicence_CoversTheVersionsItSpans()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite 2.*");

        ValidateLicenseViewModel page = ValidatePage();
        page.ExpectedProductId = "Acme Suite 2.17";

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(LicenseVerdictSeverity.Success, Assert.IsType<LicenseVerdict>(page.Verdict).Severity);
    }

    /// <summary>
    /// The device lock, reproduced from both sides with the button that exists for it.
    /// </summary>
    [Fact]
    public async Task ADeviceLockedLicence_PassesOnThisMachine_AndFailsForAnother()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page =>
        {
            page.ProductId = "Acme Suite";
            page.UseThisMachineCommand.Execute(null);
        });

        ValidateLicenseViewModel here = ValidatePage();
        here.UseThisMachineCommand.Execute(null);

        await here.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(LicenseVerdictSeverity.Success, Assert.IsType<LicenseVerdict>(here.Verdict).Severity);
        Assert.Equal(DeviceIdentifier.Generate(), Assert.IsType<LicenseSummary>(here.Summary).DeviceId);

        ValidateLicenseViewModel elsewhere = ValidatePage();
        elsewhere.ExpectedDeviceId = "a machine that is not this one";

        await elsewhere.ValidateLicenseCommand.ExecuteAsync(null);

        LicenseVerdict verdict = Assert.IsType<LicenseVerdict>(elsewhere.Verdict);
        Assert.Equal("Wrong device", verdict.Headline);
        Assert.Equal(LicenseVerdictSeverity.Warning, verdict.Severity);
    }

    /// <summary>
    /// The trap the page warns about in so many words: an empty device field <b>skips</b> the check, so a licence
    /// locked to another machine validates here. It is the library's documented behaviour, and an issuer has to
    /// be able to see it happen.
    /// </summary>
    [Fact]
    public async Task LeavingTheDeviceFieldEmpty_SkipsTheCheck_RatherThanFailingIt()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page =>
        {
            page.ProductId = "Acme Suite";
            page.DeviceId = "a machine that is not this one";
        });

        ValidateLicenseViewModel page = ValidatePage();

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.Equal(LicenseVerdictSeverity.Success, Assert.IsType<LicenseVerdict>(page.Verdict).Severity);
        Assert.Equal("a machine that is not this one", Assert.IsType<LicenseSummary>(page.Summary).DeviceId);
    }

    /// <summary>
    /// Checking with the private half is the mistake a hurried issuer makes. The library refuses it rather than
    /// verifying, and the page's job is to say so — as a notification, because it is not a verdict about the
    /// licence at all.
    /// </summary>
    [Fact]
    public async Task CheckingWithThePrivateHalf_IsReported_AndIsNotAVerdict()
    {
        await WriteKeyPairAsync();
        await IssueAsync(page => page.ProductId = "Acme Suite");

        ValidateLicenseViewModel page = ValidatePage();
        page.PublicKeyPath = _privateKeyPath;

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(_notifications.Notifications[^1].IsError);
        Assert.False(page.HasVerdict);
        Assert.False(page.IsBusy);
    }

    [Fact]
    public async Task AMissingLicenceFile_IsReported_AndIsNotAVerdict()
    {
        await WriteKeyPairAsync();

        ValidateLicenseViewModel page = ValidatePage();

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(_notifications.Notifications).IsError);
        Assert.False(page.HasVerdict);
    }

    /// <summary>
    /// A file that exists and is not a licence <i>is</i> a verdict — picking the key file twice is the obvious
    /// slip, and it must not look like a broken application.
    /// </summary>
    [Fact]
    public async Task AKeyFileChosenAsTheLicence_IsAnInvalidFormatVerdict()
    {
        await WriteKeyPairAsync();

        ValidateLicenseViewModel page = ValidatePage();
        page.LicensePath = _publicKeyPath;

        await page.ValidateLicenseCommand.ExecuteAsync(null);

        LicenseVerdict verdict = Assert.IsType<LicenseVerdict>(page.Verdict);
        Assert.Equal("Not a readable licence", verdict.Headline);
        Assert.Equal(LicenseVerdictSeverity.Error, verdict.Severity);
        Assert.False(page.HasSummary);
        Assert.False(page.HasDocumentDetails);
        Assert.Empty(_notifications.Notifications);
    }

    /// <summary>Generates a real pair and writes both halves.</summary>
    /// <param name="algorithm">The scheme to generate for.</param>
    /// <returns>A task that completes when both files exist.</returns>
    private Task WriteKeyPairAsync(LicenseSignatureAlgorithm algorithm = LicenseSignatureAlgorithm.MLDsa44) =>
        _keys.SaveKeyPairAsync(
            _keys.GenerateKeyPair(algorithm, rsaKeySizeBits: 2048),
            _publicKeyPath,
            _privateKeyPath,
            password: null,
            Token);

    /// <summary>Signs one licence through the real generate page.</summary>
    /// <param name="configure">Fills in the form.</param>
    /// <returns>A task that completes when the licence is on disk.</returns>
    private async Task IssueAsync(Action<GenerateLicenseViewModel> configure)
    {
        GenerateLicenseViewModel page = IssuePage();
        configure(page);

        await page.GenerateLicenseCommand.ExecuteAsync(null);

        Assert.True(File.Exists(_licensePath));
    }

    private GenerateLicenseViewModel IssuePage() =>
        new(new LicenseGenerator(), new LicenseProfileStore(), _files, _busy, _notifications, _clipboard)
        {
            PrivateKeyPath = _privateKeyPath,
            OutputPath = _licensePath,
        };

    private ValidateLicenseViewModel ValidatePage() =>
        new(new LicenseValidator(), _files, _notifications)
        {
            LicensePath = _licensePath,
            PublicKeyPath = _publicKeyPath,
        };

    private async Task<LicenseEnvelope> ReadLicenseAsync()
    {
        await using FileStream input = File.OpenRead(_licensePath);

        return await LicenseEnvelope.LoadAsync(input, Token);
    }

    private async Task WriteLicenseAsync(LicenseEnvelope envelope)
    {
        await using FileStream output = File.Create(_licensePath);

        await envelope.SaveAsync(output, Token);
    }
}
