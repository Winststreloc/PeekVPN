using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using Svg.Skia;

namespace PeekVPN.App.Controls;

/// <summary>
/// Renders an Avalonia SVG asset into an <see cref="Image"/> via Svg.Skia.
/// Bitmaps are cached by URI for reuse (e.g. country flags in lists).
/// </summary>
public sealed class SvgImage : Image
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new(StringComparer.Ordinal);

    public static readonly StyledProperty<Uri?> SourceUriProperty =
        AvaloniaProperty.Register<SvgImage, Uri?>(nameof(SourceUri));

    public static readonly StyledProperty<int> RasterWidthProperty =
        AvaloniaProperty.Register<SvgImage, int>(nameof(RasterWidth), 64);

    public Uri? SourceUri
    {
        get => GetValue(SourceUriProperty);
        set => SetValue(SourceUriProperty, value);
    }

    /// <summary>Target raster width used when encoding the SVG (keeps list flags sharp).</summary>
    public int RasterWidth
    {
        get => GetValue(RasterWidthProperty);
        set => SetValue(RasterWidthProperty, value);
    }

    static SvgImage()
    {
        SourceUriProperty.Changed.AddClassHandler<SvgImage>((image, _) => image.Reload());
        RasterWidthProperty.Changed.AddClassHandler<SvgImage>((image, _) => image.Reload());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Reload();
    }

    private void Reload()
    {
        if (SourceUri is null)
        {
            Source = null;
            return;
        }

        var cacheKey = $"{SourceUri.AbsoluteUri}|{RasterWidth}";
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            Source = cached;
            return;
        }

        try
        {
            using var stream = AssetLoader.Open(SourceUri);
            using var svg = new SKSvg();
            svg.Load(stream);

            if (svg.Picture is null)
            {
                Source = null;
                return;
            }

            var bounds = svg.Picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Source = null;
                return;
            }

            var targetWidth = Math.Max(16, RasterWidth);
            var scale = targetWidth / bounds.Width;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.DrawPicture(svg.Picture);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            var bitmap = new Bitmap(ms);
            Cache[cacheKey] = bitmap;
            Source = bitmap;
        }
        catch
        {
            Source = null;
        }
    }
}
