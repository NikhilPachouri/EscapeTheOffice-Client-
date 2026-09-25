using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // bomb or key. The server tracks home / held / used; the item is on the floor only while home.
    public class ItemObject : WorldObject
    {
        public override bool Interactable => true;
        public override bool CanInteractNow => OnFloor(Value) && !IsPending;
        protected override string DefaultAction => "pickup";
        public override string Prompt => $"pick up {Type}";

        Collider2D pickupArea;

        static bool OnFloor(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return true;
            if (value.Type == JTokenType.String) return value.Value<string>() == "home";
            return WorldState.Truthy(value); // boolean keys: true = still there
        }

        protected override void Build()
        {
            if (Type == "bomb")
            {
                body.sprite = SpriteFactory.Circle;
                SetBodyColor(new Color(0.12f, 0.12f, 0.14f));
                body.transform.localScale = Vector2.one * 0.55f;
                SpriteFactory.Child(transform, "Fuse", SpriteFactory.Square, new Color(1f, 0.6f, 0.2f), Layers.ObjectTop,
                    new Vector2(0.12f, 0.3f), new Vector2(0.06f, 0.18f));
            }
            else
            {
                body.sprite = SpriteFactory.Diamond;
                SetBodyColor(new Color(1f, 0.85f, 0.2f));
                body.transform.localScale = new Vector2(0.3f, 0.55f);
            }
            var area = gameObject.AddComponent<CircleCollider2D>();
            area.isTrigger = true;
            area.radius = 0.4f;
            pickupArea = area;
        }

        protected override void OnValue(JToken value) => Show(OnFloor(value));

        void Show(bool visible)
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                if (r != glow && r != icon) r.enabled = visible;
            pickupArea.enabled = visible;
        }

        public override void Interact()
        {
            GameManager.Instance.PlayLocal("pickup", transform.position);
            Show(false);
            Predict(1f);
            RefreshGlow();
            SendInteract();
        }
    }
}
