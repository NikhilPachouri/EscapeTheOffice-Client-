using System.Collections.Generic;
using System.Linq;
using EscapeOffice.Net;
using EscapeOffice.Objects;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EscapeOffice
{
    // Builds this side of the floor from a `world` message. The client bundles no map data:
    // everything here comes from the server and is thrown away on the next `world`.
    //
    // Coordinates: the server uses 0-based (column, row) with row 0 at the top of the ASCII
    // grid. One tile is one world unit; row r maps to world y = Height - 1 - r.
    public class World : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public CameraSettings Camera { get; private set; } = new CameraSettings();
        public DebuffSettings Debuff { get; private set; } = new DebuffSettings();
        public Vector2 Spawn { get; private set; }

        public readonly Dictionary<string, WorldObject> Objects = new Dictionary<string, WorldObject>();
        public readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        public readonly List<Blocker> Blockers = new List<Blocker>();
        // Light sources outside the player's radius (burning fire): xy = position, z = radius.
        public readonly List<Vector3> Lights = new List<Vector3>();

        Transform root;
        char[][] grid;

        public Vector2 TileCenter(int x, int y) => new Vector2(x + 0.5f, Height - 1 - y + 0.5f);
        public Rect TileRect(int x, int y, int w = 1, int h = 1) =>
            new Rect(x, Height - (y + h), Mathf.Max(1, w), Mathf.Max(1, h));

        public Vector2 TileCenter(JToken at)
        {
            if (at is JArray arr && arr.Count >= 2) return TileCenter(arr[0].Value<int>(), arr[1].Value<int>());
            if (at is JObject o && o["x"] != null && o["y"] != null) return TileCenter(o.Value<int>("x"), o.Value<int>("y"));
            return Vector2.zero;
        }

        public bool IsWall(int x, int y) =>
            y < 0 || y >= Height || x < 0 || x >= grid[y].Length || grid[y][x] == '#';

        public bool IsFloor(int x, int y) => !IsWall(x, y) && grid[y][x] != ' ';

        public void Clear()
        {
            if (root != null) Destroy(root.gameObject);
            root = null;
            Objects.Clear();
            Rooms.Clear();
            Blockers.Clear();
            Lights.Clear();
            Width = Height = 0;
        }

        public void Build(WorldData data, WorldState state)
        {
            Clear();
            root = new GameObject("World").transform;
            root.SetParent(transform, false);
            Camera = data.Camera ?? new CameraSettings();
            Debuff = data.Debuff ?? new DebuffSettings();

            BuildTiles(ParseRows(data.Tiles));

            if (data.Spawn != null && data.Spawn.Length >= 2) Spawn = TileCenter(data.Spawn[0], data.Spawn[1]);
            else Spawn = new Vector2(Width * 0.5f, Height * 0.5f);

            var roomsRoot = new GameObject("Rooms").transform;
            roomsRoot.SetParent(root, false);
            foreach (var def in data.Rooms ?? new List<RoomDef>())
            {
                if (string.IsNullOrEmpty(def.Id)) continue;
                if (!Rooms.TryGetValue(def.Id, out var room))
                {
                    Rooms[def.Id] = room = new Room(def.Id, def.Lights, def.WaterKey, def.Theme);
                    room.Register(state);
                    room.Changed += OnRoomChanged;
                }
                room.AddRect(TileRect(def.X, def.Y, def.W, def.H), roomsRoot);
            }

            var objectsRoot = new GameObject("Objects").transform;
            objectsRoot.SetParent(root, false);
            foreach (var def in data.Objects ?? new List<ObjectDef>())
            {
                if (string.IsNullOrEmpty(def.Id)) { Debug.LogWarning($"[world] object without id ({def.Type}) skipped"); continue; }
                if (Objects.ContainsKey(def.Id)) { Debug.LogWarning($"[world] duplicate object id {def.Id}"); continue; }

                if (string.IsNullOrEmpty(def.Color) && data.Colors != null && data.Colors.TryGetValue(def.Id, out var color))
                    def.Color = color;
                var obj = ObjectFactory.Create(def, objectsRoot);
                var center = TileRect(def.X, def.Y, def.W, def.H).center;
                obj.Rooms.AddRange(Rooms.Values.Where(r => r.Contains(center)));
                obj.Init(this, def);
                Objects[def.Id] = obj;
                if (obj is Blocker b) Blockers.Add(b);
                if (!string.IsNullOrEmpty(def.Key)) state.Register(def.Key, obj);
            }

            BuildArt(data);
            state.ApplyAll();
        }

        void OnRoomChanged(Room room)
        {
            foreach (var o in Objects.Values)
                if (o.Rooms.Contains(room)) o.RefreshGlow();
        }

        public bool IsDarkAt(WorldObject obj) => obj.Rooms.Any(r => r.IsDark);

        static string[] ParseRows(JToken tiles)
        {
            if (tiles == null) return new string[0];
            if (tiles.Type == JTokenType.Array) return tiles.Select(t => t.ToString()).ToArray();
            return tiles.ToString().Replace("\r", "").Split('\n').Where(r => r.Length > 0).ToArray();
        }

        void BuildTiles(string[] rows)
        {
            Height = rows.Length;
            Width = rows.Length == 0 ? 0 : rows.Max(r => r.Length);
            grid = rows.Select(r => r.PadRight(Width, ' ').ToCharArray()).ToArray();

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root, false);
            gridGo.AddComponent<Grid>().cellSize = new Vector3(1, 1, 0);

            var floorMap = MakeTilemap(gridGo.transform, "Floor", Layers.Floor);
            var wallMap = MakeTilemap(gridGo.transform, "Walls", Layers.Wall);

            var floorTile = MakeTile(Palette.Floor, Tile.ColliderType.None);
            var wallTile = MakeTile(Palette.Wall, Tile.ColliderType.Grid);

            var floorCells = new List<Vector3Int>();
            var wallCells = new List<Vector3Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                char c = grid[y][x];
                if (c == ' ') continue; // void outside the building
                var cell = new Vector3Int(x, Height - 1 - y, 0);
                if (c == '#') wallCells.Add(cell);
                else floorCells.Add(cell);
            }
            floorMap.SetTiles(floorCells.ToArray(), Enumerable.Repeat<TileBase>(floorTile, floorCells.Count).ToArray());
            wallMap.SetTiles(wallCells.ToArray(), Enumerable.Repeat<TileBase>(wallTile, wallCells.Count).ToArray());

            // Tilemap + TilemapCollider2D + CompositeCollider2D for the static walls.
            var body = wallMap.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = wallMap.gameObject.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = wallMap.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        // ---------------------------------------------------------------- 3D art
        // The tilemaps above stay as the colliders; with the asset pack present they are hidden
        // and the floor is drawn with themed tiles, walls, corner decor and a key light.

        void BuildArt(WorldData data)
        {
            if (!Art.Available) return;
            foreach (var r in root.GetComponentsInChildren<TilemapRenderer>()) r.enabled = false;

            var tiles = new GameObject("Tiles3D");
            tiles.transform.SetParent(root, false);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                char c = grid[y][x];
                if (c == ' ') continue;
                var p = TileCenter(x, y);
                if (c == '#')
                {
                    Art.Spawn(TouchesFloor(x, y) ? "Wall_Full" : "Wall_Low", tiles.transform, p);
                    continue;
                }
                var floor = new Vector3(p.x, p.y, Art.FloorDepth);
                string checker = ((x + y) & 1) == 1 ? "A" : "B";
                if (Art.Spawn($"Floor_{Capitalise(ThemeAt(p))}_{checker}", tiles.transform, floor) == null)
                    Art.Spawn($"Floor_Office_{checker}", tiles.transform, floor);
                Art.Spawn("Floor_Grout", tiles.transform, floor);
            }

            BuildDecor(data, tiles.transform);
            StaticBatchingUtility.Combine(tiles);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            // From the south and a little west, so shadows fall away from the camera.
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, 0.55f, 1f));
            // Warm key light against cool shadows: the colour contrast that keeps it from looking flat.
            sun.color = new Color(1f, 0.9f, 0.76f);
            sun.intensity = 0.68f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.3f, 0.4f);
        }

        string ThemeAt(Vector2 p)
        {
            foreach (var r in Rooms.Values)
                if (r.Contains(p)) return r.Theme;
            return "hallway"; // doorways and cells outside every room
        }

        bool TouchesFloor(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (ny < 0 || ny >= Height || nx < 0 || nx >= Width) continue;
                char c = grid[ny][nx];
                if (c != '#' && c != ' ') return true;
            }
            return false;
        }

        // One themed prop in each free corner of every room big enough to spare it. A corner
        // cell never cuts a room in two, and corners near objects, doorways or the spawn stay
        // empty so no puzzle is ever blocked.
        void BuildDecor(WorldData data, Transform parent)
        {
            var taken = new HashSet<Vector2Int>();
            foreach (var o in data.Objects ?? new List<ObjectDef>())
                for (int x = o.X - 1; x <= o.X + Mathf.Max(1, o.W); x++)
                for (int y = o.Y - 1; y <= o.Y + Mathf.Max(1, o.H); y++)
                    taken.Add(new Vector2Int(x, y));
            if (data.Spawn != null && data.Spawn.Length >= 2)
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    taken.Add(new Vector2Int(data.Spawn[0] + dx, data.Spawn[1] + dy));

            foreach (var def in data.Rooms ?? new List<RoomDef>())
            {
                if (def.W < 4 || def.H < 4 || !Rooms.TryGetValue(def.Id ?? "", out var room)) continue;
                var props = Art.Catalog.DecorFor(room.Theme);
                if (props == null || props.Count == 0) continue;

                var corners = new[]
                {
                    new Vector2Int(def.X, def.Y), new Vector2Int(def.X + def.W - 1, def.Y),
                    new Vector2Int(def.X, def.Y + def.H - 1), new Vector2Int(def.X + def.W - 1, def.Y + def.H - 1),
                };
                foreach (var c in corners)
                {
                    if (taken.Contains(c) || IsWall(c.x, c.y) || NearDoorway(c, def)) continue;
                    string prefab = props[Mathf.Abs(c.x * 73856093 ^ c.y * 19349663) % props.Count];
                    var p = TileCenter(c.x, c.y);
                    if (Art.Spawn(prefab, parent, p, Art.WallYaw(this, c.x, c.y)) == null) continue;
                    taken.Add(c);
                    if (prefab == "Decor_CafeTable") continue; // walkable around, per the pack
                    var block = new GameObject($"DecorBlock {c.x},{c.y}");
                    block.transform.SetParent(root, false);
                    block.transform.position = p;
                    block.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
                }
            }
        }

        // Any floor next to the corner that lies outside the room is a doorway.
        bool NearDoorway(Vector2Int c, RoomDef room)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = c.x + dx, y = c.y + dy;
                bool inside = x >= room.X && x < room.X + room.W && y >= room.Y && y < room.Y + room.H;
                if (!inside && !IsWall(x, y) && grid[y][x] != ' ') return true;
            }
            return false;
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static Tilemap MakeTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return map;
        }

        static Tile MakeTile(Color color, Tile.ColliderType collider)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = SpriteFactory.Square;
            tile.color = color;
            tile.colliderType = collider;
            return tile;
        }
    }
}
