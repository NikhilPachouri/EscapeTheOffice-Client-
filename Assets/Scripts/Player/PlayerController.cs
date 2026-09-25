using EscapeOffice.Objects;
using UnityEngine;

namespace EscapeOffice
{
    // Top-down Rigidbody2D controller. Movement is never synced: only this client knows where
    // the player is. Also owns interaction focus and the boss debuff.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public float speed = 5f;
        public float interactRange = 0.9f;
        public float debuffSpeedFactor = 0.55f;
        public float debuffRadiusFactor = 0.6f;
        public const float Radius = 0.35f;

        public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;
        public Collider2D Collider { get; private set; }
        public WorldObject Focus { get; private set; }
        public float DebuffRemaining => Mathf.Max(0f, debuffUntil - Time.time);
        public bool Debuffed => DebuffRemaining > 0f;
        public float RadiusFactor => Debuffed ? debuffRadiusFactor : 1f;

        Rigidbody2D rb;
        Vector2 input;
        Vector2 lastSafe;
        float debuffUntil;
        SpriteRenderer bodyRenderer;
        Transform facing;
        // 3D: Player_A / Player_B from the asset pack, turned toward the walking direction.
        Transform model;
        float yaw;

        public static PlayerController Spawn(Vector2 at, string side, Transform parent)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Radius;

            var pc = go.AddComponent<PlayerController>();
            pc.Collider = col;
            pc.bodyRenderer = SpriteFactory.Child(go.transform, "Body", SpriteFactory.Circle, Palette.ForSide(side),
                Layers.Actor, scale: Vector2.one * Radius * 2f);
            pc.facing = SpriteFactory.Child(go.transform, "Facing", SpriteFactory.Circle, Color.white, Layers.Actor + 1,
                new Vector2(0, Radius * 0.6f), Vector2.one * 0.14f).transform;
            go.AddComponent<RoomTracker>();

            var model = Art.Spawn(side == "B" ? "Player_B" : "Player_A", go.transform, Vector3.zero);
            if (model != null)
            {
                pc.model = model.transform;
                pc.bodyRenderer.enabled = false;
                pc.facing.GetComponent<SpriteRenderer>().enabled = false;
            }
            return pc;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            lastSafe = rb.position;
        }

        public void Teleport(Vector2 at)
        {
            rb.position = at;
            transform.position = at;
            rb.linearVelocity = Vector2.zero;
            lastSafe = at;
        }

        public void ApplyDebuff(float seconds) => debuffUntil = Mathf.Max(debuffUntil, Time.time + seconds);

        // A door or laser switched on while we stood in it: back to the side we came from.
        public void EjectFrom(Rect blocker)
        {
            if (!Overlaps(blocker, rb.position)) return;
            rb.position = lastSafe;
            rb.linearVelocity = Vector2.zero;
        }

        static bool Overlaps(Rect r, Vector2 p)
        {
            var closest = new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
            return (closest - p).sqrMagnitude < Radius * Radius;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool canAct = gm.InputEnabled;

            input = canAct ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + UI.TouchControls.Move : Vector2.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (input.sqrMagnitude > 0.01f) facing.localPosition = input.normalized * Radius * 0.6f;

            Focus = FindFocus(gm.World);
            if (canAct && Focus != null && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || UI.TouchControls.InteractPressed))
                Focus.Interact();

            if (model != null)
            {
                if (input.sqrMagnitude > 0.01f) yaw = Mathf.LerpAngle(yaw, Art.YawFor(input), Art.Smooth(14f));
                model.localRotation = Art.Rotation(yaw);
                // Debuffed: a sluggish wobble instead of the sprite tint.
                float wobble = Debuffed ? Mathf.Sin(Time.time * 8f) * 0.06f : 0f;
                model.localScale = new Vector3(1f + wobble, 1f - wobble, 1f + wobble);
            }

            bodyRenderer.color = Debuffed
                ? Color.Lerp(Palette.ForSide(gm.Side), Color.gray, 0.5f + 0.2f * Mathf.Sin(Time.time * 8f))
                : Palette.ForSide(gm.Side);
        }

        WorldObject FindFocus(World world)
        {
            if (world == null) return null;
            WorldObject best = null;
            float bestDist = interactRange;
            foreach (var o in world.Objects.Values)
            {
                if (!o.CanInteractNow) continue;
                float d = o.DistanceTo(Position);
                if (d <= bestDist) { best = o; bestDist = d; }
            }
            return best;
        }

        void FixedUpdate()
        {
            float s = speed * (Debuffed ? debuffSpeedFactor : 1f);
            rb.linearVelocity = input * s;

            // Remember the last spot that was clear of every blocker, open or not.
            var world = GameManager.Instance.World;
            bool clear = true;
            if (world != null)
                foreach (var b in world.Blockers)
                    if (Overlaps(b.Bounds, rb.position)) { clear = false; break; }
            if (clear) lastSafe = rb.position;
        }
    }
}
