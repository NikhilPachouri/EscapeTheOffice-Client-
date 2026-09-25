using UnityEngine;

namespace EscapeOffice.UI
{
    // Start-screen look, matched to the key art (Resources/UI/MenuBackground): cool navy on the
    // left, warm orange on the right. Rounded glass panels, a blue-to-orange primary button and
    // outlined secondary buttons, all 9-sliced procedural textures so they scale cleanly.
    public static class MenuArt
    {
        public static readonly Color Cool = new Color(0.36f, 0.55f, 1f);     // left half of the art
        public static readonly Color Warm = new Color(1f, 0.52f, 0.18f);     // right half
        public static readonly Color Ink = new Color(0.95f, 0.95f, 0.93f);
        public static readonly Color Dim = new Color(0.72f, 0.75f, 0.82f);

        static Texture2D background, shade, glass, divider;
        static GUIStyle primary, secondary, ghost, field, titleStyle;

        public static Texture2D Background => background != null ? background : background = Resources.Load<Texture2D>("UI/MenuBackground");

        // The art, cropped to fill the screen, with a navy wash on the left for the menu.
        public static void DrawBackdrop(Rect screen)
        {
            if (Background != null) GUI.DrawTexture(screen, Background, ScaleMode.ScaleAndCrop);
            else Fill(screen, new Color(0.05f, 0.06f, 0.12f));
            shade ??= Horizontal(256, 4, new Color(0.03f, 0.04f, 0.1f, 0.88f), new Color(0.03f, 0.04f, 0.1f, 0f));
            GUI.DrawTexture(new Rect(screen.x, screen.y, screen.width * 0.6f, screen.height), shade);
        }

        public static void Panel(Rect r)
        {
            glass ??= Rounded(64, 64, 18, new Color(0.04f, 0.05f, 0.11f, 0.78f), new Color(1f, 1f, 1f, 0.12f), null);
            GUI.Box(r, "", new GUIStyle { normal = { background = glass }, border = new RectOffset(20, 20, 20, 20) });
            // Two-tone accent along the top: the two sides of the building.
            divider ??= Horizontal(256, 4, Cool, Warm);
            GUI.DrawTexture(new Rect(r.x + 24, r.y + 1, r.width - 48, 3), divider);
        }

