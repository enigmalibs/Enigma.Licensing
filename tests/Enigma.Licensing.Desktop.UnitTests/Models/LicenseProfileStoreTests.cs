using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Licensing.Desktop.Models;

namespace Enigma.Licensing.Desktop.UnitTests.Models;

/// <summary>
/// The profile file: what it holds, what it looks like on disk, and every way a file the user picked can turn
/// out not to be one.
/// </summary>
/// <remarks>
/// The contrast with <c>AppSettingsStoreTests</c> is the point of this suite. The preference store swallows
/// every failure because nobody asked for that file; this one reports them, because the user chose this file
/// from a dialog and is waiting for the form to fill.
/// </remarks>
public sealed class LicenseProfileStoreTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _directory =
        Directory.CreateTempSubdirectory("enigma-licensing-profiles-").FullName;

    private readonly LicenseProfileStore _store = new();

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task AProfile_SurvivesTheRoundTrip()
    {
        LicenseProfile profile = new()
        {
            ProductId = "Acme Suite 2.*",
            Owner = "Contoso Ltd",
            DeviceId = "9E3F0A",
            ValidFrom = new DateOnly(2026, 1, 1),
            ValidUntil = new DateOnly(2026, 12, 31),
            PrivateKeyPath = "/keys/acme.private.key.json",
        };

        string path = Path.Combine(_directory, "acme.profile.json");
        await _store.SaveAsync(profile, path, Token);

        LicenseProfile loaded = await _store.LoadAsync(path, Token);

        Assert.Equal(profile.ProductId, loaded.ProductId);
        Assert.Equal(profile.Owner, loaded.Owner);
        Assert.Equal(profile.DeviceId, loaded.DeviceId);
        Assert.Equal(profile.ValidFrom, loaded.ValidFrom);
        Assert.Equal(profile.ValidUntil, loaded.ValidUntil);
        Assert.Equal(profile.PrivateKeyPath, loaded.PrivateKeyPath);
        Assert.Equal(LicenseProfile.CurrentFormatVersion, loaded.FormatVersion);
    }

    /// <summary>
    /// The file is small enough that somebody will open it in an editor, so it is written to be read: camel
    /// case, indented, a date as a date, and nothing at all for a field that was left blank.
    /// </summary>
    [Fact]
    public async Task TheFile_IsWrittenToBeRead()
    {
        string path = Path.Combine(_directory, "acme.profile.json");

        await _store.SaveAsync(
            new LicenseProfile { ProductId = "Acme", ValidUntil = new DateOnly(2026, 12, 31) },
            path,
            Token);

        string json = await File.ReadAllTextAsync(path, Token);

        Assert.Contains("\"productId\": \"Acme\"", json, StringComparison.Ordinal);
        Assert.Contains("\"validUntil\": \"2026-12-31\"", json, StringComparison.Ordinal);
        Assert.Contains("\"formatVersion\": 1", json, StringComparison.Ordinal);

        // Left blank means absent, not null: an unused field should not be in the file at all.
        Assert.DoesNotContain("\"owner\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"validFrom\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// The invariant the whole type exists to keep: there is no password field, so there is nothing for a
    /// profile to leak even if one is left on a shared drive.
    /// </summary>
    [Fact]
    public async Task TheFile_HasNowhereToPutAPassword()
    {
        string path = Path.Combine(_directory, "acme.profile.json");

        await _store.SaveAsync(new LicenseProfile { ProductId = "Acme" }, path, Token);

        string json = await File.ReadAllTextAsync(path, Token);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(LicenseProfile).GetProperty("Password"));
    }

    [Fact]
    public async Task Saving_CreatesTheParentDirectory()
    {
        string path = Path.Combine(_directory, "nested", "deeper", "acme.profile.json");

        await _store.SaveAsync(new LicenseProfile { ProductId = "Acme" }, path, Token);

        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// A profile loaded from an older file and saved again is written as the current version — what is being
    /// saved is the form as it stands, not the file it came from.
    /// </summary>
    [Fact]
    public async Task Saving_StampsTheCurrentVersion()
    {
        string path = Path.Combine(_directory, "acme.profile.json");

        await _store.SaveAsync(new LicenseProfile { FormatVersion = 0, ProductId = "Acme" }, path, Token);

        Assert.Equal(LicenseProfile.CurrentFormatVersion, (await _store.LoadAsync(path, Token)).FormatVersion);
    }

    /// <summary>
    /// A file with no <c>formatVersion</c> reads as the current one: that is the only shape there has ever
    /// been, and refusing it would mean refusing a hand-written profile for a field the writer never knew about.
    /// </summary>
    [Fact]
    public async Task AFileWithNoVersion_ReadsAsTheCurrentOne()
    {
        string path = await WriteAsync("{ \"productId\": \"Acme\" }");

        LicenseProfile loaded = await _store.LoadAsync(path, Token);

        Assert.Equal(LicenseProfile.CurrentFormatVersion, loaded.FormatVersion);
        Assert.Equal("Acme", loaded.ProductId);
    }

    [Fact]
    public async Task AFileWrittenByALaterVersion_IsRefused()
    {
        string path = await WriteAsync("{ \"formatVersion\": 99, \"productId\": \"Acme\" }");

        ArgumentException failure =
            await Assert.ThrowsAsync<ArgumentException>(() => _store.LoadAsync(path, Token));

        Assert.Contains("99", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every way a chosen file can fail to be a profile. All of them are reported, because the user is
    /// looking at a form waiting to be filled.
    /// </summary>
    /// <param name="content">What the file holds.</param>
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("{ \"productId\": ")]
    [InlineData("[ \"a profile is not an array\" ]")]
    [InlineData("{ \"validUntil\": \"the end of time\" }")]
    public async Task AFileThatIsNotAProfile_IsReported(string content)
    {
        string path = await WriteAsync(content);

        await Assert.ThrowsAsync<ArgumentException>(() => _store.LoadAsync(path, Token));
    }

    /// <summary>
    /// <b>A profile written by version 1.0 still loads.</b> Those files carry <c>features</c> and
    /// <c>claims</c>, two fields the form no longer has, and the loader skips an unmapped member rather
    /// than refusing the document — which is why <see cref="LicenseProfile.CurrentFormatVersion"/> did not
    /// move when they went. Everything else in the file fills the form as it always did.
    /// </summary>
    [Fact]
    public async Task AProfileFrom10_LoadsWithItsFeaturesAndClaimsIgnored()
    {
        string path = await WriteAsync(
            "{ \"formatVersion\": 1, \"productId\": \"Acme Suite\", \"owner\": \"Contoso Ltd\", " +
            "\"validUntil\": \"2026-12-31\", \"features\": [\"pro\", \"export\"], " +
            "\"claims\": { \"seats\": \"25\" }, \"privateKeyPath\": \"/keys/acme.private.key.json\" }");

        LicenseProfile loaded = await _store.LoadAsync(path, Token);

        Assert.Equal("Acme Suite", loaded.ProductId);
        Assert.Equal("Contoso Ltd", loaded.Owner);
        Assert.Equal(new DateOnly(2026, 12, 31), loaded.ValidUntil);
        Assert.Equal("/keys/acme.private.key.json", loaded.PrivateKeyPath);
    }

    /// <summary>A missing file is an I/O failure, not a malformed profile — and it propagates untouched.</summary>
    [Fact]
    public async Task AMissingFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _store.LoadAsync(Path.Combine(_directory, "absent.profile.json"), Token));
    }

    [Fact]
    public async Task Loading_RejectsAnEmptyPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.LoadAsync("  ", Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.LoadAsync(null!, Token));
    }

    [Fact]
    public async Task Saving_RejectsMissingArguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _store.SaveAsync(null!, Path.Combine(_directory, "x.json"), Token));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.SaveAsync(new LicenseProfile(), "  ", Token));
    }

    private async Task<string> WriteAsync(string content)
    {
        string path = Path.Combine(_directory, "candidate.profile.json");
        await File.WriteAllTextAsync(path, content, Token);

        return path;
    }
}
