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
        public readonly List<Rect> Rects = new List<Rect>();

        public bool IsDark { get; private set; }
        public bool IsFlooded { get; private set; }

        public event Action<Room> Changed;

        readonly List<SpriteRenderer> darkOverlays = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> waterOverlays = new List<SpriteRenderer>();
        readonly List<Collider2D> waterColliders = new List<Collider2D>();

        public Room(string id, string lightsKey, string waterKey)
        {
            Id = id;
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

        void SetFlooded(bool flooded)
        {
            IsFlooded = flooded;
            foreach (var o in waterOverlays) o.enabled = flooded;
            foreach (var c in waterColliders) c.enabled = flooded;
            Changed?.Invoke(this);
        }
    }
}
