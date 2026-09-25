using System.Linq;
using EscapeOffice.Objects;
using UnityEngine;

namespace EscapeOffice.UI
{
    // All screens in IMGUI so the main scene needs no UI setup: join, waiting, HUD, keypad,
    // code panel, end screen, toasts and edge-of-screen cue indicators. Tutorial tips draw
    // themselves (TutorialTips); these screens only start or toggle them.
    public class GameUI : MonoBehaviour
    {
        const float RefHeight = 720f;

        public bool IsModal => keypad != null || codePanel != null || SettingsMenu.IsOpen;

        string url;
        string code = "";
        KeypadObject keypad;
        string typed = "";
        CodePanelObject codePanel;
        int modalOpenedFrame;

        string toast;
        float toastUntil;

        GUIStyle title, big, label, small, box, button, field, digits, key;
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
            UiSkin.Apply(title, big, label, small, box, button, field, digits);
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
            // Key art full-screen; the menu sits on glass in the dark left half so the tower and
            // the two players stay visible.
            MenuArt.DrawBackdrop(new Rect(0, 0, w, h));
            var headFont = Art.Catalog != null ? Art.Catalog.titleFont : null;
            var bodyFont = Art.Catalog != null ? Art.Catalog.uiFont : null;
            var primary = MenuArt.Primary(headFont);
            var secondary = MenuArt.Secondary(headFont);
            var ghost = MenuArt.Ghost(headFont);
            var codeField = MenuArt.Field(Art.Catalog != null ? Art.Catalog.codeFont : null);
            var hint = new GUIStyle(small) { font = bodyFont, fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = MenuArt.Dim } };

            var lastCode = PlayerPrefs.GetString(GameManager.PrefLastCode, "");
            var lastToken = PlayerPrefs.GetString(GameManager.PrefLastToken, "");
            bool canRejoin = lastCode.Length > 0 && lastToken.Length > 0;

            bool hasStatus = !string.IsNullOrEmpty(gm.Status);
            float pw = 420, ph = 434 + (canRejoin ? 50 : 0) + (hasStatus ? 44 : 0);
            var panel = new Rect(Mathf.Max(24, w * 0.05f), (h - ph) / 2, pw, ph);
            MenuArt.Panel(panel);
            float x = panel.x + 32, iw = pw - 64, y = panel.y + 28;

            MenuArt.Title(new Rect(x, y, iw, 56), headFont, 44);
            GUI.Label(new Rect(x, y + 54, iw, 24), "Two sides. One building. Talk to each other.", hint);
            y += 104;

            // Create: the main action.
            var create = new Rect(x, y, iw, 58);
            if (GUI.Button(create, "      Create a room", primary)) gm.Create(url);
            MenuArt.Icon(new Rect(create.x + 18, create.y + 13, 32, 32), "UI_Icon_Both_256");
            GUI.Label(new Rect(x, y + 62, iw, 22), "You get a code to read out to your partner.", hint);
            y += 104;

