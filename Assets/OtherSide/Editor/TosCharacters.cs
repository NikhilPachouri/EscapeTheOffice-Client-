using System.Collections.Generic;
using System.Linq;
using EscapeOffice;
using UnityEditor;
using UnityEngine;

// Port of tos-characters.js (three.js r128): the two player builds (short worker, tall worker)
// and the supervisor boss, as jointed prefabs in Assets/OtherSide/Prefabs/Characters (run from
// Build Assets).
//
// Same conventions as the props: the JS numbers are kept and X is mirrored into Unity's
// left-handed space; feet on y = 0, facing +Z, heads scaled to the spec height. Every joint
// (Hips › Spine › Chest › Neck › Head, UpperArm › Forearm › Hand, Thigh › Shin › Foot) carries
// one merged mesh with a submesh per material, and TosCharacter drives them. The tie is its own
// renderer under the chest so each player can tint it.
public static partial class TosProps
{
    const string CharDir = "Assets/OtherSide/Prefabs/Characters";
    const string CharMeshDir = "Assets/OtherSide/Meshes/Characters";

    class CharSpec
    {
        public string name;
        public float height;
        public uint skin, hair, shirt, trousers, shoes, belt, tie, jacketCol, lapel;
        public float hipY, hipX;
        public float[] thigh, shin, upper, fore, hand, neck, head, shoe, shoulder, torsoScale;
        public Vector2[] torso, pelvis; // lathe profiles: (radius, y)
        public string hairStyle;
        public bool rolled, glasses, angry, jacket, doubleChin;
    }

