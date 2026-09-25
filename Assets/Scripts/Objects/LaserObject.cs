using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Wall on its tiles while on (key true). Toggled by a laser switch.
    public class LaserObject : Blocker
    {
        SpriteRenderer[] beams;

        protected override bool SolidFor(JToken value) => WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
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

        protected override void UpdateVisual(bool isSolid)
        {
            SetBodyColor(new Color(1f, 0.1f, 0.1f, isSolid ? 0.18f : 0f));
            foreach (var b in beams) b.enabled = isSolid;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsSolid) return;
            for (int i = 0; i < beams.Length; i++)
            {
                var c = beams[i].color;
                c.a = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, i * 3.1f);
                beams[i].color = c;
            }
        }
    }
}
