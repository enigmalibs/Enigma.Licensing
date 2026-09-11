using System;
using System.Globalization;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The read-only card's contents: the words an absent field gets, and the shape of a formatted instant.
/// </summary>
/// <remarks>
/// The instants are asserted by <b>parsing them back</b> rather than by comparing them to a literal. They are
/// rendered in local time, so a literal would pin the test to whichever time zone the machine running it is in
/// — a test that passes here and fails in CI is worse than no test. Parsing back proves the two things that
/// matter: the format is the documented one, and no information was lost on the way through it.
/// </remarks>
public sealed class LicenseSummaryTests
{
    private const string InstantFormat = "yyyy-MM-dd HH:mm zzz";

    [Fact]
    public void Constructor_RejectsANullLicence()
        => Assert.Throws<ArgumentNullException>(() => new LicenseSummary(null!));

    [Fact]
    public void TheIdentifierAndProduct_AreShownVerbatim()
    {
        LicenseSummary summary = new(LicenseSamples.Licence(id: "01JYQ", productId: "Acme Suite 2.*"));

        Assert.Equal("01JYQ", summary.Id);
        Assert.Equal("Acme Suite 2.*", summary.ProductId);
    }

    /// <param name="owner">What the licence says about its owner.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ALicenceWithNoOwner_SaysSo_RatherThanShowingAnEmptyRow(string? owner)
    {
        LicenseSummary summary = new(LicenseSamples.Licence(owner: owner));

        Assert.Equal("Not stated", summary.Owner);
    }

    [Fact]
    public void AnOwner_IsShownAsGiven()
        => Assert.Equal("Contoso Ltd", new LicenseSummary(LicenseSamples.Licence(owner: "Contoso Ltd")).Owner);

    /// <summary>
    /// A licence with no device runs anywhere, which is a fact about it rather than a missing value — so it says
    /// so in those words.
    /// </summary>
    [Fact]
    public void ALicenceLockedToNothing_ReadsAsAnyMachine()
        => Assert.Equal("Any machine", new LicenseSummary(LicenseSamples.Licence(deviceId: null)).DeviceId);

    [Fact]
    public void ALockedLicence_ShowsItsDevice()
        => Assert.Equal("abc123", new LicenseSummary(LicenseSamples.Licence(deviceId: "abc123")).DeviceId);

    [Fact]
    public void AnOpenEndedWindow_ReadsAsImmediatelyAndNever()
    {
        LicenseSummary summary = new(LicenseSamples.Licence(validFrom: null, validUntil: null));

        Assert.Equal("Immediately", summary.ValidFrom);
        Assert.Equal("Never expires", summary.ValidUntil);
    }

    [Fact]
    public void TheIssueInstant_RoundTripsThroughTheDocumentedFormat()
    {
        LicenseSummary summary = new(LicenseSamples.Licence());

        Assert.Equal(LicenseSamples.Issued, Parse(summary.CreatedAt));
    }

    [Fact]
    public void BothEndsOfAWindow_RoundTripThroughTheDocumentedFormat()
    {
        DateTimeOffset from = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset until = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        LicenseSummary summary = new(LicenseSamples.Licence(validFrom: from, validUntil: until));

        Assert.Equal(from, Parse(summary.ValidFrom));
        Assert.Equal(until, Parse(summary.ValidUntil));
    }

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.ParseExact(value, InstantFormat, CultureInfo.InvariantCulture);
}
