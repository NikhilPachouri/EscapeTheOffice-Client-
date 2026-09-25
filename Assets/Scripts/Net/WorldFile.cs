using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Net
{
    // Converts the server's two-sided world.json (sides A/B with map files, rooms with `rects`,
    // `initial`, `codes`, rules) into the single-side level the offline FakeServer plays.
    // Does what the server does at load: builds the tiles, derives glow colours and interact
    // actions from the rules, gives keypads their colour `order`, turns the side's boss into an
    // object, and composes codes from the authored panel digits (offline has no random digits).
    // The original document rides along as `_source` so offline play can switch sides.
    public static class WorldFile
    {
        public static bool IsServerFormat(JObject doc) => doc?["sides"] is JObject;

        public static JObject ToLevel(JObject src, string side, IDictionary<string, JToken> keepState = null)
        {
            var sides = (JObject)src["sides"];
            if (sides[side] == null) side = sides.Properties().First().Name;
            var me = (JObject)sides[side];
            var rules = (src["rules"] as JArray ?? new JArray()).Cast<JObject>().ToList();
            var codes = src["codes"] as JObject ?? new JObject();

            // Which side(s) read each key: objects' `key`, rooms' lights/flooded/water.
            var readers = new Dictionary<string, HashSet<string>>();
            var readBy = new Dictionary<string, List<string>>(); // key -> object ids
            foreach (var s in sides.Properties())
            {
                foreach (var o in Objects(s.Value)) Read(readers, o.Value<string>("key"), s.Name, readBy, o.Value<string>("id"));
                foreach (var r in Rooms(s.Value))
                    foreach (var k in new[] { "lights", "flooded", "water" }) Read(readers, r.Value<string>(k), s.Name, null, null);
            }

            var objects = new JArray();
            var colors = new JObject();
            foreach (var o in Objects(me).Select(o => (JObject)o.DeepClone()))
            {
                var id = o.Value<string>("id");
                var rule = rules.FirstOrDefault(r => (r.Value<string>("on") ?? "").StartsWith(id + ":"));
                if (rule != null)
                {
                    if (o["interact"] == null) o["interact"] = rule.Value<string>("on").Substring(id.Length + 1);
                    var code = rule["requires"]?["code"]?.ToString();
                    if (code != null && codes[code]?["order"] != null) o["order"] = codes[code]["order"].DeepClone();

                    // Glow colour: the sides whose world this changes (ignoring its own cosmetic key).
                    var hit = new HashSet<string>();
                    foreach (var k in Written(rule))
                        if (readers.TryGetValue(k, out var who) && !(readBy.TryGetValue(k, out var ids) && ids.All(x => x == id)))
                            hit.UnionWith(who);
                    if (hit.Count > 0) colors[id] = hit.Count > 1 ? "both" : hit.First();
                }
                objects.Add(o);
            }

            // The side's boss is an object for the client.
            if (me["boss"] is JObject boss && boss["path"] is JArray path && path.Count > 0)
                objects.Add(new JObject
                {
                    ["id"] = "boss_" + side, ["type"] = "boss",
                    ["x"] = path[0][0], ["y"] = path[0][1], ["patrol"] = path.DeepClone(),
                    ["speed"] = boss["speed"], ["chaseSpeed"] = boss["chaseSpeed"], ["chaseRadius"] = boss["chaseRadius"],
                    ["debuff"] = src["debuff"]?["seconds"] ?? 15,
                });

            var rooms = new JArray();
            foreach (var r in Rooms(me))
            {
                var rects = r["rects"] as JArray;
                if (rects == null) { rooms.Add(r.DeepClone()); continue; }
                foreach (var rect in rects)
                {
                    var room = (JObject)r.DeepClone();
                    room.Remove("rects");
                    room["x"] = rect[0]; room["y"] = rect[1]; room["w"] = rect[2]; room["h"] = rect[3];
                    rooms.Add(room);
                }
            }

            var state = keepState != null
                ? new JObject(keepState.Select(kv => new JProperty(kv.Key, kv.Value.DeepClone())))
                : InitialState(src, sides, codes);

            return new JObject
            {
                ["side"] = side,
                ["derived"] = src["derived"]?.DeepClone() ?? new JObject(),
                ["rules"] = new JArray(rules.Select(r => r.DeepClone())),
                ["world"] = new JObject
                {
                    ["side"] = side,
                    ["tileSize"] = src["tileSize"] ?? 32,
                    ["camera"] = src["camera"]?.DeepClone(),
                    ["tiles"] = new JArray(ReadMap(me.Value<string>("map"))),
                    ["spawn"] = me["spawn"]?.DeepClone(),
                    ["rooms"] = rooms,
                    ["objects"] = objects,
                    ["colors"] = colors,
                    ["state"] = state,
                },
                ["_source"] = src,
            };
        }

        static JObject InitialState(JObject src, JObject sides, JObject codes)
        {
            var state = (JObject)(src["initial"]?.DeepClone() ?? new JObject());
            var panels = new Dictionary<string, string>(); // "A:red" -> digit
            foreach (var s in sides.Properties())
            {
                state["inv_" + s.Name] = new JArray();
                foreach (var o in Objects(s.Value))
                {
                    var key = o.Value<string>("key");
                    var type = o.Value<string>("type");
                    if (type == "code_panel")
                    {
                        var digit = o["digit"]?.ToString() ?? "0";
                        panels[s.Name + ":" + o.Value<string>("color")] = digit;
                        if (key != null) state[key] = digit;
                    }
                    else if (key != null && state[key] == null)
                        state[key] = type == "bomb" || type == "key" ? (JToken)"home" : false;
                }
            }
            // Composed answers ("Offline defaults spell 9315, 8247 and 7428"); no object reads them.
            foreach (var c in codes.Properties())
            {
                var spec = c.Value;
                var side = spec.Value<string>("side");
                state[c.Name] = string.Concat((spec["order"] ?? new JArray()).Select(col =>
                    panels.TryGetValue(side + ":" + col, out var d) ? d : "?"));
            }
            return state;
        }

        static IEnumerable<JObject> Objects(JToken side) => (side["objects"] as JArray ?? new JArray()).OfType<JObject>();
        static IEnumerable<JObject> Rooms(JToken side) => (side["rooms"] as JArray ?? new JArray()).OfType<JObject>();

        static void Read(Dictionary<string, HashSet<string>> readers, string key, string side, Dictionary<string, List<string>> readBy, string id)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!readers.TryGetValue(key, out var set)) readers[key] = set = new HashSet<string>();
            set.Add(side);
            if (readBy != null)
            {
                if (!readBy.TryGetValue(key, out var ids)) readBy[key] = ids = new List<string>();
                ids.Add(id);
            }
        }

        static IEnumerable<string> Written(JObject rule)
        {
            foreach (var step in (rule["do"] as JArray ?? new JArray()).OfType<JObject>())
            {
                if (step["toggle"] != null) yield return step.Value<string>("toggle");
                if (step["set"] is JObject set) foreach (var p in set.Properties()) yield return p.Name;
            }
        }

        // "maps/side_a.txt" -> Resources/Maps/side_a, or the level folder on disk.
        static IEnumerable<string> ReadMap(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path ?? "");
            string text = null;
            var disk = Path.Combine(LevelStore.SaveDir, path ?? "");
            if (File.Exists(disk)) text = File.ReadAllText(disk);
            else text = Resources.Load<TextAsset>("Maps/" + name)?.text;
            if (text == null)
            {
                Debug.LogWarning($"[offline] map '{path}' not found (put it in Assets/Resources/Maps/{name}.txt)");
                return new string[0];
            }
            return text.Replace("\r", "").Split('\n').Where(l => l.Length > 0);
        }
    }
}
