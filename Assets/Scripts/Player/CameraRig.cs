using UnityEngine;

namespace EscapeOffice
{
    // Follows the player. A mask darkens everything beyond the vision radius, which shrinks to
    // the dark-room radius when the lights are off and further while the boss debuff lasts.
    // Burning fire punches extra holes so it is visible from further away.
    //
    // With the 3D asset pack the camera is the prototype's: perspective, 22 m above and 8 m
    // behind the player (scaled with the vision radius), and the mask becomes a quad hovering
    // over the level that the shader projects onto the floor.
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        const int MaxLights = 16;
        const float Height = 22f, Behind = 8f, Fov = 40f, MaskHeight = 3f;
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
        static readonly int ProjectId = Shader.PropertyToID("_Project");

        bool perspective;
        // Level editor zoom; 1 = the prototype's framing.
        public float Zoom { get; set; } = 1f;

        // Screen shake (explosions, getting caught). Applied on top of the follow position.
        float shakeAmount, shakeUntil, shakeLength = 1f;
        Vector3 shakeOffset;

        public void Shake(float amount, float seconds)
        {
            if (Time.time < shakeUntil && amount < shakeAmount) return;
            shakeAmount = amount;
            shakeLength = Mathf.Max(0.01f, seconds);
            shakeUntil = Time.time + seconds;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            perspective = Art.Available;
            cam.orthographic = !perspective;
            if (perspective)
            {
                cam.fieldOfView = Fov;
                cam.nearClipPlane = 0.5f;
                cam.farClipPlane = 200f;
            }
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
            maskMaterial.SetFloat(ProjectId, perspective ? 1f : 0f);
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
            float target = dark ? settings.DarkRadius : settings.Radius;
            if (player.Debuffed) target = Mathf.Min(target, world.Debuff.Radius);
            radius = Mathf.Lerp(radius, target, 1f - Mathf.Exp(-radiusLerp * Time.deltaTime));

            transform.position -= shakeOffset; // follow from the steady position
            var p = (Vector3)player.Position;
            if (perspective)
            {
                // "Up" is -Z and "behind" is -Y (south) in the game plane.
                float scale = settings.Radius / 8f * Zoom;
                var offset = new Vector3(0f, -Behind * scale, -Height * scale);
                transform.position = Vector3.Lerp(transform.position, p + offset, 1f - Mathf.Exp(-follow * Time.deltaTime));
                transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
            }
            else
            {
                cam.orthographicSize = settings.Radius + 0.5f;
                var pos = Vector3.Lerp(transform.position, new Vector3(p.x, p.y, -10f), 1f - Mathf.Exp(-follow * Time.deltaTime));
                pos.z = -10f;
                transform.position = pos;
            }

            float left = shakeUntil - Time.time;
            shakeOffset = left > 0f
                ? new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f, 0f)
                  * (2f * shakeAmount * (left / shakeLength))
                : Vector3.zero;
            transform.position += shakeOffset;

            if (mask == null) return;
            mask.enabled = !gm.DebugNoFog;
            if (perspective)
            {
                // Over everything (walls, models, floating icons); the shader measures on the floor.
                mask.transform.SetPositionAndRotation(new Vector3(p.x, p.y, -MaskHeight), Quaternion.identity);
                mask.transform.localScale = new Vector3(400f, 400f, 1f);
            }
            else
            {
                float h = cam.orthographicSize * 2f + 2f;
                mask.transform.localScale = new Vector3(h * cam.aspect + 2f, h, 1f);
            }

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

        // Jump straight to a spawn point instead of gliding across the map.
        public void SnapTo(Vector2 p)
        {
            float scale = (GameManager.Instance.World != null ? GameManager.Instance.World.Camera.Radius / 8f : 1f) * Zoom;
            var offset = perspective ? new Vector3(0f, -Behind * scale, -Height * scale) : new Vector3(0f, 0f, -10f);
            shakeOffset = Vector3.zero;
            transform.position = (Vector3)p + offset;
            if (perspective) transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
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
