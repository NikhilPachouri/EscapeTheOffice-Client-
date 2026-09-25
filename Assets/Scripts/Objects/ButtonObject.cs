using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // button, lever, light/laser switches, final_button / latch_button. Shows its key's value; the lever
    // flips immediately on interact and snaps back if the server does not agree.
    public class ButtonObject : WorldObject
    {
        SpriteRenderer knob;
        bool latching;

        public override bool Interactable => true;
        // The final button latches: once pressed it stays pressed.
        public override bool CanInteractNow => !(latching && WorldState.Truthy(Value));
        protected override string DefaultAction => "press";

        protected override void Build()
        {
            latching = Type == "final_button" || Type == "latch_button" || Def.Get("latching", false);
            SetBodyColor(new Color(0.3f, 0.3f, 0.34f));
            body.transform.localScale = Bounds.size * 0.7f;
            knob = SpriteFactory.Child(transform, "Knob", SpriteFactory.Circle, Color.gray, Layers.ObjectTop,
                scale: Vector2.one * 0.4f);
        }

        protected override void OnValue(JToken value) => Show(WorldState.Truthy(value));

        void Show(bool on)
        {
            var tint = Tag == Palette.Tag.None ? Color.white : Palette.ForTag(Tag);
            knob.color = on ? tint : tint * 0.35f + new Color(0, 0, 0, 0.65f);
            knob.transform.localPosition = new Vector2(0, on ? -0.08f : 0.08f);
        }

        public override void Interact()
        {
            GameManager.Instance.PlayLocal("click", transform.position);
            Show(!WorldState.Truthy(Value));
            Predict();
            SendInteract();
        }
    }
}
