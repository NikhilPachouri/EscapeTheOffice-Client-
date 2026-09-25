using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // A wall until its key (e.g. wall_B3.broken) is true. Opened once with your bomb.
    public class BombableWallObject : Blocker
    {
        public override bool Interactable => true;
        public override bool CanInteractNow => IsSolid;
        protected override string DefaultAction => "use_bomb";
        public override string Prompt => "use bomb";

        protected override string ModelName => "BombableWall";

        protected override bool SolidFor(JToken value) => !WorldState.Truthy(value);

        protected override void Build()
        {
            base.Build();
            if (HasModel) return;
            // Crack marks.
            SpriteFactory.Child(transform, "Crack", SpriteFactory.Square, new Color(0.15f, 0.15f, 0.17f), Layers.ObjectTop,
                new Vector2(-0.1f, 0.05f), new Vector2(0.06f, Bounds.height * 0.7f)).transform.localRotation = Quaternion.Euler(0, 0, 25f);
            SpriteFactory.Child(transform, "Crack", SpriteFactory.Square, new Color(0.15f, 0.15f, 0.17f), Layers.ObjectTop,
                new Vector2(0.15f, -0.1f), new Vector2(0.05f, Bounds.height * 0.5f)).transform.localRotation = Quaternion.Euler(0, 0, -30f);
        }

        protected override void UpdateVisual(bool isSolid)
        {
            SetBodyColor(isSolid ? Palette.Wall * 0.85f + new Color(0, 0, 0, 0.15f) : new Color(0.3f, 0.28f, 0.25f, 0.5f));
            foreach (Transform child in transform)
                if (child.name == "Crack") child.gameObject.SetActive(isSolid);
            if (!HasModel) return;
            Art.Find(model, "Intact")?.gameObject.SetActive(isSolid);
            Art.Find(model, "Rubble")?.gameObject.SetActive(!isSolid);
        }

        public override void Interact()
        {
            if (!GameManager.Instance.HasItem("bomb")) GameManager.Instance.Toast("The wall looks weak. Something explosive might help.");
            SendInteract();
        }
    }
}
