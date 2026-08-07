using System;
using Enigma.Licensing.Desktop.Settings;
using Enigma.Licensing.Desktop.ViewModels;

namespace Enigma.Licensing.Desktop.UnitTests.ViewModels;

/// <summary>
/// The appearance selector's entries: one per <see cref="AppTheme"/>, in the order they are offered, each
/// labelled in words rather than by the enum member's name.
/// </summary>
public sealed class ThemeOptionTests
{
    [Fact]
    public void All_CoversEveryVariant_StartingWithTheDefault()
        => Assert.Collection(
            ThemeOption.All,
            option => Assert.Equal(AppTheme.System, option.Theme),
            option => Assert.Equal(AppTheme.Light, option.Theme),
            option => Assert.Equal(AppTheme.Dark, option.Theme));

    /// <summary>
    /// "System" is not what a user is choosing — "follow the system" is. The labels exist precisely because
    /// the enum's names are not offers.
    /// </summary>
    [Theory]
    [InlineData(AppTheme.System)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void Label_IsSetAndIsWhatToStringReturns(AppTheme theme)
    {
        ThemeOption option = ThemeOption.For(theme);

        Assert.False(string.IsNullOrWhiteSpace(option.Label));
        Assert.Equal(option.Label, option.ToString());
    }

    [Fact]
    public void For_ReturnsTheSameInstanceAsAll_SoSelectionComparesByReference()
        => Assert.Same(ThemeOption.All[2], ThemeOption.For(AppTheme.Dark));

    [Fact]
    public void Default_IsFollowTheSystem() => Assert.Equal(AppTheme.System, ThemeOption.Default.Theme);

    [Fact]
    public void For_RejectsAVariantThatIsNotOffered()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ThemeOption.For((AppTheme)9));
}
