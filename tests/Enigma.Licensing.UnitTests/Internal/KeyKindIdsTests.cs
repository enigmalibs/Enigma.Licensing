using System;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Internal;

/// <summary>
/// Covers <see cref="KeyKindIds"/> — the <see cref="KeyKind"/> ↔ wire-value table of the
/// <c>enigma-key/1</c> format.
/// </summary>
/// <remarks>
/// The literals are asserted as literals, not through the constants that produce them: a test that
/// says <c>Assert.Equal(KeyKindIds.Public, KeyKindIds.ToIdentifier(KeyKind.Public))</c> passes happily
/// while the format changes underneath it.
/// </remarks>
public sealed class KeyKindIdsTests
{
    [Theory]
    [InlineData(KeyKind.Public, "public")]
    [InlineData(KeyKind.Private, "private")]
    public void ToIdentifier_ReturnsTheWireValue(KeyKind kind, string expected) =>
        Assert.Equal(expected, KeyKindIds.ToIdentifier(kind));

    [Fact]
    public void ToIdentifier_ReturnsNullForAnUndefinedValue() =>
        Assert.Null(KeyKindIds.ToIdentifier((KeyKind)7));

    [Theory]
    [InlineData("public", KeyKind.Public)]
    [InlineData("private", KeyKind.Private)]
    public void TryParse_ResolvesAKnownWireValue(string identifier, KeyKind expected)
    {
        Assert.True(KeyKindIds.TryParse(identifier, out KeyKind kind));
        Assert.Equal(expected, kind);
    }

    /// <summary>Matching is ordinal and case-sensitive — the values are format constants, not input.</summary>
    [Theory]
    [InlineData("Public")]
    [InlineData("PRIVATE")]
    [InlineData("pub")]
    [InlineData("secret")]
    [InlineData(" public")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RejectsAnythingElse(string? identifier) =>
        Assert.False(KeyKindIds.TryParse(identifier, out _));

    /// <summary>Every enum member maps out and back — a new one fails here until it is added.</summary>
    [Fact]
    public void EveryMemberRoundTrips()
    {
        foreach (KeyKind kind in Enum.GetValues<KeyKind>())
        {
            string? identifier = KeyKindIds.ToIdentifier(kind);

            Assert.NotNull(identifier);
            Assert.True(KeyKindIds.TryParse(identifier, out KeyKind parsed));
            Assert.Equal(kind, parsed);
        }
    }

    [Fact]
    public void TheEnumHasExactlyTwoMembers() => Assert.Equal(2, Enum.GetValues<KeyKind>().Length);
}
