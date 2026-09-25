using UnityEngine;

namespace EscapeOffice.Objects
{
    // Opens when exit_open is true. Walking through it sends `enter { portalId }`; the server
    // re-checks exit_open and ends the game for both players.
    public class ExitDoorObject : DoorObject
    {
        protected override Color ClosedColor => Palette.Both;
        protected override Palette.Tag DefaultTag => Palette.Tag.Both;

        BoxCollider2D portal;
        float lastSent = -10f;

        protected override void Build()
        {
            base.Build();
            portal = gameObject.AddComponent<BoxCollider2D>();
            portal.isTrigger = true;
            portal.size = Bounds.size * 0.8f;
        }

        protected override void UpdateVisual(bool isSolid)
        {
            base.UpdateVisual(isSolid);
            if (portal != null) portal.enabled = !isSolid;
            if (!isSolid) SetBodyColor(new Color(Palette.Both.r, Palette.Both.g, Palette.Both.b, 0.45f));
        }

        void OnTriggerEnter2D(Collider2D other) => TryEnter(other);
        void OnTriggerStay2D(Collider2D other) => TryEnter(other);

        void TryEnter(Collider2D other)
        {
            if (IsSolid || Time.time - lastSent < 1f) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            lastSent = Time.time;
            GameManager.Instance.SendEnter(Id);
        }
    }
}
