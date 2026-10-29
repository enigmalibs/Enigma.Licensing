using System;
using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// What the About dialog says. The view is the reference's markup and is checked in the running app; what it
/// binds to — and above all which lines it hides — is pinned here.
/// </summary>
public sealed class AboutViewModelTests
{
    [Fact]
    public void TheParameterlessConstructor_ReportsTheRunningBuild()
    {
        AboutViewModel viewModel = new();

        Assert.Equal(AppInfo.Title, viewModel.DisplayName);
        Assert.Equal(AppInfo.AppVersion, viewModel.Version);
        Assert.Equal(AppInfo.BuildSha, viewModel.BuildSha);
        Assert.Equal(AppInfo.Copyright, viewModel.Copyright);
    }

    [Fact]
    public void TheGivenValues_AreShownAsTheyAre()
    {
        AboutViewModel viewModel = new("2.1.0", "4f2c9e1", "Copyright © 2026 Someone");

        Assert.Equal("Enigma Licensing", viewModel.DisplayName);
        Assert.Equal("2.1.0", viewModel.Version);
        Assert.Equal("4f2c9e1", viewModel.BuildSha);
        Assert.True(viewModel.HasBuildSha);
        Assert.Equal("Copyright © 2026 Someone", viewModel.Copyright);
        Assert.True(viewModel.HasCopyright);
    }

    /// <summary>A build from a source drop recorded no revision: the build line is absent, not blank.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ABuildWithNoRevision_ShowsNoBuildLine(string? buildSha)
    {
        AboutViewModel viewModel = new("2.1.0", buildSha, "Copyright © 2026 Someone");

        Assert.False(viewModel.HasBuildSha);
    }

    [Fact]
    public void AnAssemblyWithNoCopyright_ShowsNoCopyrightLine()
    {
        AboutViewModel viewModel = new("2.1.0", "4f2c9e1", string.Empty);

        Assert.False(viewModel.HasCopyright);
    }

    [Fact]
    public void Constructor_RejectsAMissingVersionOrCopyright()
    {
        Assert.Throws<ArgumentNullException>(() => new AboutViewModel(null!, null, string.Empty));
        Assert.Throws<ArgumentNullException>(() => new AboutViewModel("2.1.0", null, null!));
    }
}
