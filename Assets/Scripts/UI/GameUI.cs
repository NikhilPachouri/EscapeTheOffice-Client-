using System.Linq;
using EscapeOffice.Objects;
using UnityEngine;

namespace EscapeOffice.UI
{
    // All screens in IMGUI so the main scene needs no UI setup: join, waiting, HUD, keypad,
    // code panel, end screen, toasts and edge-of-screen cue indicators.
    public class GameUI : MonoBehaviour
    {
        const float RefHeight = 720f;

        public bool IsModal => keypad != null || codePanel != null;

        string url;
        string code = "";
        KeypadObject keypad;
        string typed = "";
        CodePanelObject codePanel;
        int modalOpenedFrame;

        string toast;
        float toastUntil;

        GUIStyle title, big, label, small, box, button, field, digits;
        Texture2D white;

        void Awake()
        {
            url = GameManager.DefaultServerUrl;
            white = Texture2D.whiteTexture;
        }

        public void OpenKeypad(KeypadObject k)
        {
            keypad = k;
            typed = "";
            modalOpenedFrame = Time.frameCount;
        }

        public void ShowCode(CodePanelObject panel)
        {
            codePanel = panel;
            modalOpenedFrame = Time.frameCount;
            GameManager.Instance.PlayLocal("click", panel.transform.position);
        }

        public void Toast(string message)
        {
            toast = message;
            toastUntil = Time.time + 3f;
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            box = new GUIStyle(GUI.skin.box) { fontSize = 18, padding = new RectOffset(12, 12, 10, 10) };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            digits = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            Styles();
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = RefHeight;

            var gm = GameManager.Instance;
            switch (gm.Current)
            {
                case GameManager.Phase.Join: JoinScreen(gm, w, h); break;
                case GameManager.Phase.Connecting:
                case GameManager.Phase.Waiting: WaitingScreen(gm, w, h); break;
                case GameManager.Phase.Playing: Hud(gm, w, h); break;
                case GameManager.Phase.Complete: Hud(gm, w, h); EndScreen(gm, w, h); break;
            }
        }

        // ---------------------------------------------------------------- screens

