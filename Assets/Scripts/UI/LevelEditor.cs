using System.Collections.Generic;
using System.Linq;
using EscapeOffice.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.UI
{
    // F3 (offline only): in-game level editor. Edits the offline level document
    // (FakeWorld.json format), rebuilds the world live and saves levels as JSON
    // (LevelStore → Assets/Resources/Levels).
    //
    // Walk with WASD (no clipping while editing), scroll to zoom, left-click to use the tool,
    // right-click deletes the object under the cursor, Delete removes the selection, Ctrl+Z undoes.
    public class LevelEditor : MonoBehaviour
    {
        enum Tool { Select, Wall, Floor, Erase, Spawn, Object, Room, Link }

        static readonly string[] ObjectTypes =
        {
            "button", "switch", "laser_switch", "light_switch", "valve", "latch_button",
            "button_door", "code_door", "key_door", "exit_door",
            "keypad", "code_panel", "lasers", "fire", "bombable_wall",
            "key", "bomb", "boss",
        };
        static readonly string[] Themes = { "lobby", "office", "archive", "workshop", "vault", "lab", "server", "boiler", "cafe", "hallway", "final" };
        static readonly string[] Colors = { "", "A", "B", "both", "info" };
        static readonly HashSet<string> Triggers = new HashSet<string> { "button", "latch_button", "final_button", "switch", "lever", "laser_switch", "light_switch", "valve", "drain", "keypad" };

        const float PanelWidth = 310f;
        // Same reference size as the HUD, but never so large that the panel fills a narrow window.
        static float UiScale => Mathf.Max(1f, Mathf.Min(Screen.height / 720f, Screen.width / 1280f));

        bool open, openWhenReady;
        JObject level;
        List<char[]> rows = new List<char[]>();
        Tool tool = Tool.Select;
        string placeType = "button";
        JObject selected, selectedRoom, linkSource;
        Vector2Int? hover, dragStart;
        bool painting, dragging;
        string levelName = "my_level", status = "";
        bool showLoad;
        readonly Stack<string> undo = new Stack<string>();
        Vector2 scroll, loadScroll;
        GUIStyle label, header, small;

        // Inspector buffers.
        string editId = "", editKey = "", editInteract = "", editW = "1", editH = "1", editExtra = "{}", editColor = "";
        string roomId = "", roomTheme = "office";
        bool roomDark, roomFlooded;

        // Gizmos drawn each frame from a pool.
        Transform gizmoRoot;
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        int poolUsed;

        GameManager Gm => GameManager.Instance;
        FakeServer Fake => Gm.GetComponent<FakeServer>();
        JObject World => (JObject)level["world"];
        JArray Objects => (JArray)World["objects"];
        JArray Rooms => (JArray)World["rooms"];
        JArray Rules => (JArray)level["rules"];
        JObject State => (JObject)World["state"];
        JObject ColorMap => (JObject)World["colors"];
        JArray Codes => (JArray)level["codes"];
        JObject Derived => (JObject)level["derived"];
        int W => rows.Count == 0 ? 0 : rows[0].Length;
        int H => rows.Count;

        public bool IsOpen => open;

        // Join screen: start offline and open once the world arrives.
        public void OpenWhenReady() => openWhenReady = true;

        // ------------------------------------------------------------------ open / close

        void Toggle()
        {
            if (open) { Close(); return; }
            if (!Gm.Offline || Gm.Current != GameManager.Phase.Playing || Fake == null || Fake.Level == null)
            {
                Gm.Toast("The level editor works in offline mode (Offline test / Level editor on the join screen).");
                return;
            }
            SetLevel((JObject)Fake.Level.DeepClone());
            open = true;
            Gm.EditorOpen = true;
            Gm.DebugNoFog = true;
            undo.Clear();
            status = "F3 closes the editor. Right-click deletes, Ctrl+Z undoes.";
        }

        void Close()
        {
            open = false;
            Gm.EditorOpen = false;
            Gm.DebugNoFog = false;
            Gm.CameraRig.Zoom = 1f;
            if (Gm.Player != null && Gm.Player.Collider != null) Gm.Player.Collider.enabled = true;
            selected = selectedRoom = linkSource = null;
            if (gizmoRoot != null) gizmoRoot.gameObject.SetActive(false);
        }

        void SetLevel(JObject doc)
        {
            level = doc;
            level["world"] ??= new JObject();
            World["objects"] ??= new JArray();
            World["rooms"] ??= new JArray();
            World["state"] ??= new JObject();
            World["colors"] ??= new JObject();
            World["camera"] ??= new JObject { ["radius"] = 8, ["darkRadius"] = 2.4 };
            level["rules"] ??= new JArray();
            level["codes"] ??= new JArray();
            level["derived"] ??= new JObject();
            level["side"] ??= "A";
            var tiles = World["tiles"];
            var lines = tiles is JArray a ? a.Select(t => t.ToString()).ToList()
                : (tiles?.ToString() ?? "").Replace("\r", "").Split('\n').Where(r => r.Length > 0).ToList();
            int width = lines.Count == 0 ? 0 : lines.Max(l => l.Length);
            rows = lines.Select(l => l.PadRight(width, ' ').ToCharArray()).ToList();
            selected = selectedRoom = linkSource = null;
        }

        // Writes the tiles back and hands the level to the fake server; the player stays put
        // unless it's a different level (then they go to its spawn).
        void Rebuild(bool keepPlayer = true)
        {
            World["tiles"] = new JArray(rows.Select(r => new string(r)));
            SyncExit();
            Gm.KeepPlayerOnNextWorld = keepPlayer;
            Fake.Reload(level);
        }

        void PushUndo()
        {
            World["tiles"] = new JArray(rows.Select(r => new string(r)));
            undo.Push(level.ToString(Formatting.None));
            if (undo.Count > 100) { var keep = undo.Take(100).Reverse().ToList(); undo.Clear(); foreach (var u in keep) undo.Push(u); }
        }

        void Undo()
        {
            if (undo.Count == 0) { status = "Nothing to undo."; return; }
            SetLevel(JObject.Parse(undo.Pop()));
            Rebuild();
            status = "Undone.";
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) Toggle();
            if (openWhenReady && Gm.Current == GameManager.Phase.Playing && Fake != null && Fake.Level != null)
            {
                openWhenReady = false;
                if (!open) Toggle();
            }
            if (!open) return;
            if (!Gm.Offline || Gm.Current != GameManager.Phase.Playing) { Close(); return; }

            // Walk through walls while editing.
            if (Gm.Player != null && Gm.Player.Collider != null && Gm.Player.Collider.enabled) Gm.Player.Collider.enabled = false;

            bool typing = GUIUtility.keyboardControl != 0;
            if (!typing)
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) Undo();
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) DeleteSelection();
                if (Input.GetKeyDown(KeyCode.Escape)) { selected = selectedRoom = linkSource = null; }
            }

            bool overPanel = Input.mousePosition.x > Screen.width - (PanelWidth + 20f) * UiScale; // panel is on the right
            hover = !overPanel && MouseTile(out var t) ? t : (Vector2Int?)null;

            if (!overPanel)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) Gm.CameraRig.Zoom = Mathf.Clamp(Gm.CameraRig.Zoom * (1f - wheel * 0.1f), 0.35f, 3f);
                if (hover.HasValue) HandleMouse(hover.Value);
            }
            if (Input.GetMouseButtonUp(0)) EndStroke();

            DrawGizmos();
        }

        void HandleMouse(Vector2Int c)
        {
            if (Input.GetMouseButtonDown(1))
            {
                var hit = ObjectAt(c);
                if (hit != null) { PushUndo(); RemoveObject(hit); Rebuild(); }
                return;
            }

            bool down = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0);
            switch (tool)
            {
                case Tool.Wall: case Tool.Floor: case Tool.Erase:
                    if (!held) break;
                    char ch = tool == Tool.Wall ? '#' : tool == Tool.Floor ? '.' : ' ';
                    if (rows[c.y][c.x] == ch) break;
                    if (!painting) { PushUndo(); painting = true; }
                    rows[c.y][c.x] = ch;
                    break;

                case Tool.Spawn:
                    if (!down) break;
                    PushUndo();
                    World["spawn"] = new JArray(c.x, c.y);
                    Rebuild();
                    status = $"Spawn at {c.x},{c.y}.";
                    break;

                case Tool.Object:
                    if (!down) break;
                    var existing = ObjectAt(c);
                    if (existing != null) { Select(existing); break; }
                    PushUndo();
                    Select(AddObject(placeType, c));
                    Rebuild();
                    break;

                case Tool.Select:
                    if (down)
                    {
                        var o = ObjectAt(c);
                        if (o != null) { Select(o); dragging = true; dragStart = c; }
                        else { var r = RoomAt(c); if (r != null) SelectRoom(r); else selected = selectedRoom = null; }
                    }
                    break;

                case Tool.Room:
                    if (down) dragStart = c;
                    break;

                case Tool.Link:
                    if (!down) break;
                    var target = ObjectAt(c);
                    if (linkSource == null)
                    {
                        if (target != null && Triggers.Contains(Type(target))) { linkSource = target; status = $"Linking {Id(target)}: click what it controls (door, lasers, fire, wall, code panel, or a room)."; }
                        else status = "Click a trigger first (button, switch, valve, light switch, keypad).";
                        break;
                    }
                    PushUndo();
                    status = target != null ? Link(linkSource, target) : LinkRoom(linkSource, RoomAt(c));
                    linkSource = null;
                    Rebuild();
                    break;
            }
        }

        void EndStroke()
        {
            if (painting) { painting = false; Rebuild(); }
            if (dragging)
            {
                dragging = false;
                if (selected != null && hover.HasValue && dragStart.HasValue && hover.Value != dragStart.Value && ObjectAt(hover.Value) == null)
                {
                    PushUndo();
                    selected["x"] = hover.Value.x;
                    selected["y"] = hover.Value.y;
                    Rebuild();
                }
            }
            if (tool == Tool.Room && dragStart.HasValue && hover.HasValue)
            {
                var a = dragStart.Value; var b = hover.Value;
                int x = Mathf.Min(a.x, b.x), y = Mathf.Min(a.y, b.y);
                int w = Mathf.Abs(a.x - b.x) + 1, h = Mathf.Abs(a.y - b.y) + 1;
                if (w * h >= 2)
                {
                    PushUndo();
                    var room = new JObject { ["id"] = UniqueRoomId(), ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h, ["theme"] = "office" };
                    Rooms.Add(room);
                    SelectRoom(room);
                    Rebuild();
                }
            }
            dragStart = null;
        }

        bool MouseTile(out Vector2Int tile)
        {
            tile = default;
            var world = Gm.World;
            if (world == null || world.Height == 0 || H == 0) return false;
            var ray = Gm.CameraRig.Camera.ScreenPointToRay(Input.mousePosition);
            // Wall tops first so a wall under the cursor is picked where it is drawn.
            if (Hit(ray, -1.35f, out var p) && ToTile(p, out tile) && rows[tile.y][tile.x] == '#') return true;
            return Hit(ray, 0f, out p) && ToTile(p, out tile);
        }

        static bool Hit(Ray ray, float z, out Vector3 p)
        {
            p = default;
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return false;
            float d = (z - ray.origin.z) / ray.direction.z;
            if (d < 0f) return false;
            p = ray.origin + ray.direction * d;
            return true;
        }

        bool ToTile(Vector3 p, out Vector2Int tile)
        {
            int x = Mathf.FloorToInt(p.x), y = Gm.World.Height - 1 - Mathf.FloorToInt(p.y);
            tile = new Vector2Int(x, y);
            return x >= 0 && y >= 0 && y < H && x < W;
        }

        // ------------------------------------------------------------------ level edits

        static string Id(JObject o) => o.Value<string>("id");
        static string Type(JObject o) => o.Value<string>("type");
        static string Key(JObject o) => o.Value<string>("key");
        static int Wd(JObject o) => Mathf.Max(1, o.Value<int?>("w") ?? 1);
        static int Ht(JObject o) => Mathf.Max(1, o.Value<int?>("h") ?? 1);

        JObject ObjectAt(Vector2Int c) =>
            Objects.Cast<JObject>().LastOrDefault(o =>
                c.x >= o.Value<int>("x") && c.x < o.Value<int>("x") + Wd(o) &&
                c.y >= o.Value<int>("y") && c.y < o.Value<int>("y") + Ht(o));

        JObject RoomAt(Vector2Int c) =>
            Rooms.Cast<JObject>().LastOrDefault(r =>
                c.x >= r.Value<int>("x") && c.x < r.Value<int>("x") + r.Value<int>("w") &&
                c.y >= r.Value<int>("y") && c.y < r.Value<int>("y") + r.Value<int>("h"));

        string UniqueId(string prefix)
        {
            var ids = new HashSet<string>(Objects.Cast<JObject>().Select(Id));
            for (int i = 1; ; i++) if (!ids.Contains($"{prefix}_{i}")) return $"{prefix}_{i}";
        }

        string UniqueRoomId()
        {
            var ids = new HashSet<string>(Rooms.Cast<JObject>().Select(r => r.Value<string>("id")));
            for (int i = 1; ; i++) if (!ids.Contains($"room_{i}")) return $"room_{i}";
        }

        // New object with the offline server's conventions: doors/lasers/fire own a key,
        // triggers get theirs when linked (they show their target's key).
        JObject AddObject(string type, Vector2Int c)
        {
            string id = UniqueId(type);
            var o = new JObject { ["id"] = id, ["type"] = type, ["x"] = c.x, ["y"] = c.y };
            switch (type)
            {
                case "button_door": case "code_door": case "key_door":
                    o["key"] = id; State[id] = false; break;
                case "exit_door":
                    o["key"] = "exit_open"; State["exit_open"] = false; break;
                case "lasers": case "fire":
                    o["key"] = id; State[id] = true; break;
                case "bombable_wall":
                    o["key"] = id + ".broken"; State[id + ".broken"] = false;
                    AddRule($"{id}:use_bomb", new JObject { ["holding"] = "bomb" },
                        new JObject { ["set"] = new JObject { [id + ".broken"] = true } },
                        new JObject { ["consume"] = "bomb" },
                        new JObject { ["fx"] = "explosion", ["at"] = id });
                    break;
                case "key": case "bomb":
                    o["key"] = id; State[id] = "home"; break;
                case "latch_button":
                    o["key"] = id; State[id] = false;
                    AddRule($"{id}:press", null, new JObject { ["set"] = new JObject { [id] = true } }, new JObject { ["fx"] = "final_button" });
                    break;
                case "code_panel":
                    o["key"] = "code_" + id;
                    Codes.Add("code_" + id);
                    break;
                case "keypad":
                    o["interact"] = "submit"; break;
                case "boss":
                    o["patrol"] = new JArray(new JArray(c.x, c.y), new JArray(c.x + 3, c.y));
                    o["chaseRadius"] = 4; o["speed"] = 2.2; o["chaseSpeed"] = 3.4; o["debuff"] = 15;
                    break;
            }
            if (type == "key_door")
                AddRule($"{id}:use_key", new JObject { ["holding"] = "key" },
                    new JObject { ["set"] = new JObject { [id] = true } },
                    new JObject { ["consume"] = "key" },
                    new JObject { ["fx"] = "door_clunk", ["at"] = id });
            Objects.Add(o);
            status = $"Placed {id}." + (Triggers.Contains(type) && type != "latch_button" ? " Use Link to connect it." : "");
            return o;
        }

        void RemoveObject(JObject o)
        {
            string id = Id(o);
            o.Remove();
            foreach (var r in Rules.Cast<JObject>().Where(r => (r.Value<string>("on") ?? "").StartsWith(id + ":")).ToList()) r.Remove();
            ColorMap.Remove(id);
            if (Type(o) == "code_panel") foreach (var c in Codes.Where(c => c.ToString() == Key(o)).ToList()) c.Remove();
            if (selected == o) selected = null;
            if (linkSource == o) linkSource = null;
            status = $"Deleted {id}.";
        }

        void DeleteSelection()
        {
            if (selected != null) { PushUndo(); RemoveObject(selected); Rebuild(); }
            else if (selectedRoom != null) { PushUndo(); selectedRoom.Remove(); status = "Room deleted."; selectedRoom = null; Rebuild(); }
        }

        JObject RuleFor(string on, bool create = true)
        {
            var rule = Rules.Cast<JObject>().FirstOrDefault(r => r.Value<string>("on") == on);
            if (rule == null && create) { rule = new JObject { ["on"] = on, ["do"] = new JArray() }; Rules.Add(rule); }
            return rule;
        }

        void AddRule(string on, JObject requires, params JObject[] steps)
        {
            var rule = RuleFor(on);
            if (requires != null) rule["requires"] = requires;
            var list = (JArray)rule["do"];
            foreach (var s in steps)
                if (!list.Any(x => JToken.DeepEquals(x, s))) list.Add(s);
        }

        static bool IsToggle(string type) => type is "switch" or "lever" or "laser_switch" or "light_switch";
        static string ActionOf(JObject trigger) => trigger.Value<string>("interact") ?? (Type(trigger) == "keypad" ? "submit" : "press");

        // trigger → target, in the fake world's vocabulary.
        string Link(JObject src, JObject dst)
        {
            string s = Id(src), d = Id(dst), dt = Type(dst), dk = Key(dst);
            if (src == dst) return "Pick a different object to link to.";

            if (Type(src) == "keypad")
            {
                if (dt == "code_panel")
                {
                    RuleFor($"{s}:submit")["requires"] = new JObject { ["code"] = dk };
                    return $"{s} now needs the code shown on {d}.";
                }
                if (dk == null) return $"{d} has no key to open.";
                src["key"] = dk; // the keypad lights green when its door opens
                AddRule($"{s}:submit", null, new JObject { ["set"] = new JObject { [dk] = true } }, new JObject { ["fx"] = "door_open", ["at"] = d });
                return $"{s} opens {d}. Link it to a code panel to require that code.";
            }

            if (dk == null) return $"{d} has no state key; it can't be controlled.";
            bool toggle = IsToggle(Type(src));
            JObject step; string fx;
            switch (dt)
            {
                case "lasers":
                    step = toggle ? new JObject { ["toggle"] = dk } : new JObject { ["set"] = new JObject { [dk] = false } };
                    fx = "laser_zap"; break;
                case "fire":
                    step = new JObject { ["set"] = new JObject { [dk] = false } }; fx = "fire_hiss"; break;
                case "bombable_wall":
                    step = new JObject { ["set"] = new JObject { [dk] = true } }; fx = "explosion"; break;
                default:
                    step = toggle ? new JObject { ["toggle"] = dk } : new JObject { ["set"] = new JObject { [dk] = true } };
                    fx = "door_clunk"; break;
            }
            AddRule($"{s}:{ActionOf(src)}", null, step, new JObject { ["fx"] = fx, ["at"] = d });
            if (string.IsNullOrEmpty(Key(src))) src["key"] = dk; // triggers show their target's state
            return $"{s} → {d} linked.";
        }

        string LinkRoom(JObject src, JObject room)
        {
            if (room == null) return "Click an object or a room to link to.";
            string s = Id(src), rid = room.Value<string>("id");
            string lights = room.Value<string>("lights"), flooded = room.Value<string>("flooded") ?? room.Value<string>("water");
            if (Type(src) == "light_switch" || (lights != null && flooded == null))
            {
                if (lights == null) { lights = $"{rid}.lights"; room["lights"] = lights; State[lights] = false; }
                AddRule($"{s}:{ActionOf(src)}", null, new JObject { ["toggle"] = lights }, new JObject { ["fx"] = "light_switch" });
                if (string.IsNullOrEmpty(Key(src))) src["key"] = lights;
                return $"{s} switches the lights in {rid}.";
            }
            if (flooded == null) { flooded = $"{rid}.flooded"; room["flooded"] = flooded; State[flooded] = true; }
            AddRule($"{s}:{ActionOf(src)}", null, new JObject { ["set"] = new JObject { [flooded] = false } }, new JObject { ["fx"] = "water_drain" });
            if (string.IsNullOrEmpty(Key(src))) src["key"] = flooded;
            return $"{s} drains {rid}.";
        }

        // exit_open = every latch button pressed, whenever the level has latch buttons.
        void SyncExit()
        {
            var latches = Objects.Cast<JObject>().Where(o => Type(o) is "latch_button" or "final_button").Select(Key).Where(k => k != null).ToList();
            if (latches.Count > 0) Derived["exit_open"] = string.Join(" && ", latches);
            else Derived.Remove("exit_open");
        }

        void Select(JObject o)
        {
            selected = o; selectedRoom = null;
            editId = Id(o) ?? ""; editKey = Key(o) ?? ""; editInteract = o.Value<string>("interact") ?? "";
            editW = Wd(o).ToString(); editH = Ht(o).ToString();
            editColor = ColorMap.Value<string>(editId) ?? o.Value<string>("color") ?? "";
            var extra = new JObject(o.Properties().Where(p => !new[] { "id", "type", "x", "y", "w", "h", "key", "interact", "color" }.Contains(p.Name)));
            editExtra = extra.ToString(Formatting.None);
            GUIUtility.keyboardControl = 0;
        }

        void SelectRoom(JObject r)
        {
            selectedRoom = r; selected = null;
            roomId = r.Value<string>("id") ?? ""; roomTheme = r.Value<string>("theme") ?? "office";
            roomDark = r.Value<string>("lights") != null;
            roomFlooded = (r.Value<string>("flooded") ?? r.Value<string>("water")) != null;
            GUIUtility.keyboardControl = 0;
        }

        void ApplyObject()
        {
            PushUndo();
            string oldId = Id(selected), newId = editId.Trim();
            if (newId.Length > 0 && newId != oldId)
            {
                if (Objects.Cast<JObject>().Any(o => o != selected && Id(o) == newId)) { status = $"Id {newId} is taken."; return; }
                foreach (var r in Rules.Cast<JObject>())
                {
                    var on = r.Value<string>("on") ?? "";
                    if (on.StartsWith(oldId + ":")) r["on"] = newId + on.Substring(oldId.Length);
                    foreach (var step in r["do"] ?? new JArray())
                        if (step["at"]?.Type == JTokenType.String && step.Value<string>("at") == oldId) step["at"] = newId;
                }
                ColorMap.Remove(oldId);
                selected["id"] = newId;
            }
            string id = Id(selected);
            SetOrRemove(selected, "key", editKey.Trim());
            SetOrRemove(selected, "interact", editInteract.Trim());
            selected["w"] = int.TryParse(editW, out var w) ? Mathf.Clamp(w, 1, 64) : 1;
            selected["h"] = int.TryParse(editH, out var h) ? Mathf.Clamp(h, 1, 64) : 1;
            if (editColor.Length > 0) ColorMap[id] = editColor; else ColorMap.Remove(id);
            try
            {
                var extra = JObject.Parse(string.IsNullOrWhiteSpace(editExtra) ? "{}" : editExtra);
                foreach (var p in selected.Properties().Where(p => !new[] { "id", "type", "x", "y", "w", "h", "key", "interact" }.Contains(p.Name)).ToList()) p.Remove();
                foreach (var p in extra.Properties()) selected[p.Name] = p.Value;
            }
            catch (JsonException e) { status = "Extra JSON: " + e.Message; return; }
            if (!string.IsNullOrEmpty(Key(selected)) && State[Key(selected)] == null) State[Key(selected)] = false;
            Rebuild();
            status = $"{id} updated.";
        }

        void ApplyRoom()
        {
            PushUndo();
            string id = roomId.Trim().Length > 0 ? roomId.Trim() : selectedRoom.Value<string>("id");
            selectedRoom["id"] = id;
            selectedRoom["theme"] = roomTheme;
            if (roomDark && selectedRoom["lights"] == null) { selectedRoom["lights"] = $"{id}.lights"; State[$"{id}.lights"] = false; }
            if (!roomDark) selectedRoom.Remove("lights");
            if (roomFlooded && selectedRoom["flooded"] == null && selectedRoom["water"] == null) { selectedRoom["flooded"] = $"{id}.flooded"; State[$"{id}.flooded"] = true; }
            if (!roomFlooded) { selectedRoom.Remove("flooded"); selectedRoom.Remove("water"); }
            Rebuild();
            status = $"Room {id} updated.";
        }

        static void SetOrRemove(JObject o, string name, string value)
        {
            if (value.Length > 0) o[name] = value; else o.Remove(name);
        }

        void Resize(int dw, int dh)
        {
            int nw = Mathf.Clamp(W + dw, 5, 200), nh = Mathf.Clamp(H + dh, 5, 200);
            if (nw == W && nh == H) return;
            PushUndo();
            var next = new List<char[]>();
            for (int y = 0; y < nh; y++)
            {
                var row = new char[nw];
                for (int x = 0; x < nw; x++) row[x] = y < H && x < W ? rows[y][x] : '#';
                next.Add(row);
            }
            rows = next;
            Rebuild();
            status = $"Map is {W}×{H}.";
        }

        void NewLevel(int w, int h)
        {
            PushUndo();
            var tiles = new JArray();
            for (int y = 0; y < h; y++)
                tiles.Add(new string(Enumerable.Range(0, w).Select(x => x == 0 || y == 0 || x == w - 1 || y == h - 1 ? '#' : '.').ToArray()));
            var doc = new JObject
            {
                ["side"] = "A",
                ["codes"] = new JArray(),
                ["derived"] = new JObject(),
                ["rules"] = new JArray(),
                ["world"] = new JObject
                {
                    ["camera"] = new JObject { ["radius"] = 8, ["darkRadius"] = 2.4 },
                    ["tiles"] = tiles,
                    ["rooms"] = new JArray(new JObject { ["id"] = "room_1", ["x"] = 1, ["y"] = 1, ["w"] = w - 2, ["h"] = h - 2, ["theme"] = "office" }),
                    ["objects"] = new JArray(),
                    ["spawn"] = new JArray(2, 2),
                    ["state"] = new JObject(),
                    ["colors"] = new JObject(),
                },
            };
            SetLevel(doc);
            Rebuild(keepPlayer: false);
            status = $"New {w}×{h} level.";
        }

        void Save()
        {
            World["tiles"] = new JArray(rows.Select(r => new string(r)));
            SyncExit();
            levelName = LevelStore.Sanitise(levelName);
            var path = LevelStore.Save(levelName, level);
            status = "Saved " + path;
        }

        void LoadLevel(string name)
        {
            var doc = LevelStore.Load(name);
            if (doc == null) { status = "Could not load " + name; return; }
            PushUndo();
            SetLevel(doc);
            levelName = name == LevelStore.Default ? "my_level" : name;
            Rebuild(keepPlayer: false);
            showLoad = false;
            status = "Loaded " + name;
        }

        // ------------------------------------------------------------------ scripting API
        // The panel's operations by id, for building or patching levels from code.

        public string Status => status;
        public int MapWidth => W;
        public int MapHeight => H;

        public void CreateLevel(int w, int h) { if (open) NewLevel(w, h); }

        public void SetTile(int x, int y, char c)
        {
            if (!open || y < 0 || y >= H || x < 0 || x >= W) return;
            rows[y][x] = c;
        }

        public string Place(string type, int x, int y) => open ? Id(AddObject(type, new Vector2Int(x, y))) : null;

        public string LinkIds(string source, string target)
        {
            if (!open) return "Editor closed.";
            var s = Objects.Cast<JObject>().FirstOrDefault(o => Id(o) == source);
            var t = Objects.Cast<JObject>().FirstOrDefault(o => Id(o) == target);
            if (s == null || t == null) return "Unknown id.";
            status = Link(s, t);
            return status;
        }

        public void Apply() { if (open) Rebuild(); }
        public void SaveAs(string name) { if (!open) return; levelName = name; Save(); }
        public void Open(string name) { if (open) LoadLevel(name); }

        // ------------------------------------------------------------------ gizmos

        void DrawGizmos()
        {
            if (gizmoRoot == null) gizmoRoot = new GameObject("LevelEditorGizmos").transform;
            gizmoRoot.gameObject.SetActive(true);
            poolUsed = 0;
            var world = Gm.World;
            if (world != null && world.Height > 0)
            {
                foreach (JObject r in Rooms)
                    Outline(r.Value<int>("x"), r.Value<int>("y"), r.Value<int>("w"), r.Value<int>("h"),
                        r == selectedRoom ? new Color(0.3f, 1f, 0.6f, 0.9f) : new Color(1f, 1f, 1f, 0.25f), 0.05f);
                var spawn = World["spawn"] as JArray;
                if (spawn != null && spawn.Count >= 2) Fill(spawn[0].Value<int>(), spawn[1].Value<int>(), new Color(0.3f, 0.8f, 1f, 0.35f));
                if (selected != null) Outline(selected.Value<int>("x"), selected.Value<int>("y"), Wd(selected), Ht(selected), new Color(1f, 0.85f, 0.2f, 1f), 0.08f);
                if (linkSource != null) Outline(linkSource.Value<int>("x"), linkSource.Value<int>("y"), Wd(linkSource), Ht(linkSource), new Color(0.3f, 0.9f, 1f, 1f), 0.1f);
                if (tool == Tool.Room && dragStart.HasValue && hover.HasValue)
                {
                    var a = dragStart.Value; var b = hover.Value;
                    Outline(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1, new Color(0.3f, 1f, 0.6f, 0.9f), 0.08f);
                }
                if (hover.HasValue) Fill(hover.Value.x, hover.Value.y, new Color(1f, 1f, 1f, 0.3f));
            }
            for (int i = poolUsed; i < pool.Count; i++) pool[i].enabled = false;
        }

        SpriteRenderer Quad()
        {
            if (poolUsed == pool.Count)
            {
                var sr = SpriteFactory.Child(gizmoRoot, "Gizmo", SpriteFactory.Square, Color.white, Layers.Vision - 5);
                pool.Add(sr);
            }
            var q = pool[poolUsed++];
            q.enabled = true;
            return q;
        }

        float TopAt(int x, int y) => y >= 0 && y < H && x >= 0 && x < W && rows[y][x] == '#' ? -1.37f : -0.04f;

        void Fill(int x, int y, Color c)
        {
            var r = Gm.World.TileRect(x, y);
            var q = Quad();
            q.color = c;
            q.transform.position = new Vector3(r.center.x, r.center.y, TopAt(x, y));
            q.transform.localScale = new Vector3(r.width * 0.96f, r.height * 0.96f, 1f);
        }

        void Outline(int x, int y, int w, int h, Color c, float t)
        {
            var r = Gm.World.TileRect(x, y, w, h);
            float z = -0.04f;
            Bar(new Vector3(r.center.x, r.yMax, z), new Vector2(r.width, t), c);
            Bar(new Vector3(r.center.x, r.yMin, z), new Vector2(r.width, t), c);
            Bar(new Vector3(r.xMin, r.center.y, z), new Vector2(t, r.height), c);
            Bar(new Vector3(r.xMax, r.center.y, z), new Vector2(t, r.height), c);
        }

        void Bar(Vector3 p, Vector2 size, Color c)
        {
            var q = Quad();
            q.color = c;
            q.transform.position = p;
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        // ------------------------------------------------------------------ panel

        void OnGUI()
        {
            if (!open) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
            header ??= new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(label) { fontSize = 11 };

            float scale = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale, sh = Screen.height / scale;
            var area = new Rect(sw - PanelWidth - 10, 10, PanelWidth, sh - 20);
            GUI.color = new Color(0, 0, 0, 0.82f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("Level editor  <size=11>(F3)</size>", header);
            GUILayout.BeginHorizontal();
            levelName = GUILayout.TextField(levelName, GUILayout.Width(150));
            if (GUILayout.Button("Save")) Save();
            if (GUILayout.Button(showLoad ? "Hide" : "Load")) showLoad = !showLoad;
            GUILayout.EndHorizontal();
            if (showLoad)
            {
                loadScroll = GUILayout.BeginScrollView(loadScroll, GUILayout.Height(120));
                if (GUILayout.Button(LevelStore.Default + " (built-in)")) LoadLevel(LevelStore.Default);
                foreach (var n in LevelStore.List()) if (GUILayout.Button(n)) LoadLevel(n);
                GUILayout.EndScrollView();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New 24×16")) NewLevel(24, 16);
            if (GUILayout.Button("New 40×25")) NewLevel(40, 25);
            if (GUILayout.Button("Undo")) Undo();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("<b>Tool</b>", label);
            var names = System.Enum.GetNames(typeof(Tool));
            int picked = GUILayout.SelectionGrid((int)tool, names, 4);
            if (picked != (int)tool) { tool = (Tool)picked; linkSource = null; }
            GUILayout.Label(ToolHint(), small);

            if (tool == Tool.Object)
            {
                int i = System.Array.IndexOf(ObjectTypes, placeType);
                int ni = GUILayout.SelectionGrid(Mathf.Max(0, i), ObjectTypes, 2);
                placeType = ObjectTypes[ni];
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Map</b> {W}×{H}", label, GUILayout.Width(90));
            if (GUILayout.Button("W−")) Resize(-1, 0);
            if (GUILayout.Button("W+")) Resize(1, 0);
            if (GUILayout.Button("H−")) Resize(0, -1);
            if (GUILayout.Button("H+")) Resize(0, 1);
            GUILayout.EndHorizontal();

            if (selected != null) ObjectInspector();
            else if (selectedRoom != null) RoomInspector();

            GUILayout.Space(8);
            GUILayout.Label($"<b>Rules</b> {Rules.Count}   <b>codes</b> {Codes.Count}", label);
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status, small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string ToolHint() => tool switch
        {
            Tool.Select => "Click an object or room to edit it. Drag an object to move it.",
            Tool.Wall => "Paint walls (drag).",
            Tool.Floor => "Paint floor (drag).",
            Tool.Erase => "Paint void, outside the building (drag).",
            Tool.Spawn => "Click where the player starts.",
            Tool.Object => "Click to place the type below; click an object to select it.",
            Tool.Room => "Drag a rectangle to make a room (theme, dark, flooded).",
            Tool.Link => "Click a trigger, then what it controls (door, lasers, fire, wall, panel, room).",
            _ => "",
        };

        void ObjectInspector()
        {
            GUILayout.Space(6);
            GUILayout.Label($"<b>{Type(selected)}</b> at {selected.Value<int>("x")},{selected.Value<int>("y")}", label);
            editId = Field("id", editId);
            editKey = Field("key", editKey);
            editInteract = Field("interact", editInteract);
            GUILayout.BeginHorizontal();
            GUILayout.Label("w", label, GUILayout.Width(60)); editW = GUILayout.TextField(editW, GUILayout.Width(40));
            GUILayout.Label("h", label, GUILayout.Width(20)); editH = GUILayout.TextField(editH, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("glow", label, GUILayout.Width(60));
            int ci = GUILayout.Toolbar(Mathf.Max(0, System.Array.IndexOf(Colors, editColor)), new[] { "–", "A", "B", "both", "info" });
            editColor = Colors[ci];
            GUILayout.EndHorizontal();
            GUILayout.Label("extra JSON", small);
            editExtra = GUILayout.TextArea(editExtra, GUILayout.MinHeight(40));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply")) ApplyObject();
            if (GUILayout.Button("Delete")) DeleteSelection();
            GUILayout.EndHorizontal();

            string id = Id(selected);
            foreach (var r in Rules.Cast<JObject>().Where(r => (r.Value<string>("on") ?? "").StartsWith(id + ":")).ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(r["do"]?.ToString(Formatting.None) ?? "", small);
                if (GUILayout.Button("x", GUILayout.Width(22))) { PushUndo(); r.Remove(); Rebuild(); }
                GUILayout.EndHorizontal();
            }
        }

        void RoomInspector()
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>room</b>", label);
            roomId = Field("id", roomId);
            int ti = GUILayout.SelectionGrid(Mathf.Max(0, System.Array.IndexOf(Themes, roomTheme)), Themes, 3);
            roomTheme = Themes[ti];
            roomDark = GUILayout.Toggle(roomDark, " dark (needs a light switch)");
            roomFlooded = GUILayout.Toggle(roomFlooded, " flooded (needs a valve)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply")) ApplyRoom();
            if (GUILayout.Button("Delete")) DeleteSelection();
            GUILayout.EndHorizontal();
        }

        string Field(string name, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, label, GUILayout.Width(60));
            value = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
