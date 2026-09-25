using UnityEngine;

namespace EscapeOffice
{
    // Motion for the characters ported from tos-characters.js (built by Tools → Other Side →
    // Build Assets), driving the jointed prefab directly, no rig or Animator. Joint angles are in
    // three.js space (the JS numbers) and are converted on the way in. References are filled in
    // by the builder.
    //
    // The gait goes further than the JS loop so the characters walk like people in the game:
    // Locomote() advances the stride by the distance actually covered (no skating feet), the
    // knee folds while the leg swings through and straightens for the heel strike, the soles
    // stay near level, the pelvis twists and drops over the swing leg, the chest counter-twists
    // and the head stays on target. Idle, walk and run crossfade by weight, not per-joint lag,
    // so fast strides keep their full swing.
    public class TosCharacter : MonoBehaviour
    {
        public enum Motion { APose, Idle, Walk, Run }

        public Motion motion = Motion.Idle;
        [Tooltip("Cycle rate when nothing calls Locomote (walk 6.5, run 11 rad/s × this).")]
        public float speed = 1f;

        public Transform hips, spine, chest, neck;
        public Transform thighL, thighR, shinL, shinR, footL, footR;
        public Transform upperArmL, upperArmR, forearmL, forearmR;
        public float hipY;
        public Renderer tie;
        public Color tieColor = new Color(0.79f, 0.8f, 0.82f); // plain light grey until tinted

        [Header("Gait")]
        [Tooltip("Step length as a multiple of leg length.")]
        public float walkStep = 1.1f, runStep = 1.45f;
        public float walkSwing = 0.55f, runSwing = 0.8f; // thigh swing (rad) matching those steps

        // Walk / run cycle angle; a foot lands every π.
        public float Phase => phase;

        const int HipL = 0, HipR = 1, KneeL = 2, KneeR = 3, AnkleL = 4, AnkleR = 5,
            ShoulderL = 6, ShoulderR = 7, ElbowL = 8, ElbowR = 9, Spine = 10, Neck = 11, Pelvis = 12, Count = 13;
        const float A = 0.8f; // A-pose: arms ~46° below horizontal

        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform[] joints;
        readonly Vector3[] pose = new Vector3[Count], idleP = new Vector3[Count], walkP = new Vector3[Count],
            runP = new Vector3[Count], aP = new Vector3[Count];
        float phase, clock, groundSpeed = -1f;
        float moveW, runW, aposeW; // crossfade weights
        MaterialPropertyBlock block;

        void Awake()
        {
            Bind();
            clock = Random.value * 10f; // idle breathing out of step between characters
            SetMotion(motion, true);
        }

        // Joint order matches the pose indices; rebuilt if a script reload left an old array.
        void Bind() => joints = new[] { thighL, thighR, shinL, shinR, footL, footR, upperArmL, upperArmR, forearmL, forearmR, spine, neck, hips };

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
            if (!instant) return;
            moveW = m == Motion.Walk || m == Motion.Run ? 1f : 0f;
            runW = m == Motion.Run ? 1f : 0f;
            aposeW = m == Motion.APose ? 1f : 0f;
            Pose(0f);
        }

        // Move at this ground speed (world units / s). The stride follows the distance covered,
        // so the planted foot stays put; below a crawl the character stands.
        public void Locomote(float worldSpeed, bool run)
        {
            groundSpeed = worldSpeed;
            motion = worldSpeed < 0.05f ? Motion.Idle : run ? Motion.Run : Motion.Walk;
        }

        // Hip height in world units: the leg length the stride is measured against.
        float LegLength => hips != null && hips.parent != null ? hipY * Mathf.Abs(hips.parent.lossyScale.x) : hipY;

        void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            bool moving = motion == Motion.Walk || motion == Motion.Run;
            if (moving)
            {
                float rate = groundSpeed >= 0f
                    ? groundSpeed / Mathf.Max(0.01f, LegLength * Mathf.Lerp(walkStep, runStep, runW)) * Mathf.PI
                    : (motion == Motion.Run ? 11f : 6.5f) * speed;
                phase += Mathf.Min(rate, 30f) * dt;
            }

