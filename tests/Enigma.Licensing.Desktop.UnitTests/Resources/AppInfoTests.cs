using System;
using Enigma.Licensing.Desktop.Resources;

namespace Enigma.Licensing.Desktop.UnitTests.Resources;

/// <summary>
/// What the about section and the About dialog report. The running values are read from the assemblies, so
/// those assertions are about the shape — a version, not build metadata, and never an empty label — while
/// <see cref="AppInfo.ParseVersion"/>, the pure function behind them, is pinned case by case.
/// </summary>
public sealed class AppInfoTests
{
    [Fact]
    public void AppVersion_IsAVersion() => AssertVersion(AppInfo.AppVersion);

    [Fact]
    public void LibraryVersion_IsAVersion() => AssertVersion(AppInfo.LibraryVersion);

    /// <summary>
    /// The SDK appends <c>+&lt;commit&gt;</c> to the informational version whenever source-link metadata is
    /// present. A version stays a version: the revision has a line of its own, <see cref="AppInfo.BuildSha"/>,
    /// shown quieter in the About dialog.
    /// </summary>
    [Fact]
    public void TheVersions_CarryNoBuildMetadata()
    {
        Assert.DoesNotContain('+', AppInfo.AppVersion);
        Assert.DoesNotContain('+', AppInfo.LibraryVersion);
    }

    [Fact]
    public void ProjectUrl_IsAnAbsoluteHttpsAddress()
    {
        Assert.True(Uri.TryCreate(AppInfo.ProjectUrl, UriKind.Absolute, out Uri? uri));
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
    }

    [Fact]
    public void Title_IsSet() => Assert.False(string.IsNullOrWhiteSpace(AppInfo.Title));

    /// <summary>
    /// <c>Directory.Build.props</c> stamps the copyright on every assembly; the About dialog shows it as stamped.
    /// </summary>
    [Fact]
    public void Copyright_IsTheStampedNotice() => Assert.StartsWith("Copyright ©", AppInfo.Copyright, StringComparison.Ordinal);

    /// <summary>When the build records one, the revision is short — never the full forty characters.</summary>
    [Fact]
    public void BuildSha_IsAbsentOrShort() => Assert.True(AppInfo.BuildSha is null || AppInfo.BuildSha.Length is > 0 and <= 7);

    [Theory]
    [InlineData("2.1.0+4f2c9e1d0b8a7c6e5f4d3c2b1a0f9e8d7c6b5a49", "2.1.0", "4f2c9e1")]
    [InlineData("2.1.0+abc", "2.1.0", "abc")]
    [InlineData("2.1.0-rc.1+4f2c9e1d0b", "2.1.0-rc.1", "4f2c9e1")]
    [InlineData("2.1.0", "2.1.0", null)]
    [InlineData("2.1.0+", "2.1.0", null)]
    [InlineData("  2.1.0+4f2c9e1  ", "2.1.0", "4f2c9e1")]
    public void ParseVersion_SplitsTheVersionFromTheRevision(string informational, string version, string? buildSha)
    {
        (string parsedVersion, string? parsedSha) = AppInfo.ParseVersion(informational, "9.9.9");

        Assert.Equal(version, parsedVersion);
        Assert.Equal(buildSha, parsedSha);
    }

    /// <summary>Nothing usable before the <c>+</c>: the assembly version stands in, and a build line is not invented.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+4f2c9e1")]
    public void ParseVersion_FallsBack_WhenTheInformationalVersionSaysNothing(string? informational)
    {
        Assert.Equal(("2.1.0", (string?)null), AppInfo.ParseVersion(informational, "2.1.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseVersion_ReportsTheUnknownVersion_WhenThereIsNothingAtAll(string? fallback)
    {
        Assert.Equal((AppInfo.UnknownVersion, (string?)null), AppInfo.ParseVersion(null, fallback));
    }

    private static void AssertVersion(string value)
    {
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual(AppInfo.UnknownVersion, value);

        // The leading component is always the major version, whatever suffix a pre-release adds.
        Assert.True(int.TryParse(value.Split('.', '-')[0], out int major));
        Assert.True(major >= 0);
    }
}
