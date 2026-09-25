using UnityEngine;

namespace EscapeOffice
{
    // Particle effects built in code from the pack's soft dot (FX_Additive / FX_Alpha materials).
    // Remember the game plane: up off the floor is -Z, so "rising" means moving toward -Z and
    // Unity's gravity (-Y, map south) is never used.
    public static class Fx3D
    {
        public static bool Available => Art.Available && Art.Material("FX_Additive") != null;

        static readonly Quaternion Up = Quaternion.LookRotation(Vector3.back); // local +Z → world up (-Z)

        // ---------------------------------------------------------------- one-shot bursts

        // Omni-directional pop: sparks, flashes, sparkles.
        public static void Burst(Vector3 at, Color color, int count = 14, float speed = 2f, float size = 0.12f,
            float life = 0.6f, bool additive = true)
        {
            var ps = Make("Burst", null, at, additive, loop: false);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            Drag(ps, 2f);
            FadeOut(ps, shrink: true);
            Emit(ps, count, life);
        }

        // Soft cloud that rises and grows: dust, steam, smoke puffs.
        public static void Puff(Vector3 at, Color color, int count = 10, float radius = 0.3f, float size = 0.5f,
            float life = 1.2f, float rise = 0.8f)
        {
            var ps = Make("Puff", null, at, additive: false, loop: false);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            Rise(ps, rise);
            Drag(ps, 1.5f);
            FadeOut(ps, shrink: false, grow: 1.8f);
            Emit(ps, count, life);
        }

        // ---------------------------------------------------------------- looping emitters
        // Returned stopped; call Play() / Stop() to follow state.

