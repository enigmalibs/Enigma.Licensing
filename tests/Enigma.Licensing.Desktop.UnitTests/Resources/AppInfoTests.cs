using System;
using Enigma.Licensing.Desktop.Resources;

namespace Enigma.Licensing.Desktop.UnitTests.Resources;

/// <summary>
/// What the about section reports. The versions are read from the running assemblies, so the assertions here
/// are about the shape — a version, not build metadata, and never an empty label.
/// </summary>
public sealed class AppInfoTests
{
    [Fact]
    public void AppVersion_IsAVersion() => AssertVersion(AppInfo.AppVersion);

    [Fact]
    public void LibraryVersion_IsAVersion() => AssertVersion(AppInfo.LibraryVersion);

    /// <summary>
    /// The SDK appends <c>+&lt;commit&gt;</c> to the informational version whenever source-link metadata is
    /// present, and a commit hash in an about box is noise a user cannot act on.
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

    private static void AssertVersion(string value)
    {
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual("unknown", value);

        // The leading component is always the major version, whatever suffix a pre-release adds.
        Assert.True(int.TryParse(value.Split('.', '-')[0], out int major));
        Assert.True(major >= 0);
    }
}
