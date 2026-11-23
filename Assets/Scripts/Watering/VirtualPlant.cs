using System.Collections.Generic;
using UnityEngine;

namespace CuidAR.Watering
{
    public enum PlantSpecies { Daisy, Cactus, Sunflower }

    /// <summary>
    /// The virtual potted plant the user places on a real table, built from primitives. Three species:
    /// - Daisy: leafy stem, pink flower blooms when hydrated (normal water need);
    /// - Cactus: plump column with two arms, wrinkles/shrinks and droops its arms when dry, blooms on top
    ///   (needs little water: hydrates fast);
    /// - Sunflower: tall, big leaves, a heavy head that hangs down with closed petals when dry and rises with
    ///   open yellow petals when hydrated (needs a lot of water: hydrates slowly).
    ///
    /// Everything about its look comes from <see cref="hydration"/> (0 = thirsty, 1 = hydrated), smoothed so
    /// watering looks like the plant slowly "drinking". A floating water drop asks for water when thirsty.
    /// </summary>
    public class VirtualPlant : MonoBehaviour
    {
        // Sizes in meters (real scale: a pot about 10 cm wide).
        public const float PotRadius = 0.05f;
        public const float SoilHeight = 0.085f; // top of the soil

        static readonly Color k_DryLeaf = new(0.66f, 0.56f, 0.24f);
        static readonly Color k_HealthyLeaf = new(0.16f, 0.58f, 0.2f);
        static readonly Color k_DrySoil = new(0.6f, 0.47f, 0.33f);
        static readonly Color k_WetSoil = new(0.2f, 0.12f, 0.07f);
        static readonly Color k_Withered = new(0.45f, 0.29f, 0.12f);
        const float k_RegrowSeconds = 6f;

        /// <summary>Anything that rotates around its local X and changes color with hydration (leaf, arm, petal).</summary>
        class Part
        {
            public Transform pitch;
            public Transform mesh;
            public Material material;
            public Vector3 baseScale;
            public float phase;
            public float dryPitch, wetPitch;
            public Color dry, healthy;
            public float flutter = 1f;
            // Pruning (leaves and cactus arms only).
            public bool cuttable;
            public Transform cutRoot;   // what falls off when cut
            public Transform target;    // where the scissors aim
            public bool withered;       // dead/brown: should be pruned
            public float regrow = 1f;   // 0 = just cut, 1 = fully grown
        }

        /// <summary>A line on the plant surface where pests can crawl (local to <see cref="parent"/>).</summary>
        public struct Perch
        {
            public Transform parent;
            public Vector3 a, b, normal;
        }

        [Range(0f, 1f)] public float hydration = 0.1f;

        public PlantSpecies species { get; private set; }
        public float displayedHydration => m_Display;
        public IReadOnlyList<Perch> perches => m_Perches;

        /// <summary>A leaf/arm was cut off (its root transform, e.g. so bugs on it fall too).</summary>
        public event System.Action<Transform> partCut;

        public int witheredCount
        {
            get
            {
                int count = 0;
                foreach (var part in m_Parts)
                    if (part.withered)
                        count++;
                return count;
            }
        }

        readonly List<Part> m_Parts = new();
        readonly List<Perch> m_Perches = new();
        readonly List<(Material material, Color dry, Color healthy)> m_Tinted = new();
        Transform m_Content;
        Transform m_Stem;
        Transform m_Flower;   // scales in when hydrated (daisy, cactus)
        Transform m_Head;     // bows down when dry (sunflower)
        Transform m_Plump;    // shrinks sideways when dry (cactus)
        Transform m_ThirstIcon;
        Material m_SoilMaterial;
        ParticleSystem m_Sparkles;
        float m_StemLean = 7f;
        Vector3 m_IconPosition;

        float m_Display;
        float m_DisplayVelocity;
        float m_Pulse;
        float m_Appear;

        public Vector3 SoilCenter => transform.TransformPoint(new Vector3(0f, SoilHeight, 0f));

        public static string DisplayName(PlantSpecies species) => species switch
        {
            PlantSpecies.Cactus => "Cactus",
            PlantSpecies.Sunflower => "Sunflower",
            _ => "Daisy",
        };

