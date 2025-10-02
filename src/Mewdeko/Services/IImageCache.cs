namespace Mewdeko.Services;

/// <summary>
///     Interface for managing and accessing cached images.
/// </summary>
public interface IImageCache
{
    /// <summary>
    ///     Gets the image for RIP (rest in peace).
    /// </summary>
    public byte[] Rip { get; }

    /// <summary>
    ///     Gets the overlay image for RIP.
    /// </summary>
    public byte[] RipOverlay { get; }

    /// <summary>
    ///     Reloads the image cache.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Reload();
}