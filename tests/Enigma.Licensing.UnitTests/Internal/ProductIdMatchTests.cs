using Enigma.Licensing.Internal;

namespace Enigma.Licensing.UnitTests.Internal;

/// <summary>
/// Covers the wildcard rule directly, at the seam — the escaping in particular, which is where the
/// predecessor library went wrong.
/// </summary>
public sealed class ProductIdMatchTests
{
    [Theory]
    [InlineData("MyApp", "MyApp", true)]
    [InlineData("MyApp", "myapp", false)]
    [InlineData("MyApp", "MyApp ", false)]
    [InlineData("MyApp", "", false)]
    [InlineData("", "", true)]
    public void Matches_ComparesALiteralIdentifierOrdinally(string licensed, string requested, bool expected) =>
        Assert.Equal(expected, ProductIdMatch.Matches(licensed, requested));

    [Theory]
    [InlineData("MyApp 2.*", "MyApp 2.0", true)]
    [InlineData("MyApp 2.*", "MyApp 2.17.3-beta", true)]
    [InlineData("MyApp 2.*", "MyApp 2.", true)]
    [InlineData("MyApp 2.*", "MyApp 2", false)]
    [InlineData("MyApp 2.*", "MyApp 3.0", false)]
    [InlineData("MyApp 2.*", "Prefix MyApp 2.0", false)]
    [InlineData("*", "", true)]
    [InlineData("*", "anything", true)]
    [InlineData("*Suite", "MyApp Suite", true)]
    [InlineData("*Suite", "MyApp Suite Pro", false)]
    [InlineData("My*App*", "MyBigAppTastic", true)]
    public void Matches_AppliesTheWildcardAnchoredAtBothEnds(string licensed, string requested, bool expected) =>
        Assert.Equal(expected, ProductIdMatch.Matches(licensed, requested));

    /// <summary>
    /// The whole point of escaping first. Every one of these is a regular-expression metacharacter that
    /// would match something other than itself if the pattern were built the other way round.
    /// </summary>
    [Theory]
    [InlineData("MyApp 2.0", "MyApp 2x0")]
    [InlineData("MyApp+", "MyAppp")]
    [InlineData("MyApp?", "MyAp")]
    [InlineData("(MyApp)", "MyApp")]
    [InlineData("My|App", "My")]
    [InlineData("MyApp[1]", "MyApp1")]
    [InlineData("MyApp{2}", "MyAppApp")]
    [InlineData("MyApp\\d", "MyApp5")]
    public void Matches_TreatsRegexMetacharactersLiterally(string licensed, string requested)
    {
        Assert.False(ProductIdMatch.Matches(licensed, requested));
        Assert.True(ProductIdMatch.Matches(licensed, licensed));
    }

    /// <summary>Metacharacters keep working literally alongside a wildcard.</summary>
    [Theory]
    [InlineData("MyApp 2.* (Pro)", "MyApp 2.17 (Pro)", true)]
    [InlineData("MyApp 2.* (Pro)", "MyApp 2.17 xPro)", false)]
    public void Matches_EscapesMetacharactersAroundAWildcard(string licensed, string requested, bool expected) =>
        Assert.Equal(expected, ProductIdMatch.Matches(licensed, requested));

    /// <summary>
    /// <c>\z</c> rather than <c>$</c>: in .NET <c>$</c> also matches immediately before a trailing
    /// newline, which would let <c>"MyApp 2.0\n"</c> satisfy a <c>"MyApp 2.*"</c> license.
    /// </summary>
    [Fact]
    public void Matches_DoesNotAcceptATrailingNewline()
    {
        Assert.False(ProductIdMatch.Matches("MyApp 2.*", "MyApp 2.0\n"));
        Assert.False(ProductIdMatch.Matches("MyApp*", "MyApp\n"));
    }

    /// <summary>A wildcard never spans a line either — <c>.</c> not matching <c>\n</c> is the default.</summary>
    [Fact]
    public void Matches_DoesNotLetAWildcardSpanALineBreak() =>
        Assert.False(ProductIdMatch.Matches("MyApp*Pro", "MyApp\nPro"));

    [Fact]
    public void ToPattern_AnchorsWithBackslashAAndBackslashZ()
    {
        string pattern = ProductIdMatch.ToPattern("MyApp 2.*");

        Assert.StartsWith(@"\A", pattern, System.StringComparison.Ordinal);
        Assert.EndsWith(@"\z", pattern, System.StringComparison.Ordinal);
        Assert.Contains(@"\.", pattern, System.StringComparison.Ordinal);
        Assert.Contains(".*", pattern, System.StringComparison.Ordinal);
    }
}
