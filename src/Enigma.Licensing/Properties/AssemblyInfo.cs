using System.Runtime.CompilerServices;

// The format's two load-bearing seams are internal by design and neither is reachable from the public
// surface in a way that can pin it down: AlgorithmIds is the single enum-to-wire-string table, and
// LicensePayload is the codec whose bytes are what a signature covers. Both need direct tests — a
// mistyped identifier or a codec that stops round-tripping is a broken format, not a broken service —
// so the test assembly gets friend access. Declared here rather than as an MSBuild AssemblyAttribute
// so the reason travels with the attribute.
[assembly: InternalsVisibleTo("Enigma.Licensing.UnitTests")]
