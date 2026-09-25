using System.Collections.Generic;
using System.Linq;
using EscapeOffice.Objects;
using UnityEngine;

namespace EscapeOffice.UI
{
    // The tutorial is the offline tutorial world with text next to things: a tip beside each
    // object in the player's current room and its doorways (what it does and how to get past
    // it, by its current state), hints on the controls until the player has used each one, and
    // a goal banner. The HUD's Tips button can also show the object tips in any game.
    // Text comes from Resources/Tutorial.json.
    public class TutorialTips : MonoBehaviour
    {
        const float RefHeight = 720f; // same as GameUI, so HUD rects line up
        const int MaxObjectTips = 4;
        const float TipWidth = 240f, HintWidth = 260f;

        public bool Show { get; set; }
        public bool InTutorial { get; private set; }

        TutorialContent content;
        TouchControls touch;
        bool moved, used, switched;
        string lastSide;
        Vector2? start;
        float uiScale = 1f;
        readonly List<Rect> placed = new List<Rect>();

        GUIStyle title, body, button;
        static readonly Color Panel = new Color(0.04f, 0.05f, 0.11f, 0.9f); // start-screen navy glass
        static readonly Color Ink = new Color32(0xe8, 0xee, 0xf2, 0xff);
        static readonly Color WaterTip = new Color(0.35f, 0.62f, 1f);

        void Awake() => content = TutorialContent.Load("Tutorial");

        void Start()
        {
            touch = GetComponent<TouchControls>();
            GameManager.Instance.Interacted += OnInteracted;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.Interacted -= OnInteracted;
        }

        public void BeginTutorial()
        {
            InTutorial = Show = true;
            moved = used = switched = false;
            lastSide = null;
            start = null;
        }

        public void EndTutorial() => InTutorial = Show = false;

        void OnInteracted(string id) => used = true;

        // Progress for the control hints.
        void Update()
        {
            var gm = GameManager.Instance;
            if (!InTutorial || gm.Current != GameManager.Phase.Playing || gm.Player == null) return;
            start ??= gm.Player.Position;
            if (Vector2.Distance(start.Value, gm.Player.Position) > 1.5f) moved = true;
            if (lastSide != null && lastSide != gm.Side) switched = true;
            lastSide = gm.Side;
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (!Show || content == null || gm.Current != GameManager.Phase.Playing || gm.UI.IsModal || gm.EditorOpen) return;
            Styles();
            uiScale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float w = Screen.width / uiScale, h = RefHeight;

            placed.Clear();
            ReserveHud(gm, w, h);
            if (InTutorial)
            {
                if (Banner(gm, w)) return; // left the tutorial
                ControlHint(gm, h);
            }
            WorldTips(gm, w, h);
        }

        // ---------------------------------------------------------------- tutorial only

        // Goal and a way out, top-right (free offline: status and voice only show online).
        bool Banner(GameManager gm, float w)
        {
            if (string.IsNullOrEmpty(content.Goal)) return false;
            var r = new Rect(w - 12 - 380, 12, 380, 0);
            float textH = body.CalcHeight(new GUIContent(content.Goal), r.width - 28);
            r.height = 34 + textH + 50;
            Box(r, Palette.ForSide(gm.Side));
            Title(new Rect(r.x + 16, r.y + 8, r.width - 28, 22), "TUTORIAL", Palette.ForSide(gm.Side));
            GUI.Label(new Rect(r.x + 16, r.y + 32, r.width - 28, textH), content.Goal, body);
            placed.Add(r);
            TouchControls.Block(r); // the whole banner, so tapping it never uses what's behind it
            if (GUI.Button(new Rect(r.xMax - 12 - 150, r.yMax - 12 - 34, 150, 34), "Exit tutorial", button))
            {
                gm.Leave();
                return true;
            }
            return false;
        }

        // One hint at a time on the controls, in the order a new player needs them.
        void ControlHint(GameManager gm, float h)
        {
            var color = Palette.ForSide(gm.Side);
            if (!moved && touch != null && !string.IsNullOrEmpty(content.Hud.Move))
            {
                var c = ToGui(touch.StickHome, h);
                var a = c + new Vector2(touch.StickRadius / uiScale, 0f);
                Callout(a, new Vector2(a.x + 24f, a.y), HintWidth, null, content.Hud.Move, color, alignLeft: true);
            }
            else if (!used && touch != null && !string.IsNullOrEmpty(content.Hud.Use))
            {
                var c = ToGui(touch.ButtonCenter, h);
                var a = c - new Vector2(touch.ButtonRadius / uiScale, 0f);
                Callout(a, new Vector2(a.x - 24f, a.y), HintWidth, null, content.Hud.Use, color, alignLeft: false);
            }
            else if (!switched && gm.CanSwitchSide && !string.IsNullOrEmpty(content.Hud.Switch))
            {
                // GameUI's "Play side" button: (270, 12, 150, 44).
                var a = new Vector2(345f, 56f);
                var box = Measure(HintWidth, null, content.Hud.Switch);
                Draw(a, new Rect(270f, 80f, box.x, box.y), null, content.Hud.Switch, color);
            }
        }

