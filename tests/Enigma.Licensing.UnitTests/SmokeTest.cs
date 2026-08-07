using System.Reflection;

namespace Enigma.Licensing.UnitTests;

/// <summary>
/// Toolchain smoke test: proves the MTP-native xUnit v3 runner builds and executes green, and that
/// the library assembly this suite references loads at runtime on every test TFM.
/// </summary>
/// <remarks>
/// It survives now that real types exist because it fails differently from every other test here —
/// a broken assembly reference, a target-framework mismatch or a runner misconfiguration shows up as
/// this one failing on its own, before any assertion about licensing behaviour is even reached.
/// </remarks>
public sealed class SmokeTest
{
    [Fact]
    public void LibraryAssembly_Loads()
    {
        Assembly assembly = Assembly.Load(new AssemblyName("Enigma.Licensing"));

        Assert.Equal("Enigma.Licensing", assembly.GetName().Name);
    }
}
