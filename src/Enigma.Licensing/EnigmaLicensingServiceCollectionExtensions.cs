using System;
using Enigma.Licensing;
using Enigma.Licensing.Keys;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Deliberately in Microsoft.Extensions.DependencyInjection rather than Enigma.Licensing, so
// AddEnigmaLicensing() shows up on IServiceCollection wherever a consumer is already configuring
// services — no extra using directive. That is the convention every Microsoft.Extensions.*
// registration extension follows, and the one Enigma.LicenseManager used before it.
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Enigma.Licensing's services with an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// The name is qualified rather than the bare <c>ServiceCollectionExtensions</c> because this class
/// shares a namespace with every other library's registration extensions — including
/// Enigma.DataEncryption's — and two identically named types in one namespace are ambiguous to anyone
/// who names the type instead of just calling the method.
/// </remarks>
public static class EnigmaLicensingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the four licensing services as singletons.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <remarks>
    /// <para>Registers, all as singletons:</para>
    /// <list type="bullet">
    ///   <item><description><see cref="IKeyService"/> → <see cref="KeyService"/> — generate, save and load keys;</description></item>
    ///   <item><description><see cref="ILicenseGenerator"/> → <see cref="LicenseGenerator"/> — sign a <see cref="LicenseRequest"/> into a <see cref="LicenseEnvelope"/>;</description></item>
    ///   <item><description><see cref="ILicenseValidator"/> → <see cref="LicenseValidator"/> — the verdict over an envelope;</description></item>
    ///   <item><description><see cref="ILicenseStore"/> → <see cref="LicenseStore"/> — the registry of installed licenses.</description></item>
    /// </list>
    /// <para>
    /// <b>Nothing from Enigma.Core is registered.</b> Unlike a library that injects Core's factories,
    /// every service here news up the Core primitives it needs in its own constructor — Core ships no
    /// registration extension of its own, and there is no seam a consumer would want to substitute.
    /// The four registrations above are therefore the whole of it.
    /// </para>
    /// <para>
    /// <b>Singleton is correct.</b> The generator, the validator and the key service are stateless —
    /// all per-call state (keys, payload bytes, passwords) lives on the stack of the call. The store is
    /// the exception: it holds the registry of installed licenses, which is precisely why it has to be a
    /// singleton, and it guards that registry with a lock. <see cref="LicenseStore"/> resolves the
    /// registered <see cref="ILicenseValidator"/> through its constructor, so substituting the validator
    /// substitutes it for the store too.
    /// </para>
    /// <para>
    /// <b>Every registration uses <c>TryAdd</c></b>, so a consumer who has already registered their own
    /// implementation of any of the four keeps it, and calling this method twice is harmless.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddEnigmaLicensing(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<IKeyService, KeyService>();
        services.TryAddSingleton<ILicenseGenerator, LicenseGenerator>();
        services.TryAddSingleton<ILicenseValidator, LicenseValidator>();

        // Explicit factory rather than TryAddSingleton<ILicenseStore, LicenseStore>(): the container
        // would pick LicenseStore's greediest satisfiable constructor, which happens to be the right one
        // here — but only by accident. Naming the dependency makes it a decision, so a store always
        // validates through whatever ILicenseValidator this collection ended up with, including a
        // consumer's own.
        services.TryAddSingleton<ILicenseStore>(provider =>
            new LicenseStore(provider.GetRequiredService<ILicenseValidator>()));

        return services;
    }
}
