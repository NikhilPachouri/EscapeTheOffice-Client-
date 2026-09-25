using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace EscapeOffice
{
    // The presentation layer's settings, loaded from Resources/ArtDirection.json: world light and
    // grading, how interactables present themselves, per-side player looks, room light pools, props
    // and hero objects. Gameplay never reads this; every field has a default so a missing file or
    // key still renders.
    public class ArtDirection
    {
        public class WorldLook
        {
            public string sunColor = "#ffd6a0";
            public float sunIntensity = 0.5f;
            public string ambient = "#232a38";
            public string fog = "#04050a";
            public float fogAlpha = 0.965f;
        }

        public class CameraLook
        {
            public float height = 22f, behind = 8f, fov = 40f;
        }

        public class GradeLook
        {
            public float saturation = 1f, contrast = 1.12f, exposure = 1.02f, splitTone = 0.45f;
            public string highlights = "#ffe9cc", shadows = "#bcd0ff";
            public float bloomThreshold = 0.9f, bloomKnee = 0.08f, bloom = 0.55f;
            public float vignette = 0.55f, vignetteStart = 0.35f;
            public float tiltShift = 0.75f, tiltFocus = 0.28f;
        }

        public class InteractionLook
        {
            public float nearDistance = 3.2f;
            public float idlePool = 0.1f, nearPool = 0.28f, focusPool = 0.55f, poolSize = 1.5f;
            public float react = 0.05f, focusRing = 0.55f;
        }

        public class PlayerLook
        {
            public string lamp = "#ffe2b8";
            public float lampIntensity = 0.65f;
            public float width = 1f, height = 1f, hop = 0.07f, stepRate = 11f, lean = 9f, sway = 3f;
            public string accessory;
        }

        public class RoomLook
        {
            public string light = "#ffe2b8";
            public float pool = 0.18f;
            public List<string> props = new List<string>();
            public float density = 0.15f;
            public string hero;
        }

        public class HeroPart
        {
            public string prefab, shape, material;
            public float[] pos, rot, scale;
            public float spin;
            public List<HeroPart> children;
        }

        public class HeroDef
        {
            public string name;
            public float scale = 1f;
            public List<HeroPart> parts = new List<HeroPart>();
        }

        public WorldLook world = new WorldLook();
        public CameraLook camera = new CameraLook();
        public GradeLook grading = new GradeLook();
        public InteractionLook interaction = new InteractionLook();
        public Dictionary<string, float> objectScale = new Dictionary<string, float>();
        public Dictionary<string, PlayerLook> players = new Dictionary<string, PlayerLook>();
        public Dictionary<string, RoomLook> rooms = new Dictionary<string, RoomLook>();
        public Dictionary<string, HeroDef> heroes = new Dictionary<string, HeroDef>();

        static ArtDirection current;

        public static ArtDirection Current
        {
            get
            {
                if (current != null) return current;
                var asset = Resources.Load<TextAsset>("ArtDirection");
                try { current = asset != null ? JsonConvert.DeserializeObject<ArtDirection>(asset.text) : null; }
                catch (JsonException e) { Debug.LogWarning("[art] ArtDirection.json: " + e.Message); }
                return current ??= new ArtDirection();
            }
        }

        public PlayerLook Player(string side) =>
            players.TryGetValue(side ?? "A", out var p) ? p : new PlayerLook();

        public RoomLook Room(string theme) =>
            theme != null && rooms.TryGetValue(theme, out var r) ? r : new RoomLook();

        public float ScaleFor(string model) =>
            model != null && objectScale.TryGetValue(model, out var s) ? s : 1f;

        public static Color Hex(string hex, Color fallback = default) =>
            ColorUtility.TryParseHtmlString(hex ?? "", out var c) ? c : fallback;

        public static Vector3 V(float[] v, Vector3 fallback) =>
            v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : fallback;
    }
}
