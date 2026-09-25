using UnityEngine;

namespace EscapeOffice
{
    // Post effect on the 3D camera (Resources/ColorGrade.shader): bloom, then saturation,
    // contrast, warm highlights / cool shadows and a vignette. CameraRig adds it with the
    // asset pack; tweak the fields on the Main Camera in Play mode to try a look.
    [RequireComponent(typeof(Camera))]
    public class ColorGrading : MonoBehaviour
    {
        [Header("Bloom")]
        [Range(0f, 2f)] public float threshold = 0.94f; // LDR: only lasers, lamps, sparks and glows
        [Range(0.01f, 1f)] public float softKnee = 0.08f;
        [Range(0f, 3f)] public float bloom = 0.8f;
        [Range(1, 6)] public int iterations = 5;

        [Header("Grade")]
        [Range(0f, 2f)] public float saturation = 1.18f;
        [Range(0.5f, 1.5f)] public float contrast = 1.08f;
        [Range(0.5f, 2f)] public float exposure = 1f;
        [Range(0f, 1f)] public float splitTone = 0.35f;
        public Color highlights = new Color(1f, 0.93f, 0.82f);
        public Color shadows = new Color(0.78f, 0.86f, 1f);

        [Header("Tilt-shift")]
        [Range(0f, 1f)] public float tiltShift = 0.75f;
        [Range(0f, 1f)] public float tiltFocus = 0.28f;

        [Header("Vignette")]
        [Range(0f, 1f)] public float vignette = 0.38f;
        [Range(0f, 1f)] public float vignetteStart = 0.45f;

        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[8];

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int GradeId = Shader.PropertyToID("_Grade");
        static readonly int WarmId = Shader.PropertyToID("_Warm");
        static readonly int CoolId = Shader.PropertyToID("_Cool");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        static readonly int BlurTexId = Shader.PropertyToID("_BlurTex");
        static readonly int TiltId = Shader.PropertyToID("_Tilt");

        // Values from Resources/ArtDirection.json ("grading").
        public void Apply(ArtDirection.GradeLook g)
        {
            threshold = g.bloomThreshold; softKnee = g.bloomKnee; bloom = g.bloom;
            saturation = g.saturation; contrast = g.contrast; exposure = g.exposure; splitTone = g.splitTone;
            highlights = ArtDirection.Hex(g.highlights, highlights);
            shadows = ArtDirection.Hex(g.shadows, shadows);
            vignette = g.vignette; vignetteStart = g.vignetteStart;
            tiltShift = g.tiltShift; tiltFocus = g.tiltFocus;
        }

        void OnEnable()
        {
            var shader = Shader.Find("EscapeOffice/ColorGrade");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[camera] ColorGrade shader missing or unsupported; colour grading is off.");
                enabled = false;
                return;
            }
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable()
        {
            if (mat != null) Destroy(mat);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }

            mat.SetVector(ThresholdId, new Vector4(threshold, softKnee, bloom, 0f));
            mat.SetVector(GradeId, new Vector4(saturation, contrast, exposure, splitTone));
            mat.SetColor(WarmId, highlights);
            mat.SetColor(CoolId, shadows);
            mat.SetVector(VignetteId, new Vector4(vignette, vignetteStart, 0f, 0f));
            mat.SetVector(TiltId, new Vector4(tiltShift, tiltFocus, 0f, 0f));

            // Tilt-shift source: the frame at 1/8 size, smoothed back up to 1/4.
            var half = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0, src.format);
            var quarter = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0, src.format);
            var eighth = RenderTexture.GetTemporary(src.width / 8, src.height / 8, 0, src.format);
            var blur = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0, src.format);
            Graphics.Blit(src, half, mat, 1);
            Graphics.Blit(half, quarter, mat, 1);
            Graphics.Blit(quarter, eighth, mat, 1);
            Graphics.Blit(eighth, blur, mat, 1);
            mat.SetTexture(BlurTexId, blur);

            // Bloom: bright parts at half size, blurred down a mip chain and back up.
            int w = src.width / 2, h = src.height / 2, levels = 0;
            chain[0] = RenderTexture.GetTemporary(w, h, 0, src.format);
            Graphics.Blit(src, chain[0], mat, 0);
            for (int i = 1; i < iterations && w > 8 && h > 8; i++)
            {
                w /= 2; h /= 2;
                chain[i] = RenderTexture.GetTemporary(w, h, 0, src.format);
                Graphics.Blit(chain[i - 1], chain[i], mat, 1);
                levels = i;
            }
            for (int i = levels; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], mat, 2);

            mat.SetTexture(BloomTexId, chain[0]);
            Graphics.Blit(src, dst, mat, 3);

            for (int i = 0; i <= levels; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
            RenderTexture.ReleaseTemporary(half);
            RenderTexture.ReleaseTemporary(quarter);
            RenderTexture.ReleaseTemporary(eighth);
            RenderTexture.ReleaseTemporary(blur);
        }
    }
}
