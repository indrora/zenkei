// Generates Assets/zenkei.ico — a multi-resolution Windows icon for the Zenkei app.
// Usage: dotnet run -- <output-path>
// Produces a PNG-in-ICO with sizes 256, 48, 32, 16 px.

using SkiaSharp;

var outPath = args.Length > 0 ? args[0] : "zenkei.ico";

var sizes = new[] { 256, 48, 32, 16 };
var pngs = sizes.Select(sz => RenderIcon(sz)).ToArray();

WriteIco(outPath, pngs, sizes);
Console.WriteLine($"Wrote {outPath}");

// ── Icon rendering ────────────────────────────────────────────────────────────

static byte[] RenderIcon(int size)
{
    using var bmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);

    float s = size;
    float cx = s / 2f;
    float cy = s / 2f;
    float r = s / 2f - 1f;

    // Background: deep indigo circle
    using var bgPaint = new SKPaint
    {
        IsAntialias = true,
        Color = new SKColor(18, 14, 52),   // #120E34 — dark indigo
    };
    canvas.DrawCircle(cx, cy, r, bgPaint);

    // Panoramic horizon band — thin amber stripe across the middle
    float bandH = Math.Max(1f, s * 0.07f);
    using var bandPaint = new SKPaint
    {
        IsAntialias = true,
        Color = new SKColor(230, 160, 50, 200),  // amber, semi-transparent
        Style = SKPaintStyle.Fill,
    };
    using var bandPath = new SKPath();
    var bandRect = new SKRect(0, cy - bandH / 2f, s, cy + bandH / 2f);
    // Clip band to the circle
    using (var clip = new SKPath())
    {
        clip.AddCircle(cx, cy, r);
        canvas.Save();
        canvas.ClipPath(clip, SKClipOperation.Intersect, antialias: true);
        canvas.DrawRect(bandRect, bandPaint);
        canvas.Restore();
    }

    // 全 kanji — only legible at ≥ 32px
    if (size >= 32)
    {
        float fontSize = s * 0.52f;
        // Try to find a font that can render CJK; fall back gracefully
        using var typeface = FindCjkTypeface();
        using var font = new SKFont(typeface, fontSize);
        font.Subpixel = true;

        // Measure to center
        float textWidth = font.MeasureText("全");
        float textX = cx - textWidth / 2f;
        // Optical vertical center (slightly above mid because of descent)
        float textY = cy + fontSize * 0.35f;

        // Subtle drop shadow
        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.Black.WithAlpha(160),
        };
        canvas.DrawText("全", textX + s * 0.015f, textY + s * 0.015f, font, shadowPaint);

        // Main glyph — warm white
        using var glyphPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(240, 230, 200),
        };
        canvas.DrawText("全", textX, textY, font, glyphPaint);
    }

    // Thin border ring
    using var ringPaint = new SKPaint
    {
        IsAntialias = true,
        Color = new SKColor(120, 100, 200, 180),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = Math.Max(1f, s * 0.018f),
    };
    canvas.DrawCircle(cx, cy, r - ringPaint.StrokeWidth / 2f, ringPaint);

    // Encode as PNG
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

// Prefer a CJK-capable font, fall back to default
static SKTypeface FindCjkTypeface()
{
    // Common CJK fonts on Windows
    string[] candidates = ["Yu Gothic UI", "MS Gothic", "Meiryo", "Segoe UI Historic", "Arial Unicode MS"];
    foreach (var name in candidates)
    {
        var tf = SKTypeface.FromFamilyName(name);
        if (tf != null && tf.FamilyName != SKTypeface.Default.FamilyName)
            return tf;
    }
    return SKTypeface.Default;
}

// ── ICO writer ────────────────────────────────────────────────────────────────
// Modern ICO format: PNG images embedded directly (Vista+ compatible)

static void WriteIco(string path, byte[][] pngs, int[] sizes)
{
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);

    int count = pngs.Length;
    int headerSize = 6 + count * 16;

    // Compute offsets
    var offsets = new int[count];
    offsets[0] = headerSize;
    for (int i = 1; i < count; i++)
        offsets[i] = offsets[i - 1] + pngs[i - 1].Length;

    // ICONDIR header
    bw.Write((short)0);   // reserved
    bw.Write((short)1);   // type: icon
    bw.Write((short)count);

    // ICONDIRENTRY per image
    for (int i = 0; i < count; i++)
    {
        // width/height: 0 means 256
        bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        bw.Write((byte)0);   // color count (0 = not a palette image)
        bw.Write((byte)0);   // reserved
        bw.Write((short)1);  // color planes
        bw.Write((short)32); // bits per pixel
        bw.Write((int)pngs[i].Length);
        bw.Write((int)offsets[i]);
    }

    // PNG data
    foreach (var png in pngs)
        bw.Write(png);

    bw.Flush();
    File.WriteAllBytes(path, ms.ToArray());
}
