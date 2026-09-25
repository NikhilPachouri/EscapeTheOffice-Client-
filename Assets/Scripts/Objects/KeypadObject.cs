using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Code door keypad. The code lives on the other side's panel; we only send what the player
    // typed and the server compares. Its key is the door it opens, so it lights green when solved.
    public class KeypadObject : WorldObject
    {
        SpriteRenderer led;

        public override bool Interactable => true;
        public override bool CanInteractNow => !WorldState.Truthy(Value);
        protected override string DefaultAction => "submit";
        public override string Prompt => "enter code";
        public int CodeLength => Def.Get("length", 4);

        protected override void Build()
        {
            SetBodyColor(new Color(0.25f, 0.27f, 0.3f));
            body.transform.localScale = Bounds.size * 0.6f;
            led = SpriteFactory.Child(transform, "Led", SpriteFactory.Circle, Color.red, Layers.ObjectTop,
                new Vector2(0, 0.18f), Vector2.one * 0.15f);
        }

        protected override void OnValue(JToken value) =>
            led.color = WorldState.Truthy(value) ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.2f);

        public override void Interact() => GameManager.Instance.UI.OpenKeypad(this);

        public void Submit(string code)
        {
            GameManager.Instance.PlayLocal("click", transform.position);
            SendInteract(code);
        }
    }
}
