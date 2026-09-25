using System.Collections.Generic;
using EscapeOffice.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // Base for everything in a side's `objects` list. Each object reads exactly one state key
    // and redraws itself in Apply; it never decides game logic, it only reports interactions.
    //
    // Prefab authors: assign `body` to your own art and it replaces the placeholder square.
    // If the prefab has an Animator, its "Active" bool follows the key's truthiness.
    public abstract class WorldObject : MonoBehaviour, IStateListener
    {
        [SerializeField] protected SpriteRenderer body;

        public ObjectDef Def { get; private set; }
        public World World { get; private set; }
        public JToken Value { get; private set; }
        public Rect Bounds { get; private set; }
        public Palette.Tag Tag { get; protected set; }
        public readonly List<Room> Rooms = new List<Room>();

        public string Id => Def.Id;
        public string Type => Def.Type;

        // Interaction ------------------------------------------------------------------
        public virtual bool Interactable => false;
        // Objects that can be interacted with right now (e.g. not a pressed latching button).
        public virtual bool CanInteractNow => Interactable;
        protected virtual string DefaultAction => "press";
        public string Action => string.IsNullOrEmpty(Def.Interact) ? DefaultAction : Def.Interact;
        public virtual string Prompt => Action.Replace('_', ' ');

        public virtual void Interact() => SendInteract();

        protected void SendInteract(string value = null) =>
            GameManager.Instance.SendInteract(Id, Action, value);

        // Glow -------------------------------------------------------------------------
        protected SpriteRenderer glow, icon;
        protected virtual bool ShowGlow => CanInteractNow;
        protected virtual bool IsDim => Def.Dim;
        protected virtual Palette.Tag DefaultTag => Palette.Tag.None;

        Animator animator;
        float pendingUntil = -1f;
        float glowPhase;

        public void Init(World world, ObjectDef def)
        {
            World = world;
            Def = def;
            Bounds = world.TileRect(def.X, def.Y, def.W, def.H);
            name = $"{def.Type}:{def.Id}";
            transform.position = Bounds.center;
            Tag = Palette.ParseTag(def.Color);
            if (Tag == Palette.Tag.None) Tag = DefaultTag;
            animator = GetComponentInChildren<Animator>();
            glowPhase = Random.value * 10f;

            if (body == null)
                body = SpriteFactory.Child(transform, "Body", SpriteFactory.Square, Color.white, Layers.Object,
                    scale: Bounds.size);
            Build();
            if (Interactable) BuildGlow();
            OnValue(null); // defaults until state arrives
            RefreshGlow();
        }

        protected virtual void Build() { }

        void BuildGlow()
        {
            var color = Palette.ForTag(Tag);
            float size = Mathf.Max(Bounds.width, Bounds.height) * 2.2f;
            glow = SpriteFactory.Child(transform, "Glow", SpriteFactory.Glow, color, Layers.Glow,
                scale: Vector2.one * size);
            var iconSprite = Palette.IconFor(Tag);
            if (iconSprite != null)
            {
                icon = SpriteFactory.Child(transform, "Icon", iconSprite, color, Layers.ObjectTop,
                    new Vector2(Bounds.width * 0.5f, Bounds.height * 0.5f), Vector2.one * 0.35f);
            }
        }

        public void Apply(JToken value)
        {
            Value = value;
            pendingUntil = -1f;
            OnValue(value);
            if (animator != null) animator.SetBool("Active", WorldState.Truthy(value));
            RefreshGlow();
        }

        protected virtual void OnValue(JToken value) { }

        // Glows are hidden in dark rooms so players find interactables with the flashlight.
        public void RefreshGlow()
        {
            if (glow == null) return;
            bool visible = ShowGlow && !World.IsDarkAt(this);
            glow.enabled = visible;
            if (icon != null) icon.enabled = visible;
        }

        // Responsive feel: play the cosmetic part now; OnValue(Value) snaps it back if no patch
        // arrives in time.
        protected void Predict(float seconds = 0.6f) => pendingUntil = Time.time + seconds;
        protected bool IsPending => pendingUntil > 0f;

        protected virtual void Update()
        {
            if (pendingUntil > 0f && Time.time > pendingUntil)
            {
                pendingUntil = -1f;
                OnValue(Value);
                RefreshGlow();
            }

            if (glow != null && glow.enabled)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin((Time.time + glowPhase) * 3f);
                var c = glow.color;
                c.a = (IsDim ? 0.25f : 0.8f) * pulse;
                glow.color = c;
            }
        }

        public float DistanceTo(Vector2 p)
        {
            var closest = new Vector2(Mathf.Clamp(p.x, Bounds.xMin, Bounds.xMax), Mathf.Clamp(p.y, Bounds.yMin, Bounds.yMax));
            return Vector2.Distance(p, closest);
        }

        protected void SetBodyColor(Color c)
        {
            if (body != null) body.color = c;
        }
    }
}
