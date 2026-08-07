using Enigma.Licensing.Keys;

namespace Enigma.Licensing.Internal;

/// <summary>
/// The single source of truth for the mapping between <see cref="KeyKind"/> and the strings that
/// travel in the <c>kind</c> field of an <c>enigma-key/1</c> file.
/// </summary>
/// <remarks>
/// The same discipline as <see cref="AlgorithmIds"/>, for the same reason: one table, ordinal and
/// case-sensitive matching, and the unknown → null/false pattern rather than throwing, so the caller
/// decides what an unmappable value means. These two values are lowercase because the field describes
/// the document rather than naming an algorithm; <c>"Public"</c> is not an <c>enigma-key/1</c>
/// document.
/// </remarks>
internal static class KeyKindIds
{
    /// <summary>Wire value for <see cref="KeyKind.Public"/>.</summary>
    internal const string Public = "public";

    /// <summary>Wire value for <see cref="KeyKind.Private"/>.</summary>
    internal const string Private = "private";

    /// <summary>
    /// Returns the wire value for <paramref name="kind"/>, or <see langword="null"/> when the value is
    /// not a defined <see cref="KeyKind"/> member.
    /// </summary>
    /// <param name="kind">The kind to write to a key file.</param>
    /// <returns>The wire value, or <see langword="null"/> for an undefined enum value.</returns>
    internal static string? ToIdentifier(KeyKind kind) => kind switch
    {
        KeyKind.Public => Public,
        KeyKind.Private => Private,
        _ => null,
    };

    /// <summary>
    /// Resolves a wire value read from a key file back to its <see cref="KeyKind"/>.
    /// </summary>
    /// <param name="identifier">
    /// The value as it appeared in the document. <see langword="null"/> — an absent field — is a normal
    /// input here and simply fails to resolve.
    /// </param>
    /// <param name="kind">
    /// The resolved kind on success; otherwise <see cref="KeyKind.Public"/> (the enum's default), which
    /// callers must not read when the method returns <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> when the value is one this library knows.</returns>
    internal static bool TryParse(string? identifier, out KeyKind kind)
    {
        switch (identifier)
        {
            case Public:
                kind = KeyKind.Public;
                return true;
            case Private:
                kind = KeyKind.Private;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
