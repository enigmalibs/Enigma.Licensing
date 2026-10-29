# Security Policy

Enigma.Licensing is the component that decides whether a license is authentic, so a defect in it is
a defect in every application that relies on it: a forged or tampered license accepted as valid
defeats the licensing of the product outright, and a signing key mishandled on the issuing side
compromises every license ever issued under it. Vulnerability reports are taken seriously and
handled with priority.

## Supported versions

Security fixes are provided for the latest released version. Enigma.Licensing follows
[Semantic Versioning](https://semver.org/), and users are encouraged to stay current with the
newest release.

| Version | Supported          |
|---------|--------------------|
| 2.1.x   | :white_check_mark: |
| 2.0.x   | :x:                |
| 1.0.x   | :x:                |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull
requests.** Public disclosure before a fix is available puts every user at risk.

Instead, use **GitHub's private vulnerability reporting**:

1. Go to the repository's **Security** tab.
2. Select **Report a vulnerability** to open a private advisory.
3. Include as much detail as you can — the affected version, the component involved, a description
   of the issue, and, where possible, a minimal reproduction and its impact.

This keeps the report private between you and the maintainers while it is triaged and fixed.

## What to expect

- Your report will be acknowledged and triaged as promptly as possible.
- The issue will be investigated and, once confirmed, a fix prepared and released.
- Coordinated disclosure is preferred: please allow a reasonable period for a fix to ship before
  any public discussion of the vulnerability.
- Your contribution will be credited in the resulting advisory unless you ask to remain anonymous.

## Scope

Reports concerning the license and key document formats, the signing and verification paths, the
validation order and its verdicts, password handling and key-material encryption, and the public API
surface of Enigma.Licensing are in scope — as is the Enigma.Licensing.Desktop application in this
repository. Because Enigma.Licensing performs no cryptography of its own and builds on
[Enigma.Core](https://github.com/enigmalibs/Enigma.Core), which in turn wraps
[BouncyCastle](https://github.com/bcgit/bc-csharp), issues rooted in either of those libraries
should also be reported upstream to the project concerned.
