using UnityEngine;

namespace EscapeOffice
{
    // Motion for the characters ported from tos-characters.js (built by Tools → Other Side →
    // Build Assets): the JS setMotion/update loop driving the jointed prefab directly, no rig or
    // Animator. Joint angles are the JS ones (three.js space) and are converted on the way in.
    // References are filled in by the builder.
    public class TosCharacter : MonoBehaviour
    {
        public enum Motion { APose, Idle, Walk, Run }

        public Motion motion = Motion.Idle;
        [Tooltip("Scales the walk / run cycle rate.")]
        public float speed = 1f;

        public Transform hips, spine, chest, neck;
        public Transform thighL, thighR, shinL, shinR, footL, footR;
        public Transform upperArmL, upperArmR, forearmL, forearmR;
        public float hipY;
        public Renderer tie;
        public Color tieColor = new Color(0.79f, 0.8f, 0.82f); // plain light grey until tinted

        // Walk / run cycle angle; a step lands every π.
        public float Phase => phase;

        const int HipL = 0, HipR = 1, KneeL = 2, KneeR = 3, AnkleL = 4, AnkleR = 5,
            ShoulderL = 6, ShoulderR = 7, ElbowL = 8, ElbowR = 9, Spine = 10, Neck = 11, Count = 12;
        const float A = 0.8f; // A-pose: arms ~46° below horizontal

        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform[] joints;
        readonly Vector3[] pose = new Vector3[Count], target = new Vector3[Count];
        float phase, bob, clock;
        MaterialPropertyBlock block;

        void Awake()
        {
            joints = new[] { thighL, thighR, shinL, shinR, footL, footR, upperArmL, upperArmR, forearmL, forearmR, spine, neck };
            clock = Random.value * 10f; // idle breathing out of step between characters
            SetMotion(motion, true);
        }

        // Tie colour per player (the side colour); null restores the build's grey.
        public void Tint(Color? color)
        {
            if (tie == null) return;
            block ??= new MaterialPropertyBlock();
            tie.GetPropertyBlock(block);
            block.SetColor(ColorId, color ?? tieColor);
            tie.SetPropertyBlock(block);
        }

        public void SetMotion(Motion m, bool instant = false)
        {
            motion = m;
            if (!instant || joints == null) return;
            Target(m, phase, clock);
            System.Array.Copy(target, pose, Count);
            Apply();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            bool run = motion == Motion.Run;
            if (motion == Motion.Walk || run) phase += dt * (run ? 11f : 6.5f) * speed;

            Target(motion, phase, clock);
            float k = Mathf.Min(1f, dt * (motion == Motion.APose ? 6f : 12f));
            for (int i = 0; i < Count; i++) pose[i] += (target[i] - pose[i]) * k;
            Apply();

            float bt = motion == Motion.Walk ? Mathf.Abs(Mathf.Cos(phase)) * 0.025f : run ? Mathf.Abs(Mathf.Cos(phase)) * 0.06f : 0f;
            bob += (bt - bob) * k;
            if (hips != null)
            {
                var p = hips.localPosition;
                hips.localPosition = new Vector3(p.x, hipY + bob - (run ? 0.03f : 0f), p.z);
            }
            float breath = motion == Motion.Idle ? Mathf.Sin(clock * 1.6f) * 0.008f : 0f;
            if (chest != null) chest.localScale = new Vector3(1f + breath * 0.5f, 1f + breath, 1f + breath);
        }

        void Target(Motion m, float ph, float t)
        {
            for (int i = 0; i < Count; i++) target[i] = Vector3.zero;
            if (m == Motion.APose) { target[ShoulderL].z = A; target[ShoulderR].z = -A; return; }

            target[ShoulderL].z = 0.1f; target[ShoulderR].z = -0.1f;
            target[ElbowL].x = target[ElbowR].x = -0.12f;
            if (m == Motion.Idle)
            {
                float b = Mathf.Sin(t * 1.6f);
                target[Spine].x = b * 0.012f;
                target[Neck].x = Mathf.Sin(t * 0.8f) * 0.03f;
                target[ShoulderL].z += b * 0.02f;
                target[ShoulderR].z -= b * 0.02f;
                return;
            }

            bool run = m == Motion.Run;
            float sw = run ? 0.75f : 0.42f, s1 = Mathf.Sin(ph), c1 = Mathf.Cos(ph);
            target[HipL].x = -sw * s1; target[HipR].x = sw * s1;
            target[KneeL].x = Mathf.Max(0f, s1) * (run ? 1.3f : 0.6f);
            target[KneeR].x = Mathf.Max(0f, -s1) * (run ? 1.3f : 0.6f);
            target[AnkleL].x = -target[KneeL].x * 0.3f; target[AnkleR].x = -target[KneeR].x * 0.3f;
            target[ShoulderL].x = sw * 0.9f * s1; target[ShoulderR].x = -sw * 0.9f * s1;
            target[ElbowL].x = target[ElbowR].x = run ? -1.2f : -0.3f;
            target[Spine].x = run ? 0.18f : 0.04f;
            target[Spine].y = c1 * (run ? 0.12f : 0.06f);
        }

        void Apply()
        {
            for (int i = 0; i < Count; i++)
                if (joints[i] != null) joints[i].localRotation = ThreeEuler(pose[i]);
        }

        // three.js Euler (XYZ order, right-handed, radians) → Unity rotation, mirrored across X.
        public static Quaternion ThreeEuler(Vector3 e)
        {
            var q = Quaternion.AngleAxis(e.x * Mathf.Rad2Deg, Vector3.right)
                  * Quaternion.AngleAxis(e.y * Mathf.Rad2Deg, Vector3.up)
                  * Quaternion.AngleAxis(e.z * Mathf.Rad2Deg, Vector3.forward);
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }
    }
}
