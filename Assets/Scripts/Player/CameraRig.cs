using UnityEngine;

namespace EscapeOffice
{
    // Follows the player. A mask darkens everything beyond the vision radius, which shrinks to
    // the dark-room radius when the lights are off and further while the boss debuff lasts.
    // Burning fire punches extra holes so it is visible from further away.
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        const int MaxLights = 16;
        public float follow = 10f;
        public float radiusLerp = 4f;
        public float softness = 1.2f;

        Camera cam;
        SpriteRenderer mask;
        Material maskMaterial;
        float radius = 8f;
        readonly Vector4[] lights = new Vector4[MaxLights];

        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int LightsId = Shader.PropertyToID("_Lights");
        static readonly int LightCountId = Shader.PropertyToID("_LightCount");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var shader = Shader.Find("EscapeOffice/VisionMask");
            if (shader == null)
            {
                Debug.LogWarning("[camera] VisionMask shader missing; the vision radius will not be drawn.");
                return;
            }
            maskMaterial = new Material(shader);
            maskMaterial.SetColor(ColorId, Palette.Darkness);
            mask = SpriteFactory.Child(transform, "VisionMask", SpriteFactory.Square, Color.white, Layers.Vision);
            mask.transform.localPosition = new Vector3(0, 0, 1f); // just past the near plane
            mask.sharedMaterial = maskMaterial;
            mask.enabled = false;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            var player = gm.Player;
            var world = gm.World;
            if (player == null || world == null || world.Width == 0)
            {
                if (mask != null) mask.enabled = false;
                return;
            }

            var settings = world.Camera;
            var room = player.GetComponent<RoomTracker>().Current;
            bool dark = room != null && room.IsDark;
            float target = (dark ? settings.DarkRadius : settings.Radius) * player.RadiusFactor;
            radius = Mathf.Lerp(radius, target, 1f - Mathf.Exp(-radiusLerp * Time.deltaTime));

            cam.orthographicSize = settings.Radius + 0.5f;
            var p = (Vector3)player.Position;
            var pos = Vector3.Lerp(transform.position, new Vector3(p.x, p.y, -10f), 1f - Mathf.Exp(-follow * Time.deltaTime));
            pos.z = -10f;
            transform.position = pos;

            if (mask == null) return;
            mask.enabled = !gm.DebugNoFog;
            float h = cam.orthographicSize * 2f + 2f;
            mask.transform.localScale = new Vector3(h * cam.aspect + 2f, h, 1f);

            maskMaterial.SetVector(CenterId, new Vector4(p.x, p.y, radius, softness));
            int n = 0;
            foreach (var l in world.Lights)
            {
                if (n >= MaxLights) break;
                if (l.z > 0f) lights[n++] = new Vector4(l.x, l.y, l.z, 1.5f);
            }
            for (int i = n; i < MaxLights; i++) lights[i] = Vector4.zero;
            maskMaterial.SetVectorArray(LightsId, lights);
            maskMaterial.SetInt(LightCountId, n);
        }

        // Is a world point outside the visible area? Used for edge-of-screen cue indicators.
        public bool IsOffscreen(Vector2 worldPos)
        {
            var player = GameManager.Instance.Player;
            if (player != null && Vector2.Distance(worldPos, player.Position) > radius) return true;
            var vp = cam.WorldToViewportPoint(worldPos);
            return vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1;
        }

        public Camera Camera => cam;
    }
}
