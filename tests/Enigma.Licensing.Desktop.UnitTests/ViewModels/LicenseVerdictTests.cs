using System;
using System.Collections.Generic;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The verdict mapping: one row per outcome the library can reach, plus the row for one it cannot yet.
/// </summary>
/// <remarks>
/// This is a table, and a table's failure mode is a row that is quietly wrong — an expired licence painted red
/// as though it were forged, or a signature failure explained as though the customer simply needed a new
/// licence. The severities are asserted member by member for that reason, not for coverage.
/// </remarks>
public sealed class LicenseVerdictTests
{
    [Fact]
    public void For_RejectsANullResult()
        => Assert.Throws<ArgumentNullException>(() => LicenseVerdict.For(null!));

    [Fact]
    public void AValidLicence_IsASuccess()
    {
        LicenseVerdict verdict = LicenseVerdict.For(LicenseSamples.Valid());

        Assert.Equal(LicenseVerdictSeverity.Success, verdict.Severity);
        Assert.Equal("Valid", verdict.Headline);
        Assert.True(verdict.IsSuccess);
        Assert.False(verdict.IsWarning);
        Assert.False(verdict.IsError);
    }

    /// <summary>
    /// A valid result carries no message, so there is nothing to show beneath the explanation — and the card
    /// must not leave an empty line where it would go.
    /// </summary>
    [Fact]
    public void AValidLicence_HasNoDetailToShow()
    {
        LicenseVerdict verdict = LicenseVerdict.For(LicenseSamples.Valid());

        Assert.Null(verdict.Detail);
        Assert.False(verdict.HasDetail);
    }

    /// <summary>
    /// The split that is the whole point of having three severities: a document that cannot be trusted is an
    /// error, and a genuine licence that does not apply here is a warning.
    /// </summary>
    /// <param name="failure">The failure the library reported.</param>
    /// <param name="severity">How it should be painted.</param>
    [Theory]
    [InlineData(LicenseFailure.InvalidFormat, LicenseVerdictSeverity.Error)]
    [InlineData(LicenseFailure.UnknownAlgorithm, LicenseVerdictSeverity.Error)]
    [InlineData(LicenseFailure.InvalidSignature, LicenseVerdictSeverity.Error)]
    [InlineData(LicenseFailure.NotYetValid, LicenseVerdictSeverity.Warning)]
    [InlineData(LicenseFailure.Expired, LicenseVerdictSeverity.Warning)]
    [InlineData(LicenseFailure.ProductMismatch, LicenseVerdictSeverity.Warning)]
    [InlineData(LicenseFailure.DeviceMismatch, LicenseVerdictSeverity.Warning)]
    public void EachFailure_IsPaintedByWhetherTheDocumentCanBeTrusted(
        LicenseFailure failure,
        LicenseVerdictSeverity severity)
    {
        LicenseVerdict verdict = LicenseVerdict.For(LicenseSamples.Invalid(failure));

        Assert.Equal(severity, verdict.Severity);
        Assert.False(verdict.IsSuccess);
    }

    /// <param name="failure">The failure the library reported.</param>
    [Theory]
    [InlineData(LicenseFailure.InvalidFormat)]
    [InlineData(LicenseFailure.UnknownAlgorithm)]
    [InlineData(LicenseFailure.InvalidSignature)]
    [InlineData(LicenseFailure.NotYetValid)]
    [InlineData(LicenseFailure.Expired)]
    [InlineData(LicenseFailure.ProductMismatch)]
    [InlineData(LicenseFailure.DeviceMismatch)]
    public void EachFailure_SaysSomething_AndSaysItDifferently(LicenseFailure failure)
    {
        LicenseVerdict verdict = LicenseVerdict.For(LicenseSamples.Invalid(failure));

        Assert.False(string.IsNullOrWhiteSpace(verdict.Headline));
        Assert.False(string.IsNullOrWhiteSpace(verdict.Explanation));
        Assert.NotEqual(verdict.Headline, verdict.Explanation);
    }

    /// <summary>
    /// No two failures may share a headline: the headline is what a user reads, and two different problems
    /// wearing one label is the same as not reporting one of them.
    /// </summary>
    [Fact]
    public void NoTwoFailures_ShareAHeadline()
    {
        LicenseFailure[] failures = Enum.GetValues<LicenseFailure>();
        HashSet<string> headlines = [];

        foreach (LicenseFailure failure in failures)
        {
            Assert.True(headlines.Add(LicenseVerdict.For(LicenseSamples.Invalid(failure)).Headline));
        }

        Assert.Equal(failures.Length, headlines.Count);
    }

    /// <summary>The library's own message is quoted rather than replaced — it carries the specifics.</summary>
    [Fact]
    public void TheLibrarysMessage_IsCarriedThrough()
    {
        LicenseValidationResult result =
            LicenseSamples.Invalid(LicenseFailure.Expired, "The license expired on 2026-03-03T00:00:00.0000000+00:00.");

        LicenseVerdict verdict = LicenseVerdict.For(result);

        Assert.Equal(result.Message, verdict.Detail);
        Assert.True(verdict.HasDetail);
    }

    /// <param name="message">A message that says nothing.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyMessage_IsNotShown(string? message)
    {
        LicenseVerdict verdict = LicenseVerdict.For(LicenseSamples.Invalid(LicenseFailure.Expired, message));

        Assert.False(verdict.HasDetail);
    }

    /// <summary>
    /// A failure member added by a later version of the library reaches a page built against this one. It has to
    /// come out as a rejection with the library's own message, not as an exception on a page a user is looking
    /// at.
    /// </summary>
    [Fact]
    public void AFailureThisVersionDoesNotKnow_IsStillReported()
    {
        LicenseValidationResult result =
            LicenseSamples.Invalid((LicenseFailure)9999, "Something this build has never heard of.");

        LicenseVerdict verdict = LicenseVerdict.For(result);

        Assert.Equal(LicenseVerdictSeverity.Error, verdict.Severity);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Headline));
        Assert.Equal("Something this build has never heard of.", verdict.Detail);
    }

    /// <summary>
    /// Exactly one of the three flags is true for any verdict — they drive three style classes on one border,
    /// and two at once would paint it twice.
    /// </summary>
    [Fact]
    public void ExactlyOneSeverityFlag_IsEverTrue()
    {
        List<LicenseVerdict> verdicts = [LicenseVerdict.For(LicenseSamples.Valid())];

        foreach (LicenseFailure failure in Enum.GetValues<LicenseFailure>())
        {
            verdicts.Add(LicenseVerdict.For(LicenseSamples.Invalid(failure)));
        }

        foreach (LicenseVerdict verdict in verdicts)
        {
            int painted = (verdict.IsSuccess ? 1 : 0) + (verdict.IsWarning ? 1 : 0) + (verdict.IsError ? 1 : 0);

            Assert.Equal(1, painted);
        }
    }
}
