using System;
using System.IO;

namespace Enigma.Licensing.Desktop.UnitTests;

/// <summary>
/// The application icon is written next to the executable as <c>app.ico</c> — the name every Enigma app ships
/// it under — so a shortcut, a launcher or an installer can point at a file.
/// </summary>
/// <remarks>
/// The app's output flows into this project's output through the project reference, which is where the test
/// reads it; the source asset is found by walking up to the solution file. A publish folder gets the same file
/// by the same item, which is checked by hand (<c>docs/done/FEATURE-0156.md</c>).
/// </remarks>
public sealed class AppIconTests
{
    [Fact]
    public void TheIconFile_SitsInTheOutputDirectory_ByteForByteTheAsset()
    {
        string output = Path.Combine(AppContext.BaseDirectory, "app.ico");
        string asset = Path.Combine(SolutionDirectory(), "src", "Enigma.Licensing.Desktop", "Assets", "appicon.ico");

        Assert.True(File.Exists(output), $"No app.ico in {AppContext.BaseDirectory}.");
        Assert.Equal(File.ReadAllBytes(asset), File.ReadAllBytes(output));
    }

    /// <summary>The copy lands at the root, not in an <c>Assets</c> folder a launcher would have to know about.</summary>
    [Fact]
    public void TheIconFile_IsNotCopiedUnderItsSourceFolder()
    {
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "appicon.ico")));
    }

    private static string SolutionDirectory()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Enigma.Licensing.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No Enigma.Licensing.slnx above {AppContext.BaseDirectory}.");
    }
}
