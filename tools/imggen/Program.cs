using SkiaSharp;

// Output directory passed as first arg, or current dir
var outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);

const int W = 4096;
const int H = 2048;

// Each entry: (filename, horizon gradient top-color, horizon gradient bottom-color, accent)
var palettes = new (string Name, SKColor Sky, SKColor Horizon, SKColor Ground, SKColor Grid)[]
{
    ("pano_sunset.jpg",
        new SKColor(20, 10, 60),
        new SKColor(230, 100, 30),
        new SKColor(60, 30, 10),
        new SKColor(255, 220, 120, 200)),

    ("pano_arctic.jpg",
        new SKColor(130, 200, 240),
        new SKColor(220, 240, 255),
        new SKColor(160, 210, 230),
        new SKColor(0, 80, 160, 200)),

    ("pano_forest.jpg",
        new SKColor(10, 40, 10),
        new SKColor(40, 100, 30),
        new SKColor(20, 60, 10),
        new SKColor(120, 255, 80, 200)),

    ("pano_desert.jpg",
        new SKColor(80, 140, 200),
        new SKColor(240, 180, 90),
        new SKColor(200, 140, 60),
        new SKColor(255, 240, 160, 200)),
};

foreach (var (name, sky, horizon, ground, gridColor) in palettes)
{
    using var bmp = new SKBitmap(W, H);
    using var canvas = new SKCanvas(bmp);

    DrawBackground(canvas, W, H, sky, horizon, ground);
    DrawGrid(canvas, W, H, gridColor);
    DrawLabels(canvas, W, H, gridColor);
    DrawHorizonLine(canvas, W, H, gridColor);

    var path = Path.Combine(outDir, name);
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Jpeg, 92);
    using var fs = File.OpenWrite(path);
    data.SaveTo(fs);
    Console.WriteLine($"Wrote {path}");
}

// ── Helpers ──────────────────────────────────────────────────────────────────

static void DrawBackground(SKCanvas canvas, int w, int h,
    SKColor sky, SKColor horizon, SKColor ground)
{
    // Sky half: vertical gradient from top → horizon at midpoint
    var skyShader = SKShader.CreateLinearGradient(
        new SKPoint(0, 0), new SKPoint(0, h / 2f),
        new[] { sky, horizon }, null, SKShaderTileMode.Clamp);
    using var skyPaint = new SKPaint { Shader = skyShader };
    canvas.DrawRect(0, 0, w, h / 2f, skyPaint);

    // Ground half: gradient from horizon → ground at bottom
    var gndShader = SKShader.CreateLinearGradient(
        new SKPoint(0, h / 2f), new SKPoint(0, h),
        new[] { horizon, ground }, null, SKShaderTileMode.Clamp);
    using var gndPaint = new SKPaint { Shader = gndShader };
    canvas.DrawRect(0, h / 2f, w, h / 2f, gndPaint);
}

static void DrawGrid(SKCanvas canvas, int w, int h, SKColor color)
{
    // Grid lines every 30° (π/6 rad) in yaw and pitch
    using var thinPaint = new SKPaint
    {
        Color = color.WithAlpha(120),
        StrokeWidth = 1.5f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
    };
    using var thickPaint = new SKPaint
    {
        Color = color.WithAlpha(200),
        StrokeWidth = 3f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
    };

    // Vertical lines (yaw -180°→+180° in 30° steps)
    for (int deg = -180; deg <= 180; deg += 30)
    {
        float x = YawDegToX(deg, w);
        var paint = (deg == 0 || deg == -180 || deg == 180) ? thickPaint : thinPaint;
        canvas.DrawLine(x, 0, x, h, paint);
    }

    // Horizontal lines (pitch 0°→180° mapped to y=0→h, in 30° steps)
    for (int deg = 0; deg <= 180; deg += 30)
    {
        float y = PitchDegToY(deg, h);
        var paint = (deg == 90) ? thickPaint : thinPaint; // equator is thick
        canvas.DrawLine(0, y, w, y, paint);
    }
}

static void DrawHorizonLine(SKCanvas canvas, int w, int h, SKColor color)
{
    // Subtle glow at the equator (pitch = 90° = y = h/2)
    using var glowPaint = new SKPaint
    {
        Color = color.WithAlpha(40),
        StrokeWidth = 20f,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12f),
    };
    canvas.DrawLine(0, h / 2f, w, h / 2f, glowPaint);
}

static void DrawLabels(SKCanvas canvas, int w, int h, SKColor color)
{
    using var font = new SKFont(SKTypeface.Default, 22);
    using var bigFont = new SKFont(SKTypeface.Default, 28) { Embolden = true };
    using var paint = new SKPaint { Color = color, IsAntialias = true };
    using var shadowPaint = new SKPaint { Color = SKColors.Black.WithAlpha(160), IsAntialias = true };

    // Yaw labels along the equator
    for (int deg = -180; deg <= 180; deg += 30)
    {
        float x = YawDegToX(deg, w);
        float y = PitchDegToY(90, h); // equator
        string label = $"{deg}°";

        // Shadow then label
        canvas.DrawText(label, x + 2, y - 14 + 2, SKTextAlign.Center, bigFont, shadowPaint);
        canvas.DrawText(label, x, y - 14, SKTextAlign.Center, bigFont, paint);
    }

    // Pitch labels along the prime meridian (yaw = 0)
    for (int deg = 0; deg <= 180; deg += 30)
    {
        float x = YawDegToX(0, w) + 6;
        float y = PitchDegToY(deg, h);
        string label = $"{deg - 90}°";  // show as -90 → 0 → +90 (elevation style)

        canvas.DrawText(label, x + 2, y + 8 + 2, SKTextAlign.Left, font, shadowPaint);
        canvas.DrawText(label, x, y + 8, SKTextAlign.Left, font, paint);
    }

    // Corner annotation
    canvas.DrawText("Equirectangular 2:1 — test grid", w / 2f + 2, 36 + 2,
        SKTextAlign.Center, bigFont, shadowPaint);
    canvas.DrawText("Equirectangular 2:1 — test grid", w / 2f, 36,
        SKTextAlign.Center, bigFont, paint);
}

// Map yaw degrees (-180→+180) to pixel X (0→W)
static float YawDegToX(float deg, int w) => (deg + 180f) / 360f * w;

// Map pitch degrees (0=top, 90=equator, 180=bottom) to pixel Y (0→H)
static float PitchDegToY(float deg, int h) => deg / 180f * h;
