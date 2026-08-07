using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Enigma.Licensing.Desktop.Models;

/// <summary>
/// A saved generate-page form: everything an issuer types to produce one kind of licence, so the next
/// one for the same product does not have to be typed again.
/// </summary>
/// <remarks>
/// <para>
/// A profile is a convenience of <i>this application</i> and nothing more. It is never signed, never
/// validated and never read by the library — the library's own documents are the key files and the licence
/// envelope. Losing a profile costs some typing and nothing else.
/// </para>
/// <para>
/// <b>The password is not in it, and never will be.</b> A profile names the signing key by
/// <see cref="PrivateKeyPath"/>; the password that opens that key is typed for each issue and lives no
/// longer than the command that uses it. A file that remembered both would turn "I keep my form fields
/// handy" into "my signing key is unprotected", which is the opposite of what encrypting it was for.
/// </para>
/// <para>
/// <b>There is no algorithm here either.</b> The signature scheme belongs to the key and is decided when
/// the pair is generated, so a profile field for it could only ever be written and not read — and could
/// silently disagree with the key file it sits next to.
/// </para>
/// <para>
/// The dates are <see cref="DateOnly"/> because that is what the page holds: a calendar day chosen from a
/// picker. Turning a day into the instant a licence starts or stops being usable is the generate page's
/// job, and doing it at the last moment keeps the file readable — <c>"validUntil": "2026-12-31"</c> says
/// what it means to anyone who opens it in an editor.
/// </para>
/// </remarks>
public sealed record LicenseProfile
{
    /// <summary>The <see cref="FormatVersion"/> this version of the application writes.</summary>
    public const int CurrentFormatVersion = 1;

    private static readonly IReadOnlyDictionary<string, string> EmptyClaims =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.Ordinal));

    private readonly IReadOnlyList<string> _features = [];
    private readonly IReadOnlyDictionary<string, string> _claims = EmptyClaims;

    /// <summary>
    /// The shape of this document. A file declaring a higher version than
    /// <see cref="CurrentFormatVersion"/> is refused rather than guessed at.
    /// </summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>The product the licence is for. May carry a <c>*</c> wildcard, as the licence itself may.</summary>
    public string? ProductId { get; init; }

    /// <summary>Who the licence is issued to.</summary>
    public string? Owner { get; init; }

    /// <summary>The device the licence is locked to, or <see langword="null"/> for one that runs anywhere.</summary>
    public string? DeviceId { get; init; }

    /// <summary>The day the licence starts being usable, or <see langword="null"/> for immediately.</summary>
    public DateOnly? ValidFrom { get; init; }

    /// <summary>The last day the licence is usable, or <see langword="null"/> for a perpetual licence.</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>The feature flags to grant. Never <see langword="null"/>: an explicit null reads as empty.</summary>
    public IReadOnlyList<string> Features
    {
        get => _features;
        init => _features = value ?? [];
    }

    /// <summary>The key/value claims to carry. Never <see langword="null"/>, as <see cref="Features"/> is not.</summary>
    public IReadOnlyDictionary<string, string> Claims
    {
        get => _claims;
        init => _claims = value ?? EmptyClaims;
    }

    /// <summary>
    /// The private key file to sign with — a path, not the key. The file is read at signing time, so a
    /// profile survives the key being re-encrypted, and a profile whose key has moved fails loudly when it
    /// is used rather than quietly when it is loaded.
    /// </summary>
    public string? PrivateKeyPath { get; init; }
}
