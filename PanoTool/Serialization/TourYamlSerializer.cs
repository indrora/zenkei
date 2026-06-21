using System.Globalization;
using System.Text;
using PanoTool.Models;
using PanoTool.Models.Markers;
using YamlDotNet.RepresentationModel;

namespace PanoTool.Serialization;

/// <summary>
/// Loads and saves TourDocument to/from the YAML schema.
/// Uses YamlDotNet's representation model (node-level) to handle
/// polymorphic marker lists without requiring a custom type converter.
/// </summary>
public static class TourYamlSerializer
{
    public static TourDocument Load(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        var doc = new TourDocument { FilePath = filePath };
        if (stream.Documents.Count == 0) return doc;

        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        if (TryGetMapping(root, "information", out var infoNode))
        {
            doc.Information.Author = GetString(infoNode, "author") ?? "";
            doc.Information.Title = GetString(infoNode, "title") ?? "Untitled Tour";
        }

        if (TryGetMapping(root, "default", out var defNode))
        {
            if (TryGetDouble(defNode, "hfov", out var hfov))
                doc.Default.HFov = hfov;
        }

        if (TryGetMapping(root, "markers", out var iconsNode))
        {
            foreach (var kvp in iconsNode.Children)
                doc.Icons[Scalar(kvp.Key)] = Scalar(kvp.Value);
        }

        if (TryGetMapping(root, "scenes", out var scenesNode))
        {
            foreach (var kvp in scenesNode.Children)
            {
                var id = Scalar(kvp.Key);
                var sceneMap = (YamlMappingNode)kvp.Value;
                doc.Scenes[id] = ParseScene(id, sceneMap);
            }
        }

        return doc;
    }

    private static Scene ParseScene(string id, YamlMappingNode node)
    {
        var scene = new Scene { Id = id };
        scene.Image = GetString(node, "image") ?? "";
        scene.Title = GetString(node, "title") ?? id;
        scene.Description = GetString(node, "description");

        if (TryGetDouble(node, "hfov", out var hfov))
            scene.HFov = hfov;

        if (TryGetSequence(node, "initial", out var initialSeq) && initialSeq.Children.Count >= 2)
        {
            scene.Initial = [
                ParseDouble(Scalar(initialSeq.Children[0])),
                ParseDouble(Scalar(initialSeq.Children[1]))
            ];
        }

        if (TryGetSequence(node, "markers", out var markersSeq))
        {
            foreach (var item in markersSeq.Children)
            {
                if (item is YamlMappingNode markerMap)
                    scene.Markers.Add(ParseMarker(markerMap));
            }
        }

        return scene;
    }

    private static MarkerBase ParseMarker(YamlMappingNode node)
    {
        var type = GetString(node, "type") ?? "info";

        MarkerBase marker = type switch
        {
            "link" => new LinkMarker { Url = GetString(node, "url") ?? "" },
            "scene" => new SceneMarker
            {
                TargetScene = GetString(node, "scene") ?? "",
                Text = GetString(node, "text")
            },
            _ => new InfoMarker { Text = GetString(node, "text") ?? "" }
        };

        marker.Marker = GetString(node, "marker");

        if (TryGetSequence(node, "coords", out var coordsSeq) && coordsSeq.Children.Count >= 2)
        {
            marker.Coords = [
                ParseDouble(Scalar(coordsSeq.Children[0])),
                ParseDouble(Scalar(coordsSeq.Children[1]))
            ];
        }

        return marker;
    }

    // ─── Save ────────────────────────────────────────────────────────────────

    public static void Save(TourDocument doc, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("information:");
        sb.AppendLine($"  author: {QuoteString(doc.Information.Author)}");
        sb.AppendLine($"  title: {QuoteString(doc.Information.Title)}");
        sb.AppendLine();
        sb.AppendLine("default:");
        sb.AppendLine($"  hfov: {F(doc.Default.HFov)}");
        sb.AppendLine();

        sb.AppendLine("scenes:");
        foreach (var (id, scene) in doc.Scenes)
        {
            sb.AppendLine($"  {id}:");
            sb.AppendLine($"    image: {scene.Image}");
            sb.AppendLine($"    title: {QuoteString(scene.Title)}");
            if (!string.IsNullOrEmpty(scene.Description))
                sb.AppendLine($"    description: {QuoteString(scene.Description)}");
            if (scene.HFov.HasValue)
                sb.AppendLine($"    hfov: {F(scene.HFov.Value)}");
            sb.AppendLine($"    initial: [{F(scene.Initial[0])}, {F(scene.Initial[1])}]");

            if (scene.Markers.Count > 0)
            {
                sb.AppendLine("    markers:");
                foreach (var m in scene.Markers)
                    WriteMarker(sb, m);
            }
        }

        sb.AppendLine();
        if (doc.Icons.Count > 0)
        {
            sb.AppendLine("markers:");
            foreach (var (name, path) in doc.Icons)
                sb.AppendLine($"  {name}: {path}");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteMarker(StringBuilder sb, MarkerBase m)
    {
        sb.AppendLine($"      - type: {m.Type}");
        if (m.Coords is { Length: >= 2 })
            sb.AppendLine($"        coords: [{F(m.Coords[0])}, {F(m.Coords[1])}]");
        if (!string.IsNullOrEmpty(m.Marker))
            sb.AppendLine($"        marker: {m.Marker}");

        switch (m)
        {
            case LinkMarker lm:
                sb.AppendLine($"        url: {lm.Url}");
                break;
            case InfoMarker im:
                if (im.Text.Contains('\n'))
                    sb.AppendLine($"        text: |\n          {im.Text.Replace("\n", "\n          ")}");
                else
                    sb.AppendLine($"        text: {QuoteString(im.Text)}");
                break;
            case SceneMarker sm:
                sb.AppendLine($"        scene: {sm.TargetScene}");
                if (!string.IsNullOrEmpty(sm.Text))
                    sb.AppendLine($"        text: {QuoteString(sm.Text)}");
                break;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string Scalar(YamlNode node) =>
        node is YamlScalarNode s ? s.Value ?? "" : "";

    private static string? GetString(YamlMappingNode node, string key)
    {
        var k = new YamlScalarNode(key);
        return node.Children.TryGetValue(k, out var v) && v is YamlScalarNode sv
            ? sv.Value : null;
    }

    private static bool TryGetMapping(YamlMappingNode node, string key, out YamlMappingNode result)
    {
        result = null!;
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlMappingNode m)
        {
            result = m;
            return true;
        }
        return false;
    }

    private static bool TryGetSequence(YamlMappingNode node, string key, out YamlSequenceNode result)
    {
        result = null!;
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlSequenceNode s)
        {
            result = s;
            return true;
        }
        return false;
    }

    private static bool TryGetDouble(YamlMappingNode node, string key, out double value)
    {
        value = 0;
        var s = GetString(node, key);
        return s != null && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static double ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string F(double v) => v.ToString("G6", CultureInfo.InvariantCulture);

    private static string QuoteString(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        // Quote if it contains special YAML chars
        if (s.Any(c => ":{}[]|>&*!,#?".Contains(c)) || s.StartsWith(' ') || s.EndsWith(' '))
            return $"\"{s.Replace("\"", "\\\"")}\"";
        return s;
    }
}
