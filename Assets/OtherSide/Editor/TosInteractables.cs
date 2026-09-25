using System.Collections.Generic;
using System.IO;
using System.Linq;
using EscapeOffice;
using UnityEditor;
using UnityEngine;

// Port of tos-interactables.js: doorButton, riddleKeypad, codePanel, lightSwitch, laserLever,
// sprinklerValve, drainValve, breaker, finalButton → TOS_* prefabs in Prefabs/Props.
//
// Same conventions as the props (see TosProps.cs). Look: matte neutral grey metal, raised white
// symbols, one white emissive ring per prop that the game tints with the side colour.
// Moving parts, the ring, the keypad swatches/display and the panel face stay separate for
// TosInteractable; the symbols are also saved as textures (Textures/Symbols) for the UI.
public static partial class TosProps
{
    const string SymbolDir = "Assets/OtherSide/Textures/Symbols";
    static readonly Dictionary<string, Material> symbolMats = new Dictionary<string, Material>();

    static int BuildInteractables()
    {
        OtherSideImporter.EnsureFolder(SymbolDir);
        symbolMats.Clear();
        foreach (var name in Symbols.Keys) SymbolTexture(name);

        int n = 0;
        void Make(string name, System.Action<Kit> build) { var k = new Kit(name, false); build(k); Save(k); n++; }
        Make("TOS_DoorButton", DoorButton);
        Make("TOS_RiddleKeypad", RiddleKeypad);
        Make("TOS_CodePanel", CodePanel);
        Make("TOS_LightSwitch", LightSwitch);
        Make("TOS_LaserLever", LaserLever);
        Make("TOS_SprinklerValve", k => SprinklerValve(k));
        Make("TOS_DrainValve", k => DrainValve(k));
        Make("TOS_Breaker", Breaker);
        Make("TOS_FinalButton", FinalButton);
        return n;
    }

    // ------------------------------------------------------------------ palette + scaffolding

    class Mats { public Material metal, dark, light, cap, recess, frame, ring; }

    static Mats MatsFor(Kit k) => new Mats
    {
        metal = k.M(0x8e939a, new Opt { metal = 0.3f, rough = 0.78f }),
        dark = k.M(0x676c73, new Opt { metal = 0.3f, rough = 0.8f }),
        light = k.M(0xb7bbc0, new Opt { metal = 0.3f, rough = 0.72f }),
        cap = k.M(0xa4a9af, new Opt { metal = 0.25f, rough = 0.7f }),
        recess = k.M(0x4d5158, new Opt { metal = 0.2f, rough = 0.85f }),
        frame = k.M(0xd2d5d8, new Opt { metal = 0.25f, rough = 0.6f }),
        ring = k.M(0xffffff, new Opt { emissive = 0xffffff, emissiveI = 1f, rough = 0.4f }),
    };

    static GameObject RingPart(Kit k, GameObject go) { k.ringParts.Add(go.transform); return go; }

    static Transform Moving(Kit k, Transform t) { t.name = "Moving"; k.keep.Add(t); k.merge.Add(t); return t; }

    // floor foot + short post against the wall; returns a head group facing +Z, tilted up
    static Transform WallPost(Kit k, Mats m, float postH, float headY, float tilt, float headZ = -0.26f)
    {
        k.Add(Shape.Box, m.dark, 0.34f, 0.04f, 0.26f, 0, 0.02f, -0.36f);
        k.Add(Shape.Box, m.metal, 0.12f, postH, 0.12f, 0, 0.04f + postH / 2, -0.38f);
        k.Add(Shape.Box, m.dark, 0.2f, 0.035f, 0.2f, 0, 0.06f, -0.37f);
        k.Add(Shape.Box, m.dark, 0.06f, 0.16f, 0.02f, 0, postH - 0.02f, -0.49f);
        return k.Group("Head", 0, headY, headZ, null, -tilt, 0, 0);
    }

    // raised symbol on the parent's +Z face (a cut-out quad just proud of the surface)
    static void Emboss(Kit k, Transform parent, string sym, float size, float x, float y, float z, float height = 0.014f) =>
        k.Add(Shape.Plane, SymbolMat(sym), size, size, 1f, x, y, z + height, parent);

    // raised symbol on an upward-facing surface; reads upright from the game camera
    static void EmbossUp(Kit k, Transform parent, string sym, float size, float x, float y, float z, float height = 0.014f) =>
        Emboss(k, k.Group("Symbol", x, y, z, parent, -Mathf.PI / 2, 0, 0), sym, size, 0, 0, 0, height);

