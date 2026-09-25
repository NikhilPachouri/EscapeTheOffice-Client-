using UnityEngine;

namespace EscapeOffice.UI
{
    // "View" tuning panel: camera distance/angle/FOV and the dark vision circle, live while playing.
    // Opens from Settings ("Camera & vision") or F3. Not modal, so you can walk around with it
    // open; values are saved on release (ViewTuning) and Copy JSON puts them on the clipboard.
    public class ViewTuner : MonoBehaviour
    {
        const float RefHeight = 720f;
        const float W = 360f;

        public static bool IsOpen { get; set; }

        static readonly Color Panel = new Color32(0x14, 0x18, 0x1d, 0xe6);
        static readonly Color Ink = new Color32(0xe8, 0xee, 0xf2, 0xff);

        GUIStyle title, header, row, value, button, slider, thumb;
        Vector2 scroll;
        bool dirty;
        string copied;
        float copiedUntil;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) IsOpen = !IsOpen;
            // Save once the drag ends rather than every frame of it.
            if (dirty && !Input.GetMouseButton(0) && Input.touchCount == 0)
            {
                ViewTuning.Save();
                dirty = false;
            }
        }

        void OnGUI()
        {
            if (!IsOpen || SettingsMenu.IsOpen) return;
            ViewTuning.EnsureLoaded();
            Styles();
            GUI.depth = -5;
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));

            var p = new Rect(12, 76, W, RefHeight - 88);
            TouchControls.Block(p); // drags on the panel never become the joystick
            Fill(p, Panel);
            Fill(new Rect(p.x, p.y, p.width, 4), Palette.ForSide(GameManager.Instance.Side));
            GUI.Label(new Rect(p.x + 16, p.y + 10, p.width - 80, 36), "VIEW", title);
            if (GUI.Button(new Rect(p.xMax - 52, p.y + 10, 40, 36), "X", button)) { IsOpen = false; return; }

            var body = new Rect(p.x + 8, p.y + 54, p.width - 16, p.height - 54 - 64);
            var content = new Rect(0, 0, body.width - 20, 3 * 32f + 12 * 50f);
            scroll = GUI.BeginScrollView(body, scroll, content);
            float y = 0f, w = content.width;
            GUI.changed = false;

            Header(ref y, w, "Camera");
            ViewTuning.Distance = Slider(ref y, w, "Distance", ViewTuning.Distance, 0.4f, 2.5f, "x0.00");
            ViewTuning.Height = Slider(ref y, w, "Height", ViewTuning.Height, 5f, 40f, "0.0");
            ViewTuning.Behind = Slider(ref y, w, "Behind (tilt)", ViewTuning.Behind, 0f, 30f, "0.0");
            ViewTuning.Yaw = Slider(ref y, w, "Side angle", ViewTuning.Yaw, -90f, 90f, "0°");
            ViewTuning.Fov = Slider(ref y, w, "Field of view", ViewTuning.Fov, 15f, 90f, "0");
            ViewTuning.Follow = Slider(ref y, w, "Follow speed", ViewTuning.Follow, 1f, 30f, "0.0");

            var cam = GameManager.Instance.World != null ? GameManager.Instance.World.Camera : null;
            Header(ref y, w, "Vision circle");
            ViewTuning.VisionScale = Slider(ref y, w, "Circle size", ViewTuning.VisionScale, 0.3f, 3f, "x0.00",
                cam != null ? $"{cam.Radius * ViewTuning.VisionScale:0.0} tiles" : null);
            ViewTuning.DarkScale = Slider(ref y, w, "Dark-room size", ViewTuning.DarkScale, 0.3f, 4f, "x0.00",
                cam != null ? $"{cam.DarkRadius * ViewTuning.DarkScale:0.0} tiles" : null);
            ViewTuning.Softness = Slider(ref y, w, "Edge softness", ViewTuning.Softness, 0.05f, 6f, "0.00");
            ViewTuning.FogAlpha = Slider(ref y, w, "Darkness", ViewTuning.FogAlpha, 0f, 1f, "0.00");

            Header(ref y, w, "Screen edges");
            ViewTuning.Vignette = Slider(ref y, w, "Vignette", ViewTuning.Vignette, 0f, 1f, "0.00");
            ViewTuning.VignetteStart = Slider(ref y, w, "Vignette start", ViewTuning.VignetteStart, 0f, 1f, "0.00");
            if (GUI.changed) dirty = true;
            GUI.EndScrollView();

            // Footer: fog toggle for comparing, reset, copy.
            float by = p.yMax - 56, bw = (p.width - 16 - 16) / 3f;
            var gm = GameManager.Instance;
            if (GUI.Button(new Rect(p.x + 8, by, bw, 44), gm.DebugNoFog ? "Fog: off" : "Fog: on", button)) gm.DebugNoFog = !gm.DebugNoFog;
            if (GUI.Button(new Rect(p.x + 16 + bw, by, bw, 44), "Reset", button)) { ViewTuning.Reset(); dirty = false; }
            if (GUI.Button(new Rect(p.x + 24 + bw * 2, by, bw, 44), Time.unscaledTime < copiedUntil ? copied : "Copy JSON", button))
            {
                GUIUtility.systemCopyBuffer = ViewTuning.ToJson(cam);
                Debug.Log("[view] tuned values:\n" + GUIUtility.systemCopyBuffer);
                copied = "Copied";
                copiedUntil = Time.unscaledTime + 1.5f;
            }
        }

        void Header(ref float y, float w, string text)
        {
            y += 6f;
            GUI.Label(new Rect(8, y, w - 16, 22), text.ToUpperInvariant(), header);
            y += 26f;
        }

        float Slider(ref float y, float w, string label, float v, float min, float max, string fmt, string extra = null)
        {
            GUI.Label(new Rect(8, y, w * 0.6f, 20), label, row);
            string shown = v.ToString(fmt) + (extra != null ? $"  ({extra})" : "");
            GUI.Label(new Rect(w * 0.4f, y, w * 0.6f - 8, 20), shown, value);
            v = GUI.HorizontalSlider(new Rect(8, y + 22, w - 16, 20), v, min, max, slider, thumb);
            y += 50f;
            return v;
        }

        void Styles()
        {
            if (title != null) return;
            var font = Art.Catalog != null ? Art.Catalog.codeFont : null;
            var text = Art.Catalog != null ? Art.Catalog.uiFont : null;
            var bold = font != null ? FontStyle.Normal : FontStyle.Bold; // the font carries the weight
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = bold, alignment = TextAnchor.MiddleLeft, font = font, normal = { textColor = Ink } };
            header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = bold, font = font, normal = { textColor = new Color(1, 1, 1, 0.5f) } };
            row = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft, font = text, normal = { textColor = Ink } };
            value = new GUIStyle(row) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1, 1, 1, 0.7f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, font = font };
            slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 10, margin = new RectOffset() };
            thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = 22, fixedWidth = 22 };
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
