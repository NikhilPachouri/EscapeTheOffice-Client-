using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Wall plaque: one digit (state[key]) on a plate in its `color` (red/green/blue/yellow).
    // Not interactable; self-lit so it stays readable in a dark room. Older levels put a whole
    // code under the key, which shows too.
    public class CodePanelObject : WorldObject
    {
        Color Plate => Palette.ForName(Def.Color);

        public string Code
        {
            get
            {
                // The live value of the key we read (state["code_A1"] = "4821"); `code` in the
                // object data names a state key, so it is looked up too, never shown as-is.
                var v = Value ?? GameManager.Instance.State.Get(Def.Get<string>("code"));
                if (v != null && v.Type != JTokenType.Null && v.Type != JTokenType.Boolean) return v.ToString();
                return Def.Get<string>("digit") ?? "?"; // authored offline fallback
            }
        }

        protected override string ModelName => "CodePanel";
        protected override float ModelYaw => Art.WallYaw(World, Def.X, Def.Y);
        TextMesh text;

        protected override void Build()
        {
            World.Lights.Add(new Vector3(Bounds.center.x, Bounds.center.y, 1.2f)); // self-lit
            if (HasModel)
            {
                BuildScreenText();
                return;
            }
            SetBodyColor(Plate);
            body.transform.localScale = Bounds.size * 0.85f;
            var go = new GameObject("CodeText");
            go.transform.SetParent(transform, false);
            text = go.AddComponent<TextMesh>();
            text.font = Art.Catalog != null && Art.Catalog.codeFont != null ? Art.Catalog.codeFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.characterSize = 0.1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Plate.grayscale > 0.6f ? Color.black : Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = text.font.material;
            mr.sortingOrder = Layers.ObjectTop;
        }

        // Pack: text on CodeText_Anchor in Chakra Petch Bold, #8ef0b0.
        void BuildScreenText()
        {
            var anchor = Art.Find(model, "CodeText_Anchor");
            var font = Art.Catalog.codeFont;
            if (anchor == null || font == null) return;
            var go = new GameObject("CodeText");
            go.transform.SetParent(anchor, false);
            // TextMesh reads toward local -Z; turn it so it faces out of the screen (+Z).
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.02f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Plate; // digit glows in the plate colour
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        protected override void Update()
        {
            base.Update();
            if (text != null && text.text != Code) text.text = Code;
        }
    }
}