        /// <summary>How fast watering fills this species (cactus needs little water, sunflower a lot).</summary>
        public static float WaterSpeed(PlantSpecies species) => species switch
        {
            PlantSpecies.Cactus => 2f,
            PlantSpecies.Sunflower => 0.7f,
            _ => 1f,
        };

        public static VirtualPlant Create(Transform parent, PlantSpecies species)
        {
            var go = new GameObject("VirtualPlant " + species);
            go.transform.SetParent(parent, false);
            var plant = go.AddComponent<VirtualPlant>();
            plant.species = species;
            plant.Build();
            return plant;
        }

        void Build()
        {
            m_Content = ArtKit.Node("Content", transform);
            m_Display = hydration;

            // Pot: terracotta body, darker rim, soil on top.
            var terracotta = ArtKit.Lit(new Color(0.78f, 0.38f, 0.22f), 0.2f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Pot", m_Content, new Vector3(0f, 0.04f, 0f),
                new Vector3(PotRadius * 1.8f, 0.04f, PotRadius * 1.8f), terracotta);
            ArtKit.Shape(PrimitiveType.Cylinder, "Rim", m_Content, new Vector3(0f, 0.076f, 0f),
                new Vector3(PotRadius * 2.1f, 0.005f, PotRadius * 2.1f), ArtKit.Lit(new Color(0.66f, 0.3f, 0.17f), 0.2f));
            m_SoilMaterial = ArtKit.Lit(k_DrySoil, 0.1f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Soil", m_Content, new Vector3(0f, SoilHeight - 0.003f, 0f),
                new Vector3(PotRadius * 1.85f, 0.003f, PotRadius * 1.85f), m_SoilMaterial);

            // Stem pivot at the soil, so the whole plant can lean when dry.
            m_Stem = ArtKit.Node("Stem", m_Content, new Vector3(0f, SoilHeight, 0f));
            switch (species)
            {
                case PlantSpecies.Cactus: BuildCactus(); break;
                case PlantSpecies.Sunflower: BuildSunflower(); break;
                default: BuildDaisy(); break;
            }

            // Floating water drop right above the plant: "I'm thirsty, water me". It fades away when the phone
            // gets close (so it never blocks the lens) and once the plant has had enough water.
            m_IconPosition = new Vector3(0f, species switch
            {
                PlantSpecies.Cactus => 0.29f,
                PlantSpecies.Sunflower => 0.36f,
                _ => 0.31f,
            }, 0f);
            m_ThirstIcon = ArtKit.Node("ThirstIcon", transform, m_IconPosition);
            var dropMaterial = ArtKit.Lit(new Color(0.2f, 0.55f, 1f), 0.9f);
            ArtKit.Shape(PrimitiveType.Sphere, "Drop", m_ThirstIcon, Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), dropMaterial);
            // Narrow elongated sphere on top turns the ball into a teardrop.
            ArtKit.Shape(PrimitiveType.Sphere, "Tip", m_ThirstIcon, new Vector3(0f, 0.013f, 0f),
                new Vector3(0.014f, 0.032f, 0.014f), dropMaterial);

            m_Sparkles = CreateSparkles();
            WitherRandom(species == PlantSpecies.Cactus ? 1 : 2); // something to prune from the start
            transform.localScale = Vector3.zero; // pops in (see Update)
        }

        // ---------- species ----------

        void BuildDaisy()
        {
            const float stemHeight = 0.15f;
            ArtKit.Shape(PrimitiveType.Cylinder, "StemMesh", m_Stem, new Vector3(0f, stemHeight / 2f, 0f),
                new Vector3(0.007f, stemHeight / 2f, 0.007f), ArtKit.Lit(new Color(0.25f, 0.45f, 0.15f)));

            // Leaves spiral up the stem (golden angle), bigger at the bottom.
            const int leafCount = 9;
            for (int i = 0; i < leafCount; i++)
            {
                float t = i / (leafCount - 1f);
                float length = Mathf.Lerp(0.085f, 0.045f, t);
                AddLeaf(m_Stem, Mathf.Lerp(0.02f, stemHeight - 0.01f, t), i * 137.5f, length, 0.42f, 22f, -35f);
            }

            m_Flower = BuildFlower(m_Stem, new Vector3(0f, stemHeight + 0.005f, 0f), new Color(0.95f, 0.45f, 0.65f), 1f);
        }

