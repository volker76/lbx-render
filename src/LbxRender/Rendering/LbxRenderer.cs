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

        using var font = new SKFont(typeface, text.FontSize);

        var lines = text.Text.Split('\n');
        var lineHeight = font.Spacing;
        var totalTextHeight = lineHeight * lines.Length;

        float yOffset = 0;
        if (text.VerticalAlignment.Equals("CENTER", StringComparison.OrdinalIgnoreCase))
            yOffset = (text.Height - totalTextHeight) / 2f;
        else if (text.VerticalAlignment.Equals("BOTTOM", StringComparison.OrdinalIgnoreCase))
            yOffset = text.Height - totalTextHeight;

        var y = yOffset - font.Metrics.Ascent;

        foreach (var line in lines)
        {
            float x = 0;
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

            DrawTextWithEffect(canvas, line, x, y, font, paint, text.TextEffect);
            DrawTextDecorations(canvas, line, x, y, font, paint, text.Underline, text.Strikeout);
            y += lineHeight;
        }
    }

    private static void RenderTextWithSpans(SKCanvas canvas, TextElement text)
    {
        var lines = text.Text.Split('\n');

        // Use top-level font for line height consistency
        var topTypeface = SKTypeface.FromFamilyName(
            text.FontFamily,
            text.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            text.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        using var topFont = new SKFont(topTypeface, text.FontSize);
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
                    using var sf = CreateSpanFont(span);
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
                    using var spanFont = CreateSpanFont(currentSpan);
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

            // Account for the '\n' separator in global char index
            globalCharIndex++;
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

    private static SKFont CreateSpanFont(TextSpan span)
    {
        var weight = span.Weight >= 700 ? SKFontStyleWeight.Bold :
                     span.Weight >= 500 ? SKFontStyleWeight.Medium :
                     SKFontStyleWeight.Normal;
        var typeface = SKTypeface.FromFamilyName(
            span.FontFamily,
            weight,
            SKFontStyleWidth.Normal,
            span.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        return new SKFont(typeface, span.FontSize);
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

            default: // NOEFFECT
                canvas.DrawText(text, x, y, font, basePaint);
                break;
        }
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
            var writer = new BarcodeWriter
            {
                Format = format.Value,
                Options = new EncodingOptions
                {
                    Width = Math.Max(1, (int)(barcode.Width * 4)),
                    Height = Math.Max(1, (int)(barcode.Height * 4)),
                    PureBarcode = !barcode.HumanReadable,
                    Margin = 0,
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

            using var barcodeBitmap = writer.Write(barcode.Data);
            if (barcodeBitmap is null)
            {
                RenderBarcodePlaceholder(canvas, barcode);
                return;
            }

            using var barcodeImage = SKImage.FromBitmap(barcodeBitmap);
            var dest = new SKRect(0, 0, barcode.Width, barcode.Height);
            using var paint = new SKPaint { IsAntialias = false };
            canvas.DrawImage(barcodeImage, dest, paint);
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