        public static ParticleSystem Embers(Transform parent, Vector3 localPos, Vector2 area)
        {
            var ps = Make("Embers", parent, localPos, additive: true, loop: true);
            if (ps == null) return null;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.35f));
            Area(ps, area);
            Rate(ps, 6f * area.x * area.y);
            Rise(ps, 1.6f);
            Wobble(ps, 0.6f);
            FadeOut(ps, shrink: true);
            return ps;
        }

        public static ParticleSystem Smoke(Transform parent, Vector3 localPos, Vector2 area)
        {
            var ps = Make("Smoke", parent, localPos, additive: false, loop: true);
            if (ps == null) return null;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startColor = new Color(0.3f, 0.29f, 0.3f, 0.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Area(ps, area * 0.8f);
            Rate(ps, 3f * area.x * area.y);
            Rise(ps, 0.45f);
            Wobble(ps, 0.25f);
            FadeOut(ps, shrink: false, grow: 2.2f);
            return ps;
        }

        public static ParticleSystem Sparks(Transform parent, Vector3 localPos, Color color)
        {
            var ps = Make("Sparks", parent, localPos, additive: true, loop: true);
            if (ps == null) return null;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startColor = color;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 5, 0, 0.45f) });
            FadeOut(ps, shrink: true);
            return ps;
        }

        // Faint dust hanging in the air around a moving target (the player).
        public static ParticleSystem Motes(Transform parent)
        {
            var ps = Make("Motes", parent, new Vector3(0, 0, -1f), additive: true, loop: true);
            if (ps == null) return null;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
            main.maxParticles = 120;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 12f, 1.6f);
            shape.rotation = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;
            Rate(ps, 14f);
            Wobble(ps, 0.12f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeInOut();
            return ps;
        }

        // ---------------------------------------------------------------- authored shapes
        // Rings, arcs, streaks and debris: effects with a shape of their own, tied to the object.

        // A ring on the floor that grows and fades (pulses, ripples, shockwaves).
        public static void Ring(Vector3 at, Color color, float from, float to, float seconds, float delay = 0f)
        {
            var mat = Art.Material("FX_Additive");
            if (mat == null) return;
            Runner.StartCoroutine(RingRoutine(at, color, from, to, seconds, delay, mat));
        }

        static System.Collections.IEnumerator RingRoutine(Vector3 at, Color color, float from, float to, float seconds, float delay, Material mat)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var sr = SpriteFactory.Child(null, "FX Ring", SpriteFactory.Ring, color, Layers.Fx);
            sr.sharedMaterial = mat;
            sr.transform.position = new Vector3(at.x, at.y, Mathf.Min(at.z, -0.02f));
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (sr == null) yield break;
                float k = t / seconds, e = 1f - (1f - k) * (1f - k); // ease out
                sr.transform.localScale = Vector3.one * Mathf.Lerp(from, to, e);
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
                yield return null;
            }
            if (sr != null) Object.Destroy(sr.gameObject);
        }

        // Angular electric arc between two points, re-jagged every frame for a moment.
        public static void Arc(Vector3 from, Vector3 to, Color color, float seconds = 0.3f, float width = 0.05f)
        {
            var mat = Art.Material("FX_Energy");
            if (mat == null) return;
            Runner.StartCoroutine(ArcRoutine(from, to, color, seconds, width, mat));
        }

        static System.Collections.IEnumerator ArcRoutine(Vector3 from, Vector3 to, Color color, float seconds, float width, Material mat)
        {
            var line = NewLine("FX Arc", mat, width);
            const int n = 9;
            line.positionCount = n;
            var side = Vector3.Cross((to - from).normalized, Vector3.back).normalized;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                for (int i = 0; i < n; i++)
                {
                    float k = i / (n - 1f), jag = (i == 0 || i == n - 1) ? 0f : Random.Range(-0.18f, 0.18f);
                    line.SetPosition(i, Vector3.Lerp(from, to, k) + side * jag + Vector3.back * Random.Range(0f, 0.1f));
                }
                var c = new Color(color.r, color.g, color.b, 1f - t / seconds);
                line.startColor = line.endColor = c;
                yield return null;
            }
            Object.Destroy(line.gameObject);
        }

        // A short directional streak (a lever throw, a quick motion).
        public static void Streak(Vector3 from, Vector3 to, Color color, float seconds = 0.25f)
        {
            var mat = Art.Material("FX_Energy");
            if (mat == null) return;
            Runner.StartCoroutine(StreakRoutine(from, to, color, seconds, mat));
        }

        static System.Collections.IEnumerator StreakRoutine(Vector3 from, Vector3 to, Color color, float seconds, Material mat)
        {
            var line = NewLine("FX Streak", mat, 0.08f);
            line.positionCount = 2;
            line.widthCurve = AnimationCurve.Linear(0f, 0.02f, 1f, 1f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                line.SetPosition(0, Vector3.Lerp(from, to, k * 0.7f));
                line.SetPosition(1, Vector3.Lerp(from, to, Mathf.Min(1f, k * 1.6f)));
                line.startColor = line.endColor = new Color(color.r, color.g, color.b, 1f - k);
                yield return null;
            }
            Object.Destroy(line.gameObject);
        }

        // Small solid chunks thrown up and falling back (door grit, wall rubble).
        public static void Debris(Vector3 at, int count, float size, string material = "M_Rubble", float speed = 2.5f)
        {
            var mat = Art.Material(material);
            if (mat == null) return;
            var go = new GameObject("FX Debris");
            go.transform.position = at;
            go.transform.rotation = Up;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 50f; shape.radius = 0.15f;
            var force = ps.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(0f); force.y = new ParticleSystem.MinMaxCurve(0f); force.z = new ParticleSystem.MinMaxCurve(9f); // falls back (down is +Z)
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f); rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f); rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = CubeMesh;
            r.sharedMaterial = mat;
            ps.Play();
            ps.Emit(count);
            Object.Destroy(go, 1.5f);
        }

        // The signature: energy in the side's colour runs through the object (a brief glowing
        // shell) and out across the floor. Shared objects get orange and cyan rings that meet
        // and become purple.
        public static void Energy(Transform model, Vector3 at, float size, Palette.Tag tag)
        {
            if (!Available) return;
            if (tag == Palette.Tag.Both)
            {
                var a = Palette.SideA; var b = Palette.SideB;
                Ring(at, new Color(a.r, a.g, a.b, 0.8f), size * 2.4f, size * 0.5f, 0.32f);
                Ring(at + new Vector3(0f, 0f, -0.005f), new Color(b.r, b.g, b.b, 0.8f), size * 2.4f, size * 0.5f, 0.32f);
                Ring(at, new Color(Palette.Both.r, Palette.Both.g, Palette.Both.b, 0.9f), size * 0.5f, size * 2.2f, 0.55f, delay: 0.3f);
                Runner.StartCoroutine(Delayed(0.3f, () => Shell(model, Palette.Both, 0.6f)));
                return;
            }
            var c = tag == Palette.Tag.None ? new Color(0.85f, 0.88f, 1f, 0.45f) : Palette.ForTag(tag);
            Ring(at, new Color(c.r, c.g, c.b, tag == Palette.Tag.None ? 0.35f : 0.75f), size * 0.4f, size * 1.9f, 0.5f);
            Shell(model, c, tag == Palette.Tag.None ? 0.25f : 0.55f);
        }

        // A glowing copy of the model's meshes that fades out.
        public static void Shell(Transform model, Color color, float strength)
        {
            var mat = Art.Material("FX_Energy");
            if (model == null || mat == null) return;
            Runner.StartCoroutine(ShellRoutine(model, color, strength, mat));
        }

        static System.Collections.IEnumerator ShellRoutine(Transform model, Color color, float strength, Material mat)
        {
            var copies = new System.Collections.Generic.List<GameObject>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                var go = new GameObject("FX Shell");
                go.transform.SetParent(mf.transform, false);
                go.transform.localScale = Vector3.one * 1.04f;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                copies.Add(go);
            }
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = 1f - t / 0.5f;
                block.SetColor(TintId, new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, strength * k * k));
                foreach (var go in copies) if (go != null) go.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                yield return null;
            }
            foreach (var go in copies) if (go != null) Object.Destroy(go);
        }

        static readonly int TintId = Shader.PropertyToID("_TintColor");

        static System.Collections.IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        static LineRenderer NewLine(string name, Material mat, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.sortingOrder = Layers.Fx;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        static Mesh cubeMesh;
        static Mesh CubeMesh
        {
            get
            {
                if (cubeMesh != null) return cubeMesh;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
                return cubeMesh;
            }
        }

        // Hosts effect coroutines (Room and static callers have no MonoBehaviour of their own).
        class FxRunner : MonoBehaviour { }
        static FxRunner runner;
        static MonoBehaviour Runner
        {
            get
            {
                if (runner == null) runner = new GameObject("FX Runner").AddComponent<FxRunner>();
                return runner;
            }
        }

        // ---------------------------------------------------------------- building blocks

        static ParticleSystem Make(string name, Transform parent, Vector3 pos, bool additive, bool loop)
        {
            var mat = Art.Material(additive ? "FX_Additive" : "FX_Alpha");
            if (mat == null) return null;
            var go = new GameObject("FX " + name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
            }
            else go.transform.position = pos;
            go.transform.rotation = Up;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.duration = loop ? 1f : 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = Layers.Fx;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        static void Emit(ParticleSystem ps, int count, float life)
        {
            ps.Play(); // emission rate is 0; Play just keeps the emitted particles simulating
            ps.Emit(count);
            Object.Destroy(ps.gameObject, life + 0.5f);
        }

        static void Area(ParticleSystem ps, Vector2 area)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(0.2f, area.x), Mathf.Max(0.2f, area.y), 0.05f);
        }

        static void Rate(ParticleSystem ps, float perSecond)
        {
            var emission = ps.emission;
            emission.rateOverTime = perSecond;
        }

        // Constant upward push (world -Z).
        static void Rise(ParticleSystem ps, float amount)
        {
            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(0f);
            force.y = new ParticleSystem.MinMaxCurve(0f);
            force.z = new ParticleSystem.MinMaxCurve(-amount);
        }

        static void Wobble(ParticleSystem ps, float strength)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
        }

        static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
        }

        static void FadeOut(ParticleSystem ps, bool shrink, float grow = 1f)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, shrink
                ? AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f)
                : AnimationCurve.EaseInOut(0f, 0.6f, 1f, grow));
        }

        static Gradient FadeInOut()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
