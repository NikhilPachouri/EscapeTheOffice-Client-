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

        protected override string ModelName => Type == "bomb" ? "Pickup_Bomb" : "Pickup_Key";
        Transform item;
        Vector3 itemRest;
        float bobPhase;

        static bool OnFloor(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return true;
            if (value.Type == JTokenType.String) return value.Value<string>() == "home";
            return WorldState.Truthy(value); // boolean keys: true = still there
        }

        protected override void Build()
        {
            if (HasModel)
            {
                item = Art.Find(model, Type == "bomb" ? "Bomb" : "Key");
                if (item != null) itemRest = item.localPosition;
                bobPhase = Random.value * 6f;
            }
            else if (Type == "bomb")
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
                if (r != glow && r != icon && !(HasModel && r == body)) r.enabled = visible;
            if (HasModel) model.SetActive(visible);
            pickupArea.enabled = visible;
        }

        // Pack: bob y 0.55 ± 0.08 at 2.5 rad/s and spin 1.5 rad/s while waiting to be picked up.
        protected override void Update()
        {
            base.Update();
            if (item == null || !model.activeSelf) return;
            float t = Time.time + bobPhase;
            item.localPosition = itemRest + new Vector3(0f, Mathf.Sin(t * 2.5f) * 0.08f, 0f);
            item.localRotation = Quaternion.Euler(0f, t * 1.5f * Mathf.Rad2Deg, 0f);
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
