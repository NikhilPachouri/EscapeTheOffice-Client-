using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // door, button_door, code_door: key true = open. The client never needs to know why.
    public class DoorObject : Blocker
    {
        const float LeafClosed = 0.25f, LeafOpen = 0.71f;

        protected virtual Color ClosedColor => new Color(0.55f, 0.36f, 0.22f);

        protected override string ModelName => Type switch
        {
            "code_door" => "Door_Code",
            "key_door" => "Door_Key",
            "exit_door" or "exit" => "Door_Exit",
            _ => "Door_Button",
        };
        protected override float ModelYaw => Art.SpanYaw(World, Def.X, Def.Y, Def.W, Def.H);

        Transform leafL, leafR;
        Renderer lamp;
        float leafTarget = LeafClosed;

        protected override bool SolidFor(JToken value) => !WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
            if (HasModel)
            {
                leafL = Art.Find(model, "LeafL");
                leafR = Art.Find(model, "LeafR");
                lamp = Art.Find(model, "Lamp")?.GetComponent<Renderer>();
                return;
            }
            // Thin frame so an open door still reads as a doorway.
            SpriteFactory.Child(transform, "Frame", SpriteFactory.Ring, new Color(1, 1, 1, 0.15f), Layers.Object - 1,
                scale: Bounds.size);
        }

        protected override void UpdateVisual(bool isSolid)
        {
            var c = ClosedColor;
            c.a = isSolid ? 1f : 0.12f;
            SetBodyColor(c);

            leafTarget = isSolid ? LeafClosed : LeafOpen;
            var lampMat = Art.Material(isSolid ? "M_Lamp_Red" : "M_Lamp_Green");
            if (lamp != null && lampMat != null) lamp.sharedMaterial = lampMat;
        }

        protected override void Update()
        {
            base.Update();
            if (leafL == null || leafR == null) return;
            float k = Art.Smooth(5f);
            leafL.localPosition = new Vector3(Mathf.Lerp(leafL.localPosition.x, -leafTarget, k), leafL.localPosition.y, leafL.localPosition.z);
            leafR.localPosition = new Vector3(Mathf.Lerp(leafR.localPosition.x, leafTarget, k), leafR.localPosition.y, leafR.localPosition.z);
        }
    }
}
