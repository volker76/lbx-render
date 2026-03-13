using System.Collections.Concurrent;
using System.Reflection;
using SkiaSharp;

namespace LbxRender.Rendering;

internal static class FrameAssets
{
    private static readonly ConcurrentDictionary<string, SKBitmap?> Cache = new();
    private static readonly Assembly ThisAssembly = typeof(FrameAssets).Assembly;

    /// <summary>
    /// Loads a frame asset bitmap for the given category and style.
    /// Returns null if no asset exists for that combination.
    /// The returned bitmap is cached and must NOT be disposed by the caller.
    /// </summary>
    public static SKBitmap? Load(string category, int style)
    {
        var key = $"{category.ToUpperInvariant()}_{style}";
        return Cache.GetOrAdd(key, static k =>
        {
            var resourceName = $"LbxRender.Rendering.Frames.{k}.png";
            using var stream = ThisAssembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            return SKBitmap.Decode(stream);
        });
    }

    /// <summary>
    /// Checks whether a frame asset exists for the given category and style.
    /// </summary>
    public static bool Exists(string category, int style)
    {
        var resourceName = $"LbxRender.Rendering.Frames.{category.ToUpperInvariant()}_{style}.png";
        return ThisAssembly.GetManifestResourceNames().Contains(resourceName);
    }
}