        public static void Title(Rect r, Font font, int size)
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleLeft };
            titleStyle.font = font;
            titleStyle.fontSize = size;
            GUI.Label(r, $"<color=#{ColorUtility.ToHtmlStringRGB(Cool)}>THE OTHER</color> <color=#{ColorUtility.ToHtmlStringRGB(Warm)}>SIDE</color>", titleStyle);
        }

        public static void Divider(Rect r, string text, GUIStyle small)
        {
            var mid = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Dim } };
            float tw = mid.CalcSize(new GUIContent(text)).x + 24;
            Fill(new Rect(r.x, r.center.y, (r.width - tw) / 2, 1), new Color(1, 1, 1, 0.15f));
            Fill(new Rect(r.xMax - (r.width - tw) / 2, r.center.y, (r.width - tw) / 2, 1), new Color(1, 1, 1, 0.15f));
            GUI.Label(r, text, mid);
        }

        // Blue-to-orange filled button, the main call to action.
        public static GUIStyle Primary(Font font)
        {
            if (primary != null) return primary;
            primary = Button(font, 22,
                Rounded(128, 48, 14, null, new Color(1, 1, 1, 0.25f), new[] { Cool * 0.9f, Warm * 0.9f }),
                Rounded(128, 48, 14, null, new Color(1, 1, 1, 0.55f), new[] { Cool, Warm }),
                Rounded(128, 48, 14, null, new Color(1, 1, 1, 0.4f), new[] { Cool * 0.7f, Warm * 0.7f }));
            return primary;
        }

        // Outlined glass button with a warm edge (Join).
        public static GUIStyle Secondary(Font font)
        {
            if (secondary != null) return secondary;
            secondary = Button(font, 20,
                Rounded(128, 48, 14, new Color(0.08f, 0.09f, 0.16f, 0.85f), Warm * 0.85f, null),
                Rounded(128, 48, 14, new Color(0.16f, 0.12f, 0.12f, 0.9f), Warm, null),
                Rounded(128, 48, 14, new Color(0.05f, 0.05f, 0.1f, 0.9f), Warm, null));
            return secondary;
        }

        // Quiet chip for the extras (tutorial, offline, editor, rejoin).
        public static GUIStyle Ghost(Font font)
        {
            if (ghost != null) return ghost;
            ghost = Button(font, 16,
                Rounded(128, 40, 12, new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.14f), null),
                Rounded(128, 40, 12, new Color(1, 1, 1, 0.12f), new Color(1, 1, 1, 0.35f), null),
                Rounded(128, 40, 12, new Color(1, 1, 1, 0.04f), new Color(1, 1, 1, 0.4f), null));
            ghost.normal.textColor = Dim;
            return ghost;
        }

        // In-game button: the same glass as Ghost but solid enough to read over the level.
        public static GUIStyle Hud(Font font)
        {
            if (hud != null) return hud;
            hud = Button(font, 16,
                Rounded(128, 40, 12, new Color(0.04f, 0.05f, 0.11f, 0.86f), new Color(1, 1, 1, 0.18f), null),
                Rounded(128, 40, 12, new Color(0.07f, 0.09f, 0.18f, 0.92f), Cool, null),
                Rounded(128, 40, 12, new Color(0.16f, 0.09f, 0.05f, 0.95f), Warm, null));
            return hud;
        }
        static GUIStyle hud;

        public static GUIStyle Field(Font font)
        {
            if (field != null) return field;
            field = new GUIStyle(GUI.skin.textField)
            {
                font = font, fontSize = 26, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(16, 16, 16, 16), padding = new RectOffset(14, 14, 4, 4),
                normal = { background = Rounded(128, 48, 14, new Color(0.02f, 0.03f, 0.07f, 0.9f), new Color(1, 1, 1, 0.18f), null), textColor = Ink },
                focused = { background = Rounded(128, 48, 14, new Color(0.03f, 0.04f, 0.09f, 0.95f), Cool, null), textColor = Ink },
                hover = { background = Rounded(128, 48, 14, new Color(0.03f, 0.04f, 0.09f, 0.9f), new Color(1, 1, 1, 0.3f), null), textColor = Ink },
            };
            return field;
        }

        // Small rounded glass for HUD elements; `edge` tints the outline (e.g. the side colour).
        public static void Glass(Rect r, Color? edge = null, float alpha = 0.72f)
        {
            var key = (edge ?? new Color(1, 1, 1, 0.12f), alpha);
            if (!chips.TryGetValue(key, out var tex))
                chips[key] = tex = Rounded(48, 48, 12, new Color(0.04f, 0.05f, 0.11f, alpha), key.Item1, null);
            GUI.Box(r, "", new GUIStyle { normal = { background = tex }, border = new RectOffset(14, 14, 14, 14) });
        }
        static readonly System.Collections.Generic.Dictionary<(Color, float), Texture2D> chips =
            new System.Collections.Generic.Dictionary<(Color, float), Texture2D>();

        // Navy wash behind a modal.
        public static void DimScreen(Rect screen) => Fill(screen, new Color(0.02f, 0.03f, 0.08f, 0.6f));

        // Key on the keypad: glass with a cool edge that warms on press.
        public static GUIStyle Keycap(Font font)
        {
            if (keycap != null) return keycap;
            keycap = Button(font, 26,
                Rounded(96, 64, 14, new Color(0.07f, 0.08f, 0.15f, 0.92f), new Color(Cool.r, Cool.g, Cool.b, 0.45f), null),
                Rounded(96, 64, 14, new Color(0.1f, 0.12f, 0.22f, 0.95f), Cool, null),
                Rounded(96, 64, 14, new Color(0.18f, 0.1f, 0.06f, 0.95f), Warm, null));
            return keycap;
        }
        static GUIStyle keycap;

        // On/off switch track: the two-tone gradient when on, dim glass when off.
        public static Texture2D SwitchOn => switchOn ??= Rounded(128, 48, 23, null, new Color(1, 1, 1, 0.3f), new[] { Cool, Warm });
        public static Texture2D SwitchOff => switchOff ??= Rounded(128, 48, 23, new Color(1, 1, 1, 0.1f), new Color(1, 1, 1, 0.2f), null);
        public static Texture2D Knob => knob ??= Rounded(48, 48, 23, Ink, new Color(1, 1, 1, 0f), null);
        static Texture2D switchOn, switchOff, knob;

        // Standard IMGUI buttons/boxes/fields in the same language (used by UiSkin for every screen).
        public static void SkinButton(GUIStyle b)
        {
            b.normal.background = Rounded(128, 48, 12, new Color(0.07f, 0.08f, 0.15f, 0.88f), new Color(1, 1, 1, 0.16f), null);
            b.hover.background = Rounded(128, 48, 12, new Color(0.1f, 0.12f, 0.22f, 0.92f), new Color(Cool.r, Cool.g, Cool.b, 0.8f), null);
            b.active.background = Rounded(128, 48, 12, new Color(0.18f, 0.1f, 0.06f, 0.95f), Warm, null);
            b.border = new RectOffset(14, 14, 14, 14);
            b.padding = new RectOffset(12, 12, 4, 4);
            b.onNormal = b.normal; b.onHover = b.hover; b.onActive = b.active;
        }

        public static void SkinBox(GUIStyle box)
        {
            box.normal.background = Rounded(48, 48, 12, new Color(0.04f, 0.05f, 0.11f, 0.8f), new Color(1, 1, 1, 0.12f), null);
            box.border = new RectOffset(14, 14, 14, 14);
        }

        public static void SkinField(GUIStyle f)
        {
            f.normal.background = Rounded(128, 48, 12, new Color(0.02f, 0.03f, 0.07f, 0.9f), new Color(1, 1, 1, 0.18f), null);
            f.hover.background = Rounded(128, 48, 12, new Color(0.03f, 0.04f, 0.09f, 0.9f), new Color(1, 1, 1, 0.3f), null);
            f.focused.background = Rounded(128, 48, 12, new Color(0.03f, 0.04f, 0.09f, 0.95f), Cool, null);
            f.border = new RectOffset(14, 14, 14, 14);
            f.padding = new RectOffset(12, 12, 4, 4);
        }

        public static void Icon(Rect r, string sprite)
        {
            var s = Art.Sprite(sprite);
            if (s != null) GUI.DrawTexture(r, s.texture, ScaleMode.ScaleToFit);
        }

        // ---------------------------------------------------------------- textures

        static GUIStyle Button(Font font, int size, Texture2D normal, Texture2D hover, Texture2D active) => new GUIStyle
        {
            font = font, fontSize = size, alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(16, 16, 16, 16), padding = new RectOffset(12, 12, 4, 4),
            normal = { background = normal, textColor = Ink },
            hover = { background = hover, textColor = Color.white },
            active = { background = active, textColor = Color.white },
        };

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static Texture2D Horizontal(int w, int h, Color left, Color right)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) px[y * w + x] = Color.Lerp(left, right, x / (w - 1f));
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // Rounded rectangle: solid `fill` or a left-to-right `gradient`, with an antialiased edge.
        static Texture2D Rounded(int w, int h, float radius, Color? fill, Color edge, Color[] gradient)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                float d = radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)); // >0 inside
                float inside = Mathf.Clamp01(d);
                var c = gradient != null ? Color.Lerp(gradient[0], gradient[1], x / (w - 1f)) : fill ?? Color.clear;
                if (gradient != null) c.a = 1f;
                float rim = Mathf.Clamp01(1.5f - d); // ~1.5 px outline
                c = Color.Lerp(c, new Color(edge.r, edge.g, edge.b, Mathf.Max(c.a, edge.a)), rim * edge.a);
                c.a *= inside;
                px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
