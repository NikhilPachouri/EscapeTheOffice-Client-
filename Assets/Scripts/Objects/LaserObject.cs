using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Wall on its tiles while on (key true). Toggled by a laser switch.
    public class LaserObject : Blocker
    {
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        SpriteRenderer[] beams;
        // 3D: a post at every cell edge and a beam segment per cell.
        GameObject[] beamModels;
        Renderer[] beamRenderers;
        MaterialPropertyBlock block;
        Color beamTint;
        ParticleSystem[] sparks = new ParticleSystem[0];
        bool wasOn, seen;

        protected override bool SolidFor(JToken value) => WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
            if (Art.Available && BuildModels()) return;

            bool horizontal = Bounds.width >= Bounds.height;
            beams = new SpriteRenderer[3];
            for (int i = 0; i < beams.Length; i++)
            {
                float offset = (i - 1) * 0.25f;
                var pos = horizontal ? new Vector2(0, offset * Bounds.height) : new Vector2(offset * Bounds.width, 0);
                var scale = horizontal ? new Vector2(Bounds.width, 0.06f) : new Vector2(0.06f, Bounds.height);
                beams[i] = SpriteFactory.Child(transform, "Beam", SpriteFactory.Square, new Color(1f, 0.1f, 0.15f),
                    Layers.ObjectTop, pos, scale);
            }
        }

        bool BuildModels()
        {
            float yaw = Art.SpanYaw(World, Def.X, Def.Y, Def.W, Def.H);
            bool vertical = Mathf.Abs(yaw) > 45f;
            int cells = Mathf.Max(1, vertical ? Def.H : Def.W);
            var step = vertical ? Vector2.up : Vector2.right;
            var start = -step * (cells * 0.5f);

            beamModels = new GameObject[cells];
            for (int i = 0; i < cells; i++)
            {
                beamModels[i] = Art.Spawn("Laser_Beams_1m", transform, start + step * (i + 0.5f), yaw);
                if (beamModels[i] == null) return false;
                // One red light for the whole run, at its centre.
                if (i != cells / 2)
                    foreach (var l in beamModels[i].GetComponentsInChildren<Light>(true)) l.enabled = false;
            }
            for (int i = 0; i <= cells; i++) Art.Spawn("Laser_Post", transform, start + step * i, yaw);
            var red = new Color(1f, 0.25f, 0.3f);
            sparks = new[]
            {
                Fx3D.Sparks(transform, (Vector3)(start) + new Vector3(0f, 0f, -0.55f), red),
                Fx3D.Sparks(transform, (Vector3)(start + step * cells) + new Vector3(0f, 0f, -0.55f), red),
            }.Where(s => s != null).ToArray();

            body.enabled = false;
            beamRenderers = GetComponentsInChildren<Renderer>(true);
            var mat = Art.Material("M_LaserBeam");
            beamTint = mat != null && mat.HasProperty(TintId) ? mat.GetColor(TintId) : new Color(0.5f, 0.08f, 0.14f, 0.42f);
            block = new MaterialPropertyBlock();
            return true;
        }

        protected override void UpdateVisual(bool isSolid)
        {
            SetBodyColor(new Color(1f, 0.1f, 0.1f, isSolid ? 0.18f : 0f));
            if (beams != null) foreach (var b in beams) b.enabled = isSolid;
            if (beamModels != null)
                foreach (var m in beamModels)
                    if (m != null) Art.Find(m, "Beams")?.gameObject.SetActive(isSolid);
            foreach (var s in sparks)
            {
                if (isSolid && !s.isPlaying) s.Play();
                else if (!isSolid) s.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            // Switched off: a zap along the run.
            if (seen && wasOn && !isSolid && Settled && sparks.Length > 0)
                Fx3D.Burst(FxPoint(0.55f), new Color(1f, 0.3f, 0.35f), count: 10 + 4 * Mathf.Max(Def.W, Def.H), speed: 2.5f, size: 0.15f, life: 0.45f);
            seen = true;
            wasOn = isSolid;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsSolid) return;
            if (beams != null)
                for (int i = 0; i < beams.Length; i++)
                {
                    var c = beams[i].color;
                    c.a = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, i * 3.1f);
                    beams[i].color = c;
                }
            if (beamRenderers != null)
            {
                // Flicker opacity 0.75 ± 0.2 at ~6 Hz (palette.json M_LaserBeam).
                var c = beamTint;
                c.a *= (0.75f + 0.2f * Mathf.Sin(Time.time * 6f * Mathf.PI * 2f)) / 0.85f;
                block.SetColor(TintId, c);
                foreach (var r in beamRenderers)
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial.name == "M_LaserBeam") r.SetPropertyBlock(block);
            }
        }
    }
}
