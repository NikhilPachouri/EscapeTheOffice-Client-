using UnityEngine;

namespace EscapeOffice.UI
{
    // The game's controls (Android first): a floating joystick wherever the left thumb lands on
    // the left half of the screen, a context button bottom-right that takes the glow colour and
    // prompt of whatever the player can use, and tap-to-use on glowing objects in the world.
    // On PC/editor the mouse acts as a finger; WASD/E remain as a silent developer fallback.
    public class TouchControls : MonoBehaviour
    {
        public static Vector2 Move { get; private set; }
        public static bool InteractPressed { get; private set; }
        public static bool Visible { get; private set; }

        const float RadiusFraction = 0.12f;  // joystick radius as a fraction of screen height
        const float ButtonFraction = 0.10f;  // use button radius

        static readonly Color Base = new Color32(0x14, 0x18, 0x1d, 0x99); // pack grout colour
        static readonly Color Ink = new Color32(0xe8, 0xee, 0xf2, 0xff);

        bool enabledHere;
        int stickFinger = -1;
        Vector2 stickOrigin, stickPos;
        float pressFlash;

        static Texture2D disc, ring, knob, shadow;
        GUIStyle label;

        // Screen pixels, bottom-left origin. Public so the tutorial can point at the controls.
        public float StickRadius => Screen.height * RadiusFraction;
        public float ButtonRadius => Screen.height * ButtonFraction;
        public Vector2 StickHome => new Vector2(StickRadius * 1.7f, StickRadius * 1.7f);
        public Vector2 ButtonCenter => new Vector2(Screen.width - ButtonRadius * 1.9f, ButtonRadius * 1.9f);
        Vector2 TalkCenter => ButtonCenter + new Vector2(0f, ButtonRadius * 2.5f);
        bool OnTalk(Vector2 p) => Vector2.Distance(p, TalkCenter) < ButtonRadius * 0.9f * 1.3f;