    static Vector2[] Prof(params float[] ry)
    {
        var p = new Vector2[ry.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector2(ry[i * 2], ry[i * 2 + 1]);
        return p;
    }

    // Two player builds that read apart from above: short & stocky with black hair, glasses and
    // rolled sleeves vs tall & lanky with sandy hair in a quiff and a pale blue shirt.
    static readonly CharSpec Short = new CharSpec
    {
        name = "TOS_WorkerShort", height = 1.55f,
        skin = 0xc98e6b, hair = 0x1f1a17, shirt = 0xf2f2ef, trousers = 0x3d4148, shoes = 0x2a2724, belt = 0x2b2a28, tie = 0xc9ccd1,
        hipY = 0.7f, hipX = 0.09f, thigh = new[] { 0.08f, 0.068f, 0.32f }, shin = new[] { 0.068f, 0.058f, 0.32f },
        torso = Prof(0.17f, -0.02f, 0.18f, 0.08f, 0.195f, 0.17f, 0.195f, 0.26f, 0.185f, 0.32f, 0.15f, 0.37f, 0.07f, 0.395f, 0f, 0.4f),
        torsoScale = new[] { 1.12f, 0.8f }, pelvis = Prof(0f, -0.12f, 0.11f, -0.115f, 0.165f, -0.08f, 0.172f, 0.02f, 0.17f, 0.07f),
        shoulder = new[] { 0.2f, 0.32f }, upper = new[] { 0.058f, 0.052f, 0.23f }, fore = new[] { 0.052f, 0.046f, 0.21f }, hand = new[] { 0.078f, 0.092f, 0.047f },
        neck = new[] { 0.055f, 0.07f }, head = new[] { 0.168f, 0.17f, 0.162f }, shoe = new[] { 0.105f, 0.08f, 0.23f },
        hairStyle = "fringe", rolled = true, glasses = true,
    };

    static readonly CharSpec Tall = new CharSpec
    {
        name = "TOS_WorkerTall", height = 1.8f,
        skin = 0xf0c6a6, hair = 0xc9a46a, shirt = 0xcfe0ee, trousers = 0x353a42, shoes = 0x2a2724, belt = 0x2b2a28, tie = 0xc9ccd1,
        hipY = 0.98f, hipX = 0.08f, thigh = new[] { 0.07f, 0.058f, 0.46f }, shin = new[] { 0.058f, 0.048f, 0.46f },
        torso = Prof(0.14f, -0.02f, 0.142f, 0.1f, 0.15f, 0.22f, 0.168f, 0.34f, 0.165f, 0.4f, 0.135f, 0.455f, 0.065f, 0.48f, 0f, 0.487f),
        torsoScale = new[] { 1.12f, 0.66f }, pelvis = Prof(0f, -0.12f, 0.09f, -0.115f, 0.135f, -0.08f, 0.142f, 0.02f, 0.14f, 0.07f),
        shoulder = new[] { 0.19f, 0.41f }, upper = new[] { 0.05f, 0.043f, 0.31f }, fore = new[] { 0.043f, 0.037f, 0.29f }, hand = new[] { 0.07f, 0.1f, 0.042f },
        neck = new[] { 0.045f, 0.13f }, head = new[] { 0.145f, 0.175f, 0.15f }, shoe = new[] { 0.1f, 0.075f, 0.27f },
        hairStyle = "quiff",
    };

    // The supervisor's tie is deep maroon (not red) so it never reads as a code-panel colour.
    static readonly CharSpec Boss = new CharSpec
    {
        name = "TOS_Supervisor", height = 1.9f,
        skin = 0xd9a386, hair = 0x231f1d, shirt = 0xe9e7e2, trousers = 0x2b2d31, shoes = 0x181716, belt = 0x1d1d1d, tie = 0x5a1622, jacketCol = 0x303238, lapel = 0x26282c,
        hipY = 0.94f, hipX = 0.125f, thigh = new[] { 0.115f, 0.09f, 0.44f }, shin = new[] { 0.09f, 0.072f, 0.44f },
        torso = Prof(0.26f, -0.17f, 0.29f, -0.06f, 0.33f, 0.06f, 0.345f, 0.16f, 0.325f, 0.27f, 0.3f, 0.36f, 0.28f, 0.43f, 0.2f, 0.495f, 0.085f, 0.52f, 0f, 0.527f),
        torsoScale = new[] { 1.22f, 0.95f }, pelvis = Prof(0f, -0.12f, 0.16f, -0.115f, 0.23f, -0.07f, 0.25f, 0.02f),
        shoulder = new[] { 0.33f, 0.44f }, upper = new[] { 0.092f, 0.08f, 0.3f }, fore = new[] { 0.08f, 0.066f, 0.27f }, hand = new[] { 0.1f, 0.115f, 0.06f },
        neck = new[] { 0.095f, 0.07f }, head = new[] { 0.17f, 0.18f, 0.165f }, shoe = new[] { 0.13f, 0.09f, 0.29f },
        angry = true, hairStyle = "bald", jacket = true, doubleChin = true,
    };

    static int BuildCharacters()
    {
        OtherSideImporter.EnsureFolder(CharDir);
        OtherSideImporter.EnsureFolder(CharMeshDir);
        foreach (var s in new[] { Short, Tall, Boss }) Humanoid(s);
        return 3;
    }

    // ------------------------------------------------------------------ builder

    class CharKit
    {
        public readonly Transform root;
        public readonly List<Transform> groups = new List<Transform>(); // merge targets: joints + the tie
        public CharKit(string name) { root = new GameObject(name).transform; }

        public Transform Pivot(Transform parent, float x, float y, float z, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = P(x, y, z);
            groups.Add(t);
            return t;
        }

        public Transform Add(Mesh mesh, Material m, float sx, float sy, float sz, float x, float y, float z, Transform parent,
            float rx = 0f, float ry = 0f, float rz = 0f)
        {
            var go = new GameObject("Part");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = P(x, y, z);
            go.transform.localRotation = Rot(rx, ry, rz);
            go.transform.localScale = new Vector3(sx, sy, sz);
            return go.transform;
        }
    }

    static Material CM(uint hex, float rough = 0.85f, float metal = 0f) => Mat(hex, new Opt { rough = rough, metal = metal }, false);

    static void Humanoid(CharSpec S)
    {
        const float PI = Mathf.PI;
        var k = new CharKit(S.name);
        var skin = CM(S.skin); var hair = CM(S.hair); var shirt = CM(S.shirt); var trousers = CM(S.trousers);
        var shoes = CM(S.shoes, 0.6f); var belt = CM(S.belt); var eye = CM(0x221e1c, 0.4f); var mouth = CM(0x7a3b33);
        var jacket = S.jacket ? CM(S.jacketCol) : null; var lapel = S.jacket ? CM(S.lapel) : null;
        var tie = CM(S.tie, 0.75f); var glasses = S.glasses ? CM(0x2a2d33, 0.4f, 0.3f) : null;
        var box = MeshFor(Shape.Box); var sph = Shell(0, PI, 0, 32, 20, false); var cyl = MeshFor(Shape.Cyl);

        var body = new GameObject("Body").transform;
        body.SetParent(k.root, false);

        // ---- hips & legs
        var hips = k.Pivot(body, 0, S.hipY, 0, "Hips");
        float tx = S.torsoScale[0], tz = S.torsoScale[1];
        k.Add(Lathe(S.pelvis, 32), trousers, tx * 0.95f, 1, tz * 1.05f, 0, 0, 0, hips);
        foreach (var side in new[] { 1, -1 })
        {
            var L = side == 1 ? "L" : "R";
            var hip = k.Pivot(hips, side * S.hipX, -0.04f, 0, "Thigh" + L);
            k.Add(Limb(S.thigh), trousers, 1, 1, 1, 0, 0, 0, hip);
            var knee = k.Pivot(hip, 0, -S.thigh[2] + S.shin[0] * 0.4f, 0, "Shin" + L);
            k.Add(Limb(S.shin), trousers, 1, 1, 1, 0, 0, 0, knee);
            k.Add(cyl, trousers, S.shin[1] * 2.3f, 0.05f, S.shin[1] * 2.3f, 0, -S.shin[2] + 0.07f, 0, knee); // hem
            var ankle = k.Pivot(knee, 0, -S.shin[2] + 0.02f, 0, "Foot" + L);
            float sw = S.shoe[0], sh = S.shoe[1], sl = S.shoe[2];
            // shoe: sole flat on the floor, toe forward
            float floorY = -(S.hipY - 0.04f - S.thigh[2] + S.shin[0] * 0.4f - S.shin[2] + 0.02f);
            k.Add(box, belt, sw * 0.95f, 0.025f, sl, 0, floorY + 0.0125f, sl * 0.22f, ankle);
            k.Add(sph, shoes, sw, sh * 1.6f, sl, 0, floorY + sh * 0.8f + 0.004f, sl * 0.22f, ankle);
            k.Add(sph, shoes, sw * 0.9f, sh * 1.3f, sw * 1.2f, 0, floorY + sh * 0.65f + 0.012f, -0.01f, ankle);
        }

        // ---- spine & torso
        var spine = k.Pivot(hips, 0, 0.06f, 0, "Spine");
        var chest = k.Pivot(spine, 0, 0, 0, "Chest");
        var torsoMat = S.jacket ? jacket : shirt;
        k.Add(Lathe(S.torso, 32), torsoMat, tx, 1, tz, 0, 0, 0, chest);
        float RAt(float y)
        {
            var p = S.torso;
            for (int i = 1; i < p.Length; i++)
                if (p[i].y >= y) return p[i - 1].x + (p[i].x - p[i - 1].x) * (y - p[i - 1].y) / (p[i].y - p[i - 1].y);
            return 0f;
        }
        float FrontZ(float y) => RAt(y) * tz;
        float topY = S.torso[S.torso.Length - 1].y;
        // Pivot z + tilt for a flat piece hanging from y0 down to y1 so it clears the torso (belly included).
        (float z, float rx) Follow(float y0, float y1, float off)
        {
            float z0 = FrontZ(y0) + off, a = 0f;
            for (int i = 1; i <= 24; i++)
            {
                float y = y0 - (y0 - y1) * i / 24f;
                a = Mathf.Max(a, Mathf.Atan2(FrontZ(y) + off - z0, y0 - y));
            }
            return (z0, -a);
        }

        if (!S.jacket)
        {
            k.Add(cyl, belt, RAt(0.01f) * 2 * tx + 0.006f, 0.035f, RAt(0.01f) * 2 * tz + 0.006f, 0, 0.01f, 0, chest);
            k.Add(box, CM(0x9a9690, 0.4f, 0.6f), 0.045f, 0.03f, 0.01f, 0, 0.01f, FrontZ(0.01f) + 0.004f, chest); // buckle
        }
        else
        {
            // shirt V between the lapels, lapels, buttons
            float vTop = topY - 0.05f, vBot = 0.2f, vh = vTop - vBot;
            var fb = Follow(vTop, vBot, -0.004f);
            k.Add(Extrude(new[] { new Vector2(-0.14f, 0), new Vector2(0.14f, 0), new Vector2(0, -vh) }, 0.01f), shirt, 1, 1, 1, 0, vTop, fb.z, chest, fb.rx);
            var lapelShape = Extrude(new[] { new Vector2(0, 0), new Vector2(0.1f, 0), new Vector2(0.03f, -vh), new Vector2(0, -vh - 0.02f) }, 0.014f);
            foreach (var side in new[] { 1, -1 })
                k.Add(lapelShape, lapel, side, 1, 1, side * 0.05f, vTop, fb.z + 0.004f, chest, fb.rx);
            foreach (var y in new[] { 0.2f, 0.08f }) k.Add(sph, belt, 0.03f, 0.03f, 0.015f, 0, y, FrontZ(y) + 0.002f, chest);
        }

        // collar, tie
        float collarY = topY - 0.035f;
        var collar = Extrude(new[] { new Vector2(0, 0), new Vector2(0.07f, 0.01f), new Vector2(0.02f, -0.06f) }, 0.012f);
        foreach (var side in new[] { 1, -1 })
            k.Add(collar, shirt, side * (S.jacket ? 1.1f : 1f), 1, 1, side * 0.006f, collarY, FrontZ(collarY - 0.03f) - 0.012f, chest, -0.35f);
        var tieGroup = k.Pivot(chest, 0, 0, 0, "Tie");
        float tieTop = collarY - 0.01f, tieLen = S.jacket ? 0.36f : 0.32f, tw = S.jacket ? 0.05f : 0.042f;
        k.Add(Extrude(new[] { new Vector2(-tw * 0.55f, 0), new Vector2(tw * 0.55f, 0), new Vector2(tw * 0.35f, -0.045f), new Vector2(-tw * 0.35f, -0.045f) }, 0.02f),
            tie, 1, 1, 1, 0, tieTop, FrontZ(tieTop - 0.03f) - 0.006f, tieGroup, -0.25f); // knot
        var ft = Follow(tieTop - 0.04f, tieTop - 0.04f - tieLen, S.jacket ? 0.018f : 0.004f);
        k.Add(Extrude(new[] { new Vector2(-tw * 0.35f, 0), new Vector2(tw * 0.35f, 0), new Vector2(tw, -tieLen + 0.05f), new Vector2(0, -tieLen), new Vector2(-tw, -tieLen + 0.05f) }, 0.008f),
            tie, 1, 1, 1, 0, tieTop - 0.04f, ft.z, tieGroup, ft.rx); // blade

        // ---- neck & head
        var neck = k.Pivot(chest, 0, topY - 0.03f, 0, "Neck");
        k.Add(cyl, skin, S.neck[0] * 2, S.neck[1], S.neck[0] * 2 * 0.95f, 0, S.neck[1] / 2, 0, neck);
        float hx = S.head[0], hy = S.head[1], hz = S.head[2];
        var head = k.Pivot(neck, 0, S.neck[1] * 0.55f, 0, "Head");
        float hc = hy * 0.92f; // head centre above the neck pivot
        k.Add(sph, skin, hx * 2, hy * 2, hz * 2, 0, hc, 0, head);
        if (S.jacket) k.Add(sph, skin, hx * 2.08f, hy * 1.2f, hz * 1.8f, 0, hc - hy * 0.42f, 0, head); // heavy jaw
        if (S.doubleChin) k.Add(sph, skin, hx * 1.55f, hy * 0.6f, hz * 1.35f, 0, hc - hy * 0.86f, hz * 0.3f, head);
        foreach (var side in new[] { 1, -1 }) k.Add(sph, skin, 0.035f, 0.06f, 0.04f, side * hx * 0.98f, hc - 0.005f, -0.005f, head); // ears
        k.Add(sph, skin, 0.042f, 0.04f, 0.04f, 0, hc - 0.03f, hz * 0.97f, head); // nose
        float FaceZ(float y) => hz * Mathf.Sqrt(Mathf.Max(0f, 1f - (y / hy) * (y / hy))) * 0.97f;
        foreach (var side in new[] { 1, -1 })
        {
            k.Add(sph, eye, 0.034f, S.angry ? 0.03f : 0.044f, 0.02f, side * 0.055f, hc + 0.015f, FaceZ(0.015f) - 0.004f, head);
            // angry: inner ends pulled down
            k.Add(box, hair, S.angry ? 0.07f : 0.06f, S.angry ? 0.02f : 0.013f, 0.022f, side * 0.058f, hc + (S.angry ? 0.06f : 0.07f), FaceZ(0.065f) - 0.002f, head,
                0, 0, S.angry ? side * 0.38f : side * -0.06f);
            if (S.angry) k.Add(sph, skin, 0.05f, 0.02f, 0.02f, side * 0.055f, hc - 0.012f, FaceZ(-0.012f) - 0.002f, head); // eye bags
        }
        if (S.angry) k.Add(box, skin, 0.13f, 0.025f, 0.03f, 0, hc + 0.052f, FaceZ(0.05f) - 0.01f, head); // brow ridge
        if (S.glasses)
        {
            float gz = FaceZ(0.015f) + 0.012f;
            var lens = CharTorus(0.5f, 0.09f, 8, 28, PI * 2);
            foreach (var side in new[] { 1, -1 })
            {
                k.Add(lens, glasses, 0.085f, 0.075f, 0.06f, side * 0.058f, hc + 0.017f, gz, head);
                k.Add(box, glasses, 0.008f, 0.008f, hz * 0.95f, side * hx * 0.96f, hc + 0.025f, gz - hz * 0.5f, head); // arms to the ears
            }
            k.Add(box, glasses, 0.032f, 0.008f, 0.008f, 0, hc + 0.022f, gz + 0.004f, head); // bridge
        }
        // worker: slight smile; boss: frown
        k.Add(CharTorus(0.5f, 0.2f, 6, 14, PI), mouth, S.angry ? 0.075f : 0.06f, S.angry ? 0.04f : 0.03f, 0.03f,
            0, hc - (S.angry ? 0.09f : 0.08f), FaceZ(-0.085f) + (S.jacket ? 0.02f : -0.004f), head, 0, 0, S.angry ? 0f : PI);

        // hair
        if (S.hairStyle == "bald")
            k.Add(Shell(PI * 0.42f, PI * 0.7f, 0.78f), hair, hx * 2.1f, hy * 2.05f, hz * 2.1f, 0, hc, -0.006f, head); // sides + back
        else
        {
            k.Add(Shell(0, PI * 0.38f, 0), hair, hx * 2.12f, hy * 2.07f, hz * 2.13f, 0, hc + 0.012f, -0.004f, head);
            k.Add(Shell(0, PI * 0.6f, 0.95f), hair, hx * 2.1f, hy * 2.04f, hz * 2.1f, 0, hc + 0.004f, -0.008f, head);
            if (S.hairStyle == "fringe") // short, full, straight fringe
            {
                k.Add(sph, hair, hx * 1.45f, 0.06f, 0.07f, 0, hc + hy * 0.64f, hz * 0.8f, head, -0.5f);
                k.Add(sph, hair, hx * 2.06f, hy * 0.62f, hz * 2.0f, 0, hc + hy * 0.66f, -0.012f, head); // fuller crown
            }
            else // tall, swept-up quiff
            {
                k.Add(sph, hair, 0.23f, 0.13f, 0.2f, -0.015f, hc + hy * 0.92f, hz * 0.32f, head, -0.28f);
                k.Add(sph, hair, 0.16f, 0.06f, 0.1f, -0.05f, hc + hy * 0.84f, hz * 0.55f, head, 0, 0, 0.2f);
            }
        }

        // ---- arms
        var arms = new Dictionary<string, Transform>();
        foreach (var side in new[] { 1, -1 })
        {
            var L = side == 1 ? "L" : "R";
            var sh = k.Pivot(chest, side * S.shoulder[0], S.shoulder[1], 0, "UpperArm" + L);
            k.Add(sph, torsoMat, S.upper[0] * 2.3f, S.upper[0] * 2.3f, S.upper[0] * 2.2f, 0, -0.005f, 0, sh); // shoulder cap
            k.Add(Limb(S.upper), torsoMat, 1, 1, 1, 0, 0, 0, sh);
            var el = k.Pivot(sh, 0, -S.upper[2] + S.fore[0] * 0.5f, 0, "Forearm" + L);
            k.Add(Limb(S.fore), S.rolled ? skin : torsoMat, 1, 1, 1, 0, 0, 0, el);
            if (S.rolled) k.Add(cyl, shirt, S.fore[0] * 2.5f, 0.06f, S.fore[0] * 2.5f, 0, -0.03f, 0, el); // rolled-up sleeve
            else k.Add(cyl, shirt, S.fore[1] * 2.35f, 0.04f, S.fore[1] * 2.35f, 0, -S.fore[2] + 0.045f, 0, el); // cuff
            var wr = k.Pivot(el, 0, -S.fore[2] + 0.02f, 0, "Hand" + L);
            float hw = S.hand[0], hh = S.hand[1], hd = S.hand[2];
            k.Add(sph, skin, hw, hh, hd * 1.4f, 0, -hh * 0.45f, 0, wr); // mitten
            k.Add(Limb(new[] { 0.018f * hw / 0.075f, 0.016f * hw / 0.075f, 0.05f * hh / 0.095f }), skin, 1, 1, 1, 0, -hh * 0.2f, hd * 0.55f, wr, 0.6f); // thumb forward
            arms["UpperArm" + L] = sh; arms["Forearm" + L] = el;
        }

        // Scale so the top of the head lands exactly on the spec height.
        // (Vertices, not renderer bounds: those grow on rotated parts like the quiff.)
        var top = k.root.GetComponentsInChildren<MeshFilter>()
            .Max(mf => mf.sharedMesh.vertices.Max(v => mf.transform.TransformPoint(v).y));
        body.localScale = Vector3.one * (S.height / top);
        // Rest in the A-pose (what Mixamo expects); TosCharacter takes over at runtime.
        arms["UpperArmL"].localRotation = TosCharacter.ThreeEuler(new Vector3(0, 0, 0.8f));
        arms["UpperArmR"].localRotation = TosCharacter.ThreeEuler(new Vector3(0, 0, -0.8f));

        MergeJoints(k, S.name);

        var c = k.root.gameObject.AddComponent<TosCharacter>();
        Transform J(string n) => k.groups.First(g => g.name == n);
        c.hips = hips; c.spine = spine; c.chest = chest; c.neck = neck;
        c.thighL = J("ThighL"); c.thighR = J("ThighR"); c.shinL = J("ShinL"); c.shinR = J("ShinR"); c.footL = J("FootL"); c.footR = J("FootR");
        c.upperArmL = arms["UpperArmL"]; c.upperArmR = arms["UpperArmR"]; c.forearmL = arms["ForearmL"]; c.forearmR = arms["ForearmR"];
        c.hipY = S.hipY;
        c.tie = tieGroup.GetComponent<Renderer>();
        c.tieColor = C(S.tie);

        PrefabUtility.SaveAsPrefabAsset(k.root.gameObject, $"{CharDir}/{S.name}.prefab");
        Object.DestroyImmediate(k.root.gameObject);
    }

    // Each joint's own parts (its direct children, not the joints below it) become one mesh on
    // the joint, a submesh per material. All meshes go in one asset per character.
    static void MergeJoints(CharKit k, string name)
    {
        var path = $"{CharMeshDir}/{name}.asset";
        var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(m => m.name);
        bool fresh = existing.Count == 0;
        var groupSet = new HashSet<Transform>(k.groups);

        foreach (var g in k.groups)
        {
            var parts = new List<MeshFilter>();
            foreach (Transform child in g)
                if (!groupSet.Contains(child) && child.TryGetComponent<MeshFilter>(out var mf)) parts.Add(mf);
            if (parts.Count == 0) continue;

            var mats = new List<Material>();
            var verts = new List<Vector3>(); var normals = new List<Vector3>();
            var tris = new List<List<int>>();
            foreach (var mf in parts)
            {
                var mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
                int sub = mats.IndexOf(mat);
                if (sub < 0) { sub = mats.Count; mats.Add(mat); tris.Add(new List<int>()); }
                var m = g.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var nm = m.inverse.transpose;
                bool flip = m.determinant < 0f; // mirrored parts (the left lapel and collar)
                var src = mf.sharedMesh;
                int o = verts.Count;
                verts.AddRange(src.vertices.Select(v => m.MultiplyPoint3x4(v)));
                normals.AddRange(src.normals.Select(n => nm.MultiplyVector(n).normalized));
                var t = src.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    tris[sub].Add(o + t[i]);
                    tris[sub].Add(o + (flip ? t[i + 2] : t[i + 1]));
                    tris[sub].Add(o + (flip ? t[i + 1] : t[i + 2]));
                }
            }
            foreach (var mf in parts) Object.DestroyImmediate(mf.gameObject);

            var mesh = new Mesh { name = $"{name}_{g.name}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = mats.Count;
            for (int s = 0; s < mats.Count; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateBounds();

            if (existing.TryGetValue(mesh.name, out var target))
            {
                EditorUtility.CopySerialized(mesh, target);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                target = mesh;
                if (fresh) { AssetDatabase.CreateAsset(target, path); fresh = false; }
                else AssetDatabase.AddObjectToAsset(target, path);
                existing[target.name] = target;
            }
            g.gameObject.AddComponent<MeshFilter>().sharedMesh = target;
            g.gameObject.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        }
    }

    // ------------------------------------------------------------------ geometry (three's, mirrored into Unity)
    // Built in memory only: they're merged into the joint meshes above.

    // Capsule hanging from its pivot: top at y = 0, bottom at y = -len, radius r1 at top, r2 at bottom.
    static Mesh Limb(float[] rrl)
    {
        float r1 = rrl[0], r2 = rrl[1], len = rrl[2];
        var pts = new List<Vector2>();
        const int n = 8;
        for (int i = 0; i <= n; i++) { float a = -Mathf.PI / 2 + i / (float)n * (Mathf.PI / 2); pts.Add(new Vector2(Mathf.Cos(a) * r2, -len + r2 + Mathf.Sin(a) * r2)); }
        for (int i = 0; i <= n; i++) { float a = i / (float)n * (Mathf.PI / 2); pts.Add(new Vector2(Mathf.Cos(a) * r1, -r1 + Mathf.Sin(a) * r1)); }
        pts[0] = new Vector2(0.0001f, pts[0].y); pts[pts.Count - 1] = new Vector2(0.0001f, pts[pts.Count - 1].y);
        return Lathe(pts.ToArray(), 24);
    }

    // Surface of revolution about Y from (radius, y) pairs, bottom to top. Rings wrap (no seam).
    static Mesh Lathe(Vector2[] profile, int seg)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        foreach (var p in profile)
        {
            float r = Mathf.Max(p.x, 0.0001f);
            for (int j = 0; j < seg; j++) { float a = j * Mathf.PI * 2 / seg; v.Add(new Vector3(Mathf.Sin(a) * r, p.y, Mathf.Cos(a) * r)); }
        }
        for (int i = 0; i < profile.Length - 1; i++)
        {
            // The profile's outward normal in (radius, y) for this band.
            float dr = profile[i + 1].x - profile[i].x, dy = profile[i + 1].y - profile[i].y;
            for (int j = 0; j < seg; j++)
            {
                int a = i * seg + j, b = i * seg + (j + 1) % seg, c = a + seg, d = b + seg;
                foreach (var (p, q, s) in new[] { (a, c, b), (b, c, d) })
                {
                    var centre = (v[p] + v[q] + v[s]) / 3f;
                    var radial = new Vector3(centre.x, 0, centre.z).normalized;
                    Orient(v, tri, p, q, s, radial * dy + Vector3.up * -dr);
                }
            }
        }
        return Finish(v, tri);
    }

    // Sphere (part) like three's SphereGeometry(0.5, ws, hs, phiStart, phiLength, t0, t1 - t0) with
    // phi centred on the face (+Z) gap. Hair shells are double-sided, as the hair material is in
    // the JS. (Unity's built-in sphere has radius 1 here, not three's 0.5, hence our own.)
    static Mesh Shell(float t0, float t1, float faceGap, int ws = 40, int hs = 20, bool doubleSided = true)
    {
        float phi0 = Mathf.PI / 2 + faceGap, phiLen = Mathf.PI * 2 - faceGap * 2;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int iy = 0; iy <= hs; iy++)
            for (int ix = 0; ix <= ws; ix++)
            {
                float phi = phi0 + ix / (float)ws * phiLen, theta = t0 + iy / (float)hs * (t1 - t0);
                // three: x = -0.5 cos φ sin θ; mirrored into Unity
                v.Add(new Vector3(0.5f * Mathf.Cos(phi) * Mathf.Sin(theta), 0.5f * Mathf.Cos(theta), 0.5f * Mathf.Sin(phi) * Mathf.Sin(theta)));
            }
        for (int iy = 0; iy < hs; iy++)
            for (int ix = 0; ix < ws; ix++)
            {
                int a = iy * (ws + 1) + ix, b = a + 1, c = a + ws + 1, d = c + 1;
                foreach (var (p, q, s) in new[] { (a, c, b), (b, c, d) })
                    Orient(v, tri, p, q, s, (v[p] + v[q] + v[s]) / 3f);
            }
        if (!doubleSided) return Finish(v, tri);
        // back faces: a second copy, reversed, so the inside shows through the face gap
        int count = v.Count, triCount = tri.Count;
        v.AddRange(v.ToList());
        for (int i = 0; i < triCount; i += 3) tri.AddRange(new[] { tri[i] + count, tri[i + 2] + count, tri[i + 1] + count });
        return Finish(v, tri);
    }

