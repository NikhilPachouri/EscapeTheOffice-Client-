using UnityEngine;

namespace EscapeOffice
{
    // Fixed per player on both screens so the two players share a vocabulary
    // ("the orange lever affects you").
    public static class Palette
    {
        public static readonly Color SideA = new Color(1.00f, 0.55f, 0.10f); // orange
        public static readonly Color SideB = new Color(0.18f, 0.66f, 1.00f); // blue
        public static readonly Color Both = new Color(0.74f, 0.38f, 1.00f);  // purple
        public static readonly Color Info = new Color(1.00f, 0.92f, 0.45f);  // pale yellow, code panels

        public static readonly Color Floor = new Color(0.20f, 0.21f, 0.24f);
        public static readonly Color Wall = new Color(0.46f, 0.47f, 0.52f);
        public static readonly Color Darkness = new Color(0.01f, 0.01f, 0.02f, 0.97f);
        public static readonly Color Water = new Color(0.10f, 0.35f, 0.85f, 0.65f);
        public static readonly Color Unknown = Color.magenta;

        public enum Tag { None, A, B, Both, Info }

        public static Tag ParseTag(string color)
        {
            switch ((color ?? "").Trim().ToLowerInvariant())
            {
                case "a": return Tag.A;
                case "b": return Tag.B;
                case "both": case "ab": case "all": return Tag.Both;
                case "info": return Tag.Info;
                default: return Tag.None;
            }
        }

        public static Color ForTag(Tag tag)
        {
            switch (tag)
            {
                case Tag.A: return SideA;
                case Tag.B: return SideB;
                case Tag.Both: return Both;
                case Tag.Info: return Info;
                default: return Color.white;
            }
        }

        public static Color ForSide(string side) => side == "B" ? SideB : SideA;

        // Colorblind support: each glow colour also gets a shape.
        public static Sprite IconFor(Tag tag)
        {
            switch (tag)
            {
                case Tag.A: return SpriteFactory.Triangle;
                case Tag.B: return SpriteFactory.Square;
                case Tag.Both: return SpriteFactory.Diamond;
                case Tag.Info: return SpriteFactory.Circle;
                default: return null;
            }
        }
    }
}