        // ---------------------------------------------------------------- tips in the world

        void WorldTips(GameManager gm, float w, float h)
        {
            var player = gm.Player;
            var world = gm.World;
            var cam = gm.CameraRig != null ? gm.CameraRig.Camera : null;
            if (player == null || world == null || cam == null) return;
            var here = player.Position;
            var room = player.GetComponent<RoomTracker>().Current;

            // Room tips are pinned to the room, not the player.
            var dark = content.Rooms.Dark;
            if (room != null && room.IsDark && dark != null && room.Rects.Count > 0)
                Place(Anchor(cam, room.Rects[0].center, 1.2f, h), dark.Title, dark.Text, Ink, w, h);

            // Only the current room: what is in it, and what sits in its doorways (doors, lasers,
            // fire, cracked walls and exits are in wall gaps, so they belong to no room).
            // Standing in a doorway with no room, just what is right beside the player.
            var inRoom = world.Objects.Values
                .Where(o => o != null && o.isActiveAndEnabled)
                .Where(o => room != null
                    ? o.Rooms.Contains(room) || (o.Rooms.Count == 0 && Touches(room, o.Bounds, 0.5f))
                    : o.DistanceTo(here) <= 1.5f);

            // One tip per object, except grouped types (the code panels): one above the middle of the group.
            var tips = new List<(ObjectTip tip, WorldObject obj, Vector2 at, float dist)>();
            foreach (var g in inRoom.GroupBy(o => content.ForType(o.Type)).Where(g => g.Key != null))
            {
                if (g.Key.Group)
                {
                    var objs = g.ToList();
                    var mid = objs.Aggregate(Vector2.zero, (s, o) => s + (Vector2)o.transform.position) / objs.Count;
                    tips.Add((g.Key, objs[0], mid, objs.Min(o => Distance(o, here))));
                }
                else
                    foreach (var o in g) tips.Add((g.Key, o, (Vector2)o.transform.position, Distance(o, here)));
            }

            // Nearest first, so when two tips would overlap the closer object's wins.
            int shown = 0;
            foreach (var t in tips.OrderBy(t => t.dist))
            {
                if (shown >= MaxObjectTips) break;
                var text = t.tip.TextFor(t.obj.Value);
                if (string.IsNullOrEmpty(text)) continue;
                var color = t.obj.Tag == Palette.Tag.None ? Ink : Palette.ForTag(t.obj.Tag);
                var line = SideLine(gm, t.obj.Tag);
                if (line != null) text += $"\n<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{line}</b></color>";
                if (Place(Anchor(cam, t.at, 1f, h), t.tip.Title, text, color, w, h)) shown++;
            }

            // A flooded room next door (one wall away) is behind one of this room's doorways;
            // its tip sits on the flooded room's edge nearest this room.
            var flooded = content.Rooms.Flooded;
            if (flooded == null || room == null || room.Rects.Count == 0) return;
            var middle = room.Rects[0].center;
            foreach (var r in world.Rooms.Values.Where(r => r != room && r.IsFlooded))
            {
                if (!r.Rects.Any(rc => Touches(room, rc, 1.5f))) continue;
                var edge = r.Rects.Select(rc => Closest(rc, middle)).OrderBy(p => (p - middle).sqrMagnitude).First();
                Place(Anchor(cam, edge, 0.3f, h), flooded.Title, flooded.Text, WaterTip, w, h);
            }
        }

        static float Distance(WorldObject o, Vector2 p) => Mathf.Min(o.DistanceTo(p), Vector2.Distance(p, o.transform.position));

        // Whether `rect` is within `margin` tiles of any of the room's rectangles (world units).
        static bool Touches(Room room, Rect rect, float margin) =>
            room.Rects.Any(r => new Rect(r.x - margin, r.y - margin, r.width + margin * 2f, r.height + margin * 2f).Overlaps(rect));

        string SideLine(GameManager gm, Palette.Tag tag)
        {
            var s = content.SideLine;
            switch (tag)
            {
                case Palette.Tag.A: return gm.Side == "A" ? s.Mine : s.Partner;
                case Palette.Tag.B: return gm.Side == "B" ? s.Mine : s.Partner;
                case Palette.Tag.Both: return s.Both;
                default: return null;
            }
        }

