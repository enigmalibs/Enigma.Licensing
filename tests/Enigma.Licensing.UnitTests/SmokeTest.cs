using System.Reflection;

namespace Enigma.Licensing.UnitTests;

/// <summary>
/// Bootstrap smoke test: proves the MTP-native xUnit v3 toolchain builds and runs green, and that
/// the library assembly this suite references actually loads at runtime on every test TFM.
/// <c>Enigma.Licensing</c> has no public types yet, so the assembly itself is all there is to
/// assert against. Expand once real types exist (FEATURE-03A9).
/// </summary>
public sealed class SmokeTest
{
    [Fact]
    public void LibraryAssembly_Loads()
    {
        Assembly assembly = Assembly.Load(new AssemblyName("Enigma.Licensing"));

        Assert.Equal("Enigma.Licensing", assembly.GetName().Name);
    }
}
