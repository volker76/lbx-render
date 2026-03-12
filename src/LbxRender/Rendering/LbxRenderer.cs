using LbxRender.Models;
using SkiaSharp;

namespace LbxRender.Rendering;

public static class LbxRenderer
{
    private const float MmToInch = 1f / 25.4f;

    public static SKBitmap RenderToBitmap(LbxLabel label, RenderOptions? options = null)
    {
        options ??= new RenderOptions();

        var pixelsPerMm = options.Dpi * MmToInch * options.Scale;
        var width = (int)Math.Ceiling(label.Properties.LabelWidthMm * pixelsPerMm);
        var height = (int)Math.Ceiling(label.Properties.LabelHeightMm * pixelsPerMm);

        // Ensure minimum size
        if (width < 1) width = 1;
        if (height < 1) height = 1;

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColor.Parse(options.BackgroundColor));
        canvas.Scale(pixelsPerMm);

        foreach (var element in label.Elements)
        {
            canvas.Save();
            canvas.Translate(element.X, element.Y);
            if (element.Rotation != 0)
                canvas.RotateDegrees(element.Rotation, element.Width / 2, element.Height / 2);

            switch (element)
            {
                case TextElement text:
                    RenderText(canvas, text);
                    break;
                case ImageElement image:
                    RenderImage(canvas, image);
                    break;
                case ShapeElement shape:
                    RenderShape(canvas, shape);
                    break;
                case BarcodeElement barcode:
                    RenderBarcodePlaceholder(canvas, barcode);
                    break;
            }

            canvas.Restore();
        }

        return bitmap;
    }

    public static byte[] RenderToBytes(LbxLabel label, RenderOptions? options = null)
    {
        options ??= new RenderOptions();
        using var bitmap = RenderToBitmap(label, options);
        using var image = SKImage.FromBitmap(bitmap);
        var format = options.Format switch
        {
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
            ImageFormat.Tiff => SKEncodedImageFormat.Png, // SkiaSharp doesn't directly support TIFF encoding; fall back to PNG
            _ => SKEncodedImageFormat.Png
        };
        var quality = options.Format == ImageFormat.Jpeg ? options.JpegQuality : 100;
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    public static void RenderToFile(LbxLabel label, string outputPath, RenderOptions? options = null)
    {
        var bytes = RenderToBytes(label, options);
        File.WriteAllBytes(outputPath, bytes);
    }

    public static void RenderToStream(LbxLabel label, Stream outputStream, RenderOptions? options = null)
    {
        var bytes = RenderToBytes(label, options);
        outputStream.Write(bytes, 0, bytes.Length);
    }

    private static void RenderText(SKCanvas canvas, TextElement text)
    {
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(text.Color),
            IsAntialias = true,
        };

        var textSizeMm = text.FontSize * 25.4f / 72f; // pt to mm
        var typeface = SKTypeface.FromFamilyName(
            text.FontFamily,
            text.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            text.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        using var font = new SKFont(typeface, textSizeMm);

        var y = font.Metrics.CapHeight;
        canvas.DrawText(text.Text, 0, y, font, paint);
    }

    private static void RenderImage(SKCanvas canvas, ImageElement image)
    {
        if (image.ImageData is null) return;

        using var skImage = SKImage.FromEncodedData(image.ImageData);
        if (skImage is null) return;

        var dest = new SKRect(0, 0, image.Width, image.Height);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(skImage, dest, paint);
    }

    private static void RenderShape(SKCanvas canvas, ShapeElement shape)
    {
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(shape.StrokeColor),
            StrokeWidth = shape.StrokeWidth,
            IsAntialias = true,
            Style = shape.FillColor is not null ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Stroke,
        };

        if (shape.FillColor is not null)
            paint.Color = SKColor.Parse(shape.FillColor);

        switch (shape.ShapeType)
        {
            case ShapeType.Rectangle:
                canvas.DrawRect(0, 0, shape.Width, shape.Height, paint);
                break;
            case ShapeType.Line:
                canvas.DrawLine(0, 0, shape.Width, shape.Height, paint);
                break;
            case ShapeType.Ellipse:
                canvas.DrawOval(shape.Width / 2, shape.Height / 2, shape.Width / 2, shape.Height / 2, paint);
                break;
        }
    }

    private static void RenderBarcodePlaceholder(SKCanvas canvas, BarcodeElement barcode)
    {
        // Placeholder: render barcode data as text with a border
        using var borderPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.3f,
            IsAntialias = true,
        };
        canvas.DrawRect(0, 0, barcode.Width, barcode.Height, borderPaint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };
        var fontSize = Math.Min(barcode.Height * 0.3f, 3f);
        using var font = new SKFont(SKTypeface.Default, fontSize);
        canvas.DrawText(barcode.Data, 1, barcode.Height / 2, font, textPaint);
    }
}
