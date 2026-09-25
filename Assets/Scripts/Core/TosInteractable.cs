using UnityEngine;

namespace EscapeOffice
{
    // Runtime side of the interactables ported from tos-interactables.js (built by Build
    // Assets). The same hooks as the JS userData: SetState eases the moving part, Tint recolours
    // the emissive ring (the game uses the side colour; null = white), SetSwatch / SetDisplay for
    // the riddle keypad, SetPlate / SetDigit for the code panel. SetPresence brightens the ring as
    // the player comes close.
    public class TosInteractable : MonoBehaviour
    {
        public enum Motion { None, PushZ, DropY, RotX, RotY }

        public Motion motion;
        public Transform moving;
        public float from, to, ease = 10f;
        public Renderer ring;
        public Renderer[] swatches = new Renderer[0];
        public Renderer display;
        public Renderer face;
        public Transform digitAnchor;
        public Light lamp;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        float state, target;
        Color ringColor = Color.white;
        float presence;
        Material ringMat, faceMat, displayMat;
        Material[] swatchMats;
        TextMesh digit;

        void Awake()
        {
            if (ring != null) ringMat = ring.material;
            if (face != null) faceMat = face.material;
            if (display != null) displayMat = display.material;
            swatchMats = new Material[swatches.Length];
            for (int i = 0; i < swatches.Length; i++) if (swatches[i] != null) swatchMats[i] = swatches[i].material;
            ApplyRing();
            Apply();
        }

        public void SetState(bool on, bool instant = false)
        {
            target = on ? 1f : 0f;
            if (instant) { state = target; Apply(); }
        }

        public void Tint(Color? c)
        {
            ringColor = c ?? Color.white;
            ApplyRing();
        }

        // 0 idle, 0.5 near, 1 the player's target: the ring glows brighter as you approach.
        public void SetPresence(float p)
        {
            if (Mathf.Abs(p - presence) < 0.01f) return;
            presence = p;
            ApplyRing();
        }

        public void SetSwatch(int i, Color? c)
        {
            if (swatchMats == null || i < 0 || i >= swatchMats.Length || swatchMats[i] == null) return;
            var m = swatchMats[i];
            m.color = c ?? new Color(0.953f, 0.957f, 0.961f);
            m.SetColor(EmissionId, c.HasValue ? c.Value * 0.55f : Color.white * 0.18f);
        }

        public void SetDisplay(Color screen)
        {
            if (displayMat == null) return;
            displayMat.EnableKeyword("_EMISSION");
            displayMat.SetColor(EmissionId, screen);
        }

        public void SetPlate(Color c)
        {
            if (faceMat != null) faceMat.color = c;
        }

        public void SetDigit(string text, Color ink, Font font)
        {
            if (digitAnchor == null) return;
            if (digit == null)
            {
                if (font == null) return;
                var go = new GameObject("Digit");
                go.transform.SetParent(digitAnchor, false);
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TextMesh reads toward local -Z
                digit = go.AddComponent<TextMesh>();
                digit.font = font;
                digit.fontSize = 96;
                digit.characterSize = 0.045f;
                digit.anchor = TextAnchor.MiddleCenter;
                digit.alignment = TextAlignment.Center;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            digit.text = text ?? "";
            digit.color = ink;
        }

        void ApplyRing()
        {
            if (ringMat == null) return;
            ringMat.color = ringColor;
            ringMat.SetColor(EmissionId, ringColor * (0.55f + 0.9f * presence));
        }

        void Update()
        {
            if (Mathf.Approximately(state, target)) return;
            state += (target - state) * Mathf.Min(1f, Time.deltaTime * ease);
            if (Mathf.Abs(target - state) < 0.001f) state = target;
            Apply();
        }

        void Apply()
        {
            if (moving == null) return;
            float v = Mathf.Lerp(from, to, state);
            var p = moving.localPosition;
            switch (motion)
            {
                case Motion.PushZ: moving.localPosition = new Vector3(p.x, p.y, v); break;
                case Motion.DropY: moving.localPosition = new Vector3(p.x, v, p.z); break;
                case Motion.RotX: moving.localRotation = Quaternion.Euler(v * Mathf.Rad2Deg, 0f, 0f); break;
                case Motion.RotY: moving.localRotation = Quaternion.Euler(0f, v * Mathf.Rad2Deg, 0f); break;
            }
        }
    }
}
