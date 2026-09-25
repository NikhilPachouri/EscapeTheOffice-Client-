using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // button, lever, light/laser switches, final_button / latch_button. Shows its key's value; the lever
    // flips immediately on interact and snaps back if the server does not agree.
    public class ButtonObject : WorldObject
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");

        SpriteRenderer knob;
        bool latching;

        // 3D parts, whichever the model has: a Cap that presses, a Lever that flips, a Wheel that turns.
        Transform cap, lever, wheel;
        Renderer capRenderer;
        Material capNeutral;
        float capRestY, capTargetY, leverTarget, wheelTarget, wheelAngle;

        public override bool Interactable => true;
        // The final button latches: once pressed it stays pressed.
        public override bool CanInteractNow => !(latching && WorldState.Truthy(Value));
        protected override string DefaultAction => "press";

        // tos-interactables.js: one prop per kind. Switches are told apart by what they do.
        protected override string ModelName => Type switch
        {
            "final_button" or "latch_button" => "TOS_FinalButton",
            "light_switch" => "TOS_LightSwitch",
            "valve" or "drain" => "TOS_DrainValve",
            "lever" or "switch" or "laser_switch" => Mentions("sprinkler") ? "TOS_SprinklerValve" : Mentions("breaker") ? "TOS_Breaker" : "TOS_LaserLever",
            _ => "TOS_DoorButton",
        };
        protected override string FallbackModelName => Type switch
        {
            "final_button" or "latch_button" => "FinalButton",
            "lever" or "switch" or "laser_switch" => "LeverSwitch",
            "light_switch" => "LightSwitch",
            "valve" or "drain" => "Valve",
            _ => "Button",
        };
        public override string Symbol => ModelName switch
        {
            "TOS_FinalButton" => "star",
            "TOS_LightSwitch" => "bulb",
            "TOS_DrainValve" => "wavesDown",
            "TOS_SprinklerValve" => "flameDrop",
            "TOS_Breaker" => "bolt",
            "TOS_LaserLever" => "beam",
            _ => "door",
        };
        bool Mentions(string word) =>
            (Def.Id ?? "").ToLowerInvariant().Contains(word) || (Def.Get<string>("label") ?? "").ToLowerInvariant().Contains(word);
        // Final buttons are the shared goal: purple (both) unless the server colours them.
        protected override Palette.Tag DefaultTag => Type is "final_button" or "latch_button" ? Palette.Tag.Both : Palette.Tag.None;
        // Wall props back onto the nearest wall; floor props (and free-standing posts) face the camera.
        bool WallMounted => ModelName is "TOS_DoorButton" or "TOS_LightSwitch" or "TOS_Breaker";
        protected override float ModelYaw => WallMounted ? Art.WallYaw(World, Def.X, Def.Y, 180f) : 180f;

        protected override void Build()
        {
            latching = Type == "final_button" || Type == "latch_button" || Def.Get("latching", false) || Def.Get("latch", false);
            if (HasModel)
            {
                cap = Art.Find(model, "Cap");
                lever = Art.Find(model, "Lever");
                wheel = Art.Find(model, "Wheel");
                if (cap != null)
                {
                    capRestY = capTargetY = cap.localPosition.y;
                    capRenderer = cap.GetComponent<Renderer>();
                    capNeutral = capRenderer != null ? capRenderer.sharedMaterial : null;
                }
                return;
            }
            SetBodyColor(new Color(0.3f, 0.3f, 0.34f));
            body.transform.localScale = Bounds.size * 0.7f;
            knob = SpriteFactory.Child(transform, "Knob", SpriteFactory.Circle, Color.gray, Layers.ObjectTop,
                scale: Vector2.one * 0.4f);
        }

        protected override void OnValue(JToken value) => Show(WorldState.Truthy(value));

        void Show(bool on)
        {
            if (Tos != null) Tos.SetState(on, instant: !Settled);
            var tint = Tag == Palette.Tag.None ? Color.white : Palette.ForTag(Tag);
            if (knob != null)
            {
                knob.color = on ? tint : tint * 0.35f + new Color(0, 0, 0, 0.65f);
                knob.transform.localPosition = new Vector2(0, on ? -0.08f : 0.08f);
            }

            // Pack: Cap y -0.08 when pressed (switch to M_ButtonCap_Pressed), tinted with the glow colour.
            capTargetY = on ? capRestY - 0.08f : capRestY;
            if (capRenderer != null)
            {
                var pressed = Art.Material("M_ButtonCap_Pressed");
                capRenderer.sharedMaterial = on && pressed != null ? pressed : capNeutral;
                var block = new MaterialPropertyBlock();
                if (!on && Tag != Palette.Tag.None) block.SetColor(ColorId, tint);
                capRenderer.SetPropertyBlock(block);
            }
            // Lever: -0.6 rad off → +0.6 rad on. Valve wheel: about one full turn when on.
            leverTarget = (on ? 0.6f : -0.6f) * Mathf.Rad2Deg;
            wheelTarget = on ? 6f * Mathf.Rad2Deg : 0f;
        }

        protected override void Update()
        {
            base.Update();
            if (cap != null)
            {
                var p = cap.localPosition;
                cap.localPosition = new Vector3(p.x, Mathf.Lerp(p.y, capTargetY, Art.Smooth(14f)), p.z);
            }
            if (lever != null)
            {
                float x = Mathf.LerpAngle(lever.localEulerAngles.x, leverTarget, Art.Smooth(12f));
                lever.localRotation = Quaternion.Euler(x, 0f, 0f);
            }
            if (wheel != null)
            {
                wheelAngle = Mathf.MoveTowards(wheelAngle, wheelTarget, 4f * Mathf.Rad2Deg * Time.deltaTime);
                wheel.localRotation = Quaternion.Euler(0f, 0f, wheelAngle);
            }
        }

        public override void Interact()
        {
            GameManager.Instance.PlayLocal("click", transform.position);
            // Energy in the object's side colour, now (the patch's own pulse is skipped as a repeat).
            Pulse();
            if (lever != null)
            {
                // A short streak in the direction of the throw.
                bool turningOn = !WorldState.Truthy(Value);
                var up = model.transform.TransformDirection(Vector3.up) * 0.45f;
                var fwd = model.transform.TransformDirection(Vector3.forward) * 0.45f * (turningOn ? 1f : -1f);
                var from = lever.position + up - fwd;
                Fx3D.Streak(from, lever.position + up * 0.6f + fwd, GlowColor);
            }
            if (wheel != null)
                Fx3D.Ring(wheel.position, new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.6f), 0.3f, 1.1f, 0.4f, delay: 0.12f);
            Show(!WorldState.Truthy(Value));
            Predict();
            SendInteract();
        }
    }
}
