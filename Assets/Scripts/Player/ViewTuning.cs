using System.Globalization;
using UnityEngine;

namespace EscapeOffice
{
    // Live camera and vision-mask values, tuned from the in-game View panel (UI.ViewTuner).
    // Defaults come from ArtDirection.json and the level's camera settings; changes are saved
    // to PlayerPrefs so a look survives restarts until Reset. "Copy JSON" puts the values on the
    // clipboard in ArtDirection.json shape so a look can be baked in.
    public static class ViewTuning
    {
        const string Pref = "eto.view.";

        // Camera (perspective view; distance also scales the orthographic view).
        public static float Height, Behind, Fov, Distance, Follow;
        // Vision circle: multipliers on the level's radius / darkRadius, edge softness in tiles,
        // and how opaque the darkness outside it is.
        public static float VisionScale, DarkScale, Softness, FogAlpha;
        // Screen-edge darkening from the colour grade.
        public static float Vignette, VignetteStart;

        static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Defaults();
            Height = PlayerPrefs.GetFloat(Pref + "height", Height);
            Behind = PlayerPrefs.GetFloat(Pref + "behind", Behind);
            Fov = PlayerPrefs.GetFloat(Pref + "fov", Fov);
            Distance = PlayerPrefs.GetFloat(Pref + "distance", Distance);
            Follow = PlayerPrefs.GetFloat(Pref + "follow", Follow);
            VisionScale = PlayerPrefs.GetFloat(Pref + "vision", VisionScale);
            DarkScale = PlayerPrefs.GetFloat(Pref + "dark", DarkScale);
            Softness = PlayerPrefs.GetFloat(Pref + "softness", Softness);
            FogAlpha = PlayerPrefs.GetFloat(Pref + "fogAlpha", FogAlpha);
            Vignette = PlayerPrefs.GetFloat(Pref + "vignette", Vignette);
            VignetteStart = PlayerPrefs.GetFloat(Pref + "vignetteStart", VignetteStart);
        }

        static void Defaults()
        {
            var art = ArtDirection.Current;
            Height = art.camera.height;
            Behind = art.camera.behind;
            Fov = art.camera.fov;
            Distance = 1f;
            Follow = 10f;
            VisionScale = 1f;
            DarkScale = 1f;
            Softness = 1.2f;
            FogAlpha = art.world.fogAlpha;
            Vignette = art.grading.vignette;
            VignetteStart = art.grading.vignetteStart;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Pref + "height", Height);
            PlayerPrefs.SetFloat(Pref + "behind", Behind);
            PlayerPrefs.SetFloat(Pref + "fov", Fov);
            PlayerPrefs.SetFloat(Pref + "distance", Distance);
            PlayerPrefs.SetFloat(Pref + "follow", Follow);
            PlayerPrefs.SetFloat(Pref + "vision", VisionScale);
            PlayerPrefs.SetFloat(Pref + "dark", DarkScale);
            PlayerPrefs.SetFloat(Pref + "softness", Softness);
            PlayerPrefs.SetFloat(Pref + "fogAlpha", FogAlpha);
            PlayerPrefs.SetFloat(Pref + "vignette", Vignette);
            PlayerPrefs.SetFloat(Pref + "vignetteStart", VignetteStart);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            foreach (var k in new[] { "height", "behind", "fov", "distance", "follow", "vision", "dark", "softness", "fogAlpha", "vignette", "vignetteStart" })
                PlayerPrefs.DeleteKey(Pref + k);
            PlayerPrefs.Save();
            Defaults();
        }

        // ArtDirection.json snippet; vision multipliers go on the level's "camera" block instead.
        public static string ToJson(Net.CameraSettings level)
        {
            string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            float r = (level?.Radius ?? 8f) * VisionScale, d = (level?.DarkRadius ?? 2f) * DarkScale;
            return
                $"\"camera\": {{ \"height\": {F(Height * Distance)}, \"behind\": {F(Behind * Distance)}, \"fov\": {F(Fov)} }},\n" +
                $"\"world\": {{ \"fogAlpha\": {F(FogAlpha)} }},\n" +
                $"\"grading\": {{ \"vignette\": {F(Vignette)}, \"vignetteStart\": {F(VignetteStart)} }}\n" +
                $"// level camera: {{ \"radius\": {F(r)}, \"darkRadius\": {F(d)} }}, mask softness {F(Softness)}";
        }
    }
}