            float k = Mathf.Min(1f, dt * 10f);
            moveW += ((moving ? 1f : 0f) - moveW) * k;
            runW += ((motion == Motion.Run ? 1f : 0f) - runW) * k;
            aposeW += ((motion == Motion.APose ? 1f : 0f) - aposeW) * Mathf.Min(1f, dt * 6f);
            Pose(dt);
        }

        void Pose(float dt)
        {
            if (joints == null || joints.Length != Count) Bind();
            Idle(clock, idleP);
            Gait(phase, false, walkP);
            Gait(phase, true, runP);
            for (int i = 0; i < Count; i++) aP[i] = Vector3.zero;
            aP[ShoulderL].z = A; aP[ShoulderR].z = -A;
            for (int i = 0; i < Count; i++)
            {
                var m = Vector3.Lerp(walkP[i], runP[i], runW);
                pose[i] = Vector3.Lerp(Vector3.Lerp(idleP[i], m, moveW), aP[i], aposeW);
            }
            for (int i = 0; i < Count; i++)
                if (joints[i] != null) joints[i].localRotation = ThreeEuler(pose[i]);

            // Body height: highest over the planted leg (twice a stride), lower and bouncier running.
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase), gw = moveW * (1f - aposeW);
            float bob = Mathf.Lerp(Mathf.Abs(c) * 0.03f, Mathf.Abs(c) * 0.065f - 0.035f, runW) * gw;
            float sway = c * Mathf.Lerp(0.02f, 0.01f, runW) * gw; // weight over the stance foot
            if (hips != null) hips.localPosition = new Vector3(sway, hipY + bob, hips.localPosition.z);
            float breath = Mathf.Sin(clock * 1.6f) * 0.008f * (1f - moveW) * (1f - aposeW);
            if (chest != null) chest.localScale = new Vector3(1f + breath * 0.5f, 1f + breath, 1f + breath);
        }

        void Idle(float t, Vector3[] T)
        {
            for (int i = 0; i < Count; i++) T[i] = Vector3.zero;
            float b = Mathf.Sin(t * 1.6f);
            T[ShoulderL].z = 0.1f + b * 0.02f; T[ShoulderR].z = -0.1f - b * 0.02f;
            T[ElbowL].x = T[ElbowR].x = -0.12f;
            T[Spine].x = b * 0.012f;
            T[Neck].x = Mathf.Sin(t * 0.8f) * 0.03f;
            T[Neck].y = Mathf.Sin(t * 0.37f) * 0.08f; // glancing about
        }

        // One stride. In three space: +x on a hip swings the leg back; the left leg is forward at
        // s = 1 and swings through (knee folded) while cos > 0.
        void Gait(float ph, bool run, Vector3[] T)
        {
            for (int i = 0; i < Count; i++) T[i] = Vector3.zero;
            float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            float sw = run ? runSwing : walkSwing, fold = run ? 1.7f : 0.95f, flex = run ? 0.25f : 0.08f;

            T[HipL].x = -sw * s; T[HipR].x = sw * s;
            T[KneeL].x = fold * Mathf.Max(0f, c) + flex; // swing: fold through; stance: slight give
            T[KneeR].x = fold * Mathf.Max(0f, -c) + flex;
            // Soles near level with the floor, toes lifting a little for the heel strike.
            T[AnkleL].x = -(T[HipL].x + T[KneeL].x) * 0.75f - 0.1f * Mathf.Max(0f, s);
            T[AnkleR].x = -(T[HipR].x + T[KneeR].x) * 0.75f - 0.1f * Mathf.Max(0f, -s);

            // Arms counter-swing the legs; forearms swing a touch further forward.
            float arm = sw * (run ? 1.1f : 0.8f);
            T[ShoulderL].x = arm * s; T[ShoulderR].x = -arm * s;
            T[ShoulderL].z = run ? 0.18f : 0.1f; T[ShoulderR].z = -T[ShoulderL].z;
            T[ElbowL].x = (run ? -1.35f : -0.3f) - 0.25f * Mathf.Max(0f, -s);
            T[ElbowR].x = (run ? -1.35f : -0.3f) - 0.25f * Mathf.Max(0f, s);

            // Pelvis leads the forward leg and drops over the swing side; the chest twists back
            // against it and the neck keeps the head facing the way we're going.
            float twist = run ? 0.1f : 0.12f, roll = run ? 0.03f : 0.05f;
            T[Pelvis].y = -twist * s;
            T[Pelvis].z = -roll * c;
            T[Spine].y = twist * 2f * s;
            T[Spine].z = roll * c;
            T[Spine].x = run ? 0.22f : 0.05f; // lean into it
            T[Neck].y = -twist * s;
            T[Neck].x = run ? -0.12f : -0.03f; // eyes up, not at the floor
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
