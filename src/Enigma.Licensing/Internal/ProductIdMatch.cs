using System;
using System.Text.RegularExpressions;

namespace Enigma.Licensing.Internal;

/// <summary>
/// Decides whether the product identifier an application asks about is covered by the one a license
/// carries.
/// </summary>
/// <remarks>
/// <para>
/// Two rules, and the asymmetry between them is the point. <b>The license's</b> product identifier may
/// contain <c>*</c>, which is how one license covers a range of versions — <c>"MyApp 2.*"</c> matches
/// <c>"MyApp 2.0"</c> and <c>"MyApp 2.17"</c>. <b>The application's</b> is always a literal: it names
/// the one product being checked, so a <c>*</c> in it would be an application asking "does this license
/// cover anything at all", which is not a question worth answering yes to.
/// </para>
/// <para>
/// <b>The wildcard is the only special character.</b> The pattern is built by escaping the whole
/// license identifier first and only then reintroducing <c>.*</c> where a <c>*</c> was, so a product
/// name containing <c>.</c>, <c>+</c>, <c>(</c> or any other metacharacter matches literally. The
/// predecessor built its pattern the other way round and a dot in a version number quietly matched any
/// character; escaping first is what fixes it.
/// </para>
/// <para>
/// Anchors are <c>\A</c> and <c>\z</c> rather than <c>^</c> and <c>$</c>. In .NET <c>$</c> also matches
/// immediately before a trailing newline, so <c>"MyApp\n"</c> would satisfy a <c>"MyApp"</c> license;
/// <c>\z</c> means the actual end of the string.
/// </para>
/// <para>
/// Matching is case-sensitive in both branches, consistent with every other comparison in the format.
/// </para>
/// </remarks>
internal static class ProductIdMatch
{
    // The escaped form of a '*'. Regex.Escape turns '*' into "\*", and that is the token swapped for
    // ".*" once escaping has already neutralized everything else.
    private const string EscapedWildcard = @"\*";

    /// <summary>
    /// Determines whether <paramref name="requested"/> is covered by <paramref name="licensed"/>.
    /// </summary>
    /// <param name="licensed">The product identifier the license carries. May contain <c>*</c>.</param>
    /// <param name="requested">The product identifier the application is asking about. Always literal.</param>
    /// <returns><see langword="true"/> when the license covers the requested product.</returns>
    internal static bool Matches(string licensed, string requested) =>
        licensed.IndexOf('*') < 0
            ? string.Equals(licensed, requested, StringComparison.Ordinal)
            : Regex.IsMatch(requested, ToPattern(licensed), RegexOptions.CultureInvariant);

    /// <summary>
    /// Builds the anchored regular expression a wildcard product identifier stands for.
    /// </summary>
    /// <param name="licensed">The license's product identifier, containing at least one <c>*</c>.</param>
    /// <returns>The pattern.</returns>
    /// <remarks>
    /// Exposed separately so the escaping can be asserted directly. The pattern only ever alternates
    /// escaped literals with <c>.*</c>, which is why no match timeout is set: there is no nested
    /// quantifier for an input to make backtrack, and the license the pattern comes from has had its
    /// signature verified before validation gets this far.
    /// </remarks>
    internal static string ToPattern(string licensed) =>
        @"\A" + Regex.Escape(licensed).Replace(EscapedWildcard, ".*") + @"\z";
}