        void BuildCactus()
        {
            m_StemLean = 3f;
            var dry = new Color(0.6f, 0.62f, 0.32f);
            var healthy = new Color(0.18f, 0.52f, 0.24f);
            var skin = ArtKit.Lit(dry, 0.35f);
            m_Tinted.Add((skin, dry, healthy));

            m_Plump = ArtKit.Node("Body", m_Stem);
            const float radius = 0.028f, height = 0.12f;
            ArtKit.Shape(PrimitiveType.Cylinder, "Column", m_Plump, new Vector3(0f, height / 2f, 0f),
                new Vector3(radius * 2f, height / 2f, radius * 2f), skin);
            ArtKit.Shape(PrimitiveType.Sphere, "Top", m_Plump, new Vector3(0f, height, 0f), Vector3.one * radius * 2f, skin);

            // Two arms (elbow shape) that droop when dry.
            for (int i = 0; i < 2; i++)
            {
                var yaw = ArtKit.Node($"Arm{i}", m_Plump, new Vector3(0f, i == 0 ? 0.055f : 0.075f, 0f));
                yaw.localRotation = Quaternion.Euler(0f, i == 0 ? 90f : -90f, 0f);
                var pitch = ArtKit.Node("Pitch", yaw);
                var arm = ArtKit.Node("Arm", pitch);
                var armSkin = ArtKit.Lit(dry, 0.35f); // own material, so one arm can wither alone
                ArtKit.Shape(PrimitiveType.Cylinder, "Out", arm, new Vector3(0f, 0f, radius + 0.012f),
                    new Vector3(0.026f, 0.014f, 0.026f), armSkin).localRotation = Quaternion.Euler(90f, 0f, 0f);
                var up = ArtKit.Shape(PrimitiveType.Cylinder, "Up", arm, new Vector3(0f, 0.022f, radius + 0.026f),
                    new Vector3(0.024f, 0.022f, 0.024f), armSkin);
                ArtKit.Shape(PrimitiveType.Sphere, "Cap", arm, new Vector3(0f, 0.044f, radius + 0.026f), Vector3.one * 0.024f, armSkin);
                m_Parts.Add(new Part
                {
                    pitch = pitch, mesh = arm, material = armSkin, baseScale = Vector3.one, dryPitch = 35f, wetPitch = 0f,
                    flutter = 0.3f, phase = i * 3f, dry = dry, healthy = healthy,
                    cuttable = true, cutRoot = arm, target = up,
                });
                m_Perches.Add(new Perch { parent = arm, a = new Vector3(0f, 0.005f, radius + 0.04f), b = new Vector3(0f, 0.045f, radius + 0.04f), normal = Vector3.forward });
            }

            // Spines.
            var spine = ArtKit.Lit(new Color(0.95f, 0.93f, 0.85f), 0.2f);
            var random = new System.Random(3);
            for (int i = 0; i < 26; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float y = 0.01f + (float)random.NextDouble() * (height - 0.01f);
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var s = ArtKit.Shape(PrimitiveType.Cube, "Spine", m_Plump, dir * (radius + 0.003f) + Vector3.up * y,
                    new Vector3(0.0015f, 0.0015f, 0.008f), spine);
                s.localRotation = Quaternion.LookRotation(dir);
            }

            // Crawling lines up the column, on four sides.
            for (int i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward;
                m_Perches.Add(new Perch { parent = m_Plump, a = dir * radius + Vector3.up * 0.02f, b = dir * radius + Vector3.up * 0.11f, normal = dir });
            }

            m_Flower = BuildFlower(m_Plump, new Vector3(0f, height + radius * 0.8f, 0f), new Color(0.98f, 0.35f, 0.55f), 0.8f);
        }

