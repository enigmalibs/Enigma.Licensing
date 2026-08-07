using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Enigma.Licensing.Internal;

namespace Enigma.Licensing.UnitTests.Internal;

/// <summary>
/// Covers <see cref="LicensePayload"/> — the codec whose output is the signed artefact.
/// </summary>
/// <remarks>
/// The exact-JSON assertions here are not style policing. These bytes are what a signature covers, so
/// the shape is the format: camelCase names, absent optionals omitted rather than nulled, and
/// ISO-8601 dates with an explicit offset. A change to any of them is a format change and should have
/// to be made deliberately.
/// </remarks>
public sealed class LicensePayloadTests
{
    private static string Json(License license) => Encoding.UTF8.GetString(LicensePayload.Serialize(license));

    [Fact]
    public void Serialize_WritesTheDocumentedShapeForAFullLicense() =>
        Assert.Equal(
            "{\"id\":\"01JYQ7ZK3M8X2N4P6R8T0V2W4Y\"," +
            "\"createdAt\":\"2026-08-06T12:34:56+02:00\"," +
            "\"productId\":\"MyApp 2.*\"," +
            "\"deviceId\":\"DESKTOP-ABC123\"," +
            "\"validFrom\":\"2026-01-02T03:04:05+00:00\"," +
            "\"validUntil\":\"2027-01-02T03:04:05+00:00\"," +
            "\"owner\":\"Contoso Ltd\"," +
            "\"features\":[\"export\",\"sync\"]," +
            "\"claims\":{\"seats\":\"25\",\"tier\":\"pro\"}}",
            Json(LicenseTestData.FullLicense()));

    /// <summary>
    /// Optional fields that were never set are absent from the document, not written as <c>null</c>.
    /// </summary>
    [Fact]
    public void Serialize_OmitsUnsetOptionalFields() =>
        Assert.Equal(
            "{\"id\":\"01JYQ7ZK3M8X2N4P6R8T0V2W4Y\"," +
            "\"createdAt\":\"2026-08-06T12:34:56+02:00\"," +
            "\"productId\":\"MyApp\"," +
            "\"features\":[],\"claims\":{}}",
            Json(LicenseTestData.MinimalLicense()));

    /// <summary>
    /// Sub-second precision survives to the tick, so two licenses issued microseconds apart do not
    /// serialize identically.
    /// </summary>
    [Fact]
    public void Serialize_KeepsSubSecondPrecision()
    {
        License license = LicenseTestData.MinimalLicense() with
        {
            CreatedAt = new DateTimeOffset(2026, 8, 6, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567),
        };

        Assert.Contains("\"createdAt\":\"2026-08-06T12:34:56.1234567+00:00\"", Json(license), StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_PreservesEveryFieldOfAFullLicense()
    {
        License original = LicenseTestData.FullLicense();

        License restored = LicensePayload.Deserialize(LicensePayload.Serialize(original));

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal(original.ProductId, restored.ProductId);
        Assert.Equal(original.DeviceId, restored.DeviceId);
        Assert.Equal(original.ValidFrom, restored.ValidFrom);
        Assert.Equal(original.ValidUntil, restored.ValidUntil);
        Assert.Equal(original.Owner, restored.Owner);
        Assert.Equal(original.Features, restored.Features);
        Assert.Equal(original.Claims, restored.Claims);
        Assert.True(restored.HasFeature("sync"));
    }

    [Fact]
    public void RoundTrip_LeavesUnsetOptionalFieldsUnset()
    {
        License restored = LicensePayload.Deserialize(LicensePayload.Serialize(LicenseTestData.MinimalLicense()));

        Assert.Null(restored.DeviceId);
        Assert.Null(restored.ValidFrom);
        Assert.Null(restored.ValidUntil);
        Assert.Null(restored.Owner);
        Assert.Empty(restored.Features);
        Assert.Empty(restored.Claims);
    }

    /// <summary>
    /// Serializing twice must produce identical bytes, or a license could not be re-signed to the same
    /// value and nothing about the signed form would be reproducible.
    /// </summary>
    [Fact]
    public void Serialize_IsDeterministic() =>
        Assert.Equal(
            LicensePayload.Serialize(LicenseTestData.FullLicense()),
            LicensePayload.Serialize(LicenseTestData.FullLicense()));

    /// <summary>
    /// The offset is part of the value, not decoration: the same instant written with two offsets
    /// produces two different documents, and each comes back as it went in.
    /// </summary>
    [Fact]
    public void RoundTrip_KeepsTheWrittenOffsetRatherThanNormalizingToUtc()
    {
        License license = LicenseTestData.MinimalLicense() with { CreatedAt = LicenseTestData.CreatedAt };

        License restored = LicensePayload.Deserialize(LicensePayload.Serialize(license));

        Assert.Equal(TimeSpan.FromHours(2), restored.CreatedAt.Offset);
        Assert.Equal(license.CreatedAt, restored.CreatedAt);
    }

    /// <summary>
    /// A hand-written payload may say <c>null</c> where the model promises a collection. It decodes to
    /// an empty one rather than tripping the never-null guarantee.
    /// </summary>
    [Fact]
    public void Deserialize_TreatsNullCollectionsAsEmpty()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            "{\"id\":\"X\",\"createdAt\":\"2026-08-06T12:34:56+02:00\",\"productId\":\"MyApp\"," +
            "\"features\":null,\"claims\":null}");

        License license = LicensePayload.Deserialize(payload);

        Assert.Empty(license.Features);
        Assert.Empty(license.Claims);
    }

