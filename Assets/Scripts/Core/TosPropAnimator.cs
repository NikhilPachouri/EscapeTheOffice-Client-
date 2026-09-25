using UnityEngine;

namespace EscapeOffice
{
    // Motion for the procedural props ported from tos-assets.js (built by Tools → Other Side →
    // Build Assets): the server rack's fan, LED groups and status strip, and the machine's gyro
    // rings, pulsing core, beam, trim, light and live hologram. References are filled in by the
    // builder; anything missing is skipped.
    public class TosPropAnimator : MonoBehaviour
    {
        public enum Kind { Rack, Machine }
        public Kind kind;

        [Header("Rack")]
        public Transform fan;
        public Renderer[] ledGroups = new Renderer[0];
        public float[] ledRate = new float[0], ledPhase = new float[0], ledDuty = new float[0];
        public Renderer status;

        [Header("Machine")]
        public Transform gyro, inner, crystal, column, halo, beam;
        public Renderer trim, columnR, haloR, beamR, holoR;
        public Light glowLight;
        public Color colorA = new Color(0.24f, 0.81f, 0.85f), colorB = new Color(0.77f, 0.61f, 1f);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        Material statusMat, trimMat, columnMat, haloMat, beamMat;
        Color statusBase, trimBase;
        Texture2D holo;
        Color32[] holoPx;
        float holoTimer, phase;

        void Start()
        {
            phase = Random.value * 10f;
            if (status != null) { statusMat = status.material; statusBase = statusMat.GetColor(EmissionId); }
            if (trim != null) { trimMat = trim.material; trimBase = trimMat.GetColor(EmissionId); }
            if (columnR != null) columnMat = columnR.material;
            if (haloR != null) haloMat = haloR.material;
            if (beamR != null) beamMat = beamR.material;
            if (holoR != null)
            {
                holo = new Texture2D(128, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                holoPx = new Color32[128 * 64];
                holoR.material.mainTexture = holo;
                DrawHolo(0f);
            }
        }

        void OnDestroy()
        {
            if (holo != null) Destroy(holo);
        }

        void Update()
        {
            float t = Time.time + phase, dt = Time.deltaTime;
            if (kind == Kind.Rack) UpdateRack(t, dt);
            else UpdateMachine(t, dt);
        }

        void UpdateRack(float t, float dt)
        {
            if (fan != null) fan.Rotate(Vector3.up, dt * 9f * Mathf.Rad2Deg, Space.Self);
            for (int i = 0; i < ledGroups.Length; i++)
                if (ledGroups[i] != null)
                    ledGroups[i].enabled = (Mathf.Sin(t * ledRate[i] + ledPhase[i]) + 1f) / 2f < ledDuty[i] + 0.25f;
            if (statusMat != null) statusMat.SetColor(EmissionId, statusBase * (1f + Mathf.Sin(t * 2f) * 0.35f));
        }

        void UpdateMachine(float t, float dt)
        {
            if (gyro != null) gyro.Rotate(Vector3.up, -dt * 0.9f * Mathf.Rad2Deg, Space.Self);
            if (inner != null) inner.Rotate(Vector3.right, dt * 1.7f * Mathf.Rad2Deg, Space.Self);
            if (crystal != null)
            {
                crystal.Rotate(Vector3.up, -dt * 2.2f * Mathf.Rad2Deg, Space.Self);
                var p = crystal.localPosition; p.y = 0.6f + Mathf.Sin(t * 2.4f) * 0.05f; crystal.localPosition = p;
            }
            float k = 0.5f + 0.5f * Mathf.Sin(t * 3.1f);
            if (column != null) column.localScale = new Vector3(0.06f + k * 0.03f, column.localScale.y, 0.06f + k * 0.03f);
            if (halo != null) halo.localScale = new Vector3(0.14f + k * 0.05f, halo.localScale.y, 0.14f + k * 0.05f);
            Tint(columnMat, colorA, 0.6f + k * 0.35f);
            Tint(haloMat, colorA, 0.12f + k * 0.18f);
            Tint(beamMat, colorB, 0.08f + k * 0.1f);
            if (trimMat != null) trimMat.SetColor(EmissionId, trimBase * (0.8f + k * 0.6f) / 1.1f);
            if (glowLight != null) glowLight.intensity = 0.8f + k * 0.5f;
            if (holo != null && (holoTimer += dt) > 0.08f) { holoTimer = 0f; DrawHolo(t); }
        }

        static void Tint(Material m, Color c, float opacity)
        {
            if (m != null) m.SetColor(TintId, new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, opacity * 0.5f));
        }

        // The console hologram: a frame, a live waveform and bouncing bars (as the JS canvas).
        void DrawHolo(float t)
        {
            const int W = 128, H = 64;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < holoPx.Length; i++) holoPx[i] = clear;
            Color32 a = colorA, b = colorB;
            for (int x = 2; x < W - 2; x++) { Px(x, 2, a); Px(x, H - 3, a); }
            for (int y = 2; y < H - 2; y++) { Px(2, y, a); Px(W - 3, y, a); }
            int prev = -1;
            for (int x = 0; x <= 75; x++)
            {
                int y = Mathf.RoundToInt(32 + Mathf.Sin(x * 0.18f + t * 4f) * 11f * Mathf.Sin(t * 0.7f + x * 0.04f));
                if (prev >= 0) for (int yy = Mathf.Min(prev, y); yy <= Mathf.Max(prev, y); yy++) { Px(7 + x, yy, a); Px(7 + x, yy + 1, a); }
                prev = y;
            }
            for (int i = 0; i < 6; i++)
            {
                int h = Mathf.RoundToInt(6 + (Mathf.Sin(t * 3f + i * 1.3f) + 1f) * 11f);
                for (int x = 0; x < 4; x++) for (int y = 0; y < h; y++) Px(89 + i * 6 + x, 8 + y, b);
            }
            if (Mathf.Sin(t * 6f) > 0f) for (int x = 7; x < 27; x++) for (int y = H - 9; y < H - 6; y++) Px(x, y, a);
            holo.SetPixels32(holoPx);
            holo.Apply(false);
        }

        void Px(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= 128 || y >= 64) return;
            holoPx[y * 128 + x] = c;
        }
    }
}
