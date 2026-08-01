using Everdue.Server.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Everdue.Server.Infrastructure.Files;

/// <summary>
/// Files under <c>{DataDir}/files</c> — the same directory as the database and the key ring, so the
/// backup instruction stays one sentence: copy the data directory.
///
/// Keys are <c>{tenantId}/{attachmentId}</c>, both GUIDs. The resolved path is still checked against
/// the root before any I/O: "the caller always passes a safe key" is exactly the assumption that
/// stops being true the day somebody adds a second caller.
/// </summary>
public sealed class LocalDiskFileStore : IFileStore
{
    private readonly string _root;

    public LocalDiskFileStore(IConfiguration configuration)
    {
        _root = Path.Combine(InfrastructureServiceCollectionExtensions.ResolveDataDirectory(configuration), "files");
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Attachment key '{key}' resolves outside the file store.");
        }

        return path;
    }
}
