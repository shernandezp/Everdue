using Microsoft.AspNetCore.DataProtection;

namespace Everdue.Server.Infrastructure.Webhooks;

/// <summary>
/// Signing secrets are encrypted at rest with the key ring already in the data directory — the same one the
/// auth cookies and the channel credentials use, and the same consequence: <strong>lose
/// <c>{DataDir}/keys</c> and the secrets have to be regenerated</strong>, which is why the backup
/// instructions say to back up the directory rather than the database file.
///
/// Its own protector with its own purpose string rather than a reuse of the channel one: purposes exist so
/// ciphertext from one feature cannot be replayed into another.
/// </summary>
public sealed class WebhookSecretProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Everdue.WebhookSecret.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    /// <summary>
    /// Null rather than an exception when the ciphertext cannot be read: a rotated-away key ring must degrade
    /// to "this subscription cannot be signed for", not to an instance that will not start.
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
