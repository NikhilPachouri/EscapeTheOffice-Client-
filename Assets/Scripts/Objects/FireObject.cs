using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Wall on its tiles while burning (key true). Placed in the level, only ever put out.
    // Casts light beyond its tiles, so it is visible before it is inside the camera radius.
    public class FireObject : Blocker
    {
        const float LightRadius = 2.5f;

        SpriteRenderer[] flames;
        SpriteRenderer halo;
        int lightIndex = -1;

        protected override bool SolidFor(JToken value) => WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
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

        protected override void UpdateVisual(bool isSolid)
        {
            SetBodyColor(isSolid ? new Color(0.6f, 0.15f, 0.05f, 0.8f) : new Color(0.1f, 0.1f, 0.1f, 0.6f)); // ash when out
            halo.enabled = isSolid;
            foreach (var f in flames) f.enabled = isSolid;

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

        protected override void Update()
        {
            base.Update();
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
