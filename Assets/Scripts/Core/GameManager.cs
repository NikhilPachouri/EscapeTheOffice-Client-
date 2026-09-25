using System.Collections.Generic;
using System.Linq;
using EscapeOffice.Net;
using EscapeOffice.Objects;
using EscapeOffice.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice
{
    // Entry point and message router. The server owns the building; this client owns its own
    // player and renders its side as a function of the state it receives.
    public class GameManager : MonoBehaviour
    {
        public enum Phase { Join, Connecting, Waiting, Playing, Complete }

        public static GameManager Instance { get; private set; }

        public Phase Current { get; private set; } = Phase.Join;
        public string Side { get; private set; } = "A";
        public string Status { get; private set; } = "";
        public string RoomCode { get; private set; } = "";
        public bool Offline => link is FakeServer;

        public WorldState State { get; } = new WorldState();
        public World World { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public FxPlayer Fx { get; private set; }
        public GameUI UI { get; private set; }

        public bool InputEnabled => Current == Phase.Playing && !UI.IsModal;
        public bool DebugNoFog { get; set; }

        public readonly List<string> MessageLog = new List<string>();
        const int MessageLogSize = 14;

        IServerLink link;
        GameConnection connection;
        string lastRoomSent;
        int worldSession = -1;

        // Deployed Go server. The join screen still lets you type another (e.g. a local server for the demo).
        public const string DefaultServerUrl = "wss://phoenix-zv1i.onrender.com/ws";
        public const string PrefLastCode = "eto.lastCode";
        public const string PrefLastToken = "eto.lastToken";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Works in any scene: the main scene only needs this object, and even that is optional.
            if (FindAnyObjectByType<GameManager>() == null) new GameObject("GameManager").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true; // two clients side by side on one machine
            Application.targetFrameRate = 60;

            World = new GameObject("WorldRoot").AddComponent<World>();
            World.transform.SetParent(transform, false);
            Fx = gameObject.AddComponent<FxPlayer>();
            UI = gameObject.AddComponent<GameUI>();
            gameObject.AddComponent<DebugOverlay>();

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0, 0, -10);
            CameraRig = cam.GetComponent<CameraRig>();
            if (CameraRig == null) CameraRig = cam.gameObject.AddComponent<CameraRig>();
        }

        // ---------------------------------------------------------------- connecting

        public void Join(string url, string code, string token = null)
        {
            Leave();
            RoomCode = code.Trim().ToUpperInvariant();

            connection = gameObject.AddComponent<GameConnection>();
            Attach(connection);
            Current = Phase.Connecting;
            connection.Connect(url, RoomCode, token);
        }

        public void StartOffline()
        {
            Leave();
            RoomCode = "OFFLINE";
            var fake = gameObject.AddComponent<FakeServer>();
            Attach(fake);
            Current = Phase.Connecting;
            fake.Begin();
        }

        void Attach(IServerLink l)
        {
            link = l;
            link.MessageReceived += OnMessage;
            link.StatusChanged += s => Status = s;
        }

        public void Leave()
        {
            if (link != null)
            {
                link.MessageReceived -= OnMessage;
                link.Disconnect();
                Destroy((MonoBehaviour)link);
            }
            link = null;
            connection = null;
            worldSession = -1;
            ClearWorld();
            Current = Phase.Join;
            Status = "";
        }

        void ClearWorld()
        {
            if (Player != null) Destroy(Player.gameObject);
            Player = null;
            World.Clear();
            State.Reset(null);
            lastRoomSent = null;
        }

        // ---------------------------------------------------------------- inbound

        void OnMessage(string type, JToken data)
        {
            Log("◀ " + type, data);
            switch (type)
            {
                case MsgType.Assigned:
                    var a = data.ToObject<AssignedData>();
                    Side = string.IsNullOrEmpty(a.Side) ? Side : a.Side;
                    if (!Offline && !string.IsNullOrEmpty(a.Token))
                    {
                        PlayerPrefs.SetString(PrefLastCode, RoomCode);
                        PlayerPrefs.SetString(PrefLastToken, a.Token);
                        PlayerPrefs.Save();
                    }
                    if (Current != Phase.Playing) Current = Phase.Waiting;
                    break;

                case MsgType.Waiting:
                    Current = Phase.Waiting;
                    break;

                case MsgType.World:
                    BuildWorld(data.ToObject<WorldData>());
                    break;

                case MsgType.Patch:
                    if (data is JObject patch) State.Patch(patch);
                    break;

                case MsgType.Fx:
                    if (World.Width > 0) Fx.Play(data.ToObject<FxData>());
                    break;

                case MsgType.GameComplete:
                    Current = Phase.Complete;
                    Fx.PlayAt("complete", null);
                    PlayerPrefs.DeleteKey(PrefLastToken);
                    break;

                case MsgType.Error:
                    Status = data?["message"]?.ToString() ?? data?.ToString() ?? "Server error";
                    if (Current != Phase.Playing) { link?.Disconnect(); Current = Phase.Join; }
                    break;

                default:
                    Debug.LogWarning($"[net] unhandled message type '{type}'");
                    break;
            }
        }

        void BuildWorld(WorldData data)
        {
            if (!string.IsNullOrEmpty(data.Side)) Side = data.Side;

            // A world on a fresh connection is a reconnect: keep the player where they were.
            // A world on the same connection is a hot reload with fresh state: back to spawn.
            int session = connection != null ? connection.Session : 0;
            bool reconnect = Player != null && worldSession >= 0 && session != worldSession;
            Vector2 keep = Player != null ? Player.Position : Vector2.zero;
            worldSession = session;

            if (Player != null) Destroy(Player.gameObject);
            Player = null;
            lastRoomSent = null;

            State.Reset(data.State);
            World.Build(data, State);

            var spawn = World.Spawn;
            if (reconnect && !World.IsWall(Mathf.FloorToInt(keep.x), World.Height - 1 - Mathf.FloorToInt(keep.y))) spawn = keep;
            Player = PlayerController.Spawn(spawn, Side, World.transform);
            CameraRig.transform.position = new Vector3(spawn.x, spawn.y, -10);
            Current = Phase.Playing;
            Status = "";
        }

        // ---------------------------------------------------------------- outbound

        public void SendInteract(string id, string action, string value = null) =>
            Send(MsgType.Interact, new InteractData { Id = id, Action = action, Value = value });

        public void SendEnter(string portalId) => Send(MsgType.Enter, new EnterData { PortalId = portalId });

        public void SendRoom(string id)
        {
            if (id == lastRoomSent) return;
            lastRoomSent = id;
            Send(MsgType.Room, new RoomData { Id = id });
        }

        void Send(string type, object data)
        {
            if (link == null || Current == Phase.Complete) return;
            Log("▶ " + type, JToken.FromObject(data));
            link.Send(type, data);
        }

        // ---------------------------------------------------------------- helpers

        public string InventoryKey => "inv_" + Side;

        public List<string> Inventory()
        {
            var inv = State.Get(InventoryKey);
            var items = new List<string>();
            switch (inv)
            {
                case JArray arr:
                    items.AddRange(arr.Select(t => t.ToString()));
                    break;
                case JObject obj:
                    foreach (var p in obj.Properties())
                    {
                        bool held = p.Value.Type == JTokenType.String ? p.Value.ToString() == "held" : WorldState.Truthy(p.Value);
                        if (held) items.Add(p.Name);
                    }
                    break;
                case JValue v when v.Type == JTokenType.String && v.ToString().Length > 0:
                    items.Add(v.ToString());
                    break;
            }
            return items;
        }

        public bool HasItem(string item) => Inventory().Any(i => i.ToLowerInvariant().Contains(item));

        public void Toast(string message) => UI.Toast(message);

        public void PlayLocal(string effect, Vector2 at) => Fx.PlayAt(effect, at);

        void Log(string label, JToken data)
        {
            string body = data == null ? "" : data.ToString(Formatting.None);
            if (body.Length > 160) body = body.Substring(0, 157) + "…";
            MessageLog.Add($"{Time.time,7:0.0}  {label}  {body}");
            if (MessageLog.Count > MessageLogSize) MessageLog.RemoveAt(0);
        }
    }
}
