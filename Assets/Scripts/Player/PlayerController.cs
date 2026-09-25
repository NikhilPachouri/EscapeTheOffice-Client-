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
        public const float Radius = 0.35f;

        public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;
        public Collider2D Collider { get; private set; }
        public WorldObject Focus { get; private set; }
        public float DebuffRemaining => Mathf.Max(0f, debuffUntil - Time.time);
        public bool Debuffed => DebuffRemaining > 0f;

        Rigidbody2D rb;
        Vector2 input;
        Vector2 lastSafe;
        float debuffUntil;
        SpriteRenderer bodyRenderer;
        Transform facing;
        // 3D: Player_A / Player_B from the asset pack, turned toward the walking direction.
        Transform model;
        ArtDirection.PlayerLook look = new ArtDirection.PlayerLook();
        float yaw, walkPhase, lean;
        int lastStep;

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
                pc.look = ArtDirection.Current.Player(side);
                pc.Dress(side);
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

            if (model != null) AnimateModel();

            bodyRenderer.color = Debuffed
                ? Color.Lerp(Palette.ForSide(gm.Side), Color.gray, 0.5f + 0.2f * Mathf.Sin(Time.time * 8f))
                : Palette.ForSide(gm.Side);
        }

        // Side identity beyond colour (ArtDirection.json "players"): proportions, an accessory and
        // a lamp in the side's temperature. Accessories hang off the model so they move with it.
        void Dress(string side)
        {
            var lamp = GetComponentInChildren<Light>();
            if (lamp != null)
            {
                lamp.color = ArtDirection.Hex(look.lamp, lamp.color);
                lamp.intensity = look.lampIntensity;
            }
            var suit = side == "B" ? "M_PlayerB_Suit" : "M_PlayerA_Suit";
            var glow = side == "B" ? "M_Glow_B" : "M_Glow_A";
            switch (look.accessory)
            {
                case "backpack": // A: sturdy, a pack on the back and a little antenna
                    Part(PrimitiveType.Cube, "M_MetalDark", new Vector3(0f, 0.56f, -0.27f), Vector3.zero, new Vector3(0.36f, 0.42f, 0.2f));
                    Part(PrimitiveType.Cube, suit, new Vector3(0f, 0.64f, -0.38f), Vector3.zero, new Vector3(0.28f, 0.08f, 0.03f));
                    Part(PrimitiveType.Cylinder, "M_MetalDark", new Vector3(0.1f, 0.95f, -0.3f), Vector3.zero, new Vector3(0.03f, 0.16f, 0.03f));
                    Part(PrimitiveType.Sphere, glow, new Vector3(0.1f, 1.13f, -0.3f), Vector3.zero, Vector3.one * 0.08f);
                    break;
                case "crest": // B: sleek, a fin along the head and a light strip across the visor
                    Part(PrimitiveType.Sphere, suit, new Vector3(0f, 1.08f, -0.06f), Vector3.zero, new Vector3(0.09f, 0.26f, 0.46f));
                    Part(PrimitiveType.Cube, glow, new Vector3(0f, 0.92f, 0.235f), Vector3.zero, new Vector3(0.3f, 0.035f, 0.02f));
                    break;
            }
            // The floor ring is identity, not a spotlight: smaller than the pack's.
            var ring = Art.Find(model.gameObject, "SideRing");
            if (ring != null) ring.localScale = Vector3.one * 0.75f;
        }

        void Part(PrimitiveType type, string material, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var mat = Art.Material(material);
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetParent(model, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(rot);
            go.transform.localScale = scale;
        }

        // Walk cycle for the pack's static character, with each side's personality: A hops and
        // leans hard, B glides low and sways. Dust at each step.
        void AnimateModel()
        {
            bool moving = rb.linearVelocity.sqrMagnitude > 0.3f;
            if (input.sqrMagnitude > 0.01f) yaw = Mathf.LerpAngle(yaw, Art.YawFor(input), Art.Smooth(14f));

            var world = GameManager.Instance.World;
            float stepRate = look.stepRate * (Debuffed && world != null ? world.Debuff.Speed : 1f);
            if (moving) walkPhase += Time.deltaTime * stepRate;
            else walkPhase = Mathf.Lerp(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, Art.Smooth(12f));
            lean = Mathf.Lerp(lean, moving ? look.lean : 0f, Art.Smooth(8f));

            float idle = moving ? 0f : Mathf.Sin(Time.time * 2.2f) * 0.012f; // breathing
            float hop = Mathf.Abs(Mathf.Sin(walkPhase)) * look.hop + idle;
            model.localPosition = new Vector3(0f, 0f, -hop); // up is -Z
            model.localRotation = Art.Rotation(yaw) * Quaternion.Euler(lean, 0f, Mathf.Sin(walkPhase) * look.sway);

            // Debuffed: a sluggish wobble instead of the sprite tint. Landing squash on each hop.
            float wobble = Debuffed ? Mathf.Sin(Time.time * 8f) * 0.06f : 0f;
            float squash = moving ? (1f - Mathf.Abs(Mathf.Sin(walkPhase))) * look.hop * 0.8f : 0f;
            model.localScale = new Vector3(look.width * (1f + wobble + squash), look.height * (1f - wobble - squash), look.width * (1f + wobble + squash));

            int step = Mathf.FloorToInt(walkPhase / Mathf.PI);
            if (moving && step != lastStep)
                Fx3D.Puff(new Vector3(rb.position.x, rb.position.y, -0.05f), new Color(0.62f, 0.6f, 0.57f, 0.22f),
                    count: 1, radius: 0.06f, size: 0.22f, life: 0.45f, rise: 0.12f);
            lastStep = step;
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
            var world = GameManager.Instance.World;
            float s = speed * (Debuffed && world != null ? world.Debuff.Speed : 1f);
            rb.linearVelocity = input * s;

            // Remember the last spot that was clear of every blocker, open or not.
            bool clear = true;
            if (world != null)
                foreach (var b in world.Blockers)
                    if (Overlaps(b.Bounds, rb.position)) { clear = false; break; }
            if (clear) lastSafe = rb.position;
        }
    }
}
