using UnityEngine;

namespace EscapeOffice.UI
{
    // Restrained, world-matched look for the IMGUI screens: the pack's fonts (Chakra Petch for
    // headings, buttons and digits, Barlow for text), dark glass panels with a hairline edge,
    // and buttons that brighten a little on hover. Layout and behaviour stay in GameUI.
    public static class UiSkin
    {
        static readonly Color Text = new Color(0.9f, 0.9f, 0.88f);
        static readonly Color TextDim = new Color(0.62f, 0.64f, 0.66f);

        public static void Apply(GUIStyle title, GUIStyle big, GUIStyle label, GUIStyle small, GUIStyle box,
            GUIStyle button, GUIStyle field, GUIStyle digits)
        {
            var cat = Art.Catalog;
            if (cat == null) return;

            foreach (var s in new[] { title, button, digits }) Font(s, cat.titleFont);
            Font(digits, cat.codeFont);
            foreach (var s in new[] { big, label, small, box, field }) Font(s, cat.uiFont);

            title.normal.textColor = Text;
            big.normal.textColor = Text;
            label.normal.textColor = Text;
            small.normal.textColor = TextDim;
            title.fontStyle = digits.fontStyle = FontStyle.Normal; // the fonts carry the weight

            // Rounded glass from the start screen, so every screen shares one look.
            MenuArt.SkinBox(box);
            box.normal.textColor = Text;
            MenuArt.SkinButton(button);
            button.normal.textColor = button.hover.textColor = button.active.textColor = Text;
            button.onNormal = button.normal; button.onHover = button.hover; button.onActive = button.active;
            MenuArt.SkinField(field);
            field.normal.textColor = field.focused.textColor = Text;
        }

        static void Font(GUIStyle s, Font f)
        {
            if (f != null) s.font = f;
        }

        // 8x8 fill with a one-pixel edge (9-sliced by the style's border).
        static Texture2D Panel(Color fill, Color edge)
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[64];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                px[y * 8 + x] = x == 0 || y == 0 || x == 7 || y == 7 ? Color.Lerp(fill, new Color(edge.r, edge.g, edge.b, 1f), edge.a) : fill;
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
