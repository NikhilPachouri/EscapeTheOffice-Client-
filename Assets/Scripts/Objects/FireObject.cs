using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Wall on its tiles while burning (key true). Placed in the level, only ever put out.
    // Casts light beyond its tiles, so it is visible before it is inside the camera radius.
    public class FireObject : Blocker
    {
        const float LightRadius = 2.5f;
        const float ExtinguishSeconds = 0.7f;

        SpriteRenderer[] flames;
        SpriteRenderer halo;
        int lightIndex = -1;

        // 3D: one Fire_Tile per cell; its Flames group scales to 0 when put out, Scorch stays.
        readonly List<Transform> flameGroups = new List<Transform>();
        readonly List<Transform> flameParts = new List<Transform>();
        readonly List<Light> fireLights = new List<Light>();
        float flameScale = 1f;
        bool wasBurning, visualInitialised;

        protected override bool SolidFor(JToken value) => WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
            if (Art.Available && BuildModels()) return;

            halo = SpriteFactory.Child(transform, "Halo", SpriteFactory.Glow, new Color(1f, 0.45f, 0.1f, 0.5f),
                Layers.Glow, scale: Vector2.one * (Mathf.Max(Bounds.width, Bounds.height) + LightRadius * 2f));
            flames = new SpriteRenderer[4];
            for (int i = 0; i < flames.Length; i++)
            {
                var pos = new Vector2((Random.value - 0.5f) * Bounds.width * 0.6f, (Random.value - 0.5f) * Bounds.height * 0.6f);
                flames[i] = SpriteFactory.Child(transform, "Flame", SpriteFactory.Circle, new Color(1f, 0.6f, 0.1f),
                    Layers.ObjectTop, pos, Vector2.one * 0.5f);
            }
        }

        bool BuildModels()
        {
            int w = Mathf.Max(1, Def.W), h = Mathf.Max(1, Def.H);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var local = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                var tile = Art.Spawn("Fire_Tile", transform, local, Random.Range(0, 4) * 90f);
                if (tile == null) return false;
                var group = Art.Find(tile, "Flames");
                if (group != null) flameGroups.Add(group);
                foreach (var t in tile.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Flame_")) flameParts.Add(t);
                // A light every few cells is plenty (each Fire_Tile ships with one).
                var lights = tile.GetComponentsInChildren<Light>(true);
                bool keep = (x + y) % 4 == 1 || w * h == 1;
                foreach (var l in lights)
                {
                    l.enabled = keep;
                    if (keep) fireLights.Add(l);
                }
            }
            body.enabled = false;
            return true;
        }

        protected override void UpdateVisual(bool isSolid)
        {
            SetBodyColor(isSolid ? new Color(0.6f, 0.15f, 0.05f, 0.8f) : new Color(0.1f, 0.1f, 0.1f, 0.6f)); // ash when out
            if (halo != null) halo.enabled = isSolid;
            if (flames != null) foreach (var f in flames) f.enabled = isSolid;

            if (flameGroups.Count > 0)
            {
                if (!visualInitialised) flameScale = isSolid ? 1f : 0f; // no animation on load
                else if (wasBurning && !isSolid) PuffSteam();
                visualInitialised = true;
                wasBurning = isSolid;
            }

            if (isSolid && lightIndex < 0)
            {
                lightIndex = World.Lights.Count;
                World.Lights.Add(new Vector3(Bounds.center.x, Bounds.center.y, LightRadius + Mathf.Max(Bounds.width, Bounds.height) * 0.5f));
            }
            else if (!isSolid && lightIndex >= 0)
            {
                World.Lights[lightIndex] = Vector3.zero; // keep indices stable; zero radius = no light
                lightIndex = -1;
            }
        }

        void PuffSteam()
        {
            foreach (var g in flameGroups)
            {
                var puff = Art.Spawn("FX_SteamPuff", transform, (Vector2)g.parent.localPosition);
                if (puff != null) StartCoroutine(Rise(puff.transform));
            }
        }

        System.Collections.IEnumerator Rise(Transform puff)
        {
            var start = puff.localPosition;
            for (float t = 0; t < 1.2f; t += Time.deltaTime)
            {
                if (puff == null) yield break;
                float k = t / 1.2f;
                puff.localPosition = start + new Vector3(0, 0, -1.2f * k); // up is -Z
                puff.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, k) * (1f - k * k);
                yield return null;
            }
            if (puff != null) Destroy(puff.gameObject);
        }

        protected override void Update()
        {
            base.Update();

            if (flameGroups.Count > 0)
            {
                // Put out: flames shrink to nothing over ~0.7 s.
                float target = IsSolid ? 1f : 0f;
                flameScale = Mathf.MoveTowards(flameScale, target, Time.deltaTime / ExtinguishSeconds);
                foreach (var g in flameGroups) g.localScale = Vector3.one * flameScale;
                foreach (var l in fireLights) l.intensity = 2.2f * flameScale * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 8f, 0f));
                if (!IsSolid) return;
                // palette: scale.y 0.8 + sin(t*9+ph)*0.2 + sin(t*23+ph)*0.08, x wobble sin(t*5)*0.04
                for (int i = 0; i < flameParts.Count; i++)
                {
                    float ph = i * 1.7f, t = Time.time;
                    var s = flameParts[i].localScale;
                    flameParts[i].localScale = new Vector3(1f + Mathf.Sin(t * 5f + ph) * 0.04f, 0.8f + Mathf.Sin(t * 9f + ph) * 0.2f + Mathf.Sin(t * 23f + ph) * 0.08f, s.z);
                }
                return;
            }

            if (!IsSolid) return;
            for (int i = 0; i < flames.Length; i++)
            {
                float n = Mathf.PerlinNoise(Time.time * 6f, i * 1.7f);
                flames[i].transform.localScale = Vector3.one * (0.35f + 0.35f * n);
                flames[i].color = Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.85f, 0.3f), n);
            }
        }
    }
}
