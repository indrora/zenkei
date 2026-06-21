using System.Globalization;
using System.Text;
using System.Text.Json;
using PanoTool.Models;
using PanoTool.Models.Markers;

namespace PanoTool.Serialization;

/// <summary>
/// Exports a TourDocument to a Pannellum-compatible HTML/JSON tour package.
/// Output structure:
///   &lt;outDir&gt;/index.html
///   &lt;outDir&gt;/tour.json
///   &lt;outDir&gt;/pannellum.min.js  (copied from Assets/Viewer/)
///   &lt;outDir&gt;/pannellum.min.css
///   &lt;outDir&gt;/images/&lt;sceneId&gt;.jpg   (copied from source)
///   &lt;outDir&gt;/icons/&lt;name&gt;.{png,svg}  (copied from user icon paths)
/// </summary>
public static class PannellumExporter
{
    public static void Export(TourDocument doc, string outputDir, string? tourBaseDir = null)
    {
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(Path.Combine(outputDir, "images"));
        Directory.CreateDirectory(Path.Combine(outputDir, "icons"));

        var baseDir = tourBaseDir
            ?? (doc.FilePath != null ? Path.GetDirectoryName(doc.FilePath) : null)
            ?? Directory.GetCurrentDirectory();

        // Copy pannellum assets
        var assetSrc = Path.Combine(AppContext.BaseDirectory, "Assets", "Viewer");
        foreach (var f in new[] { "pannellum.min.js", "pannellum.min.css" })
        {
            var src = Path.Combine(assetSrc, f);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(outputDir, f), overwrite: true);
        }

        // Copy images
        var imageMap = new Dictionary<string, string>(); // sceneId → relative images/ path
        foreach (var (id, scene) in doc.Scenes)
        {
            if (string.IsNullOrEmpty(scene.Image)) continue;
            var srcImage = Path.IsPathRooted(scene.Image)
                ? scene.Image
                : Path.Combine(baseDir, scene.Image);
            if (!File.Exists(srcImage)) continue;
            var ext = Path.GetExtension(srcImage).ToLowerInvariant();
            var destName = $"{id}{ext}";
            File.Copy(srcImage, Path.Combine(outputDir, "images", destName), overwrite: true);
            imageMap[id] = $"images/{destName}";
        }

        // Copy user icons
        var iconCssClass = new Dictionary<string, string>(); // iconName → css class name
        foreach (var (name, path) in doc.Icons)
        {
            var srcIcon = Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
            if (!File.Exists(srcIcon)) continue;
            var destName = Path.GetFileName(srcIcon);
            File.Copy(srcIcon, Path.Combine(outputDir, "icons", destName), overwrite: true);
            iconCssClass[name] = $"hs-icon-{name}";
        }

        // Build tour.json
        var tourJson = BuildTourJson(doc, imageMap, iconCssClass);
        File.WriteAllText(Path.Combine(outputDir, "tour.json"), tourJson, Encoding.UTF8);

