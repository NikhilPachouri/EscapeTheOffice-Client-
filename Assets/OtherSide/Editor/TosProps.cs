using System.Collections.Generic;
using System.Linq;
using EscapeOffice;
using UnityEditor;
using UnityEngine;

// Port of tos-assets.js (three.js r128): bookshelf, serverRack, machine, officeChair and
// officeTable, built as prefabs in Assets/OtherSide/Prefabs/Props (run from Build Assets).
//
// The builders keep the JS numbers: 1 unit = 1 tile, sitting on y = 0, front toward +Z, back
// (the wall) at -Z. three.js is right-handed, so positions mirror X and rotations are converted.
// Decor variants are muted to the art direction (no full-strength side colours, dimmer LEDs);
// the machine keeps its energy colours. Static parts are merged into one mesh per prop; moving
// parts stay separate for TosPropAnimator.
public static partial class TosProps
{
    const string PrefabDir = "Assets/OtherSide/Prefabs/Props";
    const string MatDir = "Assets/OtherSide/Materials/Props";
    const string MeshDir = "Assets/OtherSide/Meshes/Props";

    public static int BuildAll()
    {
        OtherSideImporter.EnsureFolder(PrefabDir);
        OtherSideImporter.EnsureFolder(MatDir);
        OtherSideImporter.EnsureFolder(MeshDir);
        int n = 0;
        foreach (var (name, seed) in new[] { ("TOS_Bookshelf_A", "archive-a"), ("TOS_Bookshelf_B", "archive-b"), ("TOS_Bookshelf_C", "archive-c") })
        { var k = new Kit(name, true); Bookshelf(k, seed); Save(k); n++; }
        foreach (var (name, seed) in new[] { ("TOS_ServerRack_A", "rack-a"), ("TOS_ServerRack_B", "rack-b") })
        { var k = new Kit(name, true); ServerRack(k, seed); Save(k); n++; }
        { var k = new Kit("TOS_OfficeChair", true); OfficeChair(k, k.root, 0.25f); Save(k); n++; }
        foreach (var (name, seed, mug) in new[] { ("TOS_OfficeTable_A", "desk-a", 0xd9503fu), ("TOS_OfficeTable_B", "desk-b", 0x3d6a9bu) })
        { var k = new Kit(name, true); OfficeTable(k, seed, mug); Save(k); n++; }
        foreach (var (name, seed, mug) in new[] { ("TOS_Workstation_A", "desk-c", 0xc9a44au), ("TOS_Workstation_B", "desk-d", 0xd9503fu) })
        {
            // desk + chair pulled up to it, as the office uses them
            var k = new Kit(name, true);
            OfficeTable(k, seed, mug);
            OfficeChair(k, k.Group("Chair", 0.08f, 0f, 0.46f, null, 0f, Mathf.PI + 0.35f, 0f), 0f);
            Save(k); n++;
        }
        { var k = new Kit("TOS_Machine_Lab", false); Machine(k, 0x8fd9c8, 0xb3bcff); Save(k); n++; }
        { var k = new Kit("TOS_Machine_Final", false); Machine(k, 0xff9f43, 0xc49bff); Save(k); n++; }
        n += BuildInteractables(); // tos-interactables.js
        n += BuildCharacters();    // tos-characters.js
        return n;
    }

    // ------------------------------------------------------------------ kit

    enum Shape { Box, Cyl, Cyl8, CylOpen, Sph, Ico, Cone, Torus, Handle, Plane, Hex, Ring, RingThick, Rim, Band }

    class Opt
    {
        public float metal = 0.05f, rough = 0.8f, emissiveI = 1f, opacity = 1f;
        public uint? emissive;
        public bool transparent, basic, additive;
        public Texture map;
    }

    class Kit
    {
        public readonly string name;
        public readonly bool muted;
        public readonly Transform root;
        public readonly HashSet<Transform> keep = new HashSet<Transform>();
        public readonly List<Transform> merge = new List<Transform>();
        // Parts sharing the tintable emissive ring material; gathered into one "Ring" renderer.
        public readonly List<Transform> ringParts = new List<Transform>();
        public System.Action<GameObject> configure;

        public Kit(string name, bool muted)
        {
            this.name = name;
            this.muted = muted;
            root = new GameObject(name).transform;
        }

        public Material M(uint hex, Opt o = null) => Mat(hex, o ?? new Opt(), muted);
        public Material Glow(uint hex, float i = 1.2f, float metal = 0.05f) => M(hex, new Opt { emissive = hex, emissiveI = i, metal = metal });

        public GameObject Add(Shape s, Material m, float sx, float sy, float sz, float x, float y, float z,
            Transform parent = null, float rx = 0f, float ry = 0f, float rz = 0f)
        {
            var go = new GameObject(s.ToString());
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(s);
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.SetParent(parent ?? root, false);
            go.transform.localPosition = P(x, y, z);
            var rot = Rot(rx, ry, rz);
            if (s == Shape.Plane) rot *= Quaternion.Euler(0f, 180f, 0f); // three planes face +Z, Unity quads -Z
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(sx, sy, sz);
            return go;
        }

