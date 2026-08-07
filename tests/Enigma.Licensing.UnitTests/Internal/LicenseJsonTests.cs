using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Enigma.Licensing.Internal;
using Enigma.Licensing.Keys;

namespace Enigma.Licensing.UnitTests.Internal;

/// <summary>
/// Covers <see cref="LicenseJson"/> and the two source-generated contexts behind it — the library's
/// trim- and AOT-safety guarantee.
/// </summary>
/// <remarks>
/// <para>
/// The real proof that serialization is source-generated is that every other suite in this project
/// passes: the contexts are the <i>only</i> resolver these documents have, so a type that lost its
/// metadata would throw at the first save or load rather than quietly reflecting. What this suite adds
/// is the diagnosis for when that happens — which context was expected to cover what, and that the
/// settings the two formats specify survived the move off <see cref="JsonSerializerOptions"/>.
/// </para>
/// <para>
/// The last test is the one that matters most: a context resolves the types it was told about and
/// <b>nothing else</b>. If it ever answered for an unregistered type, the resolver would have fallen
/// back to reflection and the guarantee would be gone without a single test turning red.
/// </para>
/// </remarks>
public sealed class LicenseJsonTests
{
    /// <summary>The three documents the library reads or writes, and the context each belongs to.</summary>
    public static TheoryData<JsonTypeInfo, bool> Documents => new()
    {
        { LicenseJson.Envelope, true },
        { LicenseJson.KeyFile, true },
        { LicenseJson.Payload, false },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryDocument_IsResolvedBySourceGeneration(JsonTypeInfo document, bool _) =>
        Assert.IsAssignableFrom<JsonSerializerContext>(document.Options.TypeInfoResolver);

    /// <summary>
    /// camelCase names and omit-when-null are what both formats specify, and they were declared on
    /// <see cref="JsonSerializerOptions"/> before the move to source generation. Moving them onto
    /// <see cref="JsonSourceGenerationOptionsAttribute"/> had to leave them saying the same thing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryDocument_KeepsTheSettingsBothFormatsSpecify(JsonTypeInfo document, bool indented)
    {
        Assert.Same(JsonNamingPolicy.CamelCase, document.Options.PropertyNamingPolicy);
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, document.Options.DefaultIgnoreCondition);
        Assert.False(document.Options.PropertyNameCaseInsensitive);
        Assert.False(document.Options.AllowTrailingCommas);
        Assert.Equal(JsonCommentHandling.Disallow, document.Options.ReadCommentHandling);
        Assert.Equal(indented, document.Options.WriteIndented);
    }

    /// <summary>
    /// The two files a human opens are indented; the payload, which is stored as base64, is not. That
    /// is the only difference between the two contexts, and it is why there are two.
    /// </summary>
    [Fact]
    public void TheTwoContexts_DifferOnlyInIndentation()
    {
        Assert.Same(LicenseJson.Envelope.Options, LicenseJson.KeyFile.Options);
        Assert.NotSame(LicenseJson.Envelope.Options, LicenseJson.Payload.Options);
    }

    [Fact]
    public void EachDocument_ResolvesToItsOwnType()
    {
        Assert.Equal(typeof(LicenseEnvelope), LicenseJson.Envelope.Type);
        Assert.Equal(typeof(KeyEnvelope), LicenseJson.KeyFile.Type);
        Assert.Equal(typeof(License), LicenseJson.Payload.Type);
    }

    /// <summary>
    /// A context answers for its registered roots and for what those roots reach — and for nothing
    /// else. An unregistered type resolving would mean the resolver had reflected instead.
    /// </summary>
    [Fact]
    public void AContext_AnswersOnlyForWhatItWasGiven()
    {
        Assert.NotNull(EnvelopeJsonContext.Default.GetTypeInfo(typeof(LicenseEnvelope)));
        Assert.NotNull(EnvelopeJsonContext.Default.GetTypeInfo(typeof(KeyEnvelope)));
        Assert.NotNull(EnvelopeJsonContext.Default.GetTypeInfo(typeof(KeyEncryption)));
        Assert.NotNull(PayloadJsonContext.Default.GetTypeInfo(typeof(License)));

        Assert.Null(PayloadJsonContext.Default.GetTypeInfo(typeof(LicenseEnvelope)));
        Assert.Null(EnvelopeJsonContext.Default.GetTypeInfo(typeof(License)));
        Assert.Null(EnvelopeJsonContext.Default.GetTypeInfo(typeof(LicenseRequest)));
        Assert.Null(EnvelopeJsonContext.Default.GetTypeInfo(typeof(LicensingKey)));
    }
}