            MenuArt.Divider(new Rect(x, y, iw, 20), "HAVE A CODE?", new GUIStyle(small) { font = headFont, fontSize = 13 });
            y += 32;
            GUI.SetNextControlName("code");
            code = GUI.TextField(new Rect(x, y, iw - 150, 56), code, 12, codeField).ToUpperInvariant();
            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                         && GUI.GetNameOfFocusedControl() == "code";
            GUI.enabled = code.Trim().Length > 0;
            if (GUI.Button(new Rect(x + iw - 138, y, 138, 56), "Join", secondary) || (enter && GUI.enabled))
                gm.Join(url, code);
            GUI.enabled = true;
            if (code.Length == 0)
                GUI.Label(new Rect(x, y, iw - 150, 56), "ROOM CODE", new GUIStyle { font = codeField.font, fontSize = codeField.fontSize, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, 1, 1, 0.25f) } });
            y += 76;

            if (canRejoin)
            {
                if (GUI.Button(new Rect(x, y, iw, 40), $"Rejoin {lastCode} as my previous side", ghost))
                    gm.Join(url, lastCode, lastToken);
                y += 50;
            }

            // Extras, as quiet chips.
            float cw = (iw - 16) / 3f;
            if (GUI.Button(new Rect(x, y, cw, 40), "Tutorial", ghost)) gm.StartTutorial();
            if (GUI.Button(new Rect(x + cw + 8, y, cw, 40), "Offline test", ghost)) gm.StartOffline();
            if (GUI.Button(new Rect(x + 2 * (cw + 8), y, cw, 40), "Level editor", ghost))
            {
                gm.StartOffline();
                gm.GetComponent<LevelEditor>().OpenWhenReady();
            }
            y += 54;

            if (hasStatus)
                GUI.Label(new Rect(x, y - 6, iw, 44), gm.Status, new GUIStyle(hint) { wordWrap = true, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(1f, 0.62f, 0.5f) } });
        }

        void WaitingScreen(GameManager gm, float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0.05f, 0.05f, 0.07f, 1f));
            float cx = w / 2f;
            GUI.Label(new Rect(cx - 300, h * 0.3f, 600, 50), gm.RoomCode.Length > 0 ? $"Room {gm.RoomCode}" : "Creating room…", title);
            string msg = gm.Current == GameManager.Phase.Waiting
                ? $"Tell your partner the code: {gm.RoomCode}"
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

            if (gm.CanSwitchSide && gm.Current == GameManager.Phase.Playing &&
                HudButton(new Rect(270, 12, 150, 44), $"Play side {(gm.Side == "A" ? "B" : "A")}", button))
                gm.SwitchSide();

            // Voice: partner speaking indicator and mute for incoming audio, top-right.
            var voice = gm.Voice;
            if (voice != null && voice.Active)
            {
                var vr = new Rect(w - 232, 72, 220, 44); // below the settings gear
                Fill(vr, new Color(0, 0, 0, 0.55f));
                bool speaking = voice.PartnerSpeaking && !voice.MuteIncoming;
                var dot = new Rect(vr.x + 12, vr.y + 16, 12, 12);
                Fill(dot, speaking ? Palette.ForSide(gm.Side == "A" ? "B" : "A") * (0.7f + 0.3f * Mathf.Sin(Time.time * 12f)) : new Color(1, 1, 1, 0.2f));
                GUI.Label(new Rect(vr.x + 30, vr.y, 110, vr.height), voice.Connected ? (speaking ? "Partner speaking" : "Voice on") : voice.Status, new GUIStyle(small) { alignment = TextAnchor.MiddleLeft });
                if (HudButton(new Rect(vr.xMax - 78, vr.y + 6, 70, 32), voice.MuteIncoming ? "Unmute" : "Mute", new GUIStyle(button) { fontSize = 14 }))
                    voice.MuteIncoming = !voice.MuteIncoming;
            }

            if (player != null && player.Debuffed)
                GUI.Label(new Rect(12, 126, 300, 24), $"Sluggish… {player.DebuffRemaining:0}s", new GUIStyle(label) { normal = { textColor = new Color(1f, 0.5f, 0.4f) } });

            // Beside the side badge (after "Play side" when offline); top-right belongs to status and voice.
            if (gm.Current == GameManager.Phase.Playing && !IsModal && !gm.EditorOpen &&
                HudButton(new Rect(gm.CanSwitchSide ? 428 : 270, 12, 150, 44), gm.Tips.Show ? "Tips: on" : "Tips: off", button))
                gm.Tips.Show = !gm.Tips.Show;

            if (!string.IsNullOrEmpty(gm.Status) && !gm.Offline)
                GUI.Label(new Rect(w - 480, 12, 400, 26), gm.Status, new GUIStyle(small) { alignment = TextAnchor.UpperRight, normal = { textColor = new Color(1f, 0.7f, 0.4f) } });

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

        // A button over the game view: touches that start on it don't also move the joystick or use things.
        static bool HudButton(Rect r, string text, GUIStyle style)
        {
            TouchControls.Block(r);
            return GUI.Button(r, text, style);
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
            GUI.Label(new Rect(w / 2 - 400, h * 0.35f + 70, 800, 40), "Escape Successful. Nice teamwork.", big);
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

            // Drawn as the riddle keypad itself (tos-interactables.js): metal body, recessed face
            // ringed in the keypad's side colour, display strip, the four colour swatches, 12 keys.
            var order = keypad.Order;
            var tint = keypad.Tag == Palette.Tag.None ? Color.white : Palette.ForTag(keypad.Tag);
            var r = new Rect(w / 2 - 180, h / 2 - 265, 360, 530);
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.5f));
            Fill(r, new Color(0.43f, 0.45f, 0.48f, 0.98f));                               // body
            var face = new Rect(r.x + 14, r.y + 44, r.width - 28, r.height - 58);
            Fill(face, new Color(0.2f, 0.22f, 0.25f, 1f));                                 // recessed face
            Outline(new Rect(face.x - 3, face.y - 3, face.width + 6, face.height + 6), new Color(tint.r, tint.g, tint.b, 0.9f), 2);
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 30), "KEYPAD", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(r.xMax - 44, r.y + 6, 36, 32), "X", button)) keypad = null;

            // display strip
            var strip = new Rect(face.x + 16, face.y + 16, face.width - 32, 74);
            Fill(strip, new Color(0.72f, 0.73f, 0.75f, 1f));
            var screen = new Rect(strip.x + 6, strip.y + 6, strip.width - 12, strip.height - 12);
            Fill(screen, new Color(0.1f, 0.11f, 0.13f, 1f));
            GUI.Label(screen, typed.PadRight(length, '_'), new GUIStyle(digits) { fontSize = 50, normal = { textColor = new Color(0.4f, 1f, 0.6f) } });

            // four swatch slots: the order to read the partner's panels in; the next one is outlined
            int slots = Mathf.Max(4, length);
            float sw = 52f, gap = 12f, sx = face.center.x - (slots * sw + (slots - 1) * gap) / 2f, sy = strip.yMax + 18;
            for (int i = 0; i < slots; i++)
            {
                var cell = new Rect(sx + i * (sw + gap), sy, sw, sw);
                Fill(cell, new Color(0.4f, 0.42f, 0.45f, 1f));
                bool used = order != null && i < order.Length;
                var colour = used ? Palette.ForName(order[i]) : new Color(0.95f, 0.95f, 0.96f, 0.35f);
                Fill(new Rect(cell.x + 6, cell.y + 6, cell.width - 12, cell.height - 12), colour);
                if (i < typed.Length)
                    GUI.Label(cell, typed[i].ToString(), new GUIStyle(digits) { fontSize = 26, normal = { textColor = colour.grayscale > 0.6f ? Color.black : Color.white } });
                if (i == typed.Length && i < length)
                    Outline(new Rect(cell.x - 3, cell.y - 3, cell.width + 6, cell.height + 6), new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f)), 2);
            }
            if (order != null && order.Length > 0)
                GUI.Label(new Rect(face.x, sy + sw + 4, face.width, 22), "Read your partner's panels in this order", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            // 12 keys, 3 x 4
            key ??= UiSkin.Key(button);
            float bw = 88, bh = 50, kg = 8, bx = face.center.x - (3 * bw + 2 * kg) / 2f, by = sy + sw + 34;
            for (int i = 0; i < 9; i++)
                if (GUI.Button(new Rect(bx + (i % 3) * (bw + kg), by + (i / 3) * (bh + kg), bw, bh), (i + 1).ToString(), key) && typed.Length < length)
                    typed += (i + 1);
            if (GUI.Button(new Rect(bx, by + 3 * (bh + kg), bw, bh), "DEL", key) && typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
            if (GUI.Button(new Rect(bx + bw + kg, by + 3 * (bh + kg), bw, bh), "0", key) && typed.Length < length) typed += "0";
            GUI.enabled = typed.Length == length;
            if (GUI.Button(new Rect(bx + 2 * (bw + kg), by + 3 * (bh + kg), bw, bh), "OK", key)) SubmitKeypad();
            GUI.enabled = true;
        }

        void Outline(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
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
