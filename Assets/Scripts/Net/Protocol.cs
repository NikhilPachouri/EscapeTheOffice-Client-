using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EscapeOffice.Net
{
    // Wire contract with the Go server. Every message is { "type": ..., "data": ... }.
    // Change only with both halves of the team present.
    public static class MsgType
    {
        // Client -> server
        public const string Join = "join";
        public const string Interact = "interact";
        public const string Enter = "enter";
        public const string Room = "room";

        // Server -> client
        public const string Assigned = "assigned";
        public const string Waiting = "waiting";
        public const string World = "world";
        public const string Patch = "patch";
        public const string Fx = "fx";
        public const string GameComplete = "game_complete";
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
        [JsonProperty("side")] public string Side;
        [JsonProperty("token")] public string Token;
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
        // Glow colour per object id, derived by the server from the keys its rules write:
        // "A", "B" or "both". Objects not listed get no side colour.
        [JsonProperty("colors")] public Dictionary<string, string> Colors = new Dictionary<string, string>();
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
        // State keys this room reads. lights: true = on. water/flooded: true = impassable.
        [JsonProperty("lights")] public string Lights;
        [JsonProperty("water")] public string Water;
        [JsonProperty("flooded")] public string Flooded;
        // Floor/decor look (lobby, office, archive, ...). Optional; guessed from the id when absent.
        [JsonProperty("theme")] public string Theme;

        public string WaterKey => !string.IsNullOrEmpty(Water) ? Water : Flooded;
    }
}