    // three's ExtrudeGeometry of a convex outline in XY, depth along +Z (its bevel is only
    // thickness here). Flat shaded: every face gets its own vertices.
    static Mesh Extrude(Vector2[] outline, float depth)
    {
        const float bevel = 0.004f;
        float z0 = -bevel, z1 = depth + bevel;
        var pts = outline.Select(p => new Vector2(-p.x, p.y)).ToArray(); // mirrored into Unity
        var centre = new Vector3(pts.Average(p => p.x), pts.Average(p => p.y), (z0 + z1) / 2);
        var v = new List<Vector3>(); var tri = new List<int>();
        void Face(params Vector3[] poly)
        {
            int o = v.Count;
            v.AddRange(poly);
            var fc = poly.Aggregate(Vector3.zero, (s, p) => s + p) / poly.Length;
            for (int i = 1; i < poly.Length - 1; i++) Orient(v, tri, o, o + i, o + i + 1, fc - centre);
        }
        Face(pts.Select(p => new Vector3(p.x, p.y, z1)).ToArray());
        Face(pts.Select(p => new Vector3(p.x, p.y, z0)).ToArray());
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Length];
            Face(new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1));
        }
        return Finish(v, tri);
    }

    // Ring in the XY plane like three's TorusGeometry(radius, tube, radial, tubular, arc):
    // the arc runs from +X up through +Y, which mirroring leaves symmetric.
    static Mesh CharTorus(float R, float r, int radial, int tubular, float arc)
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
                foreach (var (p, q, s) in new[] { (a, b, d), (b, c, d) })
                {
                    var m = (v[p] + v[q] + v[s]) / 3f;
                    Orient(v, tri, p, q, s, m - new Vector3(m.x, m.y, 0).normalized * R);
                }
            }
        return Finish(v, tri);
    }

    // Same winding rule as Build(): Cross(b - a, c - a) points along the outward direction.
    static void Orient(List<Vector3> v, List<int> tri, int a, int b, int c, Vector3 outward)
    {
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), outward) < 0f) (b, c) = (c, b);
        tri.Add(a); tri.Add(b); tri.Add(c);
    }

    static Mesh Finish(List<Vector3> v, List<int> tri)
    {
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