    static void Motion(Kit k, TosInteractable.Motion motion, float from, float to, float ease, string moving = "Moving")
    {
        k.configure += go =>
        {
            var ti = go.GetComponent<TosInteractable>();
            if (ti == null) ti = go.AddComponent<TosInteractable>();
            ti.motion = motion; ti.from = from; ti.to = to; ti.ease = ease;
            ti.moving = Named(go, moving);
            ti.ring = Named(go, "Ring")?.GetComponent<Renderer>();
        };
    }

    static Transform Named(GameObject go, string name) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    const float PI = Mathf.PI;

    // ------------------------------------------------------------------ 1. DOOR BUTTON

    static void DoorButton(Kit k)
    {
        var m = MatsFor(k);
        var head = WallPost(k, m, 0.72f, 0.84f, 0.6f);
        k.Add(Shape.Box, m.metal, 0.12f, 0.14f, 0.16f, 0, -0.12f, -0.08f, head);
        k.Add(Shape.Cyl, m.metal, 0.56f, 0.08f, 0.56f, 0, 0, 0, head, PI / 2);
        k.Add(Shape.Cyl, m.light, 0.47f, 0.04f, 0.47f, 0, 0, 0.055f, head, PI / 2);
        k.Add(Shape.Cyl, m.recess, 0.4f, 0.02f, 0.4f, 0, 0, 0.07f, head, PI / 2);
        RingPart(k, k.Add(Shape.Ring, m.ring, 0.44f, 0.44f, 0.6f, 0, 0, 0.078f, head));
        var cap = Moving(k, k.Group("Cap", 0, 0, 0.07f, head));
        k.Add(Shape.Cyl, m.cap, 0.34f, 0.07f, 0.34f, 0, 0, 0.035f, cap, PI / 2);
        k.Add(Shape.Cyl, m.cap, 0.31f, 0.02f, 0.31f, 0, 0, 0.078f, cap, PI / 2);
        Emboss(k, cap, "door", 0.18f, 0, 0, 0.088f);
        Motion(k, TosInteractable.Motion.PushZ, 0.07f, 0.035f, 16f);
    }

    // ------------------------------------------------------------------ 2. RIDDLE KEYPAD