    /// <summary>
    /// Property matching is case-sensitive: <c>ProductId</c> is not <c>productId</c>, and a document
    /// using the wrong casing is missing a required property rather than quietly parsing.
    /// </summary>
    [Fact]
    public void Deserialize_IsCaseSensitiveAboutPropertyNames()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            "{\"Id\":\"X\",\"CreatedAt\":\"2026-08-06T12:34:56+02:00\",\"ProductId\":\"MyApp\"}");

        Assert.Throws<JsonException>(() => LicensePayload.Deserialize(payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"X\"}")]
    [InlineData("{\"id\":\"X\",\"createdAt\":\"2026-08-06T12:34:56+02:00\"}")]
    [InlineData("{\"id\":\"X\",\"createdAt\":\"not-a-date\",\"productId\":\"MyApp\"}")]
    [InlineData("{\"id\":42,\"createdAt\":\"2026-08-06T12:34:56+02:00\",\"productId\":\"MyApp\"}")]
    public void Deserialize_ThrowsForAPayloadItCannotRead(string json) =>
        Assert.Throws<JsonException>(() => LicensePayload.Deserialize(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Serialize_ThrowsForANullLicense() =>
        Assert.Throws<ArgumentNullException>(() => LicensePayload.Serialize(null!));

    [Fact]
    public void Deserialize_ThrowsForNullBytes() =>
        Assert.Throws<ArgumentNullException>(() => LicensePayload.Deserialize(null!));

    /// <summary>
    /// Claim keys and values are opaque strings — the codec escapes whatever is in them and gives it
    /// back unchanged, so a claim carrying quotes, braces or non-ASCII text cannot corrupt the payload.
    /// </summary>
    [Fact]
    public void RoundTrip_SurvivesAwkwardClaimAndFeatureText()
    {
        License license = LicenseTestData.MinimalLicense() with
        {
            Features = ["a\"b", "c\\d", "é→ "],
            Claims = new Dictionary<string, string>
            {
                ["key\"with\"quotes"] = "{\"nested\":true}",
                ["ünïcode"] = "naïve\nnewline",
            },
        };

        License restored = LicensePayload.Deserialize(LicensePayload.Serialize(license));

        Assert.Equal(license.Features, restored.Features);
        Assert.Equal(license.Claims, restored.Claims);
    }
}
