using UnityEngine;

namespace EscapeOffice
{
    // Procedural placeholder sprites, all one world unit (one tile) across, so the game is
    // playable before any art lands. Prefabs in the ObjectCatalog replace them.
    public static class SpriteFactory
    {
        const int Size = 32;

        static Sprite square, circle, glow, triangle, diamond, ring;

        public static Sprite Square => square ??= Make("square", (x, y) => 1f);
        public static Sprite Circle => circle ??= Make("circle", (x, y) => Edge(0.5f - Len(x, y)));
        public static Sprite Ring => ring ??= Make("ring", (x, y) => Edge(0.5f - Len(x, y)) * Edge(Len(x, y) - 0.38f));
        public static Sprite Glow => glow ??= Make("glow", (x, y) =>
        {
            float d = Mathf.Clamp01(1f - Len(x, y) * 2f);
            return d * d;
        }, 64);
        public static Sprite Triangle => triangle ??= Make("triangle", (x, y) =>
        {
            // Upward triangle inside the unit square.
            float halfWidth = 0.5f * (1f - (y + 0.5f));
            return y > -0.45f && y < 0.45f && Mathf.Abs(x) < halfWidth ? 1f : 0f;
        });
        public static Sprite Diamond => diamond ??= Make("diamond", (x, y) => Mathf.Abs(x) + Mathf.Abs(y) < 0.48f ? 1f : 0f);

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);
        static float Edge(float v) => Mathf.Clamp01(v * Size);

        // f receives coordinates in [-0.5, 0.5] and returns alpha.
        static Sprite Make(string name, System.Func<float, float, float> f, int size = Size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = (px + 0.5f) / size - 0.5f;
                float y = (py + 0.5f) / size - 0.5f;
                byte a = (byte)(Mathf.Clamp01(f(x, y)) * 255);
                pixels[py * size + px] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }

        public static SpriteRenderer AddRenderer(GameObject go, Sprite sprite, Color color, int order)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Color color, int order,
            Vector2 localPos = default, Vector2? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale ?? Vector2.one;
            return AddRenderer(go, sprite, color, order);
        }
    }

    // Sorting orders shared across the scene.
    public static class Layers
    {
        public const int Floor = 0;
        public const int Wall = 1;
        public const int RoomOverlay = 2;
        public const int Glow = 4;
        public const int Object = 5;
        public const int ObjectTop = 6;
        public const int Actor = 10;
        public const int Fx = 20;
        public const int Vision = 100;
    }
}
