using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice
{
    public class KeyListener : IStateListener
    {
        readonly Action<JToken> apply;
        public KeyListener(Action<JToken> apply) { this.apply = apply; }
        public void Apply(JToken value) => apply(value);
    }

    // One named room: one or more rectangles sharing an id (an L-shaped room is two).
    // Area effects: lights (true = on) and water (true = the whole room is impassable).
    public class Room
    {
        public readonly string Id;
        public readonly string LightsKey;
        public readonly string WaterKey;
        public readonly string Theme;
        public readonly List<Rect> Rects = new List<Rect>();

        public bool IsDark { get; private set; }
        public bool IsFlooded { get; private set; }

        public event Action<Room> Changed;

        readonly List<SpriteRenderer> darkOverlays = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> waterOverlays = new List<SpriteRenderer>();
        readonly List<GameObject> waterModels = new List<GameObject>();
        readonly List<Collider2D> waterColliders = new List<Collider2D>();

        public Room(string id, string lightsKey, string waterKey, string theme = null)
        {
            Id = id;
            Theme = string.IsNullOrEmpty(theme) ? GuessTheme(id) : theme.ToLowerInvariant();
            LightsKey = string.IsNullOrEmpty(lightsKey) ? null : lightsKey;
            WaterKey = string.IsNullOrEmpty(waterKey) ? null : waterKey;
        }

        public bool Contains(Vector2 p)
        {
            foreach (var r in Rects) if (r.Contains(p)) return true;
            return false;
        }

        public void AddRect(Rect rect, Transform parent)
        {
            Rects.Add(rect);

            var go = new GameObject($"Room {Id} #{Rects.Count}");
            go.transform.SetParent(parent, false);
            go.transform.position = rect.center;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = rect.size;
            go.AddComponent<RoomZone>().Room = this;

            var dark = SpriteFactory.Child(go.transform, "Dark", SpriteFactory.Square, new Color(0, 0, 0, 0.6f),
                Layers.RoomOverlay, scale: rect.size);
            if (Art.Available) dark.transform.localPosition = new Vector3(0, 0, -1.4f); // over the models, under wall tops
            dark.enabled = false;
            darkOverlays.Add(dark);

            var water = SpriteFactory.Child(go.transform, "Water", SpriteFactory.Square, Palette.Water,
                Layers.RoomOverlay, scale: rect.size);
            water.enabled = false;
            waterOverlays.Add(water);

            // A solid box over the room while flooded; the sprite is 1x1 so the collider size is 1.
            var solid = water.gameObject.AddComponent<BoxCollider2D>();
            solid.size = Vector2.one;
            solid.enabled = false;
            waterColliders.Add(solid);

            if (Art.Available)
            {
                water.color = Color.clear; // the tiles below replace the flat overlay
                var tiles = new GameObject("Water3D");
                tiles.transform.SetParent(go.transform, false);
                for (int x = 0; x < rect.width; x++)
                for (int y = 0; y < rect.height; y++)
                    Art.Spawn("Water_Tile", tiles.transform, new Vector3(x + 0.5f - rect.width * 0.5f, y + 0.5f - rect.height * 0.5f, 0f));
                tiles.SetActive(false);
                waterModels.Add(tiles);
            }
        }

        // The server may omit themes; pick one from the room's id so floors still vary.
        static string GuessTheme(string id)
        {
            var n = (id ?? "").ToLowerInvariant();
            string[][] table =
            {
                new[] { "hallway", "corridor", "hallway", "wing", "passage", "bay" },
                new[] { "lobby", "lobby", "reception", "entrance" },
                new[] { "office", "office", "hall", "desk", "security" },
                new[] { "archive", "archive", "record", "library", "file" },
                new[] { "workshop", "workshop", "garage", "maint" },
                new[] { "vault", "vault", "safe", "storage" },
                new[] { "lab", "lab", "laser", "science" },
                new[] { "server", "server", "data", "rack" },
                new[] { "boiler", "boiler", "furnace", "engine" },
                new[] { "cafe", "cafe", "kitchen", "canteen", "break" },
                new[] { "final", "final", "roof", "stair", "exit" },
            };
            foreach (var row in table)
                for (int i = 1; i < row.Length; i++)
                    if (n.Contains(row[i])) return row[0];
            return "office";
        }

        public void Register(WorldState state)
        {
            if (LightsKey != null) state.Register(LightsKey, new KeyListener(v => SetDark(!WorldState.Truthy(v))));
            if (WaterKey != null) state.Register(WaterKey, new KeyListener(v => SetFlooded(WorldState.Truthy(v))));
        }

        void SetDark(bool dark)
        {
            IsDark = dark;
            foreach (var o in darkOverlays) o.enabled = dark;
            Changed?.Invoke(this);
        }

        bool waterSeen;

        void SetFlooded(bool flooded)
        {
            // Drained (not on load): splashes and spray over the room.
            if (waterSeen && IsFlooded && !flooded)
                foreach (var r in Rects)
                {
                    var c = new Vector3(r.center.x, r.center.y, -0.2f);
                    int n = Mathf.Clamp(Mathf.RoundToInt(r.width * r.height * 0.6f), 12, 60);
                    Fx3D.Burst(c, new Color(0.45f, 0.75f, 1f), count: n, speed: Mathf.Max(r.width, r.height) * 0.6f, size: 0.2f, life: 0.9f);
                    Fx3D.Puff(c, new Color(0.8f, 0.9f, 1f, 0.35f), count: n / 2, radius: Mathf.Min(r.width, r.height) * 0.4f, size: 0.8f, life: 1.5f, rise: 0.6f);
                }
            waterSeen = true;
            IsFlooded = flooded;
            foreach (var o in waterOverlays) o.enabled = flooded;
            foreach (var m in waterModels) m.SetActive(flooded);
            foreach (var c in waterColliders) c.enabled = flooded;
            Changed?.Invoke(this);
        }
    }
}