    static void RiddleKeypad(Kit k)
    {
        var m = MatsFor(k);
        var head = WallPost(k, m, 0.7f, 0.88f, 0.5f, -0.27f);
        k.Add(Shape.Box, m.metal, 0.12f, 0.18f, 0.14f, 0, -0.26f, -0.07f, head);
        const float W = 0.52f, H = 0.66f, rz = 0.043f, rw = 0.012f;
        k.Add(Shape.Box, m.metal, W, H, 0.07f, 0, 0, 0, head);
        k.Add(Shape.Box, m.recess, W - 0.07f, H - 0.07f, 0.012f, 0, 0, 0.037f, head);
        RingPart(k, k.Add(Shape.Box, m.ring, W - 0.05f, rw, 0.006f, 0, (H - 0.05f) / 2, rz, head));
        RingPart(k, k.Add(Shape.Box, m.ring, W - 0.05f, rw, 0.006f, 0, -(H - 0.05f) / 2, rz, head));
        RingPart(k, k.Add(Shape.Box, m.ring, rw, H - 0.05f, 0.006f, (W - 0.05f) / 2, 0, rz, head));
        RingPart(k, k.Add(Shape.Box, m.ring, rw, H - 0.05f, 0.006f, -(W - 0.05f) / 2, 0, rz, head));
        k.Add(Shape.Box, m.light, 0.4f, 0.09f, 0.012f, 0, 0.225f, 0.046f, head);
        var display = k.Add(Shape.Box, k.M(0x2b2f34, new Opt { rough = 0.3f, metal = 0.2f, emissive = 0x000000, emissiveI = 0f }), 0.37f, 0.06f, 0.008f, 0, 0.225f, 0.054f, head);
        display.name = "Display"; k.keep.Add(display.transform);
        var swatch = k.M(0xf3f4f5, new Opt { rough = 0.5f, emissive = 0xffffff, emissiveI = 0.18f });
        for (int i = 0; i < 4; i++)
        {
            float x = -0.135f + i * 0.09f;
            k.Add(Shape.Box, m.dark, 0.08f, 0.08f, 0.01f, x, 0.12f, 0.046f, head);
            var s = k.Add(Shape.Box, swatch, 0.062f, 0.062f, 0.01f, x, 0.12f, 0.052f, head);
            s.name = "Swatch" + i; k.keep.Add(s.transform);
        }
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 3; c++)
            {
                var key = k.Add(Shape.Box, m.light, 0.09f, 0.066f, 0.024f, -0.105f + c * 0.105f, 0.015f - r * 0.083f, 0.054f, head);
                if (r * 3 + c == 4) { key.name = "Key4"; k.keep.Add(key.transform); } // centre key dips when solved
            }
        Motion(k, TosInteractable.Motion.PushZ, 0.054f, 0.042f, 10f, "Key4");
        k.configure += go =>
        {
            var ti = go.GetComponent<TosInteractable>();
            ti.display = Named(go, "Display")?.GetComponent<Renderer>();
            ti.swatches = Enumerable.Range(0, 4).Select(i => Named(go, "Swatch" + i)?.GetComponent<Renderer>()).ToArray();
        };
    }

    // ------------------------------------------------------------------ 3. CODE PANEL

    static void CodePanel(Kit k)
    {
        var m = MatsFor(k);
        var plaque = k.Group("Plaque", 0, 0.72f, -0.5f);
        const float S = 0.8f, B = 0.1f;
        k.Add(Shape.Box, m.frame, S, S, 0.03f, 0, 0, 0.015f, plaque);
        k.Add(Shape.Box, m.frame, S, B, 0.07f, 0, (S - B) / 2, 0.035f, plaque);
        k.Add(Shape.Box, m.frame, S, B, 0.07f, 0, -(S - B) / 2, 0.035f, plaque);
        k.Add(Shape.Box, m.frame, B, S - 2 * B, 0.07f, (S - B) / 2, 0, 0.035f, plaque);
        k.Add(Shape.Box, m.frame, B, S - 2 * B, 0.07f, -(S - B) / 2, 0, 0.035f, plaque);
        k.Add(Shape.Box, m.light, S - 2 * B + 0.02f, 0.012f, 0.012f, 0, (S - 2 * B) / 2, 0.066f, plaque);
        k.Add(Shape.Box, m.light, S - 2 * B + 0.02f, 0.012f, 0.012f, 0, -(S - 2 * B) / 2, 0.066f, plaque);
        foreach (var sx in new[] { -1, 1 }) foreach (var sy in new[] { -1, 1 })
            k.Add(Shape.Hex, m.light, 0.035f, 0.015f, 0.035f, sx * (S / 2 - B / 2), sy * (S / 2 - B / 2), 0.075f, plaque, PI / 2);
        var face = k.Add(Shape.Box, k.M(0xe8eaec, new Opt { rough = 0.7f, metal = 0.05f }), S - 2 * B, S - 2 * B, 0.012f, 0, 0, 0.036f, plaque);
        face.name = "Face"; k.keep.Add(face.transform);
        var digit = k.Group("DigitAnchor", 0, 0, 0.045f, plaque); k.keep.Add(digit);

        // lamp: wall arm + hood angled down at the face
        var lamp = k.Group("LampArm", 0, 1.2f, -0.5f);
        k.Add(Shape.Box, m.light, 0.16f, 0.06f, 0.02f, 0, 0, 0.01f, lamp);
        k.Add(Shape.Box, m.light, 0.04f, 0.04f, 0.14f, 0, 0.02f, 0.08f, lamp);
        var hood = k.Group("Hood", 0, 0.02f, 0.16f, lamp, 0.7f, 0, 0);
        k.Add(Shape.Box, m.frame, 0.34f, 0.035f, 0.12f, 0, 0.03f, 0, hood);
        k.Add(Shape.Box, m.frame, 0.34f, 0.06f, 0.02f, 0, 0, 0.06f, hood);
        k.Add(Shape.Box, m.frame, 0.34f, 0.06f, 0.02f, 0, 0, -0.06f, hood);
        RingPart(k, k.Add(Shape.Box, m.ring, 0.28f, 0.012f, 0.08f, 0, 0.005f, 0, hood));
        var spotGo = new GameObject("Lamp");
        spotGo.transform.SetParent(k.root, false);
        spotGo.transform.localPosition = P(0, 1.18f, -0.3f);
        spotGo.transform.localRotation = Quaternion.LookRotation(P(0, 0.7f, -0.5f) - P(0, 1.18f, -0.3f));
        var spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot; spot.range = 2.2f; spot.spotAngle = 0.75f * 2f * Mathf.Rad2Deg; spot.intensity = 0.9f;

        k.configure += go =>
        {
            var ti = go.AddComponent<TosInteractable>();
            ti.ring = Named(go, "Ring")?.GetComponent<Renderer>();
            ti.face = Named(go, "Face")?.GetComponent<Renderer>();
            ti.digitAnchor = Named(go, "DigitAnchor");
            ti.lamp = go.GetComponentInChildren<Light>();
        };
    }

    // ------------------------------------------------------------------ 4. LIGHT SWITCH

    static void LightSwitch(Kit k)
    {
        var m = MatsFor(k);
        var head = WallPost(k, m, 0.56f, 0.74f, 0.45f);
        k.Add(Shape.Box, m.metal, 0.1f, 0.12f, 0.14f, 0, -0.18f, -0.07f, head);
        k.Add(Shape.Box, m.metal, 0.32f, 0.46f, 0.05f, 0, 0, 0, head);
        k.Add(Shape.Box, m.light, 0.28f, 0.42f, 0.01f, 0, 0, 0.028f, head);
        const float ry = -0.07f, rw = 0.01f;
        RingPart(k, k.Add(Shape.Box, m.ring, 0.17f, rw, 0.006f, 0, ry + 0.12f, 0.036f, head));
        RingPart(k, k.Add(Shape.Box, m.ring, 0.17f, rw, 0.006f, 0, ry - 0.12f, 0.036f, head));
        RingPart(k, k.Add(Shape.Box, m.ring, rw, 0.25f, 0.006f, 0.08f, ry, 0.036f, head));
        RingPart(k, k.Add(Shape.Box, m.ring, rw, 0.25f, 0.006f, -0.08f, ry, 0.036f, head));
        k.Add(Shape.Box, m.recess, 0.14f, 0.21f, 0.012f, 0, ry, 0.036f, head);
        var pivot = Moving(k, k.Group("Pivot", 0, ry, 0.045f, head));
        k.Add(Shape.Box, m.cap, 0.11f, 0.19f, 0.035f, 0, 0, 0.012f, pivot);
        k.Add(Shape.Box, m.light, 0.09f, 0.012f, 0.01f, 0, 0.07f, 0.032f, pivot);
        Emboss(k, head, "bulb", 0.12f, 0, 0.14f, 0.033f);
        Motion(k, TosInteractable.Motion.RotX, -0.28f, 0.28f, 14f);
    }

    // ------------------------------------------------------------------ 5. LASER LEVER

    static void LaserLever(Kit k)
    {
        var m = MatsFor(k);
        k.Add(Shape.Box, m.dark, 0.74f, 0.06f, 0.64f, 0, 0.03f, 0);
        k.Add(Shape.Box, m.metal, 0.62f, 0.05f, 0.52f, 0, 0.085f, 0);
        foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 }) k.Add(Shape.Hex, m.light, 0.05f, 0.03f, 0.05f, sx * 0.3f, 0.125f, sz * 0.25f);
        k.Add(Shape.Box, m.metal, 0.3f, 0.14f, 0.26f, 0, 0.18f, -0.1f);
        foreach (var sx in new[] { -1, 1 }) k.Add(Shape.Box, m.dark, 0.05f, 0.28f, 0.24f, sx * 0.14f, 0.25f, -0.1f);
        k.Add(Shape.Cyl, m.light, 0.18f, 0.34f, 0.18f, 0, 0.32f, -0.1f, null, 0, 0, PI / 2);
        foreach (var sx in new[] { -1, 1 }) RingPart(k, k.Add(Shape.Ring, m.ring, 0.2f, 0.2f, 0.5f, sx * 0.172f, 0.32f, -0.1f, null, 0, PI / 2, 0));
        var arm = Moving(k, k.Group("Arm", 0, 0.32f, -0.1f));
        k.Add(Shape.Box, m.metal, 0.12f, 0.14f, 0.12f, 0, 0.05f, 0, arm);
        k.Add(Shape.Cyl, m.metal, 0.06f, 0.86f, 0.06f, 0, 0.47f, 0, arm);
        k.Add(Shape.Cyl, m.dark, 0.085f, 0.08f, 0.085f, 0, 0.16f, 0, arm);
        k.Add(Shape.Sph, m.cap, 0.17f, 0.17f, 0.17f, 0, 0.9f, 0, arm);
        EmbossUp(k, k.root, "beam", 0.22f, 0, 0.11f, 0.15f);
        Motion(k, TosInteractable.Motion.RotX, -0.45f, 0.6f, 7f);
    }

    // ------------------------------------------------------------------ valve base (6 & 7)

    static void ValveBase(Kit k, Mats m, string symbol)
    {
        k.Add(Shape.Cyl, m.metal, 0.88f, 0.07f, 0.88f, 0, 0.035f, 0);
        k.Add(Shape.Cyl, m.light, 0.4f, 0.05f, 0.4f, 0, 0.095f, 0);
        for (int i = 0; i < 8; i++) { float a = PI / 8 + i * PI / 4; k.Add(Shape.Hex, m.light, 0.045f, 0.03f, 0.045f, Mathf.Cos(a) * 0.36f, 0.085f, Mathf.Sin(a) * 0.36f); }
        RingPart(k, k.Add(Shape.Ring, m.ring, 0.9f, 0.9f, 0.7f, 0, 0.05f, 0, null, PI / 2));
        EmbossUp(k, k.root, symbol, 0.17f, 0, 0.07f, 0.3f);
    }

    // ------------------------------------------------------------------ 6. SPRINKLER VALVE

    static void SprinklerValve(Kit k)
    {
        var m = MatsFor(k);
        ValveBase(k, m, "flameDrop");
        k.Add(Shape.Cyl, m.metal, 0.15f, 0.78f, 0.15f, 0, 0.5f, 0);
        k.Add(Shape.Cyl, m.dark, 0.22f, 0.05f, 0.22f, 0, 0.14f, 0);
        k.Add(Shape.Cyl, m.dark, 0.2f, 0.04f, 0.2f, 0, 0.55f, 0);
        k.Add(Shape.Sph, m.metal, 0.3f, 0.26f, 0.3f, 0, 0.86f, 0);
        k.Add(Shape.Cyl, m.dark, 0.16f, 0.08f, 0.16f, 0, 0.99f, 0);
        k.Add(Shape.Cyl, m.light, 0.04f, 0.12f, 0.04f, 0, 1.08f, 0);
        var wheel = Moving(k, k.Group("Wheel", 0, 1.1f, 0));
        k.Add(Shape.Rim, m.cap, 0.46f, 0.46f, 0.46f, 0, 0, 0, wheel, PI / 2);
        for (int i = 0; i < 5; i++) { float a = i * PI * 2 / 5; k.Add(Shape.Box, m.cap, 0.2f, 0.025f, 0.035f, Mathf.Cos(a) * 0.12f, 0, Mathf.Sin(a) * 0.12f, wheel, 0, -a, 0); }
        k.Add(Shape.Cyl, m.light, 0.1f, 0.06f, 0.1f, 0, 0, 0, wheel);
        k.Add(Shape.Hex, m.dark, 0.06f, 0.05f, 0.06f, 0, 0.05f, 0, wheel);
        Motion(k, TosInteractable.Motion.RotY, 0f, 3f * PI, 2.5f); // three turns (JS -y, mirrored)
    }

    // ------------------------------------------------------------------ 7. DRAIN VALVE

    static void DrainValve(Kit k)
    {
        var m = MatsFor(k);
        ValveBase(k, m, "wavesDown");
        k.Add(Shape.Cyl, m.metal, 0.15f, 0.62f, 0.15f, 0, 0.42f, 0);
        k.Add(Shape.Cyl, m.dark, 0.22f, 0.05f, 0.22f, 0, 0.14f, 0);
        k.Add(Shape.Box, m.metal, 0.28f, 0.24f, 0.22f, 0, 0.8f, 0);
        foreach (var sx in new[] { -1, 1 }) k.Add(Shape.Cyl, m.dark, 0.2f, 0.26f, 0.2f, sx * 0.15f, 0.8f, 0, null, 0, 0, PI / 2);
        k.Add(Shape.Box, m.dark, 0.18f, 0.16f, 0.16f, 0, 0.99f, 0);
        k.Add(Shape.Cyl, m.light, 0.045f, 0.1f, 0.045f, 0, 1.12f, 0);
        var lever = Moving(k, k.Group("Lever", 0, 1.15f, 0));
        k.Add(Shape.Cyl, m.light, 0.11f, 0.06f, 0.11f, 0, 0, 0, lever);
        k.Add(Shape.Box, m.cap, 0.62f, 0.03f, 0.075f, 0.31f, 0, 0, lever);
        k.Add(Shape.Cyl, m.cap, 0.07f, 0.16f, 0.07f, 0.62f, 0, 0, lever, 0, 0, PI / 2);
        k.Add(Shape.Hex, m.dark, 0.06f, 0.04f, 0.06f, 0, 0.045f, 0, lever);
        Motion(k, TosInteractable.Motion.RotY, 0f, -PI / 2, 6f); // quarter turn (JS +y, mirrored)
    }

    // ------------------------------------------------------------------ 8. BREAKER

    static void Breaker(Kit k)
    {
        var m = MatsFor(k);
        k.Add(Shape.Box, m.dark, 0.34f, 0.04f, 0.26f, 0, 0.02f, -0.36f);
        k.Add(Shape.Box, m.metal, 0.14f, 0.52f, 0.14f, 0, 0.3f, -0.38f);
        const float W = 0.62f, H = 0.72f, D = 0.24f, t = 0.03f, cy = 0.92f, cz = -0.38f;
        k.Add(Shape.Box, m.metal, W, H, t, 0, cy, -0.5f + t / 2);
        k.Add(Shape.Box, m.metal, t, H, D, -W / 2 + t / 2, cy, cz);
        k.Add(Shape.Box, m.metal, t, H, D, W / 2 - t / 2, cy, cz);
        k.Add(Shape.Box, m.metal, W, t, D, 0, cy + H / 2 - t / 2, cz);
        k.Add(Shape.Box, m.metal, W, t, D, 0, cy - H / 2 + t / 2, cz);
        k.Add(Shape.Box, m.light, W + 0.04f, 0.03f, D + 0.03f, 0, cy + H / 2 + 0.015f, cz);
        k.Add(Shape.Box, m.recess, W - 2 * t, H - 2 * t, 0.01f, 0, cy, -0.5f + t + 0.005f);
        k.Add(Shape.Box, m.dark, 0.3f, 0.44f, 0.03f, 0, cy, -0.45f);
        const float fz = -0.432f, fw = 0.012f;
        RingPart(k, k.Add(Shape.Box, m.ring, 0.32f, fw, 0.006f, 0, cy + 0.225f, fz));
        RingPart(k, k.Add(Shape.Box, m.ring, 0.32f, fw, 0.006f, 0, cy - 0.225f, fz));
        RingPart(k, k.Add(Shape.Box, m.ring, fw, 0.46f, 0.006f, 0.16f, cy, fz));
        RingPart(k, k.Add(Shape.Box, m.ring, fw, 0.46f, 0.006f, -0.16f, cy, fz));
        foreach (var sx in new[] { -1, 1 })
        {
            k.Add(Shape.Box, m.light, 0.04f, 0.08f, 0.06f, sx * 0.08f, cy + 0.15f, -0.41f);
            k.Add(Shape.Box, m.light, 0.04f, 0.05f, 0.05f, sx * 0.08f, cy - 0.12f, -0.415f);
        }
        var arm = Moving(k, k.Group("Throw", 0, cy - 0.12f, -0.41f));
        foreach (var sx in new[] { -1, 1 }) k.Add(Shape.Box, m.cap, 0.025f, 0.3f, 0.025f, sx * 0.08f, 0.14f, 0, arm);
        k.Add(Shape.Box, m.cap, 0.19f, 0.025f, 0.025f, 0, 0.29f, 0, arm);
        k.Add(Shape.Cyl, m.dark, 0.045f, 0.1f, 0.045f, 0, 0.29f, 0.06f, arm, PI / 2);
        k.Add(Shape.Sph, m.dark, 0.07f, 0.07f, 0.07f, 0, 0.29f, 0.11f, arm);
        var door = k.Group("Door", -W / 2, cy, cz + D / 2, null, 0, -0.5f, 0);
        k.Add(Shape.Box, m.light, W, H, 0.025f, W / 2, 0, 0.012f, door);
        k.Add(Shape.Box, m.metal, W - 0.08f, H - 0.08f, 0.006f, W / 2, 0, 0.027f, door);
        k.Add(Shape.Box, m.dark, 0.03f, 0.12f, 0.03f, W - 0.05f, 0, 0.035f, door);
        foreach (var y in new[] { -0.26f, 0.26f }) k.Add(Shape.Cyl, m.dark, 0.03f, 0.1f, 0.03f, 0, y, 0, door);
        Emboss(k, door, "bolt", 0.3f, W / 2, 0.02f, 0.029f);
        Motion(k, TosInteractable.Motion.RotX, 0.55f, -0.35f, 9f);
    }

    // ------------------------------------------------------------------ 9. FINAL BUTTON

    static void FinalButton(Kit k)
    {
        var m = MatsFor(k);
        k.Add(Shape.Cyl, m.dark, 1.0f, 0.12f, 1.0f, 0, 0.06f, 0);
        for (int i = 0; i < 8; i++) { float a = PI / 8 + i * PI / 4; k.Add(Shape.Hex, m.light, 0.06f, 0.035f, 0.06f, Mathf.Cos(a) * 0.44f, 0.13f, Mathf.Sin(a) * 0.44f); }
        k.Add(Shape.Cyl, m.metal, 0.84f, 0.1f, 0.84f, 0, 0.17f, 0);
        k.Add(Shape.Cyl, m.metal, 0.62f, 0.44f, 0.62f, 0, 0.43f, 0);
        foreach (var y in new[] { 0.3f, 0.56f }) k.Add(Shape.Band, m.dark, 0.64f, 0.64f, 0.64f, 0, y, 0, null, PI / 2);
        k.Add(Shape.Cyl, m.light, 0.9f, 0.12f, 0.9f, 0, 0.7f, 0);
        k.Add(Shape.Cyl, m.recess, 0.7f, 0.02f, 0.7f, 0, 0.765f, 0);
        RingPart(k, k.Add(Shape.RingThick, m.ring, 0.76f, 0.76f, 0.76f, 0, 0.78f, 0, null, PI / 2));
        var cap = Moving(k, k.Group("Cap", 0, 0.77f, 0));
        k.Add(Shape.Cyl, m.cap, 0.58f, 0.1f, 0.58f, 0, 0.05f, 0, cap);
        k.Add(Shape.Sph, m.cap, 0.58f, 0.24f, 0.58f, 0, 0.1f, 0, cap);
        k.Add(Shape.Cyl, m.cap, 0.3f, 0.02f, 0.3f, 0, 0.21f, 0, cap);
        EmbossUp(k, cap, "star", 0.24f, 0, 0.22f, 0, 0.018f);
        Motion(k, TosInteractable.Motion.DropY, 0.77f, 0.695f, 12f);
    }

    // ------------------------------------------------------------------ symbols
    // The JS shapes (unit square, curves sampled), rasterised with the even-odd rule so holes
    // and shapes-inside-holes come out as in three's ExtrudeGeometry.

    class Sym
    {
        public readonly List<List<Vector2>> contours = new List<List<Vector2>>();
        List<Vector2> cur;
        Vector2 p;

        public Sym M(float x, float y) { cur = new List<Vector2> { new Vector2(x, y) }; contours.Add(cur); p = new Vector2(x, y); return this; }
        public Sym L(float x, float y) { cur.Add(new Vector2(x, y)); p = new Vector2(x, y); return this; }
        public Sym Q(float cx, float cy, float x, float y)
        {
            Vector2 c = new Vector2(cx, cy), e = new Vector2(x, y), s = p;
            for (int i = 1; i <= 16; i++) { float t = i / 16f; cur.Add(Vector2.Lerp(Vector2.Lerp(s, c, t), Vector2.Lerp(c, e, t), t)); }
            p = e; return this;
        }
        public Sym B(float ax, float ay, float bx, float by, float x, float y)
        {
            Vector2 s = p, a = new Vector2(ax, ay), b = new Vector2(bx, by), e = new Vector2(x, y);
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f, u = 1 - t;
                cur.Add(u * u * u * s + 3 * u * u * t * a + 3 * u * t * t * b + t * t * t * e);
            }
            p = e; return this;
        }
        // three's absarc: a line to the arc's start (if a path is open), then the arc.
        public Sym Arc(float cx, float cy, float r, float a0, float a1, bool clockwise)
        {
            float d = a1 - a0;
            while (d < 0) d += Mathf.PI * 2;
            while (d > Mathf.PI * 2) d -= Mathf.PI * 2;
            if (d < 1e-5f) d = Mathf.PI * 2;
            if (clockwise) d = Mathf.Approximately(d, Mathf.PI * 2) ? -Mathf.PI * 2 : d - Mathf.PI * 2;
            const int n = 40;
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + d * i / n;
                var q = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                if (cur == null) M(q.x, q.y); else L(q.x, q.y);
            }
            return this;
        }
        public Sym Rect(float x, float y, float w, float h) => M(x, y).L(x + w, y).L(x + w, y + h).L(x, y + h);
        public Sym Circle(float cx, float cy, float r) { cur = null; return Arc(cx, cy, r, 0, Mathf.PI * 2, false); }
        public Sym Poly(params float[] xy) { M(xy[0], xy[1]); for (int i = 2; i < xy.Length; i += 2) L(xy[i], xy[i + 1]); return this; }
        public Sym Drop(float cx, float cy, float r)
        {
            M(cx, cy + 1.6f * r).Q(cx + r * 1.05f, cy + 0.4f * r, cx + r, cy).Arc(cx, cy, r, 0, Mathf.PI, true);
            return Q(cx - r * 1.05f, cy + 0.4f * r, cx, cy + 1.6f * r);
        }
    }

    static readonly Dictionary<string, System.Func<Sym>> Symbols = new Dictionary<string, System.Func<Sym>>
    {
        ["door"] = () => new Sym().Rect(-0.34f, -0.44f, 0.68f, 0.9f).Rect(-0.26f, -0.44f, 0.52f, 0.82f)
            .Rect(-0.2f, -0.44f, 0.4f, 0.76f).Circle(0.11f, -0.06f, 0.045f).Rect(-0.48f, -0.5f, 0.96f, 0.06f),
        ["bulb"] = () => new Sym().Circle(0, 0.16f, 0.3f).Poly(-0.17f, -0.1f, 0.17f, -0.1f, 0.12f, -0.22f, -0.12f, -0.22f)
            .Rect(-0.13f, -0.3f, 0.26f, 0.055f).Rect(-0.11f, -0.385f, 0.22f, 0.055f).Rect(-0.06f, -0.47f, 0.12f, 0.055f),
        ["beam"] = () => new Sym().Rect(-0.42f, -0.36f, 0.13f, 0.76f).Rect(0.29f, -0.36f, 0.13f, 0.76f)
            .Rect(-0.48f, -0.46f, 0.25f, 0.08f).Rect(0.23f, -0.46f, 0.25f, 0.08f).Rect(-0.29f, -0.04f, 0.58f, 0.08f),
        ["flameDrop"] = () => new Sym().M(0, -0.46f)
            .B(0.28f, -0.46f, 0.38f, -0.22f, 0.3f, -0.02f).B(0.24f, 0.14f, 0.14f, 0.24f, 0.1f, 0.46f)
            .B(0.0f, 0.26f, -0.1f, 0.22f, -0.14f, 0.08f).B(-0.18f, 0.18f, -0.22f, 0.22f, -0.26f, 0.26f)
            .B(-0.4f, 0.02f, -0.34f, -0.46f, 0, -0.46f)
            .Drop(0, -0.24f, 0.12f).Drop(0, -0.235f, 0.075f),
        ["wavesDown"] = () =>
        {
            var s = new Sym();
            void Wave(float y0)
            {
                const int n = 28; const float amp = 0.045f, th = 0.035f;
                for (int i = 0; i <= n; i++) { float x = -0.42f + 0.84f * i / n, y = y0 + amp * Mathf.Sin(x * Mathf.PI * 2.6f) + th; if (i == 0) s.M(x, y); else s.L(x, y); }
                for (int i = n; i >= 0; i--) { float x = -0.42f + 0.84f * i / n; s.L(x, y0 + amp * Mathf.Sin(x * Mathf.PI * 2.6f) - th); }
            }
            Wave(-0.14f); Wave(-0.29f); Wave(-0.44f);
            return s.Rect(-0.05f, 0.1f, 0.1f, 0.38f).Poly(-0.17f, 0.12f, 0.17f, 0.12f, 0, -0.05f);
        },
        ["bolt"] = () => new Sym().Poly(0.12f, 0.5f, -0.24f, -0.03f, -0.03f, -0.03f, -0.14f, -0.5f, 0.25f, 0.08f, 0.04f, 0.08f, 0.22f, 0.5f),
        ["star"] = () =>
        {
            var pts = new List<float>();
            for (int i = 0; i < 10; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / 5, r = i % 2 == 1 ? 0.2f : 0.5f; pts.Add(Mathf.Cos(a) * r); pts.Add(Mathf.Sin(a) * r); }
            return new Sym().Poly(pts.ToArray());
        },
    };

    static Texture2D SymbolTexture(string name)
    {
        string path = $"{SymbolDir}/Sym_{name}.png";
        const int N = 128, SS = 3;
        var sym = Symbols[name]();
        var all = sym.contours.SelectMany(c => c).ToList();
        var center = new Vector2((all.Min(p => p.x) + all.Max(p => p.x)) / 2, (all.Min(p => p.y) + all.Max(p => p.y)) / 2);
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int hit = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        var q = new Vector2((i + (sx + 0.5f) / SS) / N - 0.5f, (j + (sy + 0.5f) / SS) / N - 0.5f) + center;
                        if (Inside(sym.contours, q)) hit++;
                    }
                px[j * N + i] = new Color32(255, 255, 255, (byte)(255 * hit / (SS * SS)));
            }
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool Inside(List<List<Vector2>> contours, Vector2 p)
    {
        bool inside = false;
        foreach (var c in contours)
            for (int a = 0, b = c.Count - 1; a < c.Count; b = a++)
                if ((c[a].y > p.y) != (c[b].y > p.y) && p.x < (c[b].x - c[a].x) * (p.y - c[a].y) / (c[b].y - c[a].y) + c[a].x)
                    inside = !inside;
        return inside;
    }

    // White raised-symbol material: Standard, cut out by the symbol's alpha.
    static Material SymbolMat(string name)
    {
        if (symbolMats.TryGetValue(name, out var cached) && cached != null) return cached;
        string path = $"{MatDir}/TOS_Sym_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = C(0xf3f4f5);
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SymbolDir}/Sym_{name}.png");
        m.SetFloat("_Glossiness", 0.45f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Mode", 1);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.EnableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 2450;
        EditorUtility.SetDirty(m);
        symbolMats[name] = m;
        return m;
    }
}
