using UnityEngine;

namespace EscapeOffice.UI
{
    // Gear button (top-right, every screen) and the settings panel: music and SFX switches
    // (saved), the camera & vision tuner, exit/leave (with confirm) and close. Android's back button toggles it.
    // On the menu the button quits the app; inside a game it becomes "Leave game", which ends the
    // game for both players (the server frees the room) and returns to the menu.
    // Music plays Resources/Music/theme if present, otherwise a generated ambient loop.
    public class SettingsMenu : MonoBehaviour
    {
        const string PrefMusic = "eto.music", PrefSfx = "eto.sfx";
        const float RefHeight = 720f;

        public static bool MusicOn { get; private set; } = true;
        public static bool SfxOn { get; private set; } = true;
        public static bool IsOpen { get; private set; }

        static readonly Color Panel = new Color32(0x14, 0x18, 0x1d, 0xf2);
        static readonly Color Ink = new Color32(0xe8, 0xee, 0xf2, 0xff);

        AudioSource music;
        bool confirmExit;
        GUIStyle title, rowLabel, button, small;

        void Awake()
        {
            MusicOn = PlayerPrefs.GetInt(PrefMusic, 1) == 1;
            SfxOn = PlayerPrefs.GetInt(PrefSfx, 1) == 1;

            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.volume = 0.35f;
            music.clip = Resources.Load<AudioClip>("Music/theme") ?? AmbientLoop();
            if (MusicOn) music.Play();

        }

        void Update()
        {
            // Android back button (Escape): open/close the menu.
            var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            bool keypadOpen = ui != null && ui.IsModal && !IsOpen; // Escape closes the keypad first
            if (Input.GetKeyDown(KeyCode.Escape) && !keypadOpen) { IsOpen = !IsOpen; confirmExit = false; }
        }

        public static void Open() { IsOpen = true; }
        public static void Close() { IsOpen = false; }

        void SetMusic(bool on)
        {
            MusicOn = on;
            PlayerPrefs.SetInt(PrefMusic, on ? 1 : 0);
            PlayerPrefs.Save();
            if (on && !music.isPlaying) music.Play();
            if (!on) music.Pause();
        }

        void SetSfx(bool on)
        {
            SfxOn = on;
            PlayerPrefs.SetInt(PrefSfx, on ? 1 : 0);
            PlayerPrefs.Save();
            if (on) GameManager.Instance.Fx.PlayAt("click", null);
        }

        void OnGUI()
        {
            GUI.depth = -10; // above the HUD
            Styles();
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = RefHeight;

            // Gear button, top-right.
            var gear = new Rect(w - 64, 12, 52, 52);
            MenuArt.Glass(gear, null, 0.8f);
            if (GUI.Button(gear, "", GUIStyle.none)) { IsOpen = !IsOpen; confirmExit = false; }
            DrawGear(gear.center, 17f, scale);

            if (!IsOpen) return;

            // Dim everything behind; tapping outside the panel closes it.
            MenuArt.DimScreen(new Rect(0, 0, w, h));
            var p = new Rect(w / 2 - 230, h / 2 - 205, 460, 410); // room for Camera & vision
            if (Event.current.type == EventType.MouseDown && !p.Contains(Event.current.mousePosition) && !gear.Contains(Event.current.mousePosition))
            {
                IsOpen = false;
                Event.current.Use();
                return;
            }

            MenuArt.Panel(p);
            var side = Palette.ForSide(GameManager.Instance.Side);
            GUI.Label(new Rect(p.x + 28, p.y + 20, p.width - 100, 44), "SETTINGS", title);
            if (GUI.Button(new Rect(p.xMax - 64, p.y + 18, 44, 44), "X", MenuArt.Hud(title.font))) { IsOpen = false; return; }

            float y = p.y + 90;
            if (Switch(new Rect(p.x + 28, y, p.width - 56, 64), "Music", MusicOn, side)) SetMusic(!MusicOn);
            y += 76;
            if (Switch(new Rect(p.x + 28, y, p.width - 56, 64), "Sound effects", SfxOn, side)) SetSfx(!SfxOn);
            y += 80;
            if (GUI.Button(new Rect(p.x + 28, y, p.width - 56, 52), "Camera & vision", MenuArt.Hud(title.font))) { IsOpen = false; ViewTuner.IsOpen = true; return; }
            y += 80;

            var gm = GameManager.Instance;
            bool inGame = gm != null && gm.Current != GameManager.Phase.Join;
            if (confirmExit)
            {
                string question = !inGame ? "Exit the game?"
                    : gm.Offline || gm.Current == GameManager.Phase.Complete ? "Leave and return to the menu?"
                    : "Leave? This ends the game for your partner too.";
                GUI.Label(new Rect(p.x, y, p.width, 30), question, new GUIStyle(rowLabel) { alignment = TextAnchor.MiddleCenter });
                if (GUI.Button(new Rect(p.x + 40, y + 40, 180, 52), "Cancel", MenuArt.Hud(title.font))) confirmExit = false;
                if (GUI.Button(new Rect(p.xMax - 220, y + 40, 180, 52), inGame ? "Leave" : "Exit", MenuArt.Secondary(title.font)))
                {
                    confirmExit = false;
                    if (inGame) { IsOpen = false; gm.Leave(); }
                    else Quit();
                }
                return;
            }
            if (GUI.Button(new Rect(p.x + 28, y, p.width - 56, 52), inGame ? "Leave game" : "Exit game", MenuArt.Secondary(title.font))) confirmExit = true;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // A row with a label and an on/off pill; returns true when tapped.
        bool Switch(Rect r, string text, bool on, Color accent)
        {
            MenuArt.Glass(r, null, 0.45f);
            GUI.Label(new Rect(r.x + 18, r.y, r.width - 150, r.height), text, rowLabel);
            var pr = new Rect(r.xMax - 118, r.y + (r.height - 40) / 2, 100, 40);
            var old = GUI.color;
            GUI.DrawTexture(pr, on ? MenuArt.SwitchOn : MenuArt.SwitchOff); // two-tone when on
            GUI.color = Ink;
            float kx = on ? pr.xMax - 36 : pr.x + 4;
            GUI.DrawTexture(new Rect(kx, pr.y + 4, 32, 32), MenuArt.Knob);
            GUI.color = old;
            GUI.Label(new Rect(pr.x - 58, r.y, 50, r.height), on ? "ON" : "OFF", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, normal = { textColor = on ? Ink : new Color(1, 1, 1, 0.45f) } });
            return GUI.Button(r, "", GUIStyle.none);
        }

