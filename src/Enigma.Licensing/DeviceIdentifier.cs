using DeviceId;

namespace Enigma.Licensing;

/// <summary>
/// Produces the string that identifies the machine a license is locked to.
/// </summary>
/// <remarks>
/// <para>
/// A device-locked license carries a <see cref="License.DeviceId"/>, and validation compares it
/// ordinally against whatever string the application supplies. This class is a <i>convenience</i> for
/// producing that string, not a requirement: any scheme an application prefers — an account
/// identifier, a hardware dongle serial, a value provisioned at install time — plugs in with no change
/// to the format, because the library never interprets the value.
/// </para>
/// <para>
/// <b>Best-effort, and deliberately so.</b> <see cref="Generate"/> combines the machine name and the
/// operating system version. That has two consequences worth designing around:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>An OS upgrade changes it.</b> A customer who installs a major update needs their license
/// reissued. Handle that in support policy rather than by looking for a component that never changes,
/// because there isn't one.
/// </description></item>
/// <item><description>
/// <b>Identical machines collide.</b> Two freshly imaged VMs with the same name and OS produce the same
/// identifier. Device locking raises the effort of casual copying; it is not a defence against a
/// determined one, and no client-side scheme is.
/// </description></item>
/// </list>
/// <para>
/// The MAC address is deliberately <b>not</b> part of it, the same call the predecessor library made:
/// docks, VPN adapters and USB network devices come and go, and an identifier that changes when
/// somebody unplugs a dock generates support tickets rather than security.
/// </para>
/// </remarks>
public static class DeviceIdentifier
{
    /// <summary>
    /// Returns an identifier for the current machine.
    /// </summary>
    /// <returns>
    /// An opaque string, stable across restarts of the same machine. It is a hash of the underlying
    /// components rather than the machine name in clear text, so a license file does not disclose what
    /// the customer calls their computer.
    /// </returns>
    /// <remarks>
    /// The exact string is an implementation detail of the underlying components and their encoding —
    /// treat it as a token to store and compare, never as something to parse. Two calls on the same
    /// machine agree; that is the whole guarantee.
    /// </remarks>
    public static string Generate() =>
        new DeviceIdBuilder()
            .AddMachineName()
            .AddOsVersion()
            .ToString();
}