        // Things the tips must not cover.
        void ReserveHud(GameManager gm, float w, float h)
        {
            placed.Add(new Rect(0, 0, 590, 60));   // side badge, Play side, Tips
            placed.Add(new Rect(0, 0, 270, 152));  // badge, inventory, debuff timer
            placed.Add(new Rect(w / 2 - 260, h - 60, 520, 40)); // toasts
            if (!gm.Offline) placed.Add(new Rect(w - 420, 0, 420, 92)); // status and voice
            if (touch == null) return;
            placed.Add(Circle(ToGui(touch.StickHome, h), touch.StickRadius / uiScale * 1.2f));
            var use = ToGui(touch.ButtonCenter, h);
            float br = touch.ButtonRadius / uiScale;
            placed.Add(Circle(use, br * 1.3f));
            placed.Add(Circle(use - new Vector2(0f, br * 2.5f), br * 1.2f)); // TALK
        }

        // ---------------------------------------------------------------- drawing

        // A tip always directly above `at` (kept on screen). If that spot would cover another tip
        // or the HUD, the tip is skipped rather than moved.
        bool Place(Vector2? at, string head, string text, Color color, float w, float h)
        {
            if (at == null) return false;
            var a = at.Value;
            if (a.x < 0 || a.x > w || a.y < 0 || a.y > h) return false;
            var size = Measure(TipWidth, head, text);
            const float gap = 26f;
            var r = new Rect(Mathf.Clamp(a.x - size.x / 2, 8, w - 8 - size.x), Mathf.Clamp(a.y - gap - size.y, 8, h - 8 - size.y), size.x, size.y);
            var padded = new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12);
            if (placed.Any(p => p.Overlaps(padded))) return false;
            Draw(a, r, head, text, color);
            return true;
        }

        // A control hint: box beside `a`, to the right of `edge` (alignLeft) or to its left.
        void Callout(Vector2 a, Vector2 edge, float width, string head, string text, Color color, bool alignLeft)
        {
            var size = Measure(width, head, text);
            var r = new Rect(alignLeft ? edge.x : edge.x - size.x, edge.y - size.y / 2, size.x, size.y);
            Draw(a, r, head, text, color);
        }

        Vector2 Measure(float width, string head, string text)
        {
            float bodyH = body.CalcHeight(new GUIContent(text), width - 28);
            return new Vector2(width, (head != null ? 30f : 10f) + bodyH + 10f);
        }

        void Draw(Vector2 anchor, Rect r, string head, string text, Color color)
        {
            placed.Add(r);
            Line(anchor, Closest(r, anchor), color);
            Fill(new Rect(anchor.x - 4, anchor.y - 4, 8, 8), color, 4);
            Box(r, color);
            float y = r.y + 8;
            if (head != null)
            {
                Title(new Rect(r.x + 16, y, r.width - 28, 20), head, color);
                y += 22;
            }
            GUI.Label(new Rect(r.x + 16, y, r.width - 28, r.yMax - y - 6), text, body);
        }

        void Box(Rect r, Color accent)
        {
            Fill(r, Panel, 8);
            Fill(new Rect(r.x, r.y + 6, 4, r.height - 12), accent, 2);
        }

        void Title(Rect r, string text, Color color)
        {
            title.normal.textColor = color;
            GUI.Label(r, text, title);
        }

        void Line(Vector2 a, Vector2 b, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var d = b - a;
            if (d.sqrMagnitude < 4f) return;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a * uiScale); // pivot in screen space, as GameUI's indicators
            GUI.DrawTexture(new Rect(a.x, a.y - 1f, d.magnitude, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(c.r, c.g, c.b, 0.8f), 0f, 0f);
            GUI.matrix = old;
        }

        static void Fill(Rect r, Color c, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, c, 0f, radius);
        }

        // World point a little above the floor (up is -Z), in GUI coordinates; null if behind the camera.
        Vector2? Anchor(Camera cam, Vector2 world, float height, float h)
        {
            var sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, -height));
            if (sp.z <= 0f) return null;
            return new Vector2(sp.x / uiScale, h - sp.y / uiScale);
        }

        // Touch controls use bottom-left screen pixels.
        Vector2 ToGui(Vector2 screen, float h) => new Vector2(screen.x / uiScale, h - screen.y / uiScale);

        static Rect Circle(Vector2 c, float r) => new Rect(c.x - r, c.y - r, r * 2f, r * 2f);

        static Vector2 Closest(Rect r, Vector2 p) =>
            new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
            body.normal.textColor = Ink;
            button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }
    }
}
