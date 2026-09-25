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
    // With the Other Side pack imported, types that name a ModelName get its 3D model instead
    // (`model`), and the placeholder sprite is hidden.
    public abstract class WorldObject : MonoBehaviour, IStateListener
    {
        [SerializeField] protected SpriteRenderer body;
        protected GameObject model;

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

        // 3D model ------------------------------------------------------------------------
        // Pack prefab for this object, or null to keep the sprite. Multi-tile objects spawn
        // their own models in Build() instead.
        protected virtual string ModelName => null;
        protected virtual float ModelYaw => 0f;
        protected bool HasModel => model != null;
        GameObject ring;

        // Effects play only for changes after the world has loaded, not for the initial state.
        float builtAt;
        protected bool Settled => Time.time - builtAt > 0.4f;
        protected Color GlowColor => Tag == Palette.Tag.None ? Color.white : Palette.ForTag(Tag);
        // World point a little above the object's centre, for bursts.
        protected Vector3 FxPoint(float height = 0.5f) => new Vector3(Bounds.center.x, Bounds.center.y, -height);

        Animator animator;
        float pendingUntil = -1f;
        float glowPhase;
        bool inSight = true;

        // 3D presence: 0 idle, 0.5 player nearby, 1 the player's interaction target.
        float presence;
        float modelScale = 1f;
        float lastPulse = -10f;
        MaterialPropertyBlock ringBlock;
        Color ringTint;
        static readonly int TintId = Shader.PropertyToID("_TintColor");

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
            builtAt = Time.time;

            if (body == null)
                body = SpriteFactory.Child(transform, "Body", SpriteFactory.Square, Color.white, Layers.Object,
                    scale: Bounds.size);
            if (Art.Available && ModelName != null)
            {
                model = Art.Spawn(ModelName, transform, Vector3.zero, ModelYaw);
                if (model != null)
                {
                    body.enabled = false;
                    // Exaggerated silhouettes for small interactables (ArtDirection.json "objectScale").
                    modelScale = ArtDirection.Current.ScaleFor(ModelName);
                    model.transform.localScale = Vector3.one * modelScale;
                }
            }
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
            if (!Art.Available) return;

            // 3D: a restrained light pool on the floor, a focus ring and an icon that appear as
            // the player comes close (see UpdatePresence).
            glow.transform.localPosition = new Vector3(0, 0, -0.01f);
            glow.transform.localScale = Vector3.one * Mathf.Max(Bounds.width, Bounds.height) * ArtDirection.Current.interaction.poolSize;
            var poolMat = Art.Material("FX_Additive");
            if (poolMat != null) glow.sharedMaterial = poolMat;
            string ringName = Tag switch
            {
                Palette.Tag.A => "GlowRing_A",
                Palette.Tag.B => "GlowRing_B",
                Palette.Tag.Both => "GlowRing_Both",
                Palette.Tag.Info => "GlowRing_Info",
                _ => null,
            };
            if (ringName != null)
            {
                ring = Art.Spawn(ringName, transform, new Vector3(0, 0, -0.005f));
                if (ring != null) ring.transform.localScale = Vector3.one * Mathf.Max(Bounds.width, Bounds.height) * 0.8f;
                ringBlock = new MaterialPropertyBlock();
                var ringMat = ring != null ? ring.GetComponentInChildren<Renderer>()?.sharedMaterial : null;
                ringTint = ringMat != null && ringMat.HasProperty(TintId) ? ringMat.GetColor(TintId) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
            }
            if (icon != null)
            {
                icon.transform.localPosition = new Vector3(0, 0, -1.55f);
                icon.transform.localScale = Vector3.one * (0.45f / Mathf.Max(0.01f, iconSprite.bounds.size.x));
                if (Tag != Palette.Tag.None && Art.Sprite(IconName(Tag)) != null) icon.color = Color.white; // pack icons are pre-coloured
            }
        }

        public void Apply(JToken value)
        {
            if (Settled && !JToken.DeepEquals(Value, value)) Pulse();
            Value = value;
            pendingUntil = -1f;
            OnValue(value);
            if (animator != null) animator.SetBool("Active", WorldState.Truthy(value));
            RefreshGlow();
        }

        protected virtual void OnValue(JToken value) { }

        // Glows are hidden in dark rooms so players find interactables with the flashlight, and
        // beyond the vision radius: they are the brightest thing in the scene and would otherwise
        // show through the fog (which is not fully opaque) from across the map.
        public void RefreshGlow()
        {
            if (glow == null) return;
            bool visible = ShowGlow && !World.IsDarkAt(this) && inSight;
            glow.enabled = visible;
            if (icon != null) icon.enabled = visible;
            if (ring != null) ring.SetActive(visible);
        }

        // The side-colour energy pulse through the object (once per change, however it arrives).
        protected void Pulse()
        {
            if (Time.time - lastPulse < 0.8f || !Art.Available) return;
            lastPulse = Time.time;
            Fx3D.Energy(model != null ? model.transform : transform, new Vector3(Bounds.center.x, Bounds.center.y, -0.02f),
                Mathf.Max(Bounds.width, Bounds.height), Tag);
        }

        // Responsive feel: play the cosmetic part now; OnValue(Value) snaps it back if no patch
        // arrives in time.
        protected void Predict(float seconds = 0.6f) => pendingUntil = Time.time + seconds;
        protected bool IsPending => pendingUntil > 0f;

        // Is any part of the object inside the player's vision circle? True when there is no fog.
        bool InSight()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.CameraRig == null || gm.DebugNoFog) return true;
            return DistanceTo(gm.Player.Position) <= gm.CameraRig.VisionRadius;
        }

        protected virtual void Update()
        {
            if (pendingUntil > 0f && Time.time > pendingUntil)
            {
                pendingUntil = -1f;
                OnValue(Value);
                RefreshGlow();
            }

            if (glow != null)
            {
                bool now = InSight();
                if (now != inSight)
                {
                    inSight = now;
                    RefreshGlow();
                }
            }

            if (Art.Available) UpdatePresence();
            else if (glow != null && glow.enabled)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin((Time.time + glowPhase) * 3f);
                var c = glow.color;
                c.a = (IsDim ? 0.25f : 0.8f) * pulse;
                glow.color = c;
            }
        }

        // Idle: a faint pool of its colour. Player nearby: brighter, icon fades in. Target: the
        // focus ring shows and the object swells a little.
        void UpdatePresence()
        {
            var look = ArtDirection.Current.interaction;
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            float target = 0f;
            if (player != null && glow != null && glow.enabled)
                target = player.Focus == this ? 1f : DistanceTo(player.Position) < look.nearDistance ? 0.5f : 0f;
            presence = Mathf.MoveTowards(presence, target, Time.deltaTime * 3f);
            float t = Time.time + glowPhase;

            if (glow != null && glow.enabled)
            {
                float a = presence < 0.5f ? Mathf.Lerp(look.idlePool, look.nearPool, presence * 2f)
                                          : Mathf.Lerp(look.nearPool, look.focusPool, (presence - 0.5f) * 2f);
                var c = glow.color;
                c.a = a * (IsDim ? 0.4f : 1f) * (0.9f + 0.1f * Mathf.Sin(t * 2f));
                glow.color = c;
            }
            if (icon != null && icon.enabled)
            {
                float show = Mathf.SmoothStep(0f, 1f, presence * 2f);
                var c = icon.color; c.a = show; icon.color = c;
                icon.transform.localPosition = new Vector3(0f, 0f, -1.35f - 0.2f * show + Mathf.Sin(t * 2f) * 0.03f);
            }
            if (ring != null && ring.activeSelf)
            {
                float focus = Mathf.Clamp01((presence - 0.5f) * 2f);
                ring.transform.localRotation = Art.Rotation(t * 25f);
                ring.transform.localScale = Vector3.one * Mathf.Max(Bounds.width, Bounds.height) * (0.75f + 0.1f * focus);
                ringBlock.SetColor(TintId, new Color(ringTint.r, ringTint.g, ringTint.b, look.focusRing * focus));
                foreach (var r in ring.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(ringBlock);
            }
            if (model != null)
            {
                float swell = 1f + look.react * Mathf.Clamp01((presence - 0.5f) * 2f) * (0.8f + 0.2f * Mathf.Sin(t * 5f));
                model.transform.localScale = Vector3.one * modelScale * swell;
            }
        }

        static string IconName(Palette.Tag tag) => tag == Palette.Tag.None ? null : "Icon_" + tag;

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
