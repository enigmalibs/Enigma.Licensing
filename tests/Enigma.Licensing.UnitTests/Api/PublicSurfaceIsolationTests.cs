using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Enigma.Licensing.UnitTests.Api;

/// <summary>
/// The reflection guard behind the library's load-bearing isolation claim: <b>only Enigma.Licensing
/// types on the public surface</b>. Modelled on Enigma.Core's own <c>BouncyCastleIsolationTests</c>,
/// which exists for the same reason one layer down.
/// </summary>
/// <remarks>
/// <para>
/// Two tests, because the invariant has two halves that need catching in different ways. The
/// namespace guard states the documented rule — no <c>Enigma.Core.*</c>, no
/// <c>Org.BouncyCastle.*</c> — and names the offender in the language the docs use. The assembly
/// guard is the one that actually closes the hole: <c>Ulid</c> is declared as <c>System.Ulid</c>, so
/// no namespace rule could ever distinguish it from a framework type, and a dependency added later
/// would slip past a denylist that was written before it existed. An allowlist of assemblies catches
/// both.
/// </para>
/// <para>
/// Why it matters: every one of these dependencies is a version the consumer does not control.
/// A <c>MLDsaParameterSet</c> or a <c>Ulid</c> on a signature would put Enigma.Core's or Cysharp's
/// versioning in the path of every application that references this library, and would make an
/// Enigma.Core major bump a breaking change here. The formats and the services are the contract; the
/// libraries behind them are not.
/// </para>
/// </remarks>
public sealed class PublicSurfaceIsolationTests
{
    /// <summary>
    /// The namespace roots the documentation forbids by name.
    /// </summary>
    private static readonly string[] ForbiddenNamespaceRoots = ["Enigma.Core", "Org.BouncyCastle"];

    /// <summary>
    /// Every assembly whose types may legitimately appear on the surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Enigma.Licensing</c> itself, the framework, and
    /// <c>Microsoft.Extensions.DependencyInjection.Abstractions</c> — the single deliberate exception,
    /// because <c>AddEnigmaLicensing()</c> takes and returns an <c>IServiceCollection</c> and an
    /// extension method that hid its own extended type would be useless. It earns the exception by
    /// being a contract package: abstractions only, versioned conservatively, and already present in
    /// any application doing dependency injection.
    /// </para>
    /// <para>
    /// Framework assemblies are matched by their <c>System.*</c> / <c>netstandard</c> / <c>mscorlib</c>
    /// names rather than enumerated, since which one a given type lands in (<c>System.Private.CoreLib</c>
    /// against <c>System.Runtime</c>, say) is a runtime detail that differs per TFM and is not what this
    /// test is about.
    /// </para>
    /// </remarks>
    private static readonly string[] AllowedAssemblies =
    [
        "Enigma.Licensing",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
    ];

