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
                var fromData = Def.Get<string>("code");
                if (!string.IsNullOrEmpty(fromData)) return fromData;
                return Value != null && Value.Type != JTokenType.Null && Value.Type != JTokenType.Boolean ? Value.ToString() : "????";
            }
        }

        protected override void Build()
        {
            SetBodyColor(new Color(0.1f, 0.2f, 0.15f));
            body.transform.localScale = Bounds.size * 0.8f;
            SpriteFactory.Child(transform, "Screen", SpriteFactory.Square, new Color(0.3f, 1f, 0.5f, 0.8f), Layers.ObjectTop,
                scale: Bounds.size * 0.55f);
        }

        public override void Interact() => GameManager.Instance.UI.ShowCode(this);
    }
}