        void DrawGear(Vector2 c, float r, float scale)
        {
            var old = GUI.color;
            var matrix = GUI.matrix;
            GUI.color = Ink;
            var tooth = SpriteFactory.Square.texture;
            for (int i = 0; i < 8; i++)
            {
                GUIUtility.RotateAroundPivot(45f, c * scale); // pivot in screen pixels
                GUI.DrawTexture(new Rect(c.x - 3.5f, c.y - r - 3, 7, 10), tooth);
            }
            GUI.matrix = matrix;
            GUI.DrawTexture(new Rect(c.x - r * 0.8f, c.y - r * 0.8f, r * 1.6f, r * 1.6f), SpriteFactory.Circle.texture);
            GUI.color = new Color(0.08f, 0.09f, 0.11f);
            GUI.DrawTexture(new Rect(c.x - r * 0.35f, c.y - r * 0.35f, r * 0.7f, r * 0.7f), SpriteFactory.Circle.texture);
            GUI.color = old;
        }

        void Styles()
        {
            if (title != null) return;
            var font = Art.Catalog != null ? Art.Catalog.codeFont : null;
            var text = Art.Catalog != null ? Art.Catalog.uiFont : null;
            var bold = font != null ? FontStyle.Normal : FontStyle.Bold; // the font carries the weight
            title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = bold, alignment = TextAnchor.MiddleLeft, font = font, normal = { textColor = Ink } };
            rowLabel = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleLeft, font = text, normal = { textColor = Ink } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = bold, font = font };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20, font = font };
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // Pill/circle texture (fully rounded ends).
        static Texture2D Rounded(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float rad = h / 2f;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, rad, w - rad);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, rad));
                px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(rad - d));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Quiet ambient pad: Am - F - C - G, 4 s each, loops seamlessly.
        static AudioClip AmbientLoop()
        {
            const int rate = 22050;
            const float chordLen = 4f;
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f, 110.00f }, // Am
                new[] { 174.61f, 220.00f, 261.63f, 87.31f },  // F
                new[] { 196.00f, 261.63f, 329.63f, 130.81f }, // C
                new[] { 196.00f, 246.94f, 293.66f, 98.00f },  // G
            };
            int per = (int)(rate * chordLen);
            var data = new float[per * chords.Length];
            for (int c = 0; c < chords.Length; c++)
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Sin(Mathf.PI * t / chordLen); // swell in and out, zero at the seams
                float s = 0f;
                var f = chords[c];
                for (int k = 0; k < 3; k++)
                    s += Mathf.Sin(2f * Mathf.PI * f[k] * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f[k] * 1.003f * t) * 0.5f;
                s += Mathf.Sin(2f * Mathf.PI * f[3] * t) * 0.8f;
                float shimmer = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                data[c * per + i] = s * env * shimmer * 0.06f;
            }
            var clip = AudioClip.Create("ambient", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
