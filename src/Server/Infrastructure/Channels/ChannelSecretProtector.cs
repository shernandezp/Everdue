using Microsoft.AspNetCore.DataProtection;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>
/// Channel credentials are encrypted at rest with the key ring that already lives in the data
/// directory (the same one the auth cookies use). The consequence is worth stating where a
/// self-hoster will read it: **lose <c>{DataDir}/keys</c> and the channel credentials have to be
/// re-entered** — which is the same trade the auth cookies already make, and is why the backup
/// instructions say "back up the directory", not "back up the database".
/// </summary>
public sealed class ChannelSecretProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Everdue.ChannelSettings.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    /// <summary>
    /// Returns null rather than throwing when the ciphertext cannot be read: a rotated-away key ring
    /// must degrade to "this channel is not configured", not to an instance that will not start.
    /// </summary>
    public string? TryUnprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