    [Fact]
    public void NoExportedMember_ExposesAnEnigmaCoreOrBouncyCastleType()
    {
        List<string> offenders =
        [
            .. SurfaceTypes()
                .Where(entry => IsInForbiddenNamespace(entry.Type))
                .Select(entry => $"{entry.Where}: {entry.Type.FullName}"),
        ];

        Assert.True(
            offenders.Count == 0,
            "Enigma.Core or BouncyCastle types leaked onto the public surface:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// The broader half of the invariant, and the one that catches what a namespace rule cannot:
    /// <c>System.Ulid</c>, <c>DeviceId.*</c>, and whatever dependency lands next.
    /// </summary>
    [Fact]
    public void NoExportedMember_ExposesATypeFromAnUnexpectedAssembly()
    {
        List<string> offenders =
        [
            .. SurfaceTypes()
                .Where(entry => !IsAllowedAssembly(entry.Type))
                .Select(entry =>
                    $"{entry.Where}: {entry.Type.FullName} " +
                    $"(from {entry.Type.Assembly.GetName().Name})"),
        ];

        Assert.True(
            offenders.Count == 0,
            "Types from assemblies that are implementation details reached the public surface. Either " +
            "wrap the type in an Enigma.Licensing one, or — if the dependency is genuinely part of the " +
            "contract — add it to AllowedAssemblies with the reason:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// A self-check on the walk, because the failure mode of a guard test is passing vacuously: a
    /// reflection bug, a renamed assembly or a wrong <see cref="BindingFlags"/> combination would
    /// inspect nothing and report success forever.
    /// </summary>
    /// <remarks>
    /// Each assertion pins one arm of the walk that a real leak would travel along: a method's return
    /// type, a method's parameter type, and an implemented interface.
    /// </remarks>
    [Fact]
    public void TheWalk_ReachesEveryKindOfMemberItClaimsTo()
    {
        List<(Type Type, string Where)> surface = [.. SurfaceTypes()];

        Assert.Contains(surface, entry =>
            entry.Where.EndsWith("LicenseEnvelope.SaveAsync (return)", StringComparison.Ordinal));
        Assert.Contains(surface, entry =>
            entry.Where.EndsWith("IKeyService.LoadKeyAsync (parameter)", StringComparison.Ordinal));
        Assert.Contains(surface, entry =>
            entry.Where.EndsWith("(base/interface)", StringComparison.Ordinal) &&
            entry.Type == typeof(ILicenseValidator));
    }

    /// <summary>
    /// Every type reachable from the exported surface, paired with where it was found so a failure says
    /// which member to fix.
    /// </summary>
    /// <remarks>
    /// Covers base types, implemented interfaces, method returns and parameters, constructor parameters
    /// and field types — with arrays, by-refs and generic arguments unwrapped to the types they are
    /// built from. Property and event types come along transitively through their accessor methods,
    /// which is why they need no case of their own.
    /// </remarks>
    private static IEnumerable<(Type Type, string Where)> SurfaceTypes()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.DeclaredOnly;

        foreach (Type type in typeof(LicenseEnvelope).Assembly.GetExportedTypes())
        {
            string name = type.FullName ?? type.Name;

            foreach (Type referenced in Unwrap(type.BaseType)
                         .Concat(type.GetInterfaces().SelectMany(Unwrap)))
            {
                yield return (referenced, $"{name} (base/interface)");
            }

            // Methods, which includes property and event accessors.
            foreach (MethodInfo method in type.GetMethods(flags).Where(IsExposed))
            {
                foreach (Type referenced in Unwrap(method.ReturnType))
                {
                    yield return (referenced, $"{name}.{method.Name} (return)");
                }

                foreach (Type referenced in method.GetParameters().SelectMany(p => Unwrap(p.ParameterType)))
                {
                    yield return (referenced, $"{name}.{method.Name} (parameter)");
                }
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(flags).Where(IsExposed))
            {
                foreach (Type referenced in constructor.GetParameters().SelectMany(p => Unwrap(p.ParameterType)))
                {
                    yield return (referenced, $"{name}.ctor (parameter)");
                }
            }

            foreach (FieldInfo field in type.GetFields(flags).Where(IsExposed))
            {
                foreach (Type referenced in Unwrap(field.FieldType))
                {
                    yield return (referenced, $"{name}.{field.Name} (field)");
                }
            }
        }
    }

    // Public, or protected in either form — the surface an external caller or inheritor can reach.
    // Private and internal-only members are not part of the exported contract, which is exactly what
    // lets LicensingKey hold its key material as an internal member.
    private static bool IsExposed(MethodBase member) =>
        member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly;

    private static bool IsExposed(FieldInfo field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    /// <summary>
    /// Reduces a type to the types it is actually built from: array, by-ref and pointer wrappers give
    /// up their element type, and a generic type yields itself plus each of its arguments.
    /// </summary>
    private static IEnumerable<Type> Unwrap(Type? type)
    {
        if (type is null || type.IsGenericParameter)
        {
            yield break;
        }

        if (type.HasElementType)
        {
            foreach (Type element in Unwrap(type.GetElementType()))
            {
                yield return element;
            }

            yield break;
        }

        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (Type argument in type.GetGenericArguments().SelectMany(Unwrap))
        {
            yield return argument;
        }
    }

    private static bool IsInForbiddenNamespace(Type type) =>
        type.Namespace is { } ns &&
        ForbiddenNamespaceRoots.Any(root =>
            string.Equals(ns, root, StringComparison.Ordinal) ||
            ns.StartsWith(root + ".", StringComparison.Ordinal));

    private static bool IsAllowedAssembly(Type type)
    {
        string? assembly = type.Assembly.GetName().Name;

        if (assembly is null)
        {
            return false;
        }

        return AllowedAssemblies.Contains(assembly, StringComparer.Ordinal) || IsFramework(assembly);
    }

    private static bool IsFramework(string assembly) =>
        string.Equals(assembly, "System", StringComparison.Ordinal) ||
        string.Equals(assembly, "netstandard", StringComparison.Ordinal) ||
        string.Equals(assembly, "mscorlib", StringComparison.Ordinal) ||
        assembly.StartsWith("System.", StringComparison.Ordinal);
}
