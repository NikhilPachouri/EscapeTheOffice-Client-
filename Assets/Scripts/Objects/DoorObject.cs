using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // door, button_door, code_door: key true = open. The client never needs to know why.
    public class DoorObject : Blocker
    {
        protected virtual Color ClosedColor => new Color(0.55f, 0.36f, 0.22f);

        protected override bool SolidFor(JToken value) => !WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
            // Thin frame so an open door still reads as a doorway.
            SpriteFactory.Child(transform, "Frame", SpriteFactory.Ring, new Color(1, 1, 1, 0.15f), Layers.Object - 1,
                scale: Bounds.size);
        }

        protected override void UpdateVisual(bool isSolid)
        {
            var c = ClosedColor;
            c.a = isSolid ? 1f : 0.12f;
            SetBodyColor(c);
        }
    }
}
