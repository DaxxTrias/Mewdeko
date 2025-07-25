﻿namespace Mewdeko.Services;

/// <summary>
///     Interface for managing and accessing cached images.
/// </summary>
public interface IImageCache
{
    /// <summary>
    ///     Gets the image URLs.
    /// </summary>
    public ImageUrls ImageUrls { get; }

    /// <summary>
    ///     Gets the background image for XP.
    /// </summary>
    public byte[] XpBackground { get; }

    /// <summary>
    ///     Gets the image for RIP (rest in peace).
    /// </summary>
    public byte[] Rip { get; }

    /// <summary>
    ///     Gets the overlay image for RIP.
    /// </summary>
    public byte[] RipOverlay { get; }

    /// <summary>
    ///     Gets a cached image by key.
    /// </summary>
    /// <param name="key">The key associated with the cached image.</param>
    /// <returns>The cached image as a byte array.</returns>
    public byte[] GetCard(string key);

    /// <summary>
    ///     Reloads the image cache.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Reload();
}