        void BuildSunflower()
        {
            m_StemLean = 5f;
            const float stemHeight = 0.2f;
            ArtKit.Shape(PrimitiveType.Cylinder, "StemMesh", m_Stem, new Vector3(0f, stemHeight / 2f, 0f),
                new Vector3(0.01f, stemHeight / 2f, 0.01f), ArtKit.Lit(new Color(0.3f, 0.5f, 0.18f)));

            // Few big, wide leaves on the lower part of the stem.
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                AddLeaf(m_Stem, Mathf.Lerp(0.025f, 0.12f, t), i * 137.5f + 20f, Mathf.Lerp(0.09f, 0.06f, t), 0.62f, 28f, -20f);
            }

            // Head: brown disc facing up (local +Y), petals around; it bows down when dry.
            m_Head = ArtKit.Node("Head", m_Stem, new Vector3(0f, stemHeight, 0f));
            var face = ArtKit.Node("Face", m_Head, new Vector3(0f, 0f, 0.012f));
            ArtKit.Shape(PrimitiveType.Cylinder, "Disc", face, Vector3.zero, new Vector3(0.06f, 0.006f, 0.06f),
                ArtKit.Lit(new Color(0.32f, 0.2f, 0.1f), 0.2f));
            ArtKit.Shape(PrimitiveType.Sphere, "Back", face, new Vector3(0f, -0.006f, 0f), new Vector3(0.05f, 0.012f, 0.05f),
                ArtKit.Lit(new Color(0.3f, 0.5f, 0.18f)));
            for (int i = 0; i < 14; i++)
            {
                var yaw = ArtKit.Node($"Petal{i}", face);
                yaw.localRotation = Quaternion.Euler(0f, i * (360f / 14f), 0f);
                var pitch = ArtKit.Node("Pitch", yaw, new Vector3(0f, 0.002f, 0.028f));
                var material = ArtKit.Lit(new Color(0.55f, 0.42f, 0.15f), 0.4f);
                var scale = new Vector3(0.014f, 0.003f, 0.034f);
                var mesh = ArtKit.Shape(PrimitiveType.Sphere, "Blade", pitch, new Vector3(0f, 0f, 0.016f), scale, material);
                // Closed (pointing up, around the disc) when dry, open flat when hydrated.
                m_Parts.Add(new Part
                {
                    pitch = pitch, mesh = mesh, material = material, baseScale = scale, phase = i,
                    dryPitch = -70f, wetPitch = -8f, dry = new Color(0.55f, 0.42f, 0.15f), healthy = new Color(1f, 0.8f, 0.08f), flutter = 0.3f,
                });
            }
        }

        void AddLeaf(Transform parent, float height, float yawDegrees, float length, float width, float dryPitch, float wetPitch)
        {
            var yaw = ArtKit.Node("Leaf", parent, new Vector3(0f, height, 0f));
            yaw.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var pitch = ArtKit.Node("Pitch", yaw);
            var healthy = k_HealthyLeaf * Random.Range(0.85f, 1.15f);
            healthy.a = 1f;
            var material = ArtKit.Lit(k_DryLeaf, 0.45f);
            var scale = new Vector3(length * width, 0.004f, length);
            var mesh = ArtKit.Shape(PrimitiveType.Sphere, "Blade", pitch, new Vector3(0f, 0f, length * 0.5f), scale, material);
            m_Parts.Add(new Part
            {
                pitch = pitch, mesh = mesh, material = material, baseScale = scale, phase = Random.value * 10f,
                dryPitch = dryPitch, wetPitch = wetPitch, dry = k_DryLeaf, healthy = healthy,
                cuttable = true, cutRoot = mesh, target = mesh,
            });
            m_Perches.Add(new Perch { parent = pitch, a = new Vector3(0f, 0.002f, length * 0.2f), b = new Vector3(0f, 0.002f, length * 0.85f), normal = Vector3.up });
        }

        static Transform BuildFlower(Transform parent, Vector3 position, Color petals, float size)
        {
            var flower = ArtKit.Node("Flower", parent, position);
            ArtKit.Shape(PrimitiveType.Sphere, "Center", flower, Vector3.zero, Vector3.one * 0.02f * size,
                ArtKit.Lit(new Color(1f, 0.82f, 0.15f), 0.5f));
            var petalMaterial = ArtKit.Lit(petals, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                var petal = ArtKit.Node($"Petal{i}", flower);
                petal.localRotation = Quaternion.Euler(-20f, i * 60f, 0f);
                ArtKit.Shape(PrimitiveType.Sphere, "Blade", petal, new Vector3(0f, 0f, 0.018f * size),
                    new Vector3(0.018f, 0.005f, 0.03f) * size, petalMaterial);
            }
            flower.localScale = Vector3.zero;
            return flower;
        }

        ParticleSystem CreateSparkles()
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.enabled = false;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = ArtKit.SphereMesh;
            renderer.sharedMaterial = ArtKit.Lit(new Color(1f, 0.95f, 0.5f), 0.9f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // ---------- pruning ----------

        /// <summary>Turns <paramref name="count"/> random healthy leaves (or cactus arms) brown and dead.</summary>
        public void WitherRandom(int count)
        {
            var candidates = new List<Part>();
            foreach (var part in m_Parts)
                if (part.cuttable && !part.withered && part.regrow >= 1f)
                    candidates.Add(part);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                candidates[index].withered = true;
                candidates.RemoveAt(index);
            }
        }

        /// <summary>
        /// The cuttable part whose projection on the screen is closest to <paramref name="screen"/> (within
        /// <paramref name="radius"/> pixels). Dead leaves win over healthy ones at similar distance.
        /// </summary>
        public int FindCuttable(Camera camera, Vector2 screen, float radius, out Vector3 worldPoint)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            worldPoint = default;
            for (int i = 0; i < m_Parts.Count; i++)
            {
                var part = m_Parts[i];
                if (!part.cuttable || part.regrow < 0.6f)
                    continue;
                var projected = camera.WorldToScreenPoint(part.target.position);
                if (projected.z <= 0f)
                    continue;
                float distance = Vector2.Distance(screen, projected);
                if (distance > radius)
                    continue;
                float score = distance - (part.withered ? radius * 0.35f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                    worldPoint = part.target.position;
                }
            }
            return best;
        }

        /// <summary>Cuts a part off: a copy falls to the table and the part regrows. Returns whether it was dead.</summary>
        public bool Cut(int index)
        {
            var part = m_Parts[index];
            bool wasWithered = part.withered;
            partCut?.Invoke(part.pitch); // bugs sit under the pitch node (leaves) or the arm inside it

            // Falling copy with frozen (copied) materials, without any bug that was on it.
            var source = part.cutRoot;
            var copy = Instantiate(source.gameObject, source.position, source.rotation);
            copy.name = "Cut " + source.name;
            copy.transform.localScale = source.lossyScale;
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                var original = renderer.sharedMaterial;
                if (!copies.TryGetValue(original, out var clone))
                    copies[original] = clone = new Material(original);
                renderer.sharedMaterial = clone;
            }
            foreach (var child in copy.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name == "Bug")
                    Destroy(child.gameObject);
            FallingPiece.Drop(copy, transform.position.y, transform.lossyScale.x);

            part.withered = false;
            part.regrow = 0f;
            part.pitch.localScale = Vector3.zero;
            m_Pulse = Mathf.Max(m_Pulse, 0.5f);
            return wasWithered;
        }

        /// <summary>Small bounce when a splash of water reaches the soil.</summary>
        public void Splash() => m_Pulse = Mathf.Max(m_Pulse, 0.6f);

        /// <summary>Sparkles and a big bounce (fully hydrated, or pests removed).</summary>
        public void Celebrate(int sparkles = 60)
        {
            m_Pulse = 1.5f;
            var scale = transform.lossyScale.x;
            for (int i = 0; i < sparkles; i++)
            {
                var offset = Random.insideUnitSphere * 0.12f;
                offset.y = Random.Range(0.05f, 0.28f);
                m_Sparkles.Emit(new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(offset),
                    velocity = (Vector3.up * Random.Range(0.03f, 0.12f) + Random.insideUnitSphere * 0.05f) * scale,
                    startSize = Random.Range(0.006f, 0.014f) * scale,
                    startLifetime = Random.Range(0.8f, 1.6f),
                }, 1);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time;

            // Pop-in when the plant is placed (or the species changes).
            m_Appear = Mathf.MoveTowards(m_Appear, 1f, dt * 1.8f);
            float appear = 1f + 2.7f * Mathf.Pow(m_Appear - 1f, 3f) + 1.7f * Mathf.Pow(m_Appear - 1f, 2f); // ease-out-back
            transform.localScale = Vector3.one * Mathf.Max(0f, appear);

            m_Display = Mathf.SmoothDamp(m_Display, hydration, ref m_DisplayVelocity, 0.6f);
            float h = m_Display;

            // Bounce (squash & stretch) when water arrives.
            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, dt * 2.5f);
            float bounce = Mathf.Sin(time * 22f) * 0.06f * m_Pulse;
            m_Content.localScale = new Vector3(1f - bounce * 0.5f, 1f + bounce, 1f - bounce * 0.5f);

            // Dry plants lean and droop; hydrated plants stand up.
            float sway = Mathf.Sin(time * 1.3f) * Mathf.Lerp(1.5f, 3f, h);
            m_Stem.localRotation = Quaternion.Euler(Mathf.Lerp(m_StemLean, 0f, h) + sway * 0.4f, 0f, sway * 0.3f);
            m_Stem.localScale = new Vector3(1f, Mathf.Lerp(0.88f, 1f, h), 1f);

            foreach (var part in m_Parts)
            {
                if (part.regrow < 1f)
                {
                    // Regrowing after a cut: sprouts from nothing with a little overshoot.
                    part.regrow = Mathf.Min(1f, part.regrow + dt / k_RegrowSeconds);
                    float g = part.regrow;
                    part.pitch.localScale = Vector3.one * (1f + 2.2f * Mathf.Pow(g - 1f, 3f) + 1.2f * Mathf.Pow(g - 1f, 2f));
                }
                float flutter = Mathf.Sin(time * 2.1f + part.phase) * Mathf.Lerp(2f, 5f, h) * part.flutter;
                float pitch = part.withered ? part.dryPitch + 20f : Mathf.Lerp(part.dryPitch, part.wetPitch, h);
                part.pitch.localRotation = Quaternion.Euler(pitch + flutter * (part.withered ? 0.3f : 1f), 0f, 0f);
                if (part.material != null)
                {
                    float plump = part.withered ? 0.75f : h;
                    part.mesh.localScale = Vector3.Scale(part.baseScale, new Vector3(Mathf.Lerp(0.8f, 1.05f, plump), 1f, Mathf.Lerp(0.9f, 1.05f, plump)));
                    ArtKit.SetColor(part.material, part.withered ? k_Withered : Color.Lerp(part.dry, part.healthy, h));
                }
            }
            foreach (var (material, dry, healthy) in m_Tinted)
                ArtKit.SetColor(material, Color.Lerp(dry, healthy, h));

            if (m_Plump != null)
            {
                // Cactus: shrivels (thinner, a bit shorter) when dry.
                float plump = Mathf.Lerp(0.78f, 1f, h);
                m_Plump.localScale = new Vector3(plump, Mathf.Lerp(0.92f, 1f, h), plump);
            }
            if (m_Head != null)
            {
                // Sunflower: the head hangs when dry and faces up/forward when hydrated.
                m_Head.localRotation = Quaternion.Euler(Mathf.Lerp(115f, 25f, h) + sway * 0.5f, 0f, 0f);
            }

            ArtKit.SetColor(m_SoilMaterial, Color.Lerp(k_DrySoil, k_WetSoil, Mathf.Clamp01(h * 1.3f)));

            if (m_Flower != null)
            {
                float bloom = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, h));
                m_Flower.localScale = Vector3.one * bloom;
                m_Flower.localRotation = Quaternion.Euler(0f, time * 15f, 0f);
            }

            float thirst = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.55f, h));
            // Fade out when the phone gets close, so it never blocks the view while watering.
            var cam = Camera.main;
            if (cam != null)
            {
                float distance = Vector3.Distance(cam.transform.position, m_ThirstIcon.position);
                thirst *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.35f, distance));
            }
            m_ThirstIcon.localScale = Vector3.one * thirst * 0.6f * (1f + Mathf.Sin(time * 4f) * 0.08f);
            m_ThirstIcon.localPosition = m_IconPosition + new Vector3(0f, Mathf.Sin(time * 2.5f) * 0.008f, 0f);
            m_ThirstIcon.localRotation = Quaternion.Euler(0f, time * 60f, 0f);
        }
    }
}
