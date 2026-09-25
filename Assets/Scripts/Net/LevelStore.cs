using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Net
{
    // Level files for offline play and the level editor, in the FakeWorld.json format
    // (side, codes, derived, rules, world). In the editor they are saved into
    // Assets/Resources/Levels so they ship with builds; a build saves to persistentDataPath.
    public static class LevelStore
    {
        // The offline default is the server's own world.json (two sides), converted on load.
        public const string Default = "OfflineWorld";

        public static string SaveDir =>
            Application.isEditor
                ? Path.Combine(Application.dataPath, "Resources", "Levels")
                : Path.Combine(Application.persistentDataPath, "Levels");

        public static List<string> List()
        {
            var names = new SortedSet<string>();
            foreach (var t in Resources.LoadAll<TextAsset>("Levels")) names.Add(t.name);
            if (Directory.Exists(SaveDir))
                foreach (var f in Directory.GetFiles(SaveDir, "*.json")) names.Add(Path.GetFileNameWithoutExtension(f));
            return names.ToList();
        }

        public static JObject Load(string name)
        {
            // A file on disk is always the freshest copy (Resources only update on reimport).
            var path = Path.Combine(SaveDir, name + ".json");
            var asset = File.Exists(path) ? null : Resources.Load<TextAsset>(name == Default ? Default : "Levels/" + name);
            var doc = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : asset != null ? JObject.Parse(asset.text) : null;
            return WorldFile.IsServerFormat(doc) ? WorldFile.ToLevel(doc, "A") : doc;
        }

        public static string Save(string name, JObject level)
        {
            Directory.CreateDirectory(SaveDir);
            var path = Path.Combine(SaveDir, Sanitise(name) + ".json");
            File.WriteAllText(path, level.ToString(Formatting.Indented));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            return path;
        }

        public static string Sanitise(string name)
        {
            var clean = new string((name ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            return clean.Length > 0 ? clean : "level";
        }
    }
}
