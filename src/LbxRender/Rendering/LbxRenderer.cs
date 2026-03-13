using LbxRender.Models;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace LbxRender.Rendering;

public static class LbxRenderer
{
    public static SKBitmap RenderToBitmap(LbxLabel label, RenderOptions? options = null)
    {
        options ??= new RenderOptions();

        var ptToPixel = options.Dpi / 72f * options.Scale;

        // Handle landscape: bitmap width = paper height, bitmap height = paper width
        float bitmapWidthPt, bitmapHeightPt;
        if (label.Properties.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase))
        {
            bitmapWidthPt = label.Properties.LabelHeightPt;
            bitmapHeightPt = label.Properties.LabelWidthPt;
        }
        else
        {
            bitmapWidthPt = label.Properties.LabelWidthPt;
            bitmapHeightPt = label.Properties.LabelHeightPt;
        }

        // Auto-length: compute dimensions from content bounding box
        if (label.Properties.AutoLength && label.Elements.Count > 0)
        {
            float contentRight = 0, contentBottom = 0;
            foreach (var el in label.Elements)
            {
                contentRight = Math.Max(contentRight, el.X + el.Width);
                contentBottom = Math.Max(contentBottom, el.Y + el.Height);
            }

            // Add small padding (2pt)
            contentRight += 2f;
            contentBottom += 2f;

            if (label.Properties.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase))
            {
                // In landscape, the "long" dimension (width) is auto-sized from content
                bitmapWidthPt = Math.Min(bitmapWidthPt, contentRight);
                // Height stays as paper width (tape width)
            }
            else
            {
                // In portrait, height is auto-sized from content
                bitmapHeightPt = Math.Min(bitmapHeightPt, contentBottom);
            }
        }

        var width = Math.Max(1, (int)(bitmapWidthPt * ptToPixel));
        var height = Math.Max(1, (int)(bitmapHeightPt * ptToPixel));

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColor.Parse(options.BackgroundColor));
        canvas.Scale(ptToPixel);

        // Clip all rendering to the label area
        canvas.ClipRect(new SKRect(0, 0, bitmapWidthPt, bitmapHeightPt));

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
                    RenderBarcode(canvas, barcode);
                    break;
                case FrameElement frame:
                    RenderFrame(canvas, frame);
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
            ImageFormat.Tiff => SKEncodedImageFormat.Png,
            _ => SKEncodedImageFormat.Png
        };
        var quality = options.Format == ImageFormat.Jpeg ? options.JpegQuality : 100;
        using var data = image.Encode(format, quality)
                         ?? image.Encode(SKEncodedImageFormat.Png, 100);
        return data!.ToArray();
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

    // ── Text Rendering ──────────────────────────────────────────────────

    private static void RenderText(SKCanvas canvas, TextElement text)
    {
        if (text.Spans.Count > 0)
        {
            RenderTextWithSpans(canvas, text);
            return;
        }

        using var paint = new SKPaint
        {
            Color = SKColor.Parse(text.Color),
            IsAntialias = true,
        };

        var typeface = SKTypeface.FromFamilyName(
            text.FontFamily,
            text.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            text.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        float fontSize = text.FontSize;

        // Shrink-to-fit: reduce font size until text fits within element bounds
        if (text.Shrink)
            fontSize = ComputeShrinkFontSize(text.Text, typeface, fontSize, text.Width, text.Height);

        using var font = new SKFont(typeface, fontSize);

        var lines = text.Text.Replace("\r\n", "\n").Split('\n');
        var lineHeight = font.Spacing;
        var totalTextHeight = lineHeight * lines.Length;

        float yOffset = 0;
        if (text.VerticalAlignment.Equals("CENTER", StringComparison.OrdinalIgnoreCase))
            yOffset = (text.Height - totalTextHeight) / 2f;
        else if (text.VerticalAlignment.Equals("BOTTOM", StringComparison.OrdinalIgnoreCase))
            yOffset = text.Height - totalTextHeight;

        var y = yOffset - font.Metrics.Ascent;
        bool isJustify = text.HorizontalAlignment.Equals("JUSTIFY", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            float x = 0;

            if (isJustify && i < lines.Length - 1 && line.Contains(' '))
            {
                // Justify: distribute extra space between words (except last line)
                DrawJustifiedLine(canvas, line, y, font, paint, text.Width, text.TextEffect, text.Underline, text.Strikeout);
                y += lineHeight;
                continue;
            }

            if (text.HorizontalAlignment.Equals("CENTER", StringComparison.OrdinalIgnoreCase))
            {
                var lineWidth = font.MeasureText(line);
                x = (text.Width - lineWidth) / 2f;
            }
            else if (text.HorizontalAlignment.Equals("RIGHT", StringComparison.OrdinalIgnoreCase))
            {
                var lineWidth = font.MeasureText(line);
                x = text.Width - lineWidth;
            }
            // JUSTIFY last line or single line falls through to LEFT (x=0)

            DrawTextWithEffect(canvas, line, x, y, font, paint, text.TextEffect);
            DrawTextDecorations(canvas, line, x, y, font, paint, text.Underline, text.Strikeout);
            y += lineHeight;
        }
    }

    private static void RenderTextWithSpans(SKCanvas canvas, TextElement text)
    {
        // Track \r\n positions for correct global char index mapping
        var originalText = text.Text;
        var normalizedText = originalText.Replace("\r\n", "\n");
        var lines = normalizedText.Split('\n');

        // Build a mapping from normalized char index to original char index
        // to correctly align spans (whose CharLength counts original chars including \r)
        var crPositions = new List<int>();
        for (int i = 0; i < originalText.Length - 1; i++)
        {
            if (originalText[i] == '\r' && originalText[i + 1] == '\n')
                crPositions.Add(i - crPositions.Count); // normalized position
        }

        // Use top-level font for line height consistency
        var topTypeface = SKTypeface.FromFamilyName(
            text.FontFamily,
            text.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            text.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        float fontSize = text.FontSize;
        if (text.Shrink)
            fontSize = ComputeShrinkFontSize(normalizedText, topTypeface, fontSize, text.Width, text.Height);

        float sizeRatio = fontSize / text.FontSize;

        using var topFont = new SKFont(topTypeface, fontSize);
        var lineHeight = topFont.Spacing;
        var totalTextHeight = lineHeight * lines.Length;

        float yOffset = 0;
        if (text.VerticalAlignment.Equals("CENTER", StringComparison.OrdinalIgnoreCase))
            yOffset = (text.Height - totalTextHeight) / 2f;
        else if (text.VerticalAlignment.Equals("BOTTOM", StringComparison.OrdinalIgnoreCase))
            yOffset = text.Height - totalTextHeight;

        // Build span boundaries from cumulative CharLength
        var spanBounds = new List<(int Start, int End, TextSpan Span)>();
        int cumulative = 0;
        foreach (var span in text.Spans)
        {
            spanBounds.Add((cumulative, cumulative + span.CharLength, span));
            cumulative += span.CharLength;
        }

        int globalCharIndex = 0;
        var y = yOffset - topFont.Metrics.Ascent;

        foreach (var line in lines)
        {
            // Measure full line width for alignment
            float totalLineWidth = 0;
            int tempIdx = globalCharIndex;
            foreach (char c in line)
            {
                var span = FindSpanAt(spanBounds, tempIdx);
                if (span != null)
                {
                    using var sf = CreateSpanFont(span, sizeRatio);
                    totalLineWidth += sf.MeasureText(c.ToString());
                }
                else
                {
                    totalLineWidth += topFont.MeasureText(c.ToString());
                }
                tempIdx++;
            }

            float x = 0;
            if (text.HorizontalAlignment.Equals("CENTER", StringComparison.OrdinalIgnoreCase))
                x = (text.Width - totalLineWidth) / 2f;
            else if (text.HorizontalAlignment.Equals("RIGHT", StringComparison.OrdinalIgnoreCase))
                x = text.Width - totalLineWidth;

            // Render character by character, grouping consecutive chars with same span
            int linePos = 0;
            while (linePos < line.Length)
            {
                var currentSpan = FindSpanAt(spanBounds, globalCharIndex);

                // Collect consecutive chars belonging to same span
                int segStart = linePos;
                while (linePos < line.Length && FindSpanAt(spanBounds, globalCharIndex + (linePos - segStart)) == currentSpan)
                    linePos++;

                var segment = line.Substring(segStart, linePos - segStart);

                if (currentSpan != null)
                {
                    using var spanFont = CreateSpanFont(currentSpan, sizeRatio);
                    using var spanPaint = new SKPaint
                    {
                        Color = SKColor.Parse(currentSpan.Color),
                        IsAntialias = true,
                    };

                    DrawTextWithEffect(canvas, segment, x, y, spanFont, spanPaint, currentSpan.Effect);
                    DrawTextDecorations(canvas, segment, x, y, spanFont, spanPaint, currentSpan.Underline, currentSpan.Strikeout);

                    x += spanFont.MeasureText(segment);
                }
                else
                {
                    using var fallbackPaint = new SKPaint
                    {
                        Color = SKColor.Parse(text.Color),
                        IsAntialias = true,
                    };
                    DrawTextWithEffect(canvas, segment, x, y, topFont, fallbackPaint, text.TextEffect);
                    DrawTextDecorations(canvas, segment, x, y, topFont, fallbackPaint, text.Underline, text.Strikeout);
                    x += topFont.MeasureText(segment);
                }

                globalCharIndex += linePos - segStart;
            }

            // Account for the line separator in global char index
            // \r\n counts as 2 chars in the original string
            int normalizedPos = globalCharIndex;
            globalCharIndex++; // for \n
            if (crPositions.Contains(normalizedPos))
                globalCharIndex++; // extra \r
            y += lineHeight;
        }
    }

    private static TextSpan? FindSpanAt(List<(int Start, int End, TextSpan Span)> bounds, int charIndex)
    {
        foreach (var (start, end, span) in bounds)
        {
            if (charIndex >= start && charIndex < end)
                return span;
        }
        return null;
    }

    private static SKFont CreateSpanFont(TextSpan span, float sizeRatio = 1f)
    {
        var weight = span.Weight >= 700 ? SKFontStyleWeight.Bold :
                     span.Weight >= 500 ? SKFontStyleWeight.Medium :
                     SKFontStyleWeight.Normal;
        var typeface = SKTypeface.FromFamilyName(
            span.FontFamily,
            weight,
            SKFontStyleWidth.Normal,
            span.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        return new SKFont(typeface, span.FontSize * sizeRatio);
    }

    // ── Text Effects ────────────────────────────────────────────────────

    private static void DrawTextWithEffect(SKCanvas canvas, string text, float x, float y,
        SKFont font, SKPaint basePaint, string effect)
    {
        var fontSize = font.Size;

        switch (effect.ToUpperInvariant())
        {
            case "OUTLINE":
                using (var strokePaint = basePaint.Clone())
                {
                    strokePaint.Style = SKPaintStyle.Stroke;
                    strokePaint.StrokeWidth = fontSize * 0.03f;
                    canvas.DrawText(text, x, y, font, strokePaint);
                }
                break;

            case "SHADOW":
                using (var shadowPaint = basePaint.Clone())
                {
                    var offset = fontSize * 0.06f;
                    shadowPaint.Color = new SKColor(128, 128, 128);
                    canvas.DrawText(text, x + offset, y + offset, font, shadowPaint);
                }
                canvas.DrawText(text, x, y, font, basePaint);
                break;

            case "SHADOWLIGHT":
                using (var shadowPaint = basePaint.Clone())
                {
                    var offset = fontSize * 0.04f;
                    shadowPaint.Color = new SKColor(192, 192, 192);
                    canvas.DrawText(text, x + offset, y + offset, font, shadowPaint);
                }
                canvas.DrawText(text, x, y, font, basePaint);
                break;

            case "SURROUND":
                // Draw white/background outline first for contrast, then fill on top
                using (var bgStrokePaint = new SKPaint())
                {
                    bgStrokePaint.Color = SKColors.White;
                    bgStrokePaint.Style = SKPaintStyle.Stroke;
                    bgStrokePaint.StrokeWidth = fontSize * 0.08f;
                    bgStrokePaint.IsAntialias = true;
                    canvas.DrawText(text, x, y, font, bgStrokePaint);
                }
                using (var strokePaint = basePaint.Clone())
                {
                    strokePaint.Style = SKPaintStyle.Stroke;
                    strokePaint.StrokeWidth = fontSize * 0.03f;
                    canvas.DrawText(text, x, y, font, strokePaint);
                }
                canvas.DrawText(text, x, y, font, basePaint);
                break;

            case "HORIZONTAL":
                // Striped/hatched text: draw text then overlay horizontal lines as mask
                {
                    // Measure text bounds
                    var textWidth = font.MeasureText(text);
                    var metrics = font.Metrics;
                    float top = y + metrics.Ascent;
                    float bottom = y + metrics.Descent;
                    float height = bottom - top;

                    if (textWidth > 0 && height > 0)
                    {
                        // Create a 2px-tall repeating tile: 1px colored, 1px transparent
                        using var tileBitmap = new SKBitmap(1, 2, SKColorType.Rgba8888, SKAlphaType.Premul);
                        tileBitmap.SetPixel(0, 0, basePaint.Color);
                        tileBitmap.SetPixel(0, 1, SKColors.Transparent);

                        using var stripedPaint = basePaint.Clone();
                        stripedPaint.Shader = SKShader.CreateBitmap(
                            tileBitmap,
                            SKShaderTileMode.Repeat,
                            SKShaderTileMode.Repeat);

                        canvas.DrawText(text, x, y, font, stripedPaint);
                    }
                }
                break;

            case "REVERSAL":
                // Inverted text: black rectangle behind, white text on top
                {
                    var textWidth = font.MeasureText(text);
                    var metrics = font.Metrics;
                    float top = y + metrics.Ascent;
                    float bottom = y + metrics.Descent;

                    if (textWidth > 0)
                    {
                        using var bgPaint = new SKPaint
                        {
                            Color = SKColors.Black,
                            Style = SKPaintStyle.Fill,
                        };
                        canvas.DrawRect(x, top, textWidth, bottom - top, bgPaint);

                        using var whitePaint = new SKPaint
                        {
                            Color = SKColors.White,
                            IsAntialias = true,
                        };
                        canvas.DrawText(text, x, y, font, whitePaint);
                    }
                }
                break;

            default: // NOEFFECT
                canvas.DrawText(text, x, y, font, basePaint);
                break;
        }
    }

    // ── Justified Text ──────────────────────────────────────────────────

    private static void DrawJustifiedLine(SKCanvas canvas, string line, float y,
        SKFont font, SKPaint paint, float availableWidth, string effect, bool underline, bool strikeout)
    {
        var words = line.Split(' ');
        if (words.Length <= 1)
        {
            DrawTextWithEffect(canvas, line, 0, y, font, paint, effect);
            DrawTextDecorations(canvas, line, 0, y, font, paint, underline, strikeout);
            return;
        }

        float totalWordWidth = 0;
        foreach (var word in words)
            totalWordWidth += font.MeasureText(word);

        float totalSpacing = availableWidth - totalWordWidth;
        float spaceBetween = totalSpacing / (words.Length - 1);

        float x = 0;
        for (int w = 0; w < words.Length; w++)
        {
            DrawTextWithEffect(canvas, words[w], x, y, font, paint, effect);
            DrawTextDecorations(canvas, words[w], x, y, font, paint, underline, strikeout);
            x += font.MeasureText(words[w]) + spaceBetween;
        }
    }

    // ── Shrink-to-fit ──────────────────────────────────────────────────

    private static float ComputeShrinkFontSize(string text, SKTypeface typeface, float nominalSize, float maxWidth, float maxHeight)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Check if nominal size fits
        using var testFont = new SKFont(typeface, nominalSize);
        if (TextFits(lines, testFont, maxWidth, maxHeight))
            return nominalSize;

        // Binary search between 1pt and nominal size
        float lo = 1f, hi = nominalSize;
        for (int i = 0; i < 20; i++)
        {
            float mid = (lo + hi) / 2f;
            using var midFont = new SKFont(typeface, mid);
            if (TextFits(lines, midFont, maxWidth, maxHeight))
                lo = mid;
            else
                hi = mid;
        }
        return lo;
    }

    private static bool TextFits(string[] lines, SKFont font, float maxWidth, float maxHeight)
    {
        float totalHeight = font.Spacing * lines.Length;
        if (totalHeight > maxHeight) return false;

        foreach (var line in lines)
        {
            if (font.MeasureText(line) > maxWidth)
                return false;
        }
        return true;
    }

    // ── Text Decorations (Underline / Strikeout) ────────────────────────

    private static void DrawTextDecorations(SKCanvas canvas, string text, float x, float y,
        SKFont font, SKPaint basePaint, bool underline, bool strikeout)
    {
        if (!underline && !strikeout) return;

        var textWidth = font.MeasureText(text);
        if (textWidth <= 0) return;

        var metrics = font.Metrics;
        var strokeWidth = font.Size * 0.05f;

        using var linePaint = new SKPaint
        {
            Color = basePaint.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
        };

        if (underline)
        {
            // Position below baseline using font metrics
            var underlineY = y + (metrics.UnderlinePosition ?? font.Size * 0.15f);
            canvas.DrawLine(x, underlineY, x + textWidth, underlineY, linePaint);
        }

        if (strikeout)
        {
            // Position at roughly middle of x-height
            var strikeY = y + (metrics.StrikeoutPosition ?? -font.Size * 0.3f);
            canvas.DrawLine(x, strikeY, x + textWidth, strikeY, linePaint);
        }
    }

    // ── Image Rendering ─────────────────────────────────────────────────

    private static void RenderImage(SKCanvas canvas, ImageElement image)
    {
        if (image.ImageData is null)
        {
            if (image.ImageType != ImageType.EmbeddedBitmap)
            {
                using var placeholderPaint = new SKPaint
                {
                    Color = SKColors.LightGray,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                };
                canvas.DrawRect(0, 0, image.Width, image.Height, placeholderPaint);

                using var borderPaint = new SKPaint
                {
                    Color = SKColors.Gray,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 0.5f,
                    IsAntialias = true,
                };
                canvas.DrawRect(0, 0, image.Width, image.Height, borderPaint);

                using var textPaint = new SKPaint { Color = SKColors.DarkGray, IsAntialias = true };
                var label = image.ImageType == ImageType.Clipart ? "Clipart" : "Picture";
                var fontSize = Math.Min(image.Height * 0.3f, 8f);
                using var font = new SKFont(SKTypeface.Default, fontSize);
                canvas.DrawText(label, 2, image.Height / 2, font, textPaint);
            }
            return;
        }

        using var skImage = SKImage.FromEncodedData(image.ImageData);
        if (skImage is null) return;

        var dest = new SKRect(0, 0, image.Width, image.Height);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(skImage, dest, paint);
    }

    // ── Shape Rendering ─────────────────────────────────────────────────

    private static void RenderShape(SKCanvas canvas, ShapeElement shape)
    {
        using var strokePaint = CreatePenPaint(shape);
        using var fillPaint = CreateBrushPaint(shape);

        switch (shape.ShapeType)
        {
            case ShapeType.Rectangle:
                if (fillPaint is not null) canvas.DrawRect(0, 0, shape.Width, shape.Height, fillPaint);
                if (strokePaint is not null) canvas.DrawRect(0, 0, shape.Width, shape.Height, strokePaint);
                break;

            case ShapeType.RoundedRectangle:
                var rx = shape.RoundnessX;
                var ry = shape.RoundnessY;
                var rrect = new SKRoundRect(new SKRect(0, 0, shape.Width, shape.Height), rx, ry);
                if (fillPaint is not null) canvas.DrawRoundRect(rrect, fillPaint);
                if (strokePaint is not null) canvas.DrawRoundRect(rrect, strokePaint);
                break;

            case ShapeType.Ellipse:
                if (fillPaint is not null) canvas.DrawOval(shape.Width / 2, shape.Height / 2, shape.Width / 2, shape.Height / 2, fillPaint);
                if (strokePaint is not null) canvas.DrawOval(shape.Width / 2, shape.Height / 2, shape.Width / 2, shape.Height / 2, strokePaint);
                break;

            case ShapeType.Line:
            case ShapeType.Polyline:
            case ShapeType.Polygon:
                RenderPoly(canvas, shape, strokePaint, fillPaint);
                break;
        }
    }

    private static void RenderPoly(SKCanvas canvas, ShapeElement shape, SKPaint? strokePaint, SKPaint? fillPaint)
    {
        if (shape.Points.Count < 2) return;

        using var path = new SKPath();
        var first = shape.Points[0];
        path.MoveTo(first.X - shape.X, first.Y - shape.Y);

        for (int i = 1; i < shape.Points.Count; i++)
        {
            var pt = shape.Points[i];
            path.LineTo(pt.X - shape.X, pt.Y - shape.Y);
        }

        if (shape.ShapeType == ShapeType.Polygon)
            path.Close();

        if (fillPaint is not null) canvas.DrawPath(path, fillPaint);
        if (strokePaint is not null) canvas.DrawPath(path, strokePaint);
    }

    // ── Frame Rendering ─────────────────────────────────────────────────

    private static void RenderFrame(SKCanvas canvas, FrameElement frame)
    {
        if (frame.PenStyle == "NULL") return;

        // Try to load a pre-rendered frame asset for this category+style
        if (int.TryParse(frame.Style, out var styleId))
        {
            var asset = FrameAssets.Load(frame.Category, styleId);
            if (asset is not null)
            {
                RenderFrameAsset(canvas, frame, asset);
                return;
            }
        }

        // Fallback: simple rectangle stroke
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(frame.PenColor),
            StrokeWidth = frame.PenWidthX,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };
        ApplyDashEffect(paint, frame.PenStyle, frame.PenWidthX);

        canvas.DrawRect(0, 0, frame.Width, frame.Height, paint);
    }

    private static void RenderFrameAsset(SKCanvas canvas, FrameElement frame, SKBitmap asset)
    {
        var dest = new SKRect(0, 0, frame.Width, frame.Height);

        // The asset is black artwork with alpha. Apply pen color via a color filter.
        var penColor = SKColor.Parse(frame.PenColor);

        using var paint = new SKPaint
        {
            IsAntialias = true,
        };

        // If pen color is not black, tint the frame artwork to the pen color.
        // The asset has black (0,0,0) pixels with varying alpha.
        // We want to replace black with the pen color while preserving alpha.
        if (penColor != SKColors.Black)
        {
            paint.ColorFilter = SKColorFilter.CreateBlendMode(penColor, SKBlendMode.SrcIn);
        }

        canvas.DrawBitmap(asset, dest, paint);
    }

    // ── Barcode Rendering ───────────────────────────────────────────────

    private static void RenderBarcode(SKCanvas canvas, BarcodeElement barcode)
    {
        var format = MapBarcodeFormat(barcode.Protocol);
        if (format is null)
        {
            RenderBarcodePlaceholder(canvas, barcode);
            return;
        }

        try
        {
            // Determine the module/cell size in points
            bool is2D = format == BarcodeFormat.QR_CODE || format == BarcodeFormat.DATA_MATRIX
                        || format == BarcodeFormat.PDF_417;

            float cellSizePt;
            if (format == BarcodeFormat.QR_CODE && barcode.QrCellSize > 0)
                cellSizePt = barcode.QrCellSize;
            else if (format == BarcodeFormat.DATA_MATRIX && barcode.DmCellSize > 0)
                cellSizePt = barcode.DmCellSize;
            else if (barcode.BarWidth > 0)
                cellSizePt = barcode.BarWidth;
            else
                cellSizePt = 0.8f; // default

            // Generate barcode at 1px = 1 module so we know the module count
            var writer = new BarcodeWriter
            {
                Format = format.Value,
                Options = new EncodingOptions
                {
                    PureBarcode = true, // we'll draw HR text ourselves
                    Margin = 0,
                    Width = 0,
                    Height = 0,
                }
            };

            // Set QR error correction level
            if (format == BarcodeFormat.QR_CODE && barcode.QrEccLevel is not null)
            {
                var eccLevel = barcode.QrEccLevel switch
                {
                    "7%" => ZXing.QrCode.Internal.ErrorCorrectionLevel.L,
                    "15%" => ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
                    "25%" => ZXing.QrCode.Internal.ErrorCorrectionLevel.Q,
                    "30%" => ZXing.QrCode.Internal.ErrorCorrectionLevel.H,
                    _ => ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
                };
                writer.Options.Hints[EncodeHintType.ERROR_CORRECTION] = eccLevel;
            }

            using var minBitmap = writer.Write(barcode.Data);
            if (minBitmap is null)
            {
                RenderBarcodePlaceholder(canvas, barcode);
                return;
            }

            int moduleCountX = minBitmap.Width;
            int moduleCountY = minBitmap.Height;

            // Compute the natural barcode size in points
            float barcodeWidthPt = moduleCountX * cellSizePt;
            float barcodeHeightPt;

            if (is2D)
            {
                barcodeHeightPt = moduleCountY * cellSizePt;
            }
            else
            {
                // 1D barcodes: height fills the element box (minus HR text area)
                barcodeHeightPt = barcode.Height;
                if (barcode.HumanReadable)
                    barcodeHeightPt -= cellSizePt * 12; // approximate text height
                barcodeHeightPt = Math.Max(barcodeHeightPt, cellSizePt * 4);
            }

            // Clamp to element bounds
            barcodeWidthPt = Math.Min(barcodeWidthPt, barcode.Width);
            barcodeHeightPt = Math.Min(barcodeHeightPt, barcode.Height);

            // Generate at target pixel resolution (4px per point for sharp edges)
            const float pxPerPt = 4f;
            int targetW = Math.Max(1, (int)(barcodeWidthPt * pxPerPt));
            int targetH = Math.Max(1, (int)(barcodeHeightPt * pxPerPt));

            var renderWriter = new BarcodeWriter
            {
                Format = format.Value,
                Options = new EncodingOptions
                {
                    Width = targetW,
                    Height = targetH,
                    PureBarcode = true,
                    Margin = 0,
                }
            };
            if (writer.Options.Hints.Count > 0)
            {
                foreach (var hint in writer.Options.Hints)
                    renderWriter.Options.Hints[hint.Key] = hint.Value;
            }

            using var barcodeBitmap = renderWriter.Write(barcode.Data);
            if (barcodeBitmap is null)
            {
                RenderBarcodePlaceholder(canvas, barcode);
                return;
            }

            // Draw the barcode at its natural size within the bounding box
            using var barcodeImage = SKImage.FromBitmap(barcodeBitmap);
            var dest = new SKRect(0, 0, barcodeWidthPt, barcodeHeightPt);
            using var paint = new SKPaint { IsAntialias = false };
            canvas.DrawImage(barcodeImage, dest, paint);

            // Draw human-readable text below the barcode
            if (barcode.HumanReadable)
            {
                float hrFontSize = Math.Min(cellSizePt * 10, barcodeHeightPt * 0.15f);
                hrFontSize = Math.Max(hrFontSize, 3f);
                using var hrFont = new SKFont(SKTypeface.FromFamilyName("Courier New"), hrFontSize);
                using var hrPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

                float hrY = barcodeHeightPt + hrFont.Spacing;
                float hrX = 0;

                if (barcode.HumanReadableAlignment.Equals("CENTER", StringComparison.OrdinalIgnoreCase))
                    hrX = (barcodeWidthPt - hrFont.MeasureText(barcode.Data)) / 2f;
                else if (barcode.HumanReadableAlignment.Equals("RIGHT", StringComparison.OrdinalIgnoreCase))
                    hrX = barcodeWidthPt - hrFont.MeasureText(barcode.Data);

                canvas.DrawText(barcode.Data, Math.Max(0, hrX), hrY, hrFont, hrPaint);
            }
        }
        catch
        {
            RenderBarcodePlaceholder(canvas, barcode);
        }
    }

    private static BarcodeFormat? MapBarcodeFormat(string protocol)
    {
        return protocol.ToUpperInvariant() switch
        {
            "CODE39" => BarcodeFormat.CODE_39,
            "CODE128" => BarcodeFormat.CODE_128,
            "QR" or "QRCODE" => BarcodeFormat.QR_CODE,
            "QRMICRO" => BarcodeFormat.QR_CODE, // fallback — ZXing doesn't support Micro QR
            "DATAMATRIX" => BarcodeFormat.DATA_MATRIX,
            "EAN13" => BarcodeFormat.EAN_13,
            "EAN8" => BarcodeFormat.EAN_8,
            "UPCA" => BarcodeFormat.UPC_A,
            "UPCE" => BarcodeFormat.UPC_E,
            "ITF" => BarcodeFormat.ITF,
            "CODABAR" or "NW7" => BarcodeFormat.CODABAR,
            "PDF417" => BarcodeFormat.PDF_417,
            _ => null,
        };
    }

    private static void RenderBarcodePlaceholder(SKCanvas canvas, BarcodeElement barcode)
    {
        using var borderPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.3f,
            IsAntialias = true,
        };
        canvas.DrawRect(0, 0, barcode.Width, barcode.Height, borderPaint);

        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        var fontSize = Math.Min(barcode.Height * 0.2f, 6f);
        using var font = new SKFont(SKTypeface.Default, fontSize);

        canvas.DrawText($"[{barcode.Protocol}]", 1, fontSize + 1, font, textPaint);
        canvas.DrawText(barcode.Data, 1, barcode.Height / 2 + fontSize / 2, font, textPaint);
    }

    // ── Paint Helpers ───────────────────────────────────────────────────

    private static SKPaint? CreatePenPaint(LbxElement element)
    {
        if (element.PenStyle == "NULL") return null;

        var paint = new SKPaint
        {
            Color = SKColor.Parse(element.PenColor),
            StrokeWidth = element.PenWidthX,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        ApplyDashEffect(paint, element.PenStyle, element.PenWidthX);
        return paint;
    }

    private static SKPaint? CreateBrushPaint(LbxElement element)
    {
        if (element.BrushStyle == "NULL") return null;

        var color = SKColor.Parse(element.BrushColor);

        var paint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        // Apply hatched pattern for PATTERN and DIBPATTERN brush styles
        if (element.BrushStyle == "PATTERN")
        {
            var patternShader = CreateHatchPattern(element.BrushPatternId, color);
            if (patternShader is not null)
                paint.Shader = patternShader;
        }
        else if (element.BrushStyle == "DIBPATTERN")
        {
            var patternShader = CreateDibPattern(element.BrushPatternId, color);
            if (patternShader is not null)
                paint.Shader = patternShader;
        }

        return paint;
    }

    private static SKShader? CreateHatchPattern(int patternId, SKColor color)
    {
        const int tileSize = 8;
        using var tileBitmap = new SKBitmap(tileSize, tileSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var tileCanvas = new SKCanvas(tileBitmap);
        tileCanvas.Clear(SKColors.Transparent);

        using var linePaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = false,
        };

        switch (patternId)
        {
            case 0: // Horizontal lines
                tileCanvas.DrawLine(0, 4, 8, 4, linePaint);
                break;
            case 1: // Vertical lines
                tileCanvas.DrawLine(4, 0, 4, 8, linePaint);
                break;
            case 2: // Forward diagonal (\)
                tileCanvas.DrawLine(0, 0, 8, 8, linePaint);
                break;
            case 3: // Backward diagonal (/)
                tileCanvas.DrawLine(8, 0, 0, 8, linePaint);
                break;
            case 4: // Cross (+)
                tileCanvas.DrawLine(0, 4, 8, 4, linePaint);
                tileCanvas.DrawLine(4, 0, 4, 8, linePaint);
                break;
            case 5: // Diagonal cross (X)
                tileCanvas.DrawLine(0, 0, 8, 8, linePaint);
                tileCanvas.DrawLine(8, 0, 0, 8, linePaint);
                break;
            // Higher IDs — denser variations
            case 6: // Dense horizontal
                tileCanvas.DrawLine(0, 2, 8, 2, linePaint);
                tileCanvas.DrawLine(0, 6, 8, 6, linePaint);
                break;
            case 7: // Dense vertical
                tileCanvas.DrawLine(2, 0, 2, 8, linePaint);
                tileCanvas.DrawLine(6, 0, 6, 8, linePaint);
                break;
            case 8: // Dense forward diagonal
                tileCanvas.DrawLine(0, 0, 8, 8, linePaint);
                tileCanvas.DrawLine(0, 4, 4, 8, linePaint);
                tileCanvas.DrawLine(4, 0, 8, 4, linePaint);
                break;
            case 9: // Dense backward diagonal
                tileCanvas.DrawLine(8, 0, 0, 8, linePaint);
                tileCanvas.DrawLine(4, 0, 0, 4, linePaint);
                tileCanvas.DrawLine(8, 4, 4, 8, linePaint);
                break;
            default:
                return null; // Fall back to solid fill
        }

        return SKShader.CreateBitmap(
            tileBitmap,
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat);
    }

    private static SKShader? CreateDibPattern(int patternId, SKColor color)
    {
        // DIBPATTERN uses device-independent bitmap patterns (dot/stipple based)
        // These are Brother-specific predefined patterns distinct from GDI hatch lines
        const int tileSize = 8;
        using var tileBitmap = new SKBitmap(tileSize, tileSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var tileCanvas = new SKCanvas(tileBitmap);
        tileCanvas.Clear(SKColors.Transparent);

        using var dotPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };

        switch (patternId)
        {
            case 0: // Sparse dot grid
                tileBitmap.SetPixel(0, 0, color);
                tileBitmap.SetPixel(4, 4, color);
                break;
            case 1: // Medium density stipple
                tileBitmap.SetPixel(0, 0, color);
                tileBitmap.SetPixel(4, 2, color);
                tileBitmap.SetPixel(2, 4, color);
                tileBitmap.SetPixel(6, 6, color);
                tileBitmap.SetPixel(0, 4, color);
                tileBitmap.SetPixel(4, 0, color);
                tileBitmap.SetPixel(2, 6, color);
                tileBitmap.SetPixel(6, 2, color);
                break;
            case 2: // Dense stipple
                tileBitmap.SetPixel(0, 0, color);
                tileBitmap.SetPixel(2, 0, color);
                tileBitmap.SetPixel(4, 0, color);
                tileBitmap.SetPixel(6, 0, color);
                tileBitmap.SetPixel(1, 1, color);
                tileBitmap.SetPixel(3, 1, color);
                tileBitmap.SetPixel(5, 1, color);
                tileBitmap.SetPixel(7, 1, color);
                tileBitmap.SetPixel(0, 2, color);
                tileBitmap.SetPixel(2, 2, color);
                tileBitmap.SetPixel(4, 2, color);
                tileBitmap.SetPixel(6, 2, color);
                tileBitmap.SetPixel(1, 3, color);
                tileBitmap.SetPixel(3, 3, color);
                tileBitmap.SetPixel(5, 3, color);
                tileBitmap.SetPixel(7, 3, color);
                tileBitmap.SetPixel(0, 4, color);
                tileBitmap.SetPixel(2, 4, color);
                tileBitmap.SetPixel(4, 4, color);
                tileBitmap.SetPixel(6, 4, color);
                tileBitmap.SetPixel(1, 5, color);
                tileBitmap.SetPixel(3, 5, color);
                tileBitmap.SetPixel(5, 5, color);
                tileBitmap.SetPixel(7, 5, color);
                tileBitmap.SetPixel(0, 6, color);
                tileBitmap.SetPixel(2, 6, color);
                tileBitmap.SetPixel(4, 6, color);
                tileBitmap.SetPixel(6, 6, color);
                tileBitmap.SetPixel(1, 7, color);
                tileBitmap.SetPixel(3, 7, color);
                tileBitmap.SetPixel(5, 7, color);
                tileBitmap.SetPixel(7, 7, color);
                break;
            case 3: // Diagonal dot pattern
                tileBitmap.SetPixel(0, 0, color);
                tileBitmap.SetPixel(1, 1, color);
                tileBitmap.SetPixel(2, 2, color);
                tileBitmap.SetPixel(3, 3, color);
                tileBitmap.SetPixel(4, 4, color);
                tileBitmap.SetPixel(5, 5, color);
                tileBitmap.SetPixel(6, 6, color);
                tileBitmap.SetPixel(7, 7, color);
                break;
            case 4: // Cross dot pattern
                tileBitmap.SetPixel(0, 0, color);
                tileBitmap.SetPixel(4, 0, color);
                tileBitmap.SetPixel(0, 4, color);
                tileBitmap.SetPixel(4, 4, color);
                tileBitmap.SetPixel(2, 2, color);
                tileBitmap.SetPixel(6, 2, color);
                tileBitmap.SetPixel(2, 6, color);
                tileBitmap.SetPixel(6, 6, color);
                break;
            case 5: // Dense cross stipple
                for (int y = 0; y < tileSize; y++)
                    for (int x = 0; x < tileSize; x++)
                        if ((x + y) % 2 == 0)
                            tileBitmap.SetPixel(x, y, color);
                break;
            default:
                return null;
        }

        return SKShader.CreateBitmap(
            tileBitmap,
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat);
    }

    private static void ApplyDashEffect(SKPaint paint, string penStyle, float width)
    {
        var w = Math.Max(width, 0.5f);
        switch (penStyle)
        {
            case "DASH":
                paint.PathEffect = SKPathEffect.CreateDash([w * 6, w * 3], 0);
                break;
            case "DASHDOT":
                paint.PathEffect = SKPathEffect.CreateDash([w * 6, w * 2, w * 1, w * 2], 0);
                break;
            case "DOT":
                paint.PathEffect = SKPathEffect.CreateDash([w * 1, w * 2], 0);
                break;
        }
    }
}
