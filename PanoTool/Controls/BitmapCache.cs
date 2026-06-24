using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace Zenkei.Controls;

/// <summary>
/// Caches downscaled editor bitmaps keyed by resolved absolute image path.
/// Decoded at <see cref="MaxEditorWidth"/> px wide — sufficient for the editor canvas
/// at any practical display resolution. Export uses original source files directly.
///
/// WeakReference values let GC reclaim bitmaps when no canvas holds a strong ref.
/// Must be accessed on the Avalonia UI thread only (same thread as LoadSceneImage).
/// </summary>
internal static class BitmapCache
{
    // Set from SettingsService.Apply() on startup and when preferences change.
    // Full-res export copies source files directly via File.Copy.
    internal static int MaxEditorWidth { get; set; } = 2048;

    private static readonly Dictionary<string, WeakReference<Bitmap>> _cache = new();

    /// <summary>
    /// Returns a cached downscaled bitmap for <paramref name="resolvedPath"/>,
    /// decoding and caching it on the first call or after GC eviction.
    /// Returns null if the file is missing or unreadable.
    /// </summary>
    public static Bitmap? Get(string resolvedPath)
    {
        if (_cache.TryGetValue(resolvedPath, out var weakRef)
            && weakRef.TryGetTarget(out var cached))
            return cached;

        if (!File.Exists(resolvedPath)) return null;

        try
        {
            using var stream = File.OpenRead(resolvedPath);
            var bmp = Bitmap.DecodeToWidth(stream, MaxEditorWidth, BitmapInterpolationMode.MediumQuality);
            _cache[resolvedPath] = new WeakReference<Bitmap>(bmp);
            return bmp;
        }
        catch { return null; }
    }

    /// <summary>
    /// Drops the cache entry for <paramref name="resolvedPath"/> so the next
    /// <see cref="Get"/> call re-decodes from disk. Call this before changing
    /// a scene's image so the old bitmap is not served to the next open.
    /// </summary>
    public static void Invalidate(string resolvedPath) => _cache.Remove(resolvedPath);
}