        // Build index.html
        var iconCss = BuildIconCss(doc.Icons, iconCssClass);
        var html = BuildHtml(iconCss);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), html, Encoding.UTF8);
    }

    private static string BuildTourJson(
        TourDocument doc,
        Dictionary<string, string> imageMap,
        Dictionary<string, string> iconCssClass)
    {
        using var ms = new MemoryStream();
        using var jw = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

        jw.WriteStartObject();

        // default section
        jw.WriteStartObject("default");
        var firstScene = doc.Scenes.Keys.FirstOrDefault() ?? "";
        jw.WriteString("firstScene", firstScene);
        jw.WriteNumber("hfov", doc.Default.HFov);
        jw.WriteNumber("sceneFadeDuration", 1000);
        jw.WriteEndObject();

        // scenes section
        jw.WriteStartObject("scenes");
        foreach (var (id, scene) in doc.Scenes)
        {
            jw.WriteStartObject(id);
            jw.WriteString("type", "equirectangular");
            jw.WriteString("panorama", imageMap.GetValueOrDefault(id, ""));
            jw.WriteString("title", scene.Title);

            var (initYaw, initPitch) = RadToPannellum(scene.Initial[0], scene.Initial[1]);
            jw.WriteNumber("yaw", Round(initYaw));
            jw.WriteNumber("pitch", Round(initPitch));
            jw.WriteNumber("hfov", scene.HFov ?? doc.Default.HFov);

            if (scene.Markers.Count > 0)
            {
                jw.WriteStartArray("hotSpots");
                foreach (var m in scene.Markers)
                    WriteHotspot(jw, m, doc, iconCssClass);
                jw.WriteEndArray();
            }

            jw.WriteEndObject();
        }
        jw.WriteEndObject();

        jw.WriteEndObject();
        jw.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteHotspot(
        Utf8JsonWriter jw,
        MarkerBase m,
        TourDocument doc,
        Dictionary<string, string> iconCssClass)
    {
        var coords = m.Coords ?? [0.0, Math.PI / 2];
        var (yaw, pitch) = RadToPannellum(coords[0], coords[1]);

        jw.WriteStartObject();
        jw.WriteNumber("yaw", Round(yaw));
        jw.WriteNumber("pitch", Round(pitch));

        switch (m)
        {
            case LinkMarker lm:
                jw.WriteString("type", "info");
                jw.WriteString("text", "Link");
                jw.WriteString("URL", lm.Url);
                break;
            case InfoMarker im:
                jw.WriteString("type", "info");
                jw.WriteString("text", im.Text);
                break;
            case SceneMarker sm:
                jw.WriteString("type", "scene");
                jw.WriteString("sceneId", sm.TargetScene);
                if (!string.IsNullOrEmpty(sm.Text))
                    jw.WriteString("text", sm.Text);
                break;
        }

        // CSS class for custom icon
        var iconName = m.Marker ?? m.Type;
        if (iconCssClass.TryGetValue(iconName, out var cssClass))
            jw.WriteString("cssClass", cssClass);
        else
            jw.WriteString("cssClass", $"hs-icon-{m.Type}");

        jw.WriteEndObject();
    }

    private static string BuildIconCss(
        Dictionary<string, string> icons,
        Dictionary<string, string> iconCssClass)
    {
        var sb = new StringBuilder();
        // Built-in type pseudo-icons
        foreach (var t in new[] { "link", "info", "scene" })
            sb.AppendLine($".hs-icon-{t} {{ background: rgba(0,0,0,0.5); border-radius:50%; width:32px; height:32px; }}");

        foreach (var (name, _) in icons)
        {
            var cls = iconCssClass.GetValueOrDefault(name, $"hs-icon-{name}");
            var ext = Path.GetExtension(icons[name]).ToLowerInvariant();
            sb.AppendLine($".{cls} {{ background-image: url('icons/{name}{ext}'); background-size:cover; width:32px; height:32px; }}");
        }
        return sb.ToString();
    }

    private static string BuildHtml(string iconCss)
    {
        return "<!DOCTYPE html>\n" +
               "<html lang=\"en\">\n<head>\n" +
               "  <meta charset=\"UTF-8\">\n" +
               "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
               "  <title>Panoramic Tour</title>\n" +
               "  <link rel=\"stylesheet\" href=\"pannellum.min.css\">\n" +
               "  <style>\n" +
               "    html, body { margin: 0; padding: 0; height: 100%; overflow: hidden; }\n" +
               "    #viewer { width: 100%; height: 100vh; }\n" +
               iconCss + "\n" +
               "  </style>\n</head>\n<body>\n" +
               "  <div id=\"viewer\"></div>\n" +
               "  <script src=\"pannellum.min.js\"></script>\n" +
               "  <script>\n" +
               "    fetch('tour.json')\n" +
               "      .then(r => r.json())\n" +
               "      .then(cfg => pannellum.viewer('viewer', cfg));\n" +
               "  </script>\n</body>\n</html>\n";
    }

    // Converts our internal radians (yaw: -π..π, pitch: 0..π top-to-bottom)
    // to Pannellum degrees (yaw: -180..180, pitch: 90=up..-90=down)
    private static (double yaw, double pitch) RadToPannellum(double yawRad, double pitchRad)
    {
        var yaw = yawRad * (180.0 / Math.PI);
        var pitch = 90.0 - pitchRad * (180.0 / Math.PI);
        return (yaw, pitch);
    }

    private static double Round(double v) =>
        Math.Round(v, 4, MidpointRounding.AwayFromZero);
}
