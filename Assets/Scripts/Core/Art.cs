using UnityEngine;

namespace EscapeOffice
{
    // 3D art from the Other Side pack, laid onto the 2D game plane.
    //
    // Gameplay stays in XY (Rigidbody2D, tile y up = map north); the camera looks down +Z, so
    // "up" off the floor is -Z. Pack models are Y-up with their front at +Z, so each one is
    // tilted -90° about X: model up → -Z (toward the camera), model front → +Y (map north).
    // Yaw is the pack README's rotation table: 0 faces north, 90 east, 180 south, -90 west.
    public static class Art
    {
        static ArtCatalog catalog;
        static bool loaded;

        public static ArtCatalog Catalog
        {
            get
            {
                if (!loaded)
                {
                    catalog = Resources.Load<ArtCatalog>("ArtCatalog");
                    loaded = true;
                }
                return catalog;
            }
        }

        public static bool Available => Catalog != null;

        // Floor tiles sit this far below the sprite plane (z = 0) so glows and overlays never z-fight.
        public const float FloorDepth = 0.02f;

        public static Quaternion Rotation(float yaw) => Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f);

        // Heading (degrees) that turns a model's front toward a 2D direction.
        public static float YawFor(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        public static GameObject Spawn(string prefab, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var source = Catalog != null ? Catalog.Prefab(prefab) : null;
            if (source == null) return null;
            var go = Object.Instantiate(source, parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Rotation(yaw);
            // Pack prefabs carry 3D colliders for a 3D game; this one collides in 2D.
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            return go;
        }

        // A pack prefab inside something already tilted onto the game plane (hero parts):
        // model space as authored, no extra tilt, 3D colliders stripped.
        public static GameObject SpawnRaw(string prefab, Transform parent)
        {
            var source = Catalog != null ? Catalog.Prefab(prefab) : null;
            if (source == null) return null;
            var go = Object.Instantiate(source, parent, false);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            return go;
        }

        public static Material Material(string name) => Catalog != null ? Catalog.Material(name) : null;
        public static Sprite Sprite(string name) => Catalog != null ? Catalog.Sprite(name) : null;

        // Yaw for a wall-mounted item at tile (x, y): back against the first neighbouring wall.
        public static float WallYaw(World world, int x, int y, float fallback = 0f)
        {
            if (world.IsWall(x, y + 1)) return 0f;   // wall south → face north
            if (world.IsWall(x, y - 1)) return 180f; // wall north → face south
            if (world.IsWall(x - 1, y)) return 90f;  // wall west → face east
            if (world.IsWall(x + 1, y)) return -90f; // wall east → face west
            return fallback;
        }

        // Doors and laser runs are authored along local X. Turn them when they span a
        // north-south gap (walls above and below) or run vertically. The front (the exit
        // door's floor sign) faces the floor side when only one side has floor.
        public static float SpanYaw(World world, int x, int y, int w, int h)
        {
            bool vertical;
            if (w != h) vertical = h > w;
            else
            {
                bool wallsNorthSouth = world.IsWall(x, y - 1) && world.IsWall(x, y + 1);
                bool wallsEastWest = world.IsWall(x - 1, y) && world.IsWall(x + 1, y);
                vertical = wallsNorthSouth && !wallsEastWest;
            }
            if (vertical) return !world.IsFloor(x + 1, y) && world.IsFloor(x - 1, y) ? -90f : 90f;
            return !world.IsFloor(x, y - 1) && world.IsFloor(x, y + 1) ? 180f : 0f;
        }

        public static Transform Find(GameObject model, string child)
        {
            if (model == null) return null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == child) return t;
            return null;
        }

        // Frame-rate independent smoothing, the pack's "k" values.
        public static float Smooth(float k) => 1f - Mathf.Exp(-k * Time.deltaTime);
    }
}
