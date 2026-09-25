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
            Rate(ps, 14f * area.x * area.y);
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