        void JoinScreen(GameManager gm, float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0.05f, 0.05f, 0.07f, 1f));
            float cx = w / 2f, y = h * 0.18f;
            GUI.Label(new Rect(cx - 300, y, 600, 60), "The Other Side", title);
            GUI.Label(new Rect(cx - 300, y + 60, 600, 30), "Two sides. One building. Talk to each other.", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });

            y += 130;
            GUI.Label(new Rect(cx - 200, y, 400, 26), "Server", label);
            url = GUI.TextField(new Rect(cx - 200, y + 26, 400, 36), url, field);
            y += 80;
            GUI.Label(new Rect(cx - 200, y, 400, 26), "Room code", label);
            GUI.SetNextControlName("code");
            code = GUI.TextField(new Rect(cx - 200, y + 26, 400, 36), code, 12, field).ToUpperInvariant();
            y += 80;

            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
            GUI.enabled = code.Trim().Length > 0;
            if (GUI.Button(new Rect(cx - 200, y, 400, 44), "Join", button) || (enter && GUI.enabled))
                gm.Join(url, code);
            GUI.enabled = true;
            y += 54;

            var lastCode = PlayerPrefs.GetString(GameManager.PrefLastCode, "");
            var lastToken = PlayerPrefs.GetString(GameManager.PrefLastToken, "");
            if (lastCode.Length > 0 && lastToken.Length > 0)
            {
                if (GUI.Button(new Rect(cx - 200, y, 400, 36), $"Rejoin {lastCode} as my previous side", button))
                    gm.Join(url, lastCode, lastToken);
                y += 46;
            }
            if (GUI.Button(new Rect(cx - 200, y, 400, 36), "Offline test (fake world)", button)) gm.StartOffline();
            y += 50;

            if (!string.IsNullOrEmpty(gm.Status))
                GUI.Label(new Rect(cx - 300, y, 600, 60), gm.Status, new GUIStyle(label) { alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(1f, 0.6f, 0.5f) } });
        }

        void WaitingScreen(GameManager gm, float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0.05f, 0.05f, 0.07f, 1f));
            float cx = w / 2f;
            GUI.Label(new Rect(cx - 300, h * 0.3f, 600, 50), $"Room {gm.RoomCode}", title);
            string msg = gm.Current == GameManager.Phase.Waiting
                ? "Waiting for your partner to join…"
                : string.IsNullOrEmpty(gm.Status) ? "Connecting…" : gm.Status;
            GUI.Label(new Rect(cx - 300, h * 0.3f + 70, 600, 40), msg, big);
            if (gm.Current == GameManager.Phase.Waiting)
            {
                var old = GUI.color;
                GUI.color = Palette.ForSide(gm.Side);
                GUI.Label(new Rect(cx - 300, h * 0.3f + 120, 600, 40), $"You are side {gm.Side}", big);
                GUI.color = old;
            }
            if (GUI.Button(new Rect(cx - 100, h * 0.3f + 190, 200, 40), "Cancel", button)) gm.Leave();
        }

        void Hud(GameManager gm, float w, float h)
        {
            var player = gm.Player;

            // Side badge, room and inventory, top-left.
            var sideColor = Palette.ForSide(gm.Side);
            Fill(new Rect(12, 12, 250, 108), new Color(0, 0, 0, 0.55f));
            Fill(new Rect(12, 12, 6, 108), sideColor);
            GUI.Label(new Rect(26, 16, 230, 26), $"Side {gm.Side}" + (gm.Offline ? "  (offline)" : ""), new GUIStyle(label) { fontStyle = FontStyle.Bold, normal = { textColor = sideColor } });
            var room = player != null ? player.GetComponent<RoomTracker>().Current : null;
            GUI.Label(new Rect(26, 40, 230, 22), room != null ? $"Room: {room.Id}" + (room.IsDark ? " (dark)" : "") : "Room: —", small);
            var inv = gm.Inventory();
            Slot(new Rect(26, 66, 110, 44), "Bomb", inv.Any(i => i.ToLowerInvariant().Contains("bomb")));
            Slot(new Rect(142, 66, 110, 44), "Key", inv.Any(i => i.ToLowerInvariant().Contains("key")));

            if (player != null && player.Debuffed)
                GUI.Label(new Rect(12, 126, 300, 24), $"Sluggish… {player.DebuffRemaining:0}s", new GUIStyle(label) { normal = { textColor = new Color(1f, 0.5f, 0.4f) } });

            if (!string.IsNullOrEmpty(gm.Status) && !gm.Offline)
                GUI.Label(new Rect(w - 412, 12, 400, 26), gm.Status, new GUIStyle(small) { alignment = TextAnchor.UpperRight, normal = { textColor = new Color(1f, 0.7f, 0.4f) } });

            // Interaction prompt.
            if (gm.Current == GameManager.Phase.Playing && !IsModal && player != null && player.Focus != null)
            {
                var f = player.Focus;
                string text = $"[E]  {f.Prompt}";
                Fill(new Rect(w / 2 - 160, h - 110, 320, 40), new Color(0, 0, 0, 0.6f));
                GUI.Label(new Rect(w / 2 - 160, h - 110, 320, 40), text, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            }

            if (toast != null && Time.time < toastUntil)
            {
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((toastUntil - Time.time) * 2f));
                Fill(new Rect(w / 2 - 260, h - 60, 520, 40), new Color(0, 0, 0, 0.7f));
                GUI.Label(new Rect(w / 2 - 260, h - 60, 520, 40), toast, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
                GUI.color = c;
            }

            EdgeIndicators(gm, w, h);
            if (keypad != null) KeypadModal(w, h);
            if (codePanel != null) CodeModal(w, h);
        }

        void Slot(Rect r, string name, bool held)
        {
            Fill(r, held ? new Color(1f, 1f, 1f, 0.18f) : new Color(1f, 1f, 1f, 0.05f));
            GUI.Label(r, held ? name : $"<color=#777>{name}</color>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, richText = true });
        }

        void EndScreen(GameManager gm, float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.75f));
            GUI.Label(new Rect(w / 2 - 400, h * 0.35f, 800, 60), "You escaped the office!", title);
            GUI.Label(new Rect(w / 2 - 400, h * 0.35f + 70, 800, 40), "Both sides made it out. Nice teamwork.", big);
            if (GUI.Button(new Rect(w / 2 - 120, h * 0.35f + 140, 240, 44), "Back to menu", button)) gm.Leave();
        }

        // ---------------------------------------------------------------- modals

        void KeypadModal(float w, float h)
        {
            var e = Event.current;
            bool justOpened = Time.frameCount == modalOpenedFrame;
            int length = keypad.CodeLength;
            if (e.type == EventType.KeyDown && !justOpened)
            {
                int d = DigitFor(e.keyCode);
                if (d >= 0 && typed.Length < length) { typed += d; e.Use(); }
                else if (e.keyCode == KeyCode.Backspace && typed.Length > 0) { typed = typed.Substring(0, typed.Length - 1); e.Use(); }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { SubmitKeypad(); e.Use(); return; }
                else if (e.keyCode == KeyCode.Escape) { keypad = null; e.Use(); return; }
            }

            var r = new Rect(w / 2 - 170, h / 2 - 250, 340, 470);
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.4f));
            Fill(r, new Color(0.12f, 0.13f, 0.15f, 0.97f));
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 30), "KEYPAD", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            Fill(new Rect(r.x + 20, r.y + 46, r.width - 40, 80), new Color(0.05f, 0.1f, 0.06f));
            GUI.Label(new Rect(r.x + 20, r.y + 46, r.width - 40, 80), typed.PadRight(length, '_'), new GUIStyle(digits) { normal = { textColor = new Color(0.4f, 1f, 0.5f) } });

            float bx = r.x + 30, by = r.y + 140, bw = 90, bh = 58, gap = 5;
            for (int i = 0; i < 9; i++)
                if (GUI.Button(new Rect(bx + (i % 3) * (bw + gap), by + (i / 3) * (bh + gap), bw, bh), (i + 1).ToString(), button) && typed.Length < length)
                    typed += (i + 1);
            if (GUI.Button(new Rect(bx, by + 3 * (bh + gap), bw, bh), "C", button)) typed = "";
            if (GUI.Button(new Rect(bx + bw + gap, by + 3 * (bh + gap), bw, bh), "0", button) && typed.Length < length) typed += "0";
            GUI.enabled = typed.Length == length;
            if (GUI.Button(new Rect(bx + 2 * (bw + gap), by + 3 * (bh + gap), bw, bh), "OK", button)) SubmitKeypad();
            GUI.enabled = true;
            GUI.Label(new Rect(r.x, r.yMax - 32, r.width, 24), "digits · Enter submit · Esc close", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        void SubmitKeypad()
        {
            if (keypad == null || typed.Length != keypad.CodeLength) return;
            keypad.Submit(typed); // the server compares; a wrong code comes back as a buzz cue
            keypad = null;
        }

        static int DigitFor(KeyCode k)
        {
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return k - KeyCode.Alpha0;
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return k - KeyCode.Keypad0;
            return -1;
        }

        void CodeModal(float w, float h)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && Time.frameCount != modalOpenedFrame &&
                (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.E || e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return))
            {
                codePanel = null;
                e.Use();
                return;
            }
            var r = new Rect(w / 2 - 200, h / 2 - 130, 400, 240);
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.4f));
            Fill(r, new Color(0.04f, 0.1f, 0.06f, 0.97f));
            Fill(new Rect(r.x, r.y, r.width, 4), Palette.Info);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 30), "The screen shows a code:", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(r.x, r.y + 56, r.width, 100), codePanel.Code, new GUIStyle(digits) { fontSize = 80, normal = { textColor = new Color(0.4f, 1f, 0.5f) } });
            GUI.Label(new Rect(r.x, r.y + 160, r.width, 24), "Your partner needs this.", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(r.x + r.width / 2 - 60, r.yMax - 44, 120, 34), "Close", button)) codePanel = null;
        }

        // ---------------------------------------------------------------- cues

        void EdgeIndicators(GameManager gm, float w, float h)
        {
            var rig = gm.CameraRig;
            var player = gm.Player;
            if (rig == null || player == null) return;
            var cam = rig.Camera;
            var center = new Vector2(w / 2, h / 2);
            float scale = Screen.height / RefHeight;

            foreach (var ind in gm.Fx.Indicators)
            {
                float age = Time.time - ind.Born;
                float alpha = Mathf.Clamp01((FxPlayer.IndicatorLifetime - age) * 1.5f);
                var sp = cam.WorldToScreenPoint(ind.Position);
                var target = new Vector2(sp.x / scale, h - sp.y / scale);
                var dir = (target - center);
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                dir.Normalize();

                // Where the vision circle meets the direction, clamped to the screen.
                float radiusPx = Mathf.Min(w, h) * 0.42f;
                var pos = center + dir * radiusPx;
                pos.x = Mathf.Clamp(pos.x, 40, w - 40);
                pos.y = Mathf.Clamp(pos.y, 40, h - 40);

                var old = GUI.color;
                var oldMatrix = GUI.matrix;
                GUI.color = new Color(1f, 0.9f, 0.4f, alpha);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f; // triangle texture points up
                GUIUtility.RotateAroundPivot(angle, pos * scale);
                GUI.DrawTexture(new Rect(pos.x - 14, pos.y - 14, 28, 28), SpriteFactory.Triangle.texture);
                GUI.matrix = oldMatrix;
                GUI.Label(new Rect(pos.x - 80, pos.y + 16, 160, 22), ind.Label, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                GUI.color = old;
            }
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }
    }
}
