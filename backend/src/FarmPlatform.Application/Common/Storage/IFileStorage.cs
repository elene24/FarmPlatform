namespace FarmPlatform.Application.Common.Storage;

/// <summary>
/// Object storage for uploaded files (Cloudflare R2 in production). Only the returned storage key is
/// persisted in PostgreSQL, never the file bytes or the user-supplied filename.
/// Implemented in a later phase together with the profile photo endpoints.
/// </summary>
public interface IFileStorage
{
    Task<string> UploadAsync(Stream content, string contentType, string keyPrefix, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Returns a URL the browser can use to display the file (public CDN or short-lived signed URL).</summary>
    Uri GetReadUrl(string storageKey);
}