        void Awake()
        {
            if (disc != null) return;
            disc = Radial(256, d => Mathf.Clamp01((1f - d) * 60f));
            ring = Radial(256, d => Mathf.Clamp01((1f - d) * 60f) * Mathf.Clamp01((d - 0.9f) * 60f));
            shadow = Radial(128, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * 0.6f);
            // Knob: light top-left, darker rim, for a slight dome.
            knob = Make(256, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Clamp01((1f - d) * 60f);
                float light = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(Mathf.Sqrt((x + 0.35f) * (x + 0.35f) + (y - 0.35f) * (y - 0.35f)) * 0.8f));
                float rim = d > 0.86f ? 0.8f : 1f;
                return new Color(light * rim, light * rim, light * rim, a);
            });
        }

        // On-screen IMGUI buttons (HUD, tutorial). A touch that starts on one belongs to it: it
        // never becomes the joystick, a USE press or a tap on the world. Buttons register each
        // repaint; Update reads what the previous frame drew.
        static readonly System.Collections.Generic.List<Rect> blocked = new System.Collections.Generic.List<Rect>();
        static readonly System.Collections.Generic.List<Rect> blockedNext = new System.Collections.Generic.List<Rect>();

        // Call from OnGUI with the rect as passed to GUI.Button (any GUI.matrix scale).
        public static void Block(Rect guiRect)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Vector2 a = GUI.matrix.MultiplyPoint3x4(guiRect.min), b = GUI.matrix.MultiplyPoint3x4(guiRect.max);
            blockedNext.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y)); // screen pixels, top-left origin
        }

        static bool IsBlocked(Vector2 touch)
        {
            var p = new Vector2(touch.x, Screen.height - touch.y);
            foreach (var r in blocked) if (r.Contains(p)) return true;
            return false;
        }

        void Update()
        {
            blocked.Clear();
            blocked.AddRange(blockedNext);
            blockedNext.Clear();

            Move = Vector2.zero;
            InteractPressed = false;
            pressFlash = Mathf.Max(0f, pressFlash - Time.deltaTime * 4f);
            enabledHere = true;
            Visible = true;
            if (!enabledHere) return;

            var gm = GameManager.Instance;
            Net.VoiceChat.TalkHeld = false;
            if (gm == null || !gm.InputEnabled) { stickFinger = -1; return; }
            bool voice = gm.Voice != null && gm.Voice.Active;

            for (int i = 0; i < Input.touchCount + (MouseTouch(out _) ? 1 : 0); i++)
            {
                var t = i < Input.touchCount ? Input.GetTouch(i) : Mouse();
                // Hold to talk: any finger resting on the TALK button.
                if (voice && t.fingerId != stickFinger && OnTalk(t.position) && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                {
                    Net.VoiceChat.TalkHeld = true;
                    continue;
                }
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (IsBlocked(t.position)) break; // a HUD button's; IMGUI handles the press
                        if (Vector2.Distance(t.position, ButtonCenter) < ButtonRadius * 1.3f)
                        {
                            InteractPressed = true;
                            pressFlash = 1f;
                        }
                        else if (TapObject(gm, t.position)) { }
                        else if (stickFinger < 0 && t.position.x < Screen.width * 0.5f)
                        {
                            stickFinger = t.fingerId;
                            stickOrigin = stickPos = t.position;
                        }
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (t.fingerId == stickFinger) stickPos = t.position;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (t.fingerId == stickFinger) stickFinger = -1;
                        break;
                }
            }

            if (stickFinger >= 0)
            {
                var d = (stickPos - stickOrigin) / StickRadius;
                Move = d.sqrMagnitude > 1f ? d.normalized : (d.magnitude < 0.15f ? Vector2.zero : d);
            }
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (!enabledHere || gm == null || gm.Current != GameManager.Phase.Playing || gm.UI.IsModal) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                if (Art.Catalog != null && Art.Catalog.codeFont != null) label.font = Art.Catalog.codeFont;
            }

            var side = Palette.ForSide(gm.Side);
            DrawStick(side);
            DrawButton(gm, side);
            if (gm.Voice != null && gm.Voice.Active) DrawTalk(gm, side);
        }

        void DrawStick(Color side)
        {
            bool held = stickFinger >= 0;
            var center = held ? stickOrigin : StickHome;
            float r = StickRadius;
            float alpha = held ? 1f : 0.55f;

            Draw(shadow, center + new Vector2(0, -r * 0.06f), r * 1.25f, new Color(0, 0, 0, 0.5f * alpha));
            Draw(disc, center, r, new Color(Base.r, Base.g, Base.b, Base.a * alpha));
            Draw(ring, center, r, new Color(side.r, side.g, side.b, 0.85f * alpha));
            Draw(ring, center, r * 0.55f, new Color(1, 1, 1, 0.12f * alpha));

            // Direction chevrons; the one nearest the drag lights up.
            var dir = held ? (stickPos - stickOrigin) : Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f; // 0 = up, clockwise
                var v = Quaternion.Euler(0, 0, -ang) * Vector2.up;
                float lit = dir.sqrMagnitude > 1f ? Mathf.Clamp01(Vector2.Dot(dir.normalized, v)) : 0f;
                var pos = center + (Vector2)v * r * 0.78f;
                var c = Color.Lerp(new Color(1, 1, 1, 0.35f * alpha), side, lit);
                Chevron(pos, r * 0.13f, ang, c);
            }

            var knobPos = center + (held ? Vector2.ClampMagnitude(stickPos - stickOrigin, r * 0.62f) : Vector2.zero);
            float kr = r * 0.42f;
            Draw(shadow, knobPos + new Vector2(0, -kr * 0.18f), kr * 1.4f, new Color(0, 0, 0, 0.6f * alpha));
            Draw(knob, knobPos, kr, new Color(side.r, side.g, side.b, alpha));
            Draw(ring, knobPos, kr, new Color(1, 1, 1, 0.35f * alpha));
        }

        void DrawTalk(GameManager gm, Color side)
        {
            var v = gm.Voice;
            bool live = v.Talking;
            var c = TalkCenter;
            float r = ButtonRadius * 0.9f * (live ? 1.08f : 1f);
            var accent = v.Connected ? side : new Color(1, 1, 1, 0.3f);
            if (live) Draw(SpriteFactory.Glow.texture, c, r * 1.9f, new Color(side.r, side.g, side.b, 0.55f));
            Draw(shadow, c + new Vector2(0, -r * 0.08f), r * 1.25f, new Color(0, 0, 0, 0.5f));
            Draw(disc, c, r, new Color(Base.r, Base.g, Base.b, live ? 0.9f : 0.6f));
            if (live) Draw(disc, c, r, new Color(side.r, side.g, side.b, 0.35f));
            Draw(ring, c, r, new Color(accent.r, accent.g, accent.b, live ? 1f : 0.6f));
            label.fontSize = Mathf.RoundToInt(r * 0.3f);
            label.normal.textColor = v.Connected ? Ink : new Color(1, 1, 1, 0.4f);
            GUI.Label(ScreenRect(c, r * 0.85f), live ? "LIVE" : v.Connected ? "TALK" : "…", label);
        }

        void DrawButton(GameManager gm, Color side)
        {
            var focus = gm.Player != null ? gm.Player.Focus : null;
            bool ready = focus != null;
            var accent = ready ? (focus.Tag == Palette.Tag.None ? Ink : Palette.ForTag(focus.Tag)) : new Color(1, 1, 1, 0.3f);
            var c = ButtonCenter;
            float r = ButtonRadius * (1f - 0.08f * pressFlash);
            float pulse = ready ? 0.85f + 0.15f * Mathf.Sin(Time.time * 5f) : 1f;

            if (ready) Draw(SpriteFactory.Glow.texture, c, r * 1.9f, new Color(accent.r, accent.g, accent.b, 0.45f * pulse));
            Draw(shadow, c + new Vector2(0, -r * 0.08f), r * 1.25f, new Color(0, 0, 0, 0.5f));
            Draw(disc, c, r, ready ? new Color(Base.r, Base.g, Base.b, 0.85f) : new Color(Base.r, Base.g, Base.b, 0.5f));
            Draw(disc, c, r, new Color(accent.r, accent.g, accent.b, (ready ? 0.22f : 0.05f) + 0.4f * pressFlash));
            Draw(ring, c, r, new Color(accent.r, accent.g, accent.b, ready ? 0.95f : 0.35f));

            // The object's own symbol (the one raised on the prop) above the text, in its side
            // colour; the colour-blind side icon moves to a badge on the rim.
            var sym = ready && Art.Catalog != null ? Art.Catalog.Symbol(focus.Symbol) : null;
            if (sym != null) Draw(sym, c + new Vector2(0, r * 0.36f), r * 0.2f, ready ? accent : Ink);
            var icon = ready ? Palette.IconFor(focus.Tag) : null;
            if (icon != null)
            {
                if (sym != null) Draw(icon.texture, c + new Vector2(r * 0.66f, r * 0.66f), r * 0.17f, accent);
                else Draw(icon.texture, c + new Vector2(0, r * 0.38f), r * 0.16f, accent);
            }

            string text = ready ? focus.Prompt.ToUpperInvariant() : "USE";
            label.fontSize = Mathf.RoundToInt(r * (text.Length > 6 ? 0.26f : 0.34f));
            label.normal.textColor = ready ? Ink : new Color(1, 1, 1, 0.4f);
            GUI.Label(ScreenRect(c + new Vector2(0, -r * 0.05f), r * 0.85f), text, label);
        }

        // Editor stand-in for one finger.
        bool MouseTouch(out Touch t)
        {
            t = default;
            if (Input.touchCount > 0 || Application.isMobilePlatform) return false;
            bool down = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0), up = Input.GetMouseButtonUp(0);
            if (!down && !held && !up) return false;
            t.fingerId = 99;
            t.position = Input.mousePosition;
            t.phase = down ? TouchPhase.Began : up ? TouchPhase.Ended : TouchPhase.Moved;
            return true;
        }

        Touch Mouse() { MouseTouch(out var t); return t; }

        // Tap on a glowing object: use it if the player is close enough, otherwise say so.
        bool TapObject(GameManager gm, Vector2 screen)
        {
            var player = gm.Player;
            var rig = gm.CameraRig;
            if (player == null || rig == null || gm.World == null) return false;
            var ray = rig.Camera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float dist)) return false;
            Vector2 p = ray.GetPoint(dist);

            Objects.WorldObject best = null;
            float bestDist = 0.6f; // tolerance for tall models and fat fingers
            foreach (var o in gm.World.Objects.Values)
            {
                if (!o.CanInteractNow) continue;
                float d = o.DistanceTo(p);
                if (d < bestDist) { best = o; bestDist = d; }
            }
            if (best == null) return false;

            if (best.DistanceTo(player.Position) <= player.interactRange + 0.3f)
            {
                pressFlash = 1f;
                best.Interact();
            }
            else gm.Toast("Move closer to use that.");
            return true;
        }

        // Preview hook: pose the stick as if a thumb were dragging it (editor/tests).
        public void PoseStick(Vector2 origin, Vector2 at) { stickFinger = 98; stickOrigin = origin; stickPos = at; }

        // ---- drawing helpers (touch space is bottom-left origin; IMGUI is top-left) ----

        static Rect ScreenRect(Vector2 center, float radius) =>
            new Rect(center.x - radius, Screen.height - center.y - radius, radius * 2f, radius * 2f);

        static void Draw(Texture tex, Vector2 center, float radius, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(ScreenRect(center, radius), tex);
            GUI.color = old;
        }

        static void Chevron(Vector2 center, float size, float degrees, Color color)
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, new Vector2(center.x, Screen.height - center.y));
            Draw(SpriteFactory.Triangle.texture, center, size, color);
            GUI.matrix = matrix;
        }

        static Texture2D Radial(int size, System.Func<float, float> alpha) =>
            Make(size, (x, y) => new Color(1, 1, 1, alpha(Mathf.Sqrt(x * x + y * y))));

        // f gets coordinates in [-1, 1].
        static Texture2D Make(int size, System.Func<float, float, Color> f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
