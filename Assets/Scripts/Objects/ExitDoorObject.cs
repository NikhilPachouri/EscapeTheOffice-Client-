using UnityEngine;

namespace EscapeOffice.Objects
{
    // Opens when exit_open is true. Walking through it sends `enter { portalId }`; the server
    // re-checks exit_open and ends the game for both players.
    public class ExitDoorObject : DoorObject
    {
        protected override Color ClosedColor => Palette.Both;
        protected override Palette.Tag DefaultTag => Palette.Tag.Both;
        // Door_Exit is authored a quarter turn off from the other door models.
        protected override float ModelYaw => base.ModelYaw + 90f;

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

        // The way out: the shared pulse (orange and cyan meeting as purple) plus a wide purple wave.
        protected override void OnDoorMoved(bool opened)
        {
            base.OnDoorMoved(opened);
            if (!opened) return;
            var c = Palette.Both;
            Fx3D.Ring(FxPoint(0.02f), new Color(c.r, c.g, c.b, 0.7f), 0.8f, 6f, 1.2f, delay: 0.35f);
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
