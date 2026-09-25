using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EscapeOffice.Objects
{
    // The boss NPC, simulated entirely by this side's client. Patrols the path from the world
    // file and chases the player within a radius while they are in its room. Catching the
    // player applies a temporary debuff; nothing the boss does is permanent.
    //
    // World file: { "type": "boss", "x":.., "y":.., "patrol": [[x,y], ...],
    //               "chaseRadius": 4, "speed": 2.5, "chaseSpeed": 3.5, "debuff": 15 }
    public class BossController : WorldObject
    {
        const float CatchDistance = 0.6f;
        const float CatchCooldown = 4f;

        readonly List<Vector2> path = new List<Vector2>();
        float speed, chaseSpeed, chaseRadius, debuffSeconds;
        float cooldownUntil;
        bool chasing, wasChasing;

        // Patrol is deliberately erratic: waypoints in random order, detours to random floor
        // tiles in its room, short pauses, speed that varies per leg, the odd mid-leg swerve.
        Vector2 target;
        float legSpeed;
        float loiterUntil;
        float swerveUntil;
        Vector2 swerveDir;
        Vector2 lastPos;
        float stuckTime;
        const float WanderChance = 0.45f;   // pick a random tile instead of a waypoint
        const float LoiterChance = 0.35f;   // pause on arrival
        const float SwervePerStep = 0.006f; // chance per FixedUpdate to veer for a moment

        Rigidbody2D rb;
        CircleCollider2D col;
        SpriteRenderer[] eyes;

        // 3D: Boss_Drone, facing where it flies; its Halo spins faster while chasing.
        protected override string ModelName => "Boss_Drone";
        Transform halo, hover;
        Light eyeLight;
        float yaw, haloAngle;

        protected override void Build()
        {
            speed = Def.Get("speed", 2.5f);
            chaseSpeed = Def.Get("chaseSpeed", 3.5f);
            chaseRadius = Def.Get("chaseRadius", 4f);
            debuffSeconds = Def.Get("debuff", 15f);

            var points = Def.Get<int[][]>("patrol") ?? Def.Get<int[][]>("path");
            if (points != null)
                foreach (var p in points.Where(p => p != null && p.Length >= 2))
                    path.Add(World.TileCenter(p[0], p[1]));
            if (path.Count == 0) path.Add(Bounds.center);
            target = Bounds.center;
            lastPos = target;
            PickTarget();

            if (HasModel)
            {
                halo = Art.Find(model, "Halo");
                hover = Art.Find(model, "Hover");
                eyeLight = hover != null ? hover.GetComponentInChildren<Light>() : null;
                eyes = new SpriteRenderer[0];
            }
            else
            {
                body.sprite = SpriteFactory.Circle;
                body.sortingOrder = Layers.Actor;
                body.transform.localScale = Vector2.one * 0.9f;
                SetBodyColor(new Color(0.55f, 0.1f, 0.12f));
                eyes = new[]
                {
                    SpriteFactory.Child(transform, "Eye", SpriteFactory.Circle, Color.white, Layers.Actor + 1, new Vector2(-0.15f, 0.12f), Vector2.one * 0.16f),
                    SpriteFactory.Child(transform, "Eye", SpriteFactory.Circle, Color.white, Layers.Actor + 1, new Vector2(0.15f, 0.12f), Vector2.one * 0.16f),
                };
            }

            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
        }

        protected override void OnValue(JToken value) { }

        void FixedUpdate()
        {
            var player = GameManager.Instance.Player;
            if (player == null) { rb.linearVelocity = Vector2.zero; return; }

            // Walls stop the boss; the player walks straight through it (the catch is by distance).
            var playerCol = player.Collider;
            if (playerCol != null && !Physics2D.GetIgnoreCollision(col, playerCol)) Physics2D.IgnoreCollision(col, playerCol);

            Vector2 pos = rb.position;
            Vector2 target = player.Position;
            bool playerInRoom = Rooms.Count == 0 || Rooms.Any(r => r.Contains(target));
            chasing = Time.time > cooldownUntil && playerInRoom && Vector2.Distance(pos, target) < chaseRadius;

            if (chasing)
            {
                if (Vector2.Distance(pos, target) < CatchDistance)
                {
                    cooldownUntil = Time.time + CatchCooldown;
                    player.ApplyDebuff(debuffSeconds);
                    GameManager.Instance.PlayLocal("caught", pos);
                    Fx3D.Burst(new Vector3(target.x, target.y, -0.6f), new Color(1f, 0.25f, 0.2f), count: 26, speed: 3f, size: 0.18f, life: 0.6f);
                    GameManager.Instance.CameraRig.Shake(0.25f, 0.45f);
                    GameManager.Instance.Toast("The boss caught you! You feel sluggish…");
                }
                rb.linearVelocity = (target - pos).normalized * chaseSpeed;
            }
            else
            {
                if (wasChasing) PickTarget(); // lost the player: head somewhere new, not straight back
                Patrol(pos);
            }
            wasChasing = chasing;
        }

        void Patrol(Vector2 pos)
        {
            if (Time.time < loiterUntil)
            {
                rb.linearVelocity = Vector2.zero;
                stuckTime = 0f;
                return;
            }
            if (Vector2.Distance(pos, target) < 0.15f)
            {
                PickTarget();
                if (Random.value < LoiterChance)
                {
                    loiterUntil = Time.time + Random.Range(0.3f, 1.6f);
                    rb.linearVelocity = Vector2.zero;
                    return;
                }
            }

            // Stuck against a wall or furniture: give up on this target.
            if ((pos - lastPos).sqrMagnitude < 0.0004f) stuckTime += Time.fixedDeltaTime;
            else stuckTime = 0f;
            lastPos = pos;
            if (stuckTime > 0.6f) { PickTarget(); stuckTime = 0f; }

            if (Time.time > swerveUntil && Random.value < SwervePerStep)
            {
                swerveUntil = Time.time + Random.Range(0.25f, 0.6f);
                var d = (target - pos).normalized;
                swerveDir = new Vector2(-d.y, d.x) * (Random.value < 0.5f ? 1f : -1f);
            }

            var dir = (target - pos).normalized;
            if (Time.time < swerveUntil) dir = (dir + swerveDir * 0.9f).normalized;
            rb.linearVelocity = dir * legSpeed;
        }

        // Next destination: a random patrol waypoint (never the one we're at) or a random floor
        // tile inside one of the boss's rooms. Each leg gets its own speed.
        void PickTarget()
        {
            legSpeed = speed * Random.Range(0.7f, 1.35f);
            Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;

            if (Rooms.Count > 0 && Random.value < WanderChance)
            {
                for (int i = 0; i < 8; i++)
                {
                    var room = Rooms[Random.Range(0, Rooms.Count)];
                    if (room.Rects.Count == 0) continue;
                    var r = room.Rects[Random.Range(0, room.Rects.Count)];
                    int tx = Mathf.FloorToInt(Random.Range(r.xMin, r.xMax));
                    int ty = World.Height - 1 - Mathf.FloorToInt(Random.Range(r.yMin, r.yMax));
                    if (!World.IsFloor(tx, ty)) continue;
                    var p = World.TileCenter(tx, ty);
                    if (Vector2.Distance(p, pos) < 1f) continue;
                    target = p;
                    return;
                }
            }

            if (path.Count == 1) { target = path[0]; return; }
            for (int i = 0; i < 6; i++)
            {
                var wp = path[Random.Range(0, path.Count)];
                if (Vector2.Distance(wp, pos) > 0.5f) { target = wp; return; }
            }
            target = path[0];
        }

        protected override void Update()
        {
            base.Update();
            var tint = chasing ? new Color(1f, 0.3f, 0.2f) : Color.white;
            foreach (var e in eyes) e.color = tint;
            if (!HasModel) return;

            var v = rb.linearVelocity;
            if (v.sqrMagnitude > 0.01f) yaw = Mathf.LerpAngle(yaw, Art.YawFor(v), Art.Smooth(8f));
            model.transform.localRotation = Art.Rotation(yaw);
            haloAngle += (chasing ? 6f : 2f) * Mathf.Rad2Deg * Time.deltaTime;
            if (halo != null) halo.localRotation = Quaternion.Euler(0f, haloAngle, 0f);
            if (hover != null) hover.localPosition = new Vector3(0f, 0.75f + Mathf.Sin(Time.time * 2f) * 0.08f, 0f);
            // Eye light pulses red while chasing.
            if (eyeLight != null) eyeLight.intensity = chasing ? 1.2f + Mathf.Sin(Time.time * 12f) * 0.7f : 0.9f;
        }
    }
}
