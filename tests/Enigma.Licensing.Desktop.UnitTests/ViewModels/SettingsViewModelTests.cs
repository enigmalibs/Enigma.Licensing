using System;
using System.Collections.Generic;
using Enigma.Licensing.Desktop.Resources;
using Enigma.Licensing.Desktop.Settings;
using Enigma.Licensing.Desktop.UnitTests.TestDoubles;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The settings page: it offers three variants, it starts on the stored one, and a change repaints and
/// persists <b>exactly once</b>.
/// </summary>
/// <remarks>
/// The counting is the point. Applying twice is invisible; writing twice is not much worse — but a page that
/// writes while it is being built rewrites the file on every visit, and one that re-writes on a re-selection
/// turns a mouse wheel over a combo box into a burst of writes to the user's home directory.
/// </remarks>
public sealed class SettingsViewModelTests
{
    [Fact]
    public void Themes_AreSystemThenLightThenDark()
    {
        Assert.Collection(
            new SettingsViewModel(new FakeThemeService(), new FakeAppSettingsStore()).Themes,
            option => Assert.Equal(AppTheme.System, option.Theme),
            option => Assert.Equal(AppTheme.Light, option.Theme),
            option => Assert.Equal(AppTheme.Dark, option.Theme));
    }

    [Theory]
    [InlineData(AppTheme.System)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void SelectedTheme_StartsOnWhatWasStored(AppTheme stored)
    {
        FakeAppSettingsStore store = new(new AppSettings { Theme = stored });

        SettingsViewModel viewModel = new(new FakeThemeService(), store);

        Assert.Equal(stored, viewModel.SelectedTheme.Theme);
    }

    /// <summary>
    /// <c>App</c> has already applied the stored variant before the window was shown, so building the page
    /// must not repaint — and above all must not write the file back.
    /// </summary>
    [Fact]
    public void Constructor_NeitherAppliesNorPersists()
    {
        FakeThemeService theme = new();
        FakeAppSettingsStore store = new(new AppSettings { Theme = AppTheme.Dark });

        _ = new SettingsViewModel(theme, store);

        Assert.Empty(theme.Applied);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void SelectedTheme_StartsOnFollowTheSystem_WhenTheStoreHandsBackSomethingUnknown()
    {
        FakeAppSettingsStore store = new(new AppSettings { Theme = (AppTheme)9 });

        SettingsViewModel viewModel = new(new FakeThemeService(), store);

        Assert.Equal(AppTheme.System, viewModel.SelectedTheme.Theme);
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.System)]
    public void SelectedTheme_AppliesAndPersistsExactlyOnce(AppTheme chosen)
    {
        // Stored as the one variant that is not the chosen one, so every case is a real change.
        AppTheme other = chosen == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        FakeThemeService theme = new();
        FakeAppSettingsStore store = new(new AppSettings { Theme = other });
        SettingsViewModel viewModel = new(theme, store);

        viewModel.SelectedTheme = ThemeOption.For(chosen);

        Assert.Equal([chosen], theme.Applied);
        AppSettings saved = Assert.Single(store.Saved);
        Assert.Equal(chosen, saved.Theme);
        Assert.Equal(AppSettings.CurrentFormatVersion, saved.FormatVersion);
    }

    [Fact]
    public void SelectedTheme_ReSelectingTheSameOne_ChangesNothing()
    {
        FakeThemeService theme = new();
        FakeAppSettingsStore store = new(new AppSettings { Theme = AppTheme.Dark });
        SettingsViewModel viewModel = new(theme, store);

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);

        Assert.Empty(theme.Applied);
        Assert.Empty(store.Saved);
    }

    /// <summary>A selector can hand back null while its items are being replaced. That is not a choice.</summary>
    [Fact]
    public void SelectedTheme_SetToNull_IsIgnored()
    {
        FakeThemeService theme = new();
        FakeAppSettingsStore store = new(new AppSettings { Theme = AppTheme.Light });
        SettingsViewModel viewModel = new(theme, store);

        viewModel.SelectedTheme = null!;

        Assert.Equal(AppTheme.Light, viewModel.SelectedTheme.Theme);
        Assert.Empty(theme.Applied);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public void SelectedTheme_RaisesPropertyChanged_SoTheSelectorFollows()
    {
        List<string?> changed = [];
        SettingsViewModel viewModel = new(new FakeThemeService(), new FakeAppSettingsStore());
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);

        Assert.Equal([nameof(SettingsViewModel.SelectedTheme)], changed);
    }

    [Fact]
    public void ChoosingSeveralTimes_PersistsEachChange_AndOnlyTheChanges()
    {
        FakeThemeService theme = new();
        FakeAppSettingsStore store = new();
        SettingsViewModel viewModel = new(theme, store);

        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);
        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Dark);
        viewModel.SelectedTheme = ThemeOption.For(AppTheme.Light);
        viewModel.SelectedTheme = ThemeOption.For(AppTheme.System);

        Assert.Equal([AppTheme.Dark, AppTheme.Light, AppTheme.System], theme.Applied);
        Assert.Equal(3, store.Saved.Count);
    }

    /// <summary>
    /// The about section reports what is actually loaded, so a bug report carries the two versions that
    /// matter — the app's and the library's — rather than what someone remembers installing.
    /// </summary>
    [Fact]
    public void TheAboutSection_ReportsTheRunningBuild()
    {
        SettingsViewModel viewModel = new(new FakeThemeService(), new FakeAppSettingsStore());

        Assert.Equal(AppInfo.AppVersion, viewModel.AppVersion);
        Assert.Equal(AppInfo.LibraryVersion, viewModel.LibraryVersion);
        Assert.Equal(AppInfo.ProjectUrl, viewModel.ProjectUrl);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, new FakeAppSettingsStore()));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(new FakeThemeService(), null!));
    }
}
