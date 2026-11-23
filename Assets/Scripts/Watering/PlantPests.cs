using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CuidAR.Watering
{
    /// <summary>
    /// Second interaction: little bugs (aphids) crawl onto the plant and the user taps them on the screen to
    /// remove them. While they are on the plant they eat it (it dries faster).
    ///
    /// The bugs are 3D objects on the leaves/arms (they move with them); a tap hits a bug when it lands near
    /// the bug's projection on the screen (no physics needed: colliders are stripped from the build).
    /// </summary>
    public class PlantPests : MonoBehaviour
    {
        class Bug
        {
            public Transform root;
            public Transform body;
            public VirtualPlant.Perch perch;
            public float t, speed, direction, wiggle;
            public float squash = -1f; // >= 0 while being squashed
        }

        public int aliveCount { get; private set; }
        public int removedCount { get; private set; }

        /// <summary>A bug was tapped away (world position).</summary>
        public event System.Action<Vector3> bugRemoved;
        /// <summary>The last bug of a wave was removed.</summary>
        public event System.Action allRemoved;

        readonly List<Bug> m_Bugs = new();
        static readonly List<RaycastResult> s_UiHits = new();
        VirtualPlant m_Plant;
        Camera m_Camera;
        Material m_Shell, m_Head, m_Leg;
        ParticleSystem m_Poof;

        public static PlantPests Create(VirtualPlant plant, Camera camera)
        {
            var pests = plant.gameObject.AddComponent<PlantPests>();
            pests.m_Plant = plant;
            pests.m_Camera = camera;
            pests.m_Shell = ArtKit.Lit(new Color(0.35f, 0.75f, 0.15f), 0.8f);
            pests.m_Head = ArtKit.Lit(new Color(0.12f, 0.2f, 0.06f), 0.6f);
            pests.m_Leg = ArtKit.Lit(new Color(0.08f, 0.1f, 0.05f), 0.2f);
            pests.m_Poof = pests.CreatePoof();
            plant.partCut += pests.OnPartCut;
            return pests;
        }

        /// <summary>Puts <paramref name="count"/> bugs on random spots of the plant.</summary>
        public void SpawnWave(int count)
        {
            var perches = m_Plant.perches;
            if (perches.Count == 0)
                return;
            // Different perches when possible, so the bugs spread over the plant.
            var order = new List<int>();
            for (int i = 0; i < perches.Count; i++)
                order.Add(i);
            for (int i = 0; i < order.Count; i++)
            {
                int j = Random.Range(i, order.Count);
                (order[i], order[j]) = (order[j], order[i]);
            }
            for (int i = 0; i < count; i++)
                m_Bugs.Add(CreateBug(perches[order[i % order.Count]]));
            aliveCount += count;
        }

        public void Clear()
        {
            foreach (var bug in m_Bugs)
                if (bug.root != null)
                    Destroy(bug.root.gameObject);
            m_Bugs.Clear();
            aliveCount = 0;
        }

        Bug CreateBug(VirtualPlant.Perch perch)
        {
            var root = ArtKit.Node("Bug", perch.parent);
            var body = ArtKit.Node("Body", root);
            // Round shell, dark head, two antennae, six little legs. About 1.4 cm long.
            ArtKit.Shape(PrimitiveType.Sphere, "Shell", body, new Vector3(0f, 0.004f, 0f), new Vector3(0.011f, 0.008f, 0.014f), m_Shell);
            ArtKit.Shape(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.004f, 0.008f), Vector3.one * 0.006f, m_Head);
            for (int side = -1; side <= 1; side += 2)
            {
                var antenna = ArtKit.Shape(PrimitiveType.Cube, "Antenna", body, new Vector3(side * 0.002f, 0.007f, 0.012f),
                    new Vector3(0.0007f, 0.0007f, 0.006f), m_Leg);
                antenna.localRotation = Quaternion.Euler(-35f, side * 25f, 0f);
                for (int k = -1; k <= 1; k++)
                {
                    var leg = ArtKit.Shape(PrimitiveType.Cube, "Leg", body, new Vector3(side * 0.006f, 0.0015f, k * 0.004f),
                        new Vector3(0.006f, 0.0008f, 0.0008f), m_Leg);
                    leg.localRotation = Quaternion.Euler(0f, k * 20f * side, side * -25f);
                }
            }
            var bug = new Bug
            {
                root = root, body = body, perch = perch,
                t = Random.value, speed = Random.Range(0.18f, 0.35f),
                direction = Random.value < 0.5f ? -1f : 1f, wiggle = Random.value * 10f,
            };
            body.localScale = Vector3.zero; // grows in
            Place(bug);
            return bug;
        }

        void Place(Bug bug)
        {
            var p = bug.perch;
            var along = (p.b - p.a).normalized * bug.direction;
            bug.root.localPosition = Vector3.Lerp(p.a, p.b, bug.t) + p.normal * 0.001f;
            bug.root.localRotation = Quaternion.LookRotation(along, p.normal)
                                     * Quaternion.Euler(0f, Mathf.Sin(Time.time * 6f + bug.wiggle) * 12f, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = m_Bugs.Count - 1; i >= 0; i--)
            {
                var bug = m_Bugs[i];
                if (bug.root == null)
                {
                    m_Bugs.RemoveAt(i);
                    continue;
                }
                if (bug.squash >= 0f)
                {
                    // Squashed: flatten, then disappear.
                    bug.squash += dt;
                    float k = Mathf.Clamp01(bug.squash / 0.18f);
                    bug.body.localScale = new Vector3(1f + k * 0.6f, 1f - k * 0.85f, 1f + k * 0.6f) * (1f - Mathf.Clamp01((bug.squash - 0.25f) / 0.2f));
                    if (bug.squash > 0.45f)
                    {
                        Destroy(bug.root.gameObject);
                        m_Bugs.RemoveAt(i);
                    }
                    continue;
                }

                // Crawl back and forth along the leaf, with tiny pauses.
                bug.body.localScale = Vector3.one * Mathf.MoveTowards(bug.body.localScale.x, 1f, dt * 3f);
                float move = Mathf.Max(0f, Mathf.Sin(Time.time * 3f + bug.wiggle)) * bug.speed * dt;
                bug.t += move * bug.direction;
                if (bug.t > 1f || bug.t < 0f)
                {
                    bug.t = Mathf.Clamp01(bug.t);
                    bug.direction = -bug.direction;
                }
                bug.body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 18f + bug.wiggle)) * 0.0008f, 0f);
                Place(bug);
            }

        }

        /// <summary>Bugs on a leaf that was cut off fall with it (they count as removed).</summary>
        void OnPartCut(Transform part)
        {
            foreach (var bug in m_Bugs)
                if (bug.squash < 0f && bug.root != null && bug.root.IsChildOf(part))
                    Remove(bug);
        }

        /// <summary>Removes the bug closest to a tap on the screen, if any is near enough. Returns whether one was hit.</summary>
        public bool TryRemoveAt(Vector2 screen)
        {
            if (aliveCount == 0 || m_Camera == null)
                return false;

            // Closest living bug to the tap, within a finger-sized radius.
            float radius = Mathf.Max(60f, Screen.height * 0.05f);
            Bug best = null;
            float bestDistance = radius;
            foreach (var bug in m_Bugs)
            {
                if (bug.squash >= 0f || bug.root == null)
                    continue;
                var projected = m_Camera.WorldToScreenPoint(bug.root.position);
                if (projected.z <= 0f)
                    continue;
                float distance = Vector2.Distance(screen, projected);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = bug;
                }
            }
            if (best == null)
                return false;
            Remove(best);
            return true;
        }

        void Remove(Bug bug)
        {
            bug.squash = 0f;
            aliveCount--;
            removedCount++;
            var position = bug.root.position;
            Poof(position);
            bugRemoved?.Invoke(position);
            if (aliveCount == 0)
                allRemoved?.Invoke();
        }

        /// <summary>Whether a screen point is over a UI button (taps there are not meant for the 3D scene).</summary>
        public static bool IsOverButton(Vector2 screen)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            s_UiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, s_UiHits);
            foreach (var hit in s_UiHits)
                if (hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            return false;
        }

        void Poof(Vector3 position)
        {
            float scale = transform.lossyScale.x;
            for (int i = 0; i < 14; i++)
            {
                m_Poof.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = (Random.onUnitSphere * Random.Range(0.08f, 0.2f) + Vector3.up * 0.08f) * scale,
                    startSize = Random.Range(0.003f, 0.006f) * scale,
                    startLifetime = Random.Range(0.3f, 0.6f),
                }, 1);
            }
        }

        ParticleSystem CreatePoof()
        {
            var go = new GameObject("BugPoof");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.3f;
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.enabled = false;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = ArtKit.SphereMesh;
            renderer.sharedMaterial = ArtKit.Lit(new Color(0.65f, 0.95f, 0.45f), 0.5f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}
