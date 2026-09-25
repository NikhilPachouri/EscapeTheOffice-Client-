using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Shows a generated 4-digit code that the other player needs. The code is either in the
    // object's data (`code`) or in the state key it reads. Reading it is purely client-side.
    public class CodePanelObject : WorldObject
    {
        public override bool Interactable => true;
        protected override Palette.Tag DefaultTag => Palette.Tag.Info;
        public override string Prompt => "read";

        public string Code
        {
            get
            {
                // The live value of the key we read (state["code_A1"] = "4821"); `code` in the
                // object data names a state key, so it is looked up too, never shown as-is.
                var state = GameManager.Instance.State;
                var v = Value ?? state.Get(Def.Get<string>("code"));
                return v != null && v.Type != JTokenType.Null && v.Type != JTokenType.Boolean ? v.ToString() : "????";
            }
        }

        protected override string ModelName => "CodePanel";
        protected override float ModelYaw => Art.WallYaw(World, Def.X, Def.Y);
        TextMesh text;

        protected override void Build()
        {
            if (HasModel)
            {
                BuildScreenText();
                return;
            }
            SetBodyColor(new Color(0.1f, 0.2f, 0.15f));
            body.transform.localScale = Bounds.size * 0.8f;
            SpriteFactory.Child(transform, "Screen", SpriteFactory.Square, new Color(0.3f, 1f, 0.5f, 0.8f), Layers.ObjectTop,
                scale: Bounds.size * 0.55f);
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
            text.color = Palette.Info;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        protected override void Update()
        {
            base.Update();
            if (text != null && text.text != Code) text.text = Code;
        }

        public override void Interact() => GameManager.Instance.UI.ShowCode(this);
    }
}
