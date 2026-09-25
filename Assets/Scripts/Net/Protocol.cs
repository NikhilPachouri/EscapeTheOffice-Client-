using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EscapeOffice.Net
{
    // Wire contract with the Go server. Every message is { "type": ..., "data": ... }.
    // Change only with both halves of the team present.
    public static class MsgType
    {
        // Client -> server
        public const string Create = "create"; // { } — server makes the room, code comes back in `assigned`
        public const string Join = "join";
        public const string Interact = "interact";
        public const string Enter = "enter";
        public const string Log = "log"; // { msg }: client diagnostics, printed in the server log
        public const string Room = "room";
        public const string Leave = "leave"; // { } — ends the game for both players

        // Server -> client
        public const string Assigned = "assigned";
        public const string Waiting = "waiting";
        public const string World = "world";
        public const string Patch = "patch";
        public const string Fx = "fx";
        public const string GameComplete = "game_complete";
        public const string GameOver = "game_over"; // { reason: partner_left | partner_timeout }; socket closes after
        public const string Error = "error"; // { reason }, e.g. "room full"; not in the contract table
    }

    public class Envelope
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("data")] public JToken Data;
    }

    // ---- Client -> server ----

    public class JoinData
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("token", NullValueHandling = NullValueHandling.Ignore)] public string Token;
    }

    public class InteractData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("action")] public string Action;
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)] public string Value;
    }

    public class EnterData
    {
        [JsonProperty("portalId")] public string PortalId;
    }

    public class RoomData
    {
        [JsonProperty("id")] public string Id;
    }

    // ---- Server -> client ----

    public class AssignedData
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("side")] public string Side;
        [JsonProperty("token")] public string Token;
        // Optional: URL of a separate voice relay (wss://host/voice). Absent when voice runs on the game server.
        [JsonProperty("voice")] public string Voice;
    }

    public class FxData
    {
        [JsonProperty("effect")] public string Effect;
        // Source position: [x, y] in tiles, { "x":..,"y":.. }, or an object id.
        [JsonProperty("at")] public JToken At;
    }

    public class CameraSettings
    {
        [JsonProperty("radius")] public float Radius = 8f;
        [JsonProperty("darkRadius")] public float DarkRadius = 2f;
    }

    public class DebuffSettings
    {
        [JsonProperty("seconds")] public float Seconds = 15f;
        [JsonProperty("speed")] public float Speed = 0.55f;
        [JsonProperty("radius")] public float Radius = 3.2f;
    }

    public class WorldData
    {
        // ASCII rows (string[]) or one newline-separated string. '#' wall, '.' floor.
        [JsonProperty("tiles")] public JToken Tiles;
        [JsonProperty("objects")] public List<ObjectDef> Objects = new List<ObjectDef>();
        [JsonProperty("rooms")] public List<RoomDef> Rooms = new List<RoomDef>();
        [JsonProperty("spawn")] public int[] Spawn;
        [JsonProperty("state")] public Dictionary<string, JToken> State = new Dictionary<string, JToken>();

        // Optional extras from world.json; defaults are used when the server omits them.
        [JsonProperty("side")] public string Side;
        [JsonProperty("tileSize")] public int TileSize = 32;
        [JsonProperty("camera")] public CameraSettings Camera = new CameraSettings();
        [JsonProperty("debuff")] public DebuffSettings Debuff = new DebuffSettings();
        // Glow colour per object id, derived by the server from the keys its rules write:
        // "A", "B" or "both". Objects not listed get no side colour.
        [JsonProperty("colors")] public Dictionary<string, string> Colors = new Dictionary<string, string>();

        // The server sends each room as { id, rects: [[x,y,w,h], ...] }. Everything client-side reads a
        // single x/y/w/h, so expand to one RoomDef per rect sharing the id (World merges them by id).
        // This matches what WorldFile does for the offline path.
        [OnDeserialized]
        void ExpandRects(StreamingContext _)
        {
            if (Rooms == null) return;
            var flat = new List<RoomDef>(Rooms.Count);
            foreach (var r in Rooms)
            {
                if (r?.Rects == null || r.Rects.Count == 0) { flat.Add(r); continue; }
                foreach (var rc in r.Rects)
                {
                    if (rc == null || rc.Length < 4) continue;
                    flat.Add(new RoomDef
                    {
                        Id = r.Id, X = rc[0], Y = rc[1], W = rc[2], H = rc[3],
                        Lights = r.Lights, Water = r.Water, Flooded = r.Flooded, Theme = r.Theme,
                    });
                }
            }
            Rooms = flat;
        }
    }

    public class ObjectDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("type")] public string Type;
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
        [JsonProperty("w")] public int W = 1;
        [JsonProperty("h")] public int H = 1;
        [JsonProperty("key")] public string Key;
        [JsonProperty("interact")] public string Interact;
        // Glow colour; filled from WorldData.Colors when the server sends it there.
        [JsonProperty("color")] public string Color;
        [JsonProperty("dim")] public bool Dim;

        // Anything type-specific: code (panels), path/chaseRadius/speed (boss), ...
        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();

        public T Get<T>(string name, T fallback = default)
        {
            if (Extra != null && Extra.TryGetValue(name, out var tok) && tok.Type != JTokenType.Null)
            {
                try { return tok.ToObject<T>(); } catch { /* fall through */ }
            }
            return fallback;
        }
    }

    public class RoomDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
        [JsonProperty("w")] public int W;
        [JsonProperty("h")] public int H;
        // Server form: one or more [x, y, w, h] rects. Expanded into separate RoomDefs on load.
        [JsonProperty("rects")] public List<int[]> Rects;
        // State keys this room reads. lights: true = on. water/flooded: true = impassable.
        [JsonProperty("lights")] public string Lights;
        [JsonProperty("water")] public string Water;
        [JsonProperty("flooded")] public string Flooded;
        // Floor/decor look (lobby, office, archive, ...). Optional; guessed from the id when absent.
        [JsonProperty("theme")] public string Theme;

        public string WaterKey => !string.IsNullOrEmpty(Water) ? Water : Flooded;
    }
}
