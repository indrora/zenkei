using System.Globalization;
using System.Text;
using Zenkei.Models;
using Zenkei.Models.Markers;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Zenkei.Serialization;

/// <summary>
/// Loads and saves TourDocument to/from the YAML schema.
/// Uses YamlDotNet's representation model (node-level) for both load and save,
/// so all quoting, escaping, and multiline string handling is handled by the library.
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
            var firstScene = GetString(defNode, "firstScene");
            if (!string.IsNullOrEmpty(firstScene))
                doc.Default.FirstScene = firstScene;
        }

        if (TryGetMapping(root, "markers", out var iconsNode))
        {
            foreach (var kvp in iconsNode.Children)
                doc.Icons[Scalar(kvp.Key)] = Scalar(kvp.Value);
        }

        var baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (TryGetMapping(root, "scenes", out var scenesNode))
        {
            foreach (var kvp in scenesNode.Children)
            {
                var id = Scalar(kvp.Key);
                var sceneMap = (YamlMappingNode)kvp.Value;
                var scene = ParseScene(id, sceneMap);
                scene.BaseDirectory = baseDir;
                doc.Scenes[id] = scene;
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
            scene.Initial = new YawPitch(
                ParseDouble(Scalar(initialSeq.Children[0])),
                ParseDouble(Scalar(initialSeq.Children[1]))
            );
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
            marker.Coords = new YawPitch(
                ParseDouble(Scalar(coordsSeq.Children[0])),
                ParseDouble(Scalar(coordsSeq.Children[1]))
            );
        }

        return marker;
    }

    // ─── Save ────────────────────────────────────────────────────────────────

    public static void Save(TourDocument doc, string filePath)
    {
        var root = new YamlMappingNode();

        var info = new YamlMappingNode();
        info.Add("author", doc.Information.Author ?? "");
        info.Add("title",  doc.Information.Title  ?? "");
        root.Add("information", info);

        var defaults = new YamlMappingNode();
        defaults.Add("hfov", NumNode(doc.Default.HFov));
        if (!string.IsNullOrEmpty(doc.Default.FirstScene))
            defaults.Add("firstScene", doc.Default.FirstScene);
        root.Add("default", defaults);

        var scenesMap = new YamlMappingNode();
        foreach (var (id, scene) in doc.Scenes)
        {
            var sceneMap = new YamlMappingNode();
            sceneMap.Add("image", scene.Image);
            sceneMap.Add("title", scene.Title);
            if (!string.IsNullOrEmpty(scene.Description))
                sceneMap.Add("description", scene.Description);
            if (scene.HFov.HasValue)
                sceneMap.Add("hfov", NumNode(scene.HFov.Value));

            // initial is in radians (Pannellum format); Scene.Initial.Yaw/Pitch are radians.
            sceneMap.Add("initial", FlowSeq(NumNode(scene.Initial.Yaw), NumNode(scene.Initial.Pitch)));

            if (scene.Markers.Count > 0)
            {
                var markersSeq = new YamlSequenceNode();
                foreach (var m in scene.Markers)
                    markersSeq.Add(BuildMarkerNode(m));
                sceneMap.Add("markers", markersSeq);
            }

            scenesMap.Add(id, sceneMap);
        }
        root.Add("scenes", scenesMap);

        if (doc.Icons.Count > 0)
        {
            var iconsMap = new YamlMappingNode();
            foreach (var (name, path) in doc.Icons)
                iconsMap.Add(name, path);
            root.Add("markers", iconsMap);
        }

        var yamlStream = new YamlStream(new YamlDocument(root));
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        yamlStream.Save(writer, assignAnchors: false);
    }

    private static YamlMappingNode BuildMarkerNode(MarkerBase m)
    {
        var node = new YamlMappingNode();
        node.Add("type", m.Type);
        if (m.Coords.HasValue)
            node.Add("coords", FlowSeq(NumNode(m.Coords.Value.Yaw), NumNode(m.Coords.Value.Pitch)));
        if (!string.IsNullOrEmpty(m.Marker))
            node.Add("marker", m.Marker);
        switch (m)
        {
            case LinkMarker lm:
                node.Add("url", lm.Url);
                break;
            case InfoMarker im:
                node.Add("text", im.Text ?? "");
                break;
            case SceneMarker sm:
                node.Add("scene", sm.TargetScene);
                if (!string.IsNullOrEmpty(sm.Text))
                    node.Add("text", sm.Text);
                break;
        }
        return node;
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

    private static YamlScalarNode NumNode(double v) =>
        new(v.ToString("G6", CultureInfo.InvariantCulture));

    private static YamlSequenceNode FlowSeq(params YamlNode[] items) =>
        new(items) { Style = YamlDotNet.Core.Events.SequenceStyle.Flow };
}
