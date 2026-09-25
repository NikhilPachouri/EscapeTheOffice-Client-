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
        // Colour sequence to read the partner's panels in (server-derived); never the digits.
        public string[] Order => Def.Get<string[]>("order");
        public int CodeLength => Order?.Length ?? Def.Get("length", 4);

        protected override string ModelName => "Keypad";
        protected override float ModelYaw => Art.WallYaw(World, Def.X, Def.Y);
        Renderer screen;

        protected override void Build()
        {
            if (HasModel)
            {
                screen = Art.Find(model, "Screen")?.GetComponent<Renderer>();
                return;
            }
            SetBodyColor(new Color(0.25f, 0.27f, 0.3f));
            body.transform.localScale = Bounds.size * 0.6f;
            led = SpriteFactory.Child(transform, "Led", SpriteFactory.Circle, Color.red, Layers.ObjectTop,
                new Vector2(0, 0.18f), Vector2.one * 0.15f);
        }

        protected override void OnValue(JToken value)
        {
            bool solved = WorldState.Truthy(value);
            if (led != null) led.color = solved ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.2f);
            var mat = Art.Material(solved ? "M_Keypad_Unlocked" : "M_Keypad_Locked");
            if (screen != null && mat != null) screen.sharedMaterial = mat;
        }

        public override void Interact() => GameManager.Instance.UI.OpenKeypad(this);

        public void Submit(string code)
        {
            GameManager.Instance.PlayLocal("click", transform.position);
            SendInteract(code);
        }
    }
}
