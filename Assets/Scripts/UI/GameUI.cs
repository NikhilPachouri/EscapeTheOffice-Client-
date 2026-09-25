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
            MenuArt.DrawBackdrop(new Rect(0, 0, w, h));
            var headFont = Art.Catalog != null ? Art.Catalog.titleFont : null;
            var codeFont = Art.Catalog != null ? Art.Catalog.codeFont : null;
            var hint = new GUIStyle(small) { fontSize = 16, alignment = TextAnchor.MiddleLeft, wordWrap = true, normal = { textColor = MenuArt.Dim } };
            bool waiting = gm.Current == GameManager.Phase.Waiting;

            float pw = 420, ph = 380;
            var p = new Rect(Mathf.Max(24, w * 0.05f), (h - ph) / 2, pw, ph);
            MenuArt.Panel(p);
            float x = p.x + 32, iw = pw - 64, y = p.y + 28;

            MenuArt.Title(new Rect(x, y, iw, 44), headFont, 34);
            y += 60;
            GUI.Label(new Rect(x, y, iw, 22), gm.RoomCode.Length > 0 ? "ROOM CODE" : "CREATING ROOM", new GUIStyle(hint) { font = headFont, fontSize = 14 });
            y += 26;

            // The code, big: it is what the player reads out to their partner.
            var codeRect = new Rect(x, y, iw, 86);
            MenuArt.Glass(codeRect, new Color(MenuArt.Warm.r, MenuArt.Warm.g, MenuArt.Warm.b, 0.6f), 0.85f);
            string shown = gm.RoomCode.Length > 0 ? gm.RoomCode : new string('.', 1 + (int)(Time.time * 3f) % 3);
            GUI.Label(codeRect, shown, new GUIStyle(digits) { font = codeFont, fontSize = 56, normal = { textColor = MenuArt.Warm } });
            y += 100;

            string msg = waiting ? "Read this code to your partner. The game starts when they join."
                : string.IsNullOrEmpty(gm.Status) ? "Connecting…" : gm.Status;
            GUI.Label(new Rect(x, y, iw, 44), msg, hint);
            y += 52;

            if (waiting)
            {
                var sideColor = Palette.ForSide(gm.Side);
                var badge = new Rect(x, y, 170, 34);
                MenuArt.Glass(badge, sideColor, 0.8f);
                GUI.Label(badge, $"You are side {gm.Side}", new GUIStyle(label) { font = headFont, fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = sideColor } });
            }
            if (GUI.Button(new Rect(p.xMax - 32 - 140, y, 140, 34), "Cancel", MenuArt.Ghost(headFont))) gm.Leave();
        }

        void Hud(GameManager gm, float w, float h)
        {
            var player = gm.Player;

            // Side badge, room and inventory, top-left.
            var sideColor = Palette.ForSide(gm.Side);
            MenuArt.Glass(new Rect(12, 12, 250, 108), new Color(sideColor.r, sideColor.g, sideColor.b, 0.55f));
            MenuArt.Fill(new Rect(24, 12, 90, 3), sideColor);
            GUI.Label(new Rect(26, 16, 230, 26), $"SIDE {gm.Side}" + (gm.Offline ? "  · offline" : ""), new GUIStyle(label) { font = Art.Catalog != null ? Art.Catalog.titleFont : label.font, normal = { textColor = sideColor } });
            var room = player != null ? player.GetComponent<RoomTracker>().Current : null;
            GUI.Label(new Rect(26, 40, 230, 22), room != null ? $"Room: {room.Id}" + (room.IsDark ? " (dark)" : "") : "Room: —", small);
            var inv = gm.Inventory();
            Slot(new Rect(26, 66, 110, 44), "Bomb", inv.Any(i => i.ToLowerInvariant().Contains("bomb")));
            Slot(new Rect(142, 66, 110, 44), "Key", inv.Any(i => i.ToLowerInvariant().Contains("key")));

            if (gm.CanSwitchSide && gm.Current == GameManager.Phase.Playing &&
                HudButton(new Rect(270, 12, 150, 44), $"Play side {(gm.Side == "A" ? "B" : "A")}", MenuArt.Hud(Art.Catalog != null ? Art.Catalog.titleFont : null)))
                gm.SwitchSide();

            // Voice: partner speaking indicator and mute for incoming audio, top-right.
            var voice = gm.Voice;
            if (voice != null && voice.Active)
            {
                var vr = new Rect(w - 232, 72, 220, 44); // below the settings gear
                MenuArt.Glass(vr);
                bool speaking = voice.PartnerSpeaking && !voice.MuteIncoming;
                var dot = new Rect(vr.x + 12, vr.y + 16, 12, 12);
                MenuArt.Fill(dot, speaking ? Palette.ForSide(gm.Side == "A" ? "B" : "A") * (0.7f + 0.3f * Mathf.Sin(Time.time * 12f)) : new Color(1, 1, 1, 0.2f));
                GUI.Label(new Rect(vr.x + 30, vr.y, 110, vr.height), voice.Connected ? (speaking ? "Partner speaking" : "Voice on") : voice.Status, new GUIStyle(small) { alignment = TextAnchor.MiddleLeft });
                if (HudButton(new Rect(vr.xMax - 78, vr.y + 6, 70, 32), voice.MuteIncoming ? "Unmute" : "Mute", MenuArt.Hud(Art.Catalog != null ? Art.Catalog.titleFont : null)))
                    voice.MuteIncoming = !voice.MuteIncoming;
            }

            if (player != null && player.Debuffed)
                GUI.Label(new Rect(12, 126, 300, 24), $"Sluggish… {player.DebuffRemaining:0}s", new GUIStyle(label) { normal = { textColor = new Color(1f, 0.5f, 0.4f) } });

            // Beside the side badge (after "Play side" when offline); top-right belongs to status and voice.
            if (gm.Current == GameManager.Phase.Playing && !IsModal && !gm.EditorOpen &&
                HudButton(new Rect(gm.CanSwitchSide ? 428 : 270, 12, 150, 44), gm.Tips.Show ? "Tips: on" : "Tips: off", MenuArt.Hud(Art.Catalog != null ? Art.Catalog.titleFont : null)))
                gm.Tips.Show = !gm.Tips.Show;

            if (!string.IsNullOrEmpty(gm.Status) && !gm.Offline)
                GUI.Label(new Rect(w - 480, 12, 400, 26), gm.Status, new GUIStyle(small) { alignment = TextAnchor.UpperRight, normal = { textColor = new Color(1f, 0.7f, 0.4f) } });

            if (toast != null && Time.time < toastUntil)
            {
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((toastUntil - Time.time) * 2f));
                var tr = new Rect(w / 2 - 280, h - 66, 560, 46);
                MenuArt.Glass(tr, new Color(MenuArt.Cool.r, MenuArt.Cool.g, MenuArt.Cool.b, 0.5f), 0.85f);
                GUI.Label(tr, toast, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
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
            MenuArt.Glass(r, held ? MenuArt.Warm : new Color(1, 1, 1, 0.1f), held ? 0.85f : 0.45f);
            GUI.Label(r, name, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = held ? MenuArt.Ink : new Color(1, 1, 1, 0.35f) } });
        }

        void EndScreen(GameManager gm, float w, float h)
        {
            MenuArt.DrawBackdrop(new Rect(0, 0, w, h));
            MenuArt.DimScreen(new Rect(0, 0, w, h));
            var headFont = Art.Catalog != null ? Art.Catalog.titleFont : null;
            var p = new Rect(w / 2 - 260, h / 2 - 150, 520, 300);
            MenuArt.Panel(p);
            GUI.Label(new Rect(p.x, p.y + 36, p.width, 50), $"<color=#{ColorUtility.ToHtmlStringRGB(MenuArt.Cool)}>YOU</color> <color=#{ColorUtility.ToHtmlStringRGB(MenuArt.Warm)}>ESCAPED</color>",
                new GUIStyle(title) { font = headFont, fontSize = 46, richText = true, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(p.x + 30, p.y + 100, p.width - 60, 60), "Escape Successful. Nice teamwork.",
                new GUIStyle(big) { fontSize = 20, normal = { textColor = MenuArt.Dim } });
            if (GUI.Button(new Rect(p.x + 110, p.yMax - 96, p.width - 220, 58), "Back to menu", MenuArt.Primary(headFont))) gm.Leave();
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
            var headFont = Art.Catalog != null ? Art.Catalog.titleFont : null;
            MenuArt.DimScreen(new Rect(0, 0, w, h));
            MenuArt.Panel(r);                                                               // body: the theme's glass
            var face = new Rect(r.x + 14, r.y + 44, r.width - 28, r.height - 58);
            MenuArt.Glass(face, new Color(tint.r, tint.g, tint.b, 0.9f), 0.9f);            // face ringed in the side colour
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 30), "KEYPAD", new GUIStyle(label) { font = headFont, alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(r.xMax - 48, r.y + 8, 38, 32), "X", MenuArt.Hud(headFont))) keypad = null;

            // display strip
            var strip = new Rect(face.x + 16, face.y + 16, face.width - 32, 74);
            MenuArt.Glass(strip, new Color(MenuArt.Warm.r, MenuArt.Warm.g, MenuArt.Warm.b, 0.55f), 0.95f);
            GUI.Label(strip, typed.PadRight(length, '_'), new GUIStyle(digits) { fontSize = 50, normal = { textColor = MenuArt.Warm } });

            // four swatch slots: the order to read the partner's panels in; the next one is outlined
            int slots = Mathf.Max(4, length);
            float sw = 52f, gap = 12f, sx = face.center.x - (slots * sw + (slots - 1) * gap) / 2f, sy = strip.yMax + 18;
            for (int i = 0; i < slots; i++)
            {
                var cell = new Rect(sx + i * (sw + gap), sy, sw, sw);
                MenuArt.Glass(cell, null, 0.9f);
                bool used = order != null && i < order.Length;
                var colour = used ? Palette.ForName(order[i]) : new Color(0.95f, 0.95f, 0.96f, 0.35f);
                Fill(new Rect(cell.x + 6, cell.y + 6, cell.width - 12, cell.height - 12), colour);
                if (i < typed.Length)
                    GUI.Label(cell, typed[i].ToString(), new GUIStyle(digits) { fontSize = 26, normal = { textColor = colour.grayscale > 0.6f ? Color.black : Color.white } });
                if (i == typed.Length && i < length)
                    Outline(new Rect(cell.x - 3, cell.y - 3, cell.width + 6, cell.height + 6), new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f)), 2);
            }
            if (order != null && order.Length > 0)
                GUI.Label(new Rect(face.x, sy + sw + 4, face.width, 22), "Read your partner's panels in this order", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = MenuArt.Dim } });

            // 12 keys, 3 x 4
            var key = MenuArt.Keycap(headFont);
            float bw = 88, bh = 50, kg = 8, bx = face.center.x - (3 * bw + 2 * kg) / 2f, by = sy + sw + 34;
            for (int i = 0; i < 9; i++)
                if (GUI.Button(new Rect(bx + (i % 3) * (bw + kg), by + (i / 3) * (bh + kg), bw, bh), (i + 1).ToString(), key) && typed.Length < length)
                    typed += (i + 1);
            if (GUI.Button(new Rect(bx, by + 3 * (bh + kg), bw, bh), "DEL", MenuArt.Hud(headFont)) && typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
            if (GUI.Button(new Rect(bx + bw + kg, by + 3 * (bh + kg), bw, bh), "0", key) && typed.Length < length) typed += "0";
            GUI.enabled = typed.Length == length;
            if (GUI.Button(new Rect(bx + 2 * (bw + kg), by + 3 * (bh + kg), bw, bh), "OK", MenuArt.Primary(headFont))) SubmitKeypad();
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
            MenuArt.DimScreen(new Rect(0, 0, w, h));
            MenuArt.Panel(r);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 30), "The screen shows a code:", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(r.x, r.y + 56, r.width, 100), codePanel.Code, new GUIStyle(digits) { fontSize = 80, normal = { textColor = MenuArt.Warm } });
            GUI.Label(new Rect(r.x, r.y + 150, r.width, 24), "Your partner needs this.", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = MenuArt.Dim } });
            if (GUI.Button(new Rect(r.x + r.width / 2 - 70, r.yMax - 52, 140, 40), "Close", MenuArt.Hud(Art.Catalog != null ? Art.Catalog.titleFont : null))) codePanel = null;
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