        public Transform Group(string n, float x, float y, float z, Transform parent = null, float rx = 0f, float ry = 0f, float rz = 0f)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent ?? root, false);
            t.localPosition = P(x, y, z);
            t.localRotation = Rot(rx, ry, rz);
            return t;
        }
    }

    // three.js (right-handed) → Unity (left-handed): mirror X.
    static Vector3 P(float x, float y, float z) => new Vector3(-x, y, z);

    static Quaternion Rot(float rx, float ry, float rz)
    {
        // three's default Euler order XYZ: R = Rx · Ry · Rz; then mirror across X.
        var q = Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(ry * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(rz * Mathf.Rad2Deg, Vector3.forward);
        return new Quaternion(q.x, -q.y, -q.z, q.w);
    }

    // The game's deterministic rng (same as the JS).
    static System.Func<float> Rng(string seed)
    {
        uint h = 2166136261;
        foreach (char c in seed) h = (h ^ c) * 16777619;
        return () =>
        {
            h += 0x6D2B79F5;
            uint t = h;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296f;
        };
    }

    // ------------------------------------------------------------------ materials

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    static Material Mat(uint hex, Opt o, bool muted)
    {
        string key = $"{hex:x6}|{o.metal}|{o.rough}|{o.emissive}|{o.emissiveI}|{o.transparent}|{o.opacity}|{o.basic}|{o.additive}|{(o.map != null ? o.map.name : "")}|{muted}";
        if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;

        string path = $"{MatDir}/TOS_{Fnv(key):x8}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = o.additive ? Shader.Find("Legacy Shaders/Particles/Additive")
            : o.basic ? Shader.Find(o.map != null ? "Unlit/Texture" : "Unlit/Color")
            : Shader.Find("Standard");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;

        var c = C(hex);
        bool glowing = o.emissive.HasValue;
        if (muted && !o.basic && !o.additive) c = Mute(c, glowing ? 0.7f : 0.55f, glowing ? 1f : 0.92f);

        if (o.additive)
        {
            m.SetColor("_TintColor", new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, o.opacity * 0.5f));
            if (o.map != null) m.mainTexture = o.map;
            m.renderQueue = 3000;
        }
        else if (o.basic)
        {
            m.color = c;
            if (o.map != null) m.mainTexture = o.map;
        }
        else
        {
            m.color = new Color(c.r, c.g, c.b, o.opacity);
            m.SetFloat("_Metallic", o.metal);
            m.SetFloat("_Glossiness", 1f - o.rough);
            if (glowing)
            {
                var e = C(o.emissive.Value);
                if (muted) e = Mute(e, 0.7f, 1f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", e * o.emissiveI * (muted ? 0.55f : 0.85f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            OtherSideImporter.SetStandardFade(m, o.transparent);
        }
        EditorUtility.SetDirty(m);
        matCache[key] = m;
        return m;
    }

    static Color C(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    static Color Mute(Color c, float s, float v)
    {
        Color.RGBToHSV(c, out var h, out var sat, out var val);
        return Color.HSVToRGB(h, sat * s, Mathf.Min(1f, val * v));
    }

    static uint Fnv(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) h = (h ^ c) * 16777619;
        return h;
    }

    // ------------------------------------------------------------------ BOOKSHELF

    static void Bookshelf(Kit k, string seed)
    {
        var R = Rng(seed);
        const float W = 0.95f, H = 1.3f, D = 0.42f, t = 0.045f;
        var wood = k.M(0x6e4f36); var dark = k.M(0x553c29); var back = k.M(0x3b2b1f);

        k.Add(Shape.Box, wood, t, H, D, -W / 2 + t / 2, H / 2, 0);
        k.Add(Shape.Box, wood, t, H, D, W / 2 - t / 2, H / 2, 0);
        k.Add(Shape.Box, wood, W, t, D, 0, H - t / 2, 0);
        k.Add(Shape.Box, dark, W + 0.04f, 0.03f, D + 0.03f, 0, H + 0.015f, 0.005f);
        k.Add(Shape.Box, dark, W - 0.02f, 0.08f, D - 0.02f, 0, 0.04f, 0.005f);
        k.Add(Shape.Box, back, W - 2 * t, H - 0.1f, 0.02f, 0, H / 2 + 0.03f, -D / 2 + 0.012f);

        float[] shelves = { 0.38f, 0.68f, 0.98f };
        foreach (var y in shelves)
        {
            k.Add(Shape.Box, wood, W - 2 * t, 0.03f, D - 0.03f, 0, y, 0);
            k.Add(Shape.Box, dark, W - 2 * t, 0.035f, 0.015f, 0, y, D / 2 - 0.02f);
        }

        uint[] PAL = { 0x9b3d3d, 0x3d6a9b, 0xc9a44a, 0x4f7a4a, 0x7b5a8f, 0xd9d2c3, 0x2f3a4a, 0xb8663a };
        var band = k.M(0xe7d7a8, new Opt { metal = 0.4f, rough = 0.4f });
        var floors = new[] { 0.08f }.Concat(shelves.Select(y => y + 0.015f)).ToArray();
        var ceils = shelves.Select(y => y - 0.015f).Concat(new[] { H - t }).ToArray();
        float x0 = -W / 2 + t + 0.012f, x1 = W / 2 - t - 0.012f;

        for (int bay = 0; bay < floors.Length; bay++)
        {
            float fy = floors[bay], bayH = ceils[bay] - fy, x = x0;
            bool stackLeft = R() < 0.3f; const float stackW = 0.2f;
            if (stackLeft) x += stackW;
            while (x < x1 - 0.06f)
            {
                float bw = 0.04f + R() * 0.05f, bh = bayH * (0.62f + R() * 0.3f), bd = 0.24f + R() * 0.08f;
                if (x + bw > x1) break;
                if (R() < 0.07f) { x += 0.03f + R() * 0.05f; continue; }
                var mat = k.M(PAL[(int)(R() * PAL.Length)]);
                float z = D / 2 - 0.035f - bd / 2;
                k.Add(Shape.Box, mat, bw, bh, bd, x + bw / 2, fy + bh / 2, z);
                if (R() < 0.45f) k.Add(Shape.Box, band, bw + 0.002f, 0.014f, 0.004f, x + bw / 2, fy + bh * (0.72f + R() * 0.15f), z + bd / 2);
                x += bw + 0.003f;
                if (x1 - x < 0.2f && x1 - x > 0.1f && R() < 0.6f)
                {
                    float lh = bayH * 0.75f, lw = 0.045f, a = 0.32f;
                    var lm = k.M(PAL[(int)(R() * PAL.Length)]);
                    k.Add(Shape.Box, lm, lw, lh, 0.26f, x + Mathf.Sin(a) * lh / 2 + lw / 2, fy + Mathf.Cos(a) * lh / 2 + Mathf.Sin(a) * lw / 2, D / 2 - 0.17f, null, 0, 0, a);
                    break;
                }
            }
            if (stackLeft)
            {
                float sy = fy;
                int count = 2 + (int)(R() * 3);
                for (int i = 0; i < count; i++)
                {
                    float th = 0.035f + R() * 0.025f, sw = 0.15f + R() * 0.04f;
                    var sm = k.M(PAL[(int)(R() * PAL.Length)]);
                    float sd = 0.24f + R() * 0.04f, ry = (R() - 0.5f) * 0.25f;
                    k.Add(Shape.Box, sm, sw, th, sd, x0 + stackW / 2, sy + th / 2, D / 2 - 0.16f, null, 0, ry, 0);
                    sy += th;
                }
            }
        }

        // top dressing: small plant + archive box
        k.Add(Shape.Cyl, k.M(0xb86b45), 0.16f, 0.14f, 0.16f, 0.3f, H + 0.1f, 0);
        k.Add(Shape.Ico, k.M(0x3f8a4a), 0.26f, 0.24f, 0.26f, 0.3f, H + 0.26f, 0);
        k.Add(Shape.Box, k.M(0xcdb68c), 0.3f, 0.16f, 0.24f, -0.2f, H + 0.11f, -0.02f, null, 0, 0.12f, 0);
        k.Add(Shape.Box, k.M(0xf2ecdf), 0.12f, 0.05f, 0.005f, -0.2f, H + 0.12f, 0.1f, null, 0, 0.12f, 0);
    }

    // ------------------------------------------------------------------ SERVER RACK

    static void ServerRack(Kit k, string seed)
    {
        var R = Rng(seed);
        const float W = 0.85f, H = 1.32f, D = 0.85f;
        var shell = k.M(0x1b2129, new Opt { metal = 0.5f, rough = 0.4f });
        var frame = k.M(0x2a323d, new Opt { metal = 0.6f, rough = 0.35f });
        var face = k.M(0x333c48, new Opt { metal = 0.45f, rough = 0.45f });
        var slot = k.M(0x0c1015, new Opt { rough = 0.6f });

        k.Add(Shape.Box, shell, 0.04f, H - 0.08f, D, -W / 2 + 0.02f, H / 2 + 0.02f, 0);
        k.Add(Shape.Box, shell, 0.04f, H - 0.08f, D, W / 2 - 0.02f, H / 2 + 0.02f, 0);
        k.Add(Shape.Box, shell, W - 0.08f, H - 0.08f, 0.03f, 0, H / 2 + 0.02f, -D / 2 + 0.015f);
        k.Add(Shape.Box, frame, W, 0.05f, D, 0, H - 0.025f, 0);
        k.Add(Shape.Box, frame, W, 0.06f, D, 0, 0.07f, 0);
        foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
            k.Add(Shape.Cyl, k.M(0x111418), 0.07f, 0.04f, 0.07f, sx * (W / 2 - 0.07f), 0.02f, sz * (D / 2 - 0.07f));
        foreach (var sx in new[] { -1, 1 }) k.Add(Shape.Box, frame, 0.035f, H - 0.14f, 0.035f, sx * (W / 2 - 0.075f), H / 2, D / 2 - 0.08f);

        // LEDs blink in three groups (one renderer each) with their own rates.
        var LED = new[] { 0x4dff9au, 0x4dc3ffu, 0xffb84du }.Select(c => k.Glow(c, 1.4f)).ToArray();
        var ledRed = k.Glow(0xff4d4d, 1.6f);
        var groups = new Transform[3];
        for (int i = 0; i < 3; i++) { groups[i] = k.Group("LEDs" + i, 0, 0, 0); k.keep.Add(groups[i]); k.merge.Add(groups[i]); }
        int ledCount = 0;
        void Led(float x, float y, float z, Material mat, float w = 0.018f) =>
            k.Add(Shape.Box, mat, w, 0.012f, 0.006f, x, y, z, groups[ledCount++ % 3]);

        float fz = D / 2 - 0.07f, iw = W - 0.2f, yy = 0.13f;
        while (yy < H - 0.12f)
        {
            float r = R();
            if (r < 0.45f && yy + 0.12f < H - 0.08f)
            {
                const float u = 0.11f;
                k.Add(Shape.Box, face, iw, u, 0.02f, 0, yy + u / 2, fz);
                for (int i = 0; i < 6; i++)
                {
                    k.Add(Shape.Box, slot, 0.07f, u * 0.62f, 0.006f, -iw / 2 + 0.07f + i * 0.075f, yy + u / 2, fz + 0.012f);
                    Led(-iw / 2 + 0.05f + i * 0.075f, yy + u * 0.3f, fz + 0.016f, LED[i % 2]);
                }
                k.Add(Shape.Box, slot, 0.12f, 0.04f, 0.006f, iw / 2 - 0.1f, yy + u / 2, fz + 0.012f);
                Led(iw / 2 - 0.03f, yy + u * 0.72f, fz + 0.016f, R() < 0.12f ? ledRed : LED[0]);
                yy += u + 0.008f;
            }
            else if (r < 0.75f)
            {
                const float u = 0.055f;
                k.Add(Shape.Box, k.M(0x252c35, new Opt { metal = 0.5f, rough = 0.4f }), iw, u, 0.02f, 0, yy + u / 2, fz);
                for (int i = 0; i < 12; i++)
                {
                    k.Add(Shape.Box, slot, 0.026f, 0.022f, 0.006f, -iw / 2 + 0.04f + i * 0.034f, yy + u / 2, fz + 0.012f);
                    if (i % 2 == 0) Led(-iw / 2 + 0.04f + i * 0.034f, yy + u - 0.008f, fz + 0.016f, LED[1], 0.01f);
                }
                yy += u + 0.008f;
            }
            else
            {
                const float u = 0.08f;
                k.Add(Shape.Box, k.M(0x20262e, new Opt { metal = 0.5f, rough = 0.45f }), iw, u, 0.02f, 0, yy + u / 2, fz);
                for (int i = 0; i < 4; i++) k.Add(Shape.Box, slot, iw - 0.1f, 0.008f, 0.006f, 0, yy + 0.015f + i * 0.017f, fz + 0.012f);
                yy += u + 0.008f;
            }
        }

        // smoked glass door + handle
        var glass = k.M(0x6fb7ff, new Opt { transparent = true, opacity = 0.14f, rough = 0.08f, metal = 0.3f });
        k.Add(Shape.Box, glass, W - 0.06f, H - 0.12f, 0.012f, 0, H / 2 + 0.01f, D / 2 - 0.01f);
        foreach (var sx in new[] { -1, 1 }) k.Add(Shape.Box, frame, 0.03f, H - 0.12f, 0.02f, sx * (W / 2 - 0.03f), H / 2 + 0.01f, D / 2 - 0.01f);
        k.Add(Shape.Box, k.M(0xb9c2cc, new Opt { metal = 0.85f, rough = 0.25f }), 0.02f, 0.22f, 0.025f, W / 2 - 0.08f, H * 0.55f, D / 2 + 0.01f);
        var status = k.Add(Shape.Box, k.Glow(0x3ecfd8, 1.3f), W - 0.14f, 0.018f, 0.01f, 0, H - 0.07f, D / 2 + 0.003f);
        k.keep.Add(status.transform);

        // top fan under a grille: what the top-down camera sees most
        k.Add(Shape.Cyl, k.M(0x0c1015), 0.44f, 0.02f, 0.44f, 0, H + 0.003f, 0);
        var fan = k.Group("Fan", 0, H + 0.01f, 0);
        k.keep.Add(fan); k.merge.Add(fan);
        var blade = k.M(0x4a5462, new Opt { metal = 0.4f });
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.2566f;
            k.Add(Shape.Box, blade, 0.19f, 0.008f, 0.06f, Mathf.Cos(a) * 0.1f, 0, Mathf.Sin(a) * 0.1f, fan, 0.35f, -a, 0);
        }
        k.Add(Shape.Cyl, k.M(0x6b7580, new Opt { metal = 0.6f }), 0.07f, 0.02f, 0.07f, 0, H + 0.012f, 0);
        var grille = k.M(0x6b7580, new Opt { metal = 0.7f, rough = 0.35f });
        foreach (var s in new[] { 0.44f, 0.3f, 0.16f }) k.Add(Shape.Torus, grille, s, s, s * 1.2f, 0, H + 0.022f, 0, null, Mathf.PI / 2, 0, 0);
        k.Add(Shape.Box, grille, 0.44f, 0.01f, 0.012f, 0, H + 0.022f, 0);
        k.Add(Shape.Box, grille, 0.012f, 0.01f, 0.44f, 0, H + 0.022f, 0);
        uint[] cables = { 0x2f3a4a, 0x3ecfd8, 0x3d6a9b, 0xffb84d };
        for (int i = 0; i < cables.Length; i++) k.Add(Shape.Cyl, k.M(cables[i], new Opt { rough = 0.6f }), 0.035f, H - 0.1f, 0.035f, -0.22f + i * 0.045f, H / 2, -D / 2 + 0.06f);
        k.Add(Shape.Box, frame, 0.28f, 0.03f, 0.12f, -0.155f, H + 0.015f, -D / 2 + 0.08f);

        k.configure = go =>
        {
            var anim = go.AddComponent<TosPropAnimator>();
            anim.kind = TosPropAnimator.Kind.Rack;
            anim.fan = go.transform.Find("Fan");
            anim.ledGroups = Enumerable.Range(0, 3).Select(i => go.transform.Find("LEDs" + i)?.GetComponent<Renderer>()).ToArray();
            anim.ledRate = Enumerable.Range(0, 3).Select(_ => 1.5f + R() * 9f).ToArray();
            anim.ledPhase = Enumerable.Range(0, 3).Select(_ => R() * 10f).ToArray();
            anim.ledDuty = Enumerable.Range(0, 3).Select(_ => 0.2f + R() * 0.7f).ToArray();
            anim.status = go.transform.Find("Box")?.GetComponent<Renderer>();
        };
        status.name = "Status";
        k.configure += go => go.GetComponent<TosPropAnimator>().status = go.transform.Find("Status")?.GetComponent<Renderer>();
    }

    // ------------------------------------------------------------------ MACHINE

    static void Machine(Kit k, uint cA, uint cB)
    {
        var metal = k.M(0x2a2f38, new Opt { metal = 0.6f, rough = 0.3f });
        var metal2 = k.M(0x3d4550, new Opt { metal = 0.7f, rough = 0.3f });
        var chrome = k.M(0x9aa4ae, new Opt { metal = 0.9f, rough = 0.2f });

        k.Add(Shape.Cyl8, metal, 0.96f, 0.1f, 0.96f, 0, 0.05f, 0, null, 0, Mathf.PI / 8, 0);
        k.Add(Shape.Cyl8, metal2, 0.8f, 0.08f, 0.8f, 0, 0.14f, 0, null, 0, Mathf.PI / 8, 0);
        var trim = k.Add(Shape.Cyl8, k.M(cB, new Opt { emissive = cB, emissiveI = 1.1f }), 0.9f, 0.02f, 0.9f, 0, 0.105f, 0, null, 0, Mathf.PI / 8, 0);
        trim.name = "Trim"; k.keep.Add(trim.transform);

        var strip = k.Glow(cA, 1.3f);
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.PI / 4 + i * Mathf.PI / 2, r = 0.36f;
            var p = k.Group("Pylon" + i, Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r, null, 0, -a + Mathf.PI / 2, 0);
            k.Add(Shape.Box, metal, 0.1f, 0.95f, 0.08f, 0, 0.6f, 0, p);
            k.Add(Shape.Box, metal2, 0.13f, 0.05f, 0.11f, 0, 1.08f, 0, p);
            k.Add(Shape.Box, strip, 0.025f, 0.7f, 0.01f, 0, 0.6f, 0.045f, p);
            k.Add(Shape.Box, chrome, 0.06f, 0.06f, 0.2f, 0, 0.95f, 0.1f, p);
        }

        k.Add(Shape.Cyl, metal2, 0.34f, 0.08f, 0.34f, 0, 0.22f, 0);
        k.Add(Shape.Cyl, metal2, 0.34f, 0.08f, 0.34f, 0, 0.98f, 0);
        k.Add(Shape.Cyl, chrome, 0.28f, 0.03f, 0.28f, 0, 0.27f, 0);
        k.Add(Shape.Cyl, chrome, 0.28f, 0.03f, 0.28f, 0, 0.93f, 0);
        k.Add(Shape.CylOpen, k.M(cA, new Opt { transparent = true, opacity = 0.22f, rough = 0.05f, metal = 0.2f }), 0.26f, 0.64f, 0.26f, 0, 0.6f, 0);
        var column = k.Add(Shape.Cyl, k.M(cA, new Opt { additive = true, opacity = 0.9f }), 0.07f, 0.64f, 0.07f, 0, 0.6f, 0);
        column.name = "Column"; k.keep.Add(column.transform);
        var halo = k.Add(Shape.Cyl, k.M(cA, new Opt { additive = true, opacity = 0.25f }), 0.16f, 0.64f, 0.16f, 0, 0.6f, 0);
        halo.name = "Halo"; k.keep.Add(halo.transform);
        var crystal = k.Add(Shape.Ico, k.M(cB, new Opt { emissive = cB, emissiveI = 1.6f, rough = 0.2f, metal = 0.3f }), 0.16f, 0.24f, 0.16f, 0, 0.6f, 0);
        crystal.name = "Crystal"; k.keep.Add(crystal.transform);

        var gyro = k.Group("Gyro", 0, 0.6f, 0); k.keep.Add(gyro);
        k.Add(Shape.Torus, k.M(0xffd166, new Opt { metal = 0.85f, rough = 0.25f }), 0.52f, 0.52f, 0.52f, 0, 0, 0, gyro, Mathf.PI / 2, 0, 0);
        var inner = k.Group("Inner", 0, 0, 0, gyro); k.keep.Add(inner);
        k.Add(Shape.Torus, k.Glow(cA, 0.9f, 0.5f), 0.44f, 0.44f, 0.44f, 0, 0, 0, inner, 0, Mathf.PI / 2, 0);

        k.Add(Shape.Cyl, metal, 0.2f, 0.06f, 0.2f, 0, 1.05f, 0);
        k.Add(Shape.Sph, k.Glow(cB, 2f), 0.09f, 0.09f, 0.09f, 0, 1.11f, 0);
        var beam = k.Add(Shape.Cone, k.M(cB, new Opt { additive = true, opacity = 0.14f }), 0.22f, 0.5f, 0.22f, 0, 1.36f, 0, null, Mathf.PI, 0, 0);
        beam.name = "Beam"; k.keep.Add(beam.transform);

        var pipe = k.M(0x7c8a91, new Opt { metal = 0.7f, rough = 0.35f });
        foreach (var sx in new[] { -1f, 1f })
        {
            var pts = new[] { P(sx * 0.22f, 0.18f, -0.34f), P(sx * 0.3f, 0.55f, -0.4f), P(sx * 0.2f, 0.95f, -0.3f), P(sx * 0.06f, 1.0f, -0.12f) };
            var tube = new GameObject("Conduit");
            tube.transform.SetParent(k.root, false);
            tube.AddComponent<MeshFilter>().sharedMesh = Tube(CatmullRom(pts, 24), 0.025f, 8);
            tube.AddComponent<MeshRenderer>().sharedMaterial = pipe;
        }

        var con = k.Group("Console", 0, 0, 0.42f);
        k.Add(Shape.Box, metal, 0.36f, 0.34f, 0.1f, 0, 0.17f, 0, con);
        k.Add(Shape.Box, metal2, 0.4f, 0.04f, 0.2f, 0, 0.36f, 0.02f, con, -0.5f, 0, 0);
        var holo = k.Add(Shape.Plane, k.M(0xffffff, new Opt { additive = true, opacity = 0.9f }), 0.46f, 0.23f, 1f, 0, 0.6f, 0.02f, con, -0.25f, 0, 0);
        holo.name = "Holo"; k.keep.Add(holo.transform);
        k.Add(Shape.Box, k.Glow(cA, 1.4f), 0.34f, 0.012f, 0.012f, 0, 0.44f, 0.04f, con);

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(k.root, false);
        lightGo.transform.localPosition = new Vector3(0, 0.7f, 0);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point; light.color = C(cA); light.intensity = 1f; light.range = 3.5f;

        k.configure = go =>
        {
            var anim = go.AddComponent<TosPropAnimator>();
            anim.kind = TosPropAnimator.Kind.Machine;
            anim.colorA = C(cA); anim.colorB = C(cB);
            Transform F(string n) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            anim.gyro = F("Gyro"); anim.inner = F("Inner"); anim.crystal = F("Crystal");
            anim.column = F("Column"); anim.halo = F("Halo"); anim.beam = F("Beam");
            anim.trim = F("Trim")?.GetComponent<Renderer>();
            anim.columnR = anim.column?.GetComponent<Renderer>();
            anim.haloR = anim.halo?.GetComponent<Renderer>();
            anim.beamR = anim.beam?.GetComponent<Renderer>();
            anim.holoR = F("Holo")?.GetComponent<Renderer>();
            anim.glowLight = go.GetComponentInChildren<Light>();
        };
    }

    // ------------------------------------------------------------------ OFFICE CHAIR

    static void OfficeChair(Kit k, Transform parent, float swivel)
    {
        var fabric = k.M(0x39414c, new Opt { rough = 0.95f });
        var fabric2 = k.M(0x2b3038, new Opt { rough = 0.95f });
        var frame = k.M(0x1d2228, new Opt { metal = 0.3f, rough = 0.5f });
        var chrome = k.M(0xb9c2cc, new Opt { metal = 0.85f, rough = 0.25f });
        var accent = k.M(0x3ecfd8, new Opt { emissive = 0x3ecfd8, emissiveI = 0.25f });

        k.Add(Shape.Cyl, frame, 0.09f, 0.05f, 0.09f, 0, 0.07f, 0, parent);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5 + Mathf.PI / 2;
            var arm = k.Group("Leg", 0, 0, 0, parent, 0, -a, 0);
            k.Add(Shape.Box, chrome, 0.26f, 0.03f, 0.045f, 0.14f, 0.07f, 0, arm, 0, 0, -0.08f);
            k.Add(Shape.Box, frame, 0.03f, 0.04f, 0.04f, 0.265f, 0.05f, 0, arm);
            k.Add(Shape.Cyl, k.M(0x111418), 0.055f, 0.03f, 0.055f, 0.265f, 0.028f, 0, arm, Mathf.PI / 2, 0, 0);
        }
        k.Add(Shape.Cyl, frame, 0.07f, 0.12f, 0.07f, 0, 0.15f, 0, parent);
        k.Add(Shape.Cyl, chrome, 0.045f, 0.1f, 0.045f, 0, 0.26f, 0, parent);

        var top = k.Group("Top", 0, 0, 0, parent, 0, swivel, 0);
        k.Add(Shape.Box, frame, 0.22f, 0.03f, 0.2f, 0, 0.31f, 0, top);
        k.Add(Shape.Box, frame, 0.04f, 0.015f, 0.12f, 0.12f, 0.3f, 0.05f, top, 0, 0.4f, 0);
        k.Add(Shape.Box, fabric, 0.46f, 0.065f, 0.4f, 0, 0.355f, -0.01f, top);
        k.Add(Shape.Cyl, fabric, 0.07f, 0.46f, 0.07f, 0, 0.352f, 0.19f, top, 0, 0, Mathf.PI / 2);
        k.Add(Shape.Box, fabric2, 0.4f, 0.012f, 0.34f, 0, 0.392f, 0, top);
        k.Add(Shape.Box, frame, 0.07f, 0.36f, 0.035f, 0, 0.5f, -0.24f, top, -0.12f, 0, 0);
        var back = k.Group("Back", 0, 0.63f, -0.25f, top, -0.14f, 0, 0);
        k.Add(Shape.Box, frame, 0.46f, 0.44f, 0.03f, 0, 0, -0.012f, back);
        k.Add(Shape.Box, fabric2, 0.4f, 0.38f, 0.03f, 0, 0, 0.006f, back);
        k.Add(Shape.Box, fabric, 0.36f, 0.1f, 0.04f, 0, -0.1f, 0.012f, back);
        k.Add(Shape.Box, accent, 0.3f, 0.012f, 0.005f, 0, 0.17f, 0.023f, back);
        k.Add(Shape.Box, fabric, 0.26f, 0.09f, 0.05f, 0, 0.27f, 0.01f, back, 0.1f, 0, 0);
        foreach (var sx in new[] { -1, 1 })
        {
            k.Add(Shape.Box, frame, 0.03f, 0.03f, 0.2f, sx * 0.21f, 0.33f, -0.02f, top);
            k.Add(Shape.Box, frame, 0.035f, 0.2f, 0.035f, sx * 0.26f, 0.44f, -0.04f, top);
            k.Add(Shape.Box, fabric2, 0.06f, 0.03f, 0.22f, sx * 0.26f, 0.55f, -0.02f, top);
        }
    }

    // ------------------------------------------------------------------ OFFICE TABLE

    static void OfficeTable(Kit k, string seed, uint mugColor)
    {
        var R = Rng(seed);
        const float W = 0.98f, D = 0.62f, TH = 0.5f;
        var top = k.M(0x9b7a5c); var edge = k.M(0x7a5d44);
        var steel = k.M(0x3a3f47, new Opt { metal = 0.5f, rough = 0.45f });
        var white = k.M(0xeef2f5, new Opt { rough = 0.4f });

        k.Add(Shape.Box, top, W, 0.045f, D, 0, TH, 0);
        k.Add(Shape.Box, edge, W + 0.004f, 0.018f, D + 0.004f, 0, TH - 0.028f, 0);
        foreach (var sx in new[] { -1, 1 })
        {
            float x = sx * (W / 2 - 0.05f);
            k.Add(Shape.Box, steel, 0.04f, TH - 0.04f, 0.04f, x, (TH - 0.04f) / 2, D / 2 - 0.08f);
            k.Add(Shape.Box, steel, 0.04f, TH - 0.04f, 0.04f, x, (TH - 0.04f) / 2, -D / 2 + 0.08f);
            k.Add(Shape.Box, steel, 0.045f, 0.03f, D - 0.1f, x, 0.015f, 0);
            k.Add(Shape.Box, steel, 0.04f, 0.03f, D - 0.16f, x, TH - 0.05f, 0);
        }
        k.Add(Shape.Box, steel, W - 0.12f, 0.2f, 0.015f, 0, TH - 0.16f, -D / 2 + 0.09f);

        float px = W / 2 - 0.22f;
        k.Add(Shape.Box, k.M(0x4a515b, new Opt { metal = 0.35f, rough = 0.5f }), 0.28f, 0.42f, D - 0.12f, px, 0.24f, -0.02f);
        for (int i = 0; i < 3; i++)
        {
            float dy = 0.1f + i * 0.13f;
            k.Add(Shape.Box, k.M(0x59616c, new Opt { metal = 0.35f, rough = 0.5f }), 0.26f, 0.115f, 0.012f, px, dy, D / 2 - 0.08f);
            k.Add(Shape.Box, k.M(0xb9c2cc, new Opt { metal = 0.85f, rough = 0.25f }), 0.1f, 0.012f, 0.015f, px, dy + 0.035f, D / 2 - 0.07f);
        }
        k.Add(Shape.Cyl, k.M(0x111418), 0.03f, 0.03f, 0.03f, px, 0.43f, D / 2 - 0.072f, null, Mathf.PI / 2, 0, 0);

        float S = TH + 0.0225f;
        k.Add(Shape.Box, steel, 0.16f, 0.012f, 0.11f, 0, S + 0.006f, -0.16f);
        k.Add(Shape.Box, steel, 0.035f, 0.14f, 0.025f, 0, S + 0.08f, -0.19f);
        k.Add(Shape.Box, k.M(0x1d2228, new Opt { rough = 0.5f }), 0.5f, 0.29f, 0.025f, 0, S + 0.27f, -0.17f, null, -0.06f, 0, 0);
        var screen = ScreenTexture(seed, R);
        k.Add(Shape.Plane, k.M(0xffffff, new Opt { basic = true, map = screen }), 0.46f, 0.255f, 1f, 0, S + 0.27f, -0.156f, null, -0.06f, 0, 0);

        k.Add(Shape.Box, k.M(0x2b3038), 0.34f, 0.016f, 0.11f, -0.04f, S + 0.008f, 0.08f);
        k.Add(Shape.Box, k.M(0x4a525d), 0.32f, 0.006f, 0.09f, -0.04f, S + 0.018f, 0.08f);
        k.Add(Shape.Box, k.M(0x23272d), 0.16f, 0.003f, 0.16f, 0.22f, S + 0.0015f, 0.1f);
        k.Add(Shape.Sph, k.M(0x2b3038, new Opt { rough = 0.4f }), 0.045f, 0.028f, 0.07f, 0.22f, S + 0.012f, 0.1f);
        var mug = k.M(mugColor, new Opt { rough = 0.5f });
        k.Add(Shape.Cyl, mug, 0.065f, 0.085f, 0.065f, -0.38f, S + 0.043f, 0.12f);
        k.Add(Shape.Cyl, k.M(0x3a2418), 0.055f, 0.005f, 0.055f, -0.38f, S + 0.08f, 0.12f);
        k.Add(Shape.Handle, mug, 0.05f, 0.05f, 0.05f, -0.34f, S + 0.045f, 0.12f, null, 0, 0, -Mathf.PI / 2);
        for (int i = 0; i < 3; i++)
        {
            float x = -0.33f + R() * 0.03f, z = -0.08f + R() * 0.03f, ry = (R() - 0.5f) * 0.5f;
            k.Add(Shape.Box, white, 0.15f, 0.003f, 0.2f, x, S + 0.002f + i * 0.003f, z, null, 0, ry, 0);
        }
        k.Add(Shape.Cyl, steel, 0.05f, 0.08f, 0.05f, 0.33f, S + 0.04f, -0.18f);
        uint[] pens = { 0xc9a44a, 0x3d6a9b, 0x9b3d3d };
        for (int i = 0; i < 3; i++) k.Add(Shape.Cyl, k.M(pens[i]), 0.01f, 0.12f, 0.01f, 0.325f + i * 0.008f, S + 0.1f, -0.18f + (i - 1) * 0.008f, null, 0, 0, (i - 1) * 0.15f);

        var lamp = k.Group("Lamp", 0.4f, S, -0.05f, null, 0, 0.6f, 0);
        k.Add(Shape.Cyl, steel, 0.1f, 0.02f, 0.1f, 0, 0.01f, 0, lamp);
        k.Add(Shape.Box, steel, 0.018f, 0.2f, 0.018f, 0, 0.1f, 0.035f, lamp, -0.35f, 0, 0);
        k.Add(Shape.Box, steel, 0.018f, 0.16f, 0.018f, 0, 0.215f, 0.13f, lamp, 1.0f, 0, 0);
        k.Add(Shape.Cone, k.M(0x3ecfd8, new Opt { rough = 0.5f }), 0.08f, 0.07f, 0.08f, 0, 0.2f, 0.2f, lamp);
        k.Add(Shape.Sph, k.Glow(0xfff0c2, 1.5f), 0.03f, 0.03f, 0.03f, 0, 0.18f, 0.2f, lamp);
    }

    // The monitor's code-editor screen (the JS canvas), saved as a texture asset.
    static Texture2D ScreenTexture(string seed, System.Func<float> R)
    {
        const int W = 256, H = 144;
        string path = $"{MatDir}/TOS_Screen_{seed}.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool fresh = tex == null;
        if (fresh) tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "TOS_Screen_" + seed };
        var px = new Color32[W * H];
        void Rect(int x, int y, int w, int h, uint hex)
        {
            var c = (Color32)C(hex);
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++)
                if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[(H - 1 - yy) * W + xx] = c; // canvas y runs down
        }
        void Dot(int cx, int cy, int r, uint hex)
        {
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) if (x * x + y * y <= r * r) Rect(cx + x, cy + y, 1, 1, hex);
        }
        Rect(0, 0, W, H, 0x0f1b26); Rect(0, 0, 54, H, 0x16293a); Rect(0, 0, W, 14, 0x16293a);
        uint[] dots = { 0xff5f57, 0xfebc2e, 0x28c840 };
        for (int i = 0; i < 3; i++) Dot(8 + i * 10, 7, 3, dots[i]);
        uint[] syn = { 0x6fb7ff, 0xc49bff, 0x8ef0b0, 0xff9f43, 0xe8eef2 };
        for (int l = 0; l < 11; l++)
        {
            float cx = 64 + (R() < 0.4f ? 14 : 0) + (R() < 0.2f ? 14 : 0);
            int n = 1 + (int)(R() * 4);
            for (int k = 0; k < n; k++)
            {
                float w = 10 + R() * 40;
                Rect((int)cx, 22 + l * 11, (int)w, 5, syn[(int)(R() * syn.Length)]);
                cx += w + 6; if (cx > 240) break;
            }
        }
        for (int l = 0; l < 8; l++) Rect(8, 22 + l * 13, (int)(30 + R() * 12), 5, l == 2 ? 0x3ecfd8u : 0x3d5670u);
        tex.SetPixels32(px);
        tex.Apply();
        if (fresh) AssetDatabase.CreateAsset(tex, path); else EditorUtility.SetDirty(tex);
        return tex;
    }

    // ------------------------------------------------------------------ save: merge + prefab

    static void Save(Kit k)
    {
        var root = k.root.gameObject;
        if (k.ringParts.Count > 0)
        {
            var ring = new GameObject("Ring").transform;
            ring.SetParent(k.root, false);
            foreach (var p in k.ringParts) p.SetParent(ring, true);
            k.keep.Add(ring); k.merge.Add(ring);
        }
        foreach (var g in k.merge) MergeInto(g, $"{k.name}_{g.name}");

        // Everything not kept (and not under a kept part) becomes one mesh, a submesh per material.
        bool Kept(Transform t)
        {
            for (var p = t; p != null && p != k.root; p = p.parent) if (k.keep.Contains(p)) return true;
            return false;
        }
        var statics = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Kept(r.transform)).ToList();
        var body = new GameObject("Body");
        body.transform.SetParent(k.root, false);
        var (mesh, mats) = Combine(statics, k.root, $"{k.name}_Body");
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        body.AddComponent<MeshRenderer>().sharedMaterials = mats;
        foreach (var r in statics)
        {
            var go = r.gameObject;
            bool hasKeptChild = go.GetComponentsInChildren<Transform>(true).Any(t => t != go.transform && k.keep.Contains(t));
            if (hasKeptChild) { Object.DestroyImmediate(r); Object.DestroyImmediate(go.GetComponent<MeshFilter>()); }
            else Object.DestroyImmediate(go);
        }
        // Empty groups left behind.
        foreach (var t in k.root.GetComponentsInChildren<Transform>(true).Reverse().ToList())
            if (t != null && t != k.root && t.childCount == 0 && t.GetComponents<Component>().Length == 1 && !k.keep.Contains(t)) Object.DestroyImmediate(t.gameObject);

        k.configure?.Invoke(root);
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{k.name}.prefab");
        Object.DestroyImmediate(root);
    }

    // Children of a moving group merged onto the group itself (LED banks, fan blades).
    static void MergeInto(Transform group, string meshName)
    {
        var rs = group.GetComponentsInChildren<MeshRenderer>(true).ToList();
        if (rs.Count == 0) return;
        var (mesh, mats) = Combine(rs, group, meshName);
        foreach (var r in rs) Object.DestroyImmediate(r.gameObject);
        group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        group.gameObject.AddComponent<MeshRenderer>().sharedMaterials = mats;
    }

    static (Mesh, Material[]) Combine(List<MeshRenderer> renderers, Transform space, string meshName)
    {
        var byMat = renderers.GroupBy(r => r.sharedMaterial).ToList();
        var parts = new List<CombineInstance>();
        var mats = new List<Material>();
        var temps = new List<Mesh>();
        foreach (var g in byMat)
        {
            var ci = g.Select(r => new CombineInstance
            {
                mesh = r.GetComponent<MeshFilter>().sharedMesh,
                transform = space.worldToLocalMatrix * r.transform.localToWorldMatrix,
            }).ToArray();
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(ci, true, true);
            temps.Add(m);
            parts.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
            mats.Add(g.Key);
        }
        var result = new Mesh { name = meshName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        result.CombineMeshes(parts.ToArray(), false, false);
        result.RecalculateBounds();
        foreach (var t in temps) Object.DestroyImmediate(t);
        return (SaveMesh(result, meshName), mats.ToArray());
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{MeshDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    // ------------------------------------------------------------------ geometry (three's unit primitives)

    static readonly Dictionary<Shape, Mesh> shapes = new Dictionary<Shape, Mesh>();

    static Mesh MeshFor(Shape s)
    {
        if (shapes.TryGetValue(s, out var m) && m != null) return m;
        switch (s)
        {
            case Shape.Box: m = Resources.GetBuiltinResource<Mesh>("Cube.fbx"); break;
            case Shape.Sph: m = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"); break;
            case Shape.Plane: m = Resources.GetBuiltinResource<Mesh>("Quad.fbx"); break;
            case Shape.Cyl: m = Cylinder(20, false); break;
            case Shape.Cyl8: m = Cylinder(8, false); break;
            case Shape.CylOpen: m = Cylinder(32, true); break;
            case Shape.Cone: m = Cone(20); break;
            case Shape.Torus: m = Torus(0.5f, 0.035f, 8, 48, Mathf.PI * 2); break;
            case Shape.Handle: m = Torus(0.5f, 0.18f, 6, 12, Mathf.PI); break;
            case Shape.Ico: m = Icosahedron(); break;
            case Shape.Hex: m = Cylinder(6, false); break;
            case Shape.Ring: m = Torus(0.5f, 0.035f, 10, 56, Mathf.PI * 2); break;
            case Shape.RingThick: m = Torus(0.5f, 0.09f, 14, 64, Mathf.PI * 2); break;
            case Shape.Rim: m = Torus(0.5f, 0.07f, 12, 48, Mathf.PI * 2); break;
            case Shape.Band: m = Torus(0.5f, 0.025f, 8, 48, Mathf.PI * 2); break;
        }
        shapes[s] = m;
        return m;
    }

    // Ø1, height 1, centred; vertices start at +Z like three's CylinderGeometry.
    static Mesh Cylinder(int seg, bool open)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2 / seg;
            var p = new Vector3(Mathf.Sin(a) * 0.5f, 0, Mathf.Cos(a) * 0.5f);
            v.Add(p + Vector3.up * 0.5f); v.Add(p - Vector3.up * 0.5f);
        }
        for (int i = 0; i < seg; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 }); }
        if (!open)
            foreach (var y in new[] { 0.5f, -0.5f })
            {
                int c = v.Count; v.Add(new Vector3(0, y, 0));
                for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2 / seg; v.Add(new Vector3(Mathf.Sin(a) * 0.5f, y, Mathf.Cos(a) * 0.5f)); }
                for (int i = 0; i < seg; i++) tri.AddRange(new[] { c, c + 1 + i, c + 2 + i });
            }
        return Build("Cylinder" + seg + (open ? "Open" : ""), v, tri, p => Mathf.Abs(Mathf.Abs(p.y) - 0.5f) < 1e-4f && new Vector2(p.x, p.z).magnitude < 0.49f ? new Vector3(0, p.y, 0) * 2 : new Vector3(p.x, 0, p.z));
    }

    static Mesh Cone(int seg)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2 / seg;
            v.Add(new Vector3(0, 0.5f, 0)); v.Add(new Vector3(Mathf.Sin(a) * 0.5f, -0.5f, Mathf.Cos(a) * 0.5f));
        }
        for (int i = 0; i < seg; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 1, a + 3 }); }
        return Build("Cone", v, tri, p => new Vector3(p.x, 0.25f, p.z));
    }

    // Ring in the XY plane like three's TorusGeometry(radius, tube, radial, tubular, arc).
    static Mesh Torus(float R, float r, int radial, int tubular, float arc)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int j = 0; j <= radial; j++)
            for (int i = 0; i <= tubular; i++)
            {
                float u = i / (float)tubular * arc, w = j / (float)radial * Mathf.PI * 2;
                v.Add(new Vector3((R + r * Mathf.Cos(w)) * Mathf.Cos(u), (R + r * Mathf.Cos(w)) * Mathf.Sin(u), r * Mathf.Sin(w)));
            }
        for (int j = 1; j <= radial; j++)
            for (int i = 1; i <= tubular; i++)
            {
                int a = (tubular + 1) * j + i - 1, b = (tubular + 1) * (j - 1) + i - 1, c = (tubular + 1) * (j - 1) + i, d = (tubular + 1) * j + i;
                tri.AddRange(new[] { a, b, d, b, c, d });
            }
        return Build($"Torus_{r}_{radial}_{tubular}_{Mathf.RoundToInt(arc * 100)}", v, tri, p => { var ring = new Vector3(p.x, p.y, 0).normalized * R; return p - ring; });
    }

    static Mesh Icosahedron()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var pts = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                          new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) }
            .Select(p => p.normalized * 0.5f).ToArray();
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var v = new List<Vector3>(); var tri = new List<int>();
        foreach (var i in f) { tri.Add(v.Count); v.Add(pts[i]); } // flat shaded: no shared vertices
        return Build("Icosahedron", v, tri, p => p);
    }

    // Tube along a polyline (the machine's conduits).
    static Mesh Tube(Vector3[] path, float r, int radial)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i < path.Length; i++)
        {
            var fwd = (path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            var side = Vector3.Cross(fwd, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(fwd, Vector3.up).normalized : Vector3.right;
            var up = Vector3.Cross(side, fwd);
            for (int j = 0; j <= radial; j++)
            {
                float a = j * Mathf.PI * 2 / radial;
                v.Add(path[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r);
            }
        }
        for (int i = 0; i < path.Length - 1; i++)
            for (int j = 0; j < radial; j++)
            {
                int a = i * (radial + 1) + j, b = a + radial + 1;
                tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var centers = path;
        return Build("Tube", v, tri, p =>
        {
            var best = centers[0]; float bd = float.MaxValue;
            foreach (var c in centers) { float d = (c - p).sqrMagnitude; if (d < bd) { bd = d; best = c; } }
            return p - best;
        }, save: false);
    }

    static Vector3[] CatmullRom(Vector3[] p, int n)
    {
        var o = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n * (p.Length - 1);
            int s = Mathf.Min((int)t, p.Length - 2);
            float u = t - s;
            Vector3 p0 = p[Mathf.Max(s - 1, 0)], p1 = p[s], p2 = p[s + 1], p3 = p[Mathf.Min(s + 2, p.Length - 1)];
            o[i] = 0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
        }
        return o;
    }

    // Winding made to face outward (Unity front faces are clockwise), normals, saved as an asset.
    static Mesh Build(string name, List<Vector3> v, List<int> tri, System.Func<Vector3, Vector3> outward, bool save = true)
    {
        for (int i = 0; i < tri.Count; i += 3)
        {
            Vector3 a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]];
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward((a + b + c) / 3f)) < 0f) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
        }
        var m = new Mesh { name = "TOS_" + name };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return save ? SaveMesh(m, "TOS_" + name) : m;
    }
}
