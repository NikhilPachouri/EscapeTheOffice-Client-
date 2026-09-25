using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Anything that behaves like a wall on its tiles while its key says so:
    // doors, lasers, fire, bombable walls.
    public abstract class Blocker : WorldObject
    {
        protected BoxCollider2D solid;
        public bool IsSolid { get; private set; }
        bool initialised;

        protected abstract bool SolidFor(JToken value);
        protected abstract void UpdateVisual(bool isSolid);

        protected override void Build()
        {
            solid = gameObject.AddComponent<BoxCollider2D>();
            solid.size = Bounds.size;
        }

        protected override void OnValue(JToken value)
        {
            bool nowSolid = SolidFor(value);
            bool closedOnPlayer = initialised && nowSolid && !IsSolid;
            IsSolid = nowSolid;
            initialised = true;
            solid.enabled = nowSolid;
            UpdateVisual(nowSolid);

            // Toggling shut on the player: move them back to the side they came from.
            var player = GameManager.Instance.Player;
            if (closedOnPlayer && player != null) player.EjectFrom(Bounds);
        }
    }
}
