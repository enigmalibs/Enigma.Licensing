using System;
using System.IO;
using Enigma.Licensing.Desktop.Resources;

namespace Enigma.Licensing.Desktop.UnitTests;

/// <summary>
/// What the Windows installer (<c>packaging/windows/buildmsi.ps1</c>) says the product is. Nothing in the build
/// reads that script, so a typo in one of its three constants would ship silently: these tests pin them as whole
/// lines.
/// </summary>
/// <remarks>
/// The upgrade code is the product line's identity in Windows Installer: an MSI that carries the installed one's
/// code replaces it, and one that does not installs beside it. The product name and the assembly are the other
/// two facts the script derives everything else from — the install folder and the shortcuts' names, and the
/// project it reads the version from, the publish folder and the executable the shortcuts start.
/// </remarks>
public sealed class WindowsInstallerIdentityTests
{
    /// <summary>The upgrade code generated for the first MSI (FEATURE-6D42): every later MSI must keep it.</summary>
    private const string UpgradeCode = "4bad4f52-dd12-4b57-8d94-52fe1936f034";

    [Fact]
    public void TheInstaller_KeepsTheProductLinesUpgradeCode()
    {
        Assert.Contains($"$UpgradeCode = '{UpgradeCode}'", Script());
    }

    [Fact]
    public void TheInstaller_NamesTheProductAsTheApplicationDoes()
    {
        Assert.Contains($"$ProductName = '{AppInfo.Title}'", Script());
    }

    [Fact]
    public void TheInstaller_PackagesTheApplicationsOwnAssembly()
    {
        string assembly = typeof(AppInfo).Assembly.GetName().Name!;

        Assert.Contains($"$Assembly = '{assembly}'", Script());
        Assert.True(
            File.Exists(Path.Combine(SolutionDirectory(), "src", assembly, assembly + ".csproj")),
            $"The script reads the version from src/{assembly}/{assembly}.csproj, which does not exist.");
    }

    private static string[] Script() =>
        File.ReadAllLines(Path.Combine(SolutionDirectory(), "packaging", "windows", "buildmsi.ps1"));

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
