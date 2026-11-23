using UnityEngine;

namespace CuidAR.Watering
{
    /// <summary>
    /// Turns the phone into a watering can.
    ///
    /// Aim: the water goes where the phone points. The center of the screen (the "+" crosshair) is cast onto the
    /// table; that spot is where the stream lands. Pour: tip the phone sideways (roll, either side), like tipping a
    /// real watering can; past <see cref="startPourRoll"/> degrees the water flows, stronger the more you tip.
    ///
    /// The can is drawn at the bottom-right of the view with its spout aimed at the center of the screen, and it
    /// visibly tips while pouring. The water is a real-scale ballistic stream (gravity) in world space whose launch
    /// velocity is solved so it leaves the spout and lands on the aimed spot.
    /// </summary>
    public class WateringCan : MonoBehaviour
    {
        /// <summary>Hose: hold the screen to spray a fast jet. Can: tilt the phone sideways to pour.</summary>
        public enum Mode { Hose, Can }

        [Tooltip("Sideways tilt of the phone (degrees) that starts the water.")]
        public float startPourRoll = 25f;
        [Tooltip("Tilt below which the water stops (lower than the start value, so it doesn't flicker).")]
        public float stopPourRoll = 18f;
        [Tooltip("Tilt for the strongest stream.")]
        public float fullPourRoll = 55f;
        [Tooltip("Droplets per second at full tilt.")]
        public float dropletsPerSecond = 170f;
        [Tooltip("Speed of the stream (m/s). Fast + light gravity = a gentle arc that reads well from the phone's viewpoint.")]
        public float streamSpeed = 1.8f;
        [Tooltip("Gravity on the drops (1 = real). Real gravity needs a high lob that looks like a loop through the lens.")]
        public float streamGravity = 0.35f;

        /// <summary>Extra roll in degrees, used by the Editor to simulate tilting the phone.</summary>
        [HideInInspector] public float simulatedRoll;
        /// <summary>Hose trigger (finger held on the screen), set by the controller.</summary>
        [HideInInspector] public bool triggerHeld;

        public Mode mode { get; private set; } = Mode.Hose;

        public bool isPouring { get; private set; }
        /// <summary>0 = just started pouring, 1 = fully tipped.</summary>
        public float flow { get; private set; }
        /// <summary>The crosshair hits the table/plant area.</summary>
        public bool hasAim { get; private set; }
        /// <summary>Where the water lands (on the soil when aiming at the pot, else on the table).</summary>
        public Vector3 aimPoint { get; private set; }
        /// <summary>The crosshair is on the pot's soil.</summary>
        public bool aimOnPot { get; private set; }

        Transform m_Camera;
        Transform m_CanRoot;
        Transform m_Body;
        Transform m_Spout;
        Transform m_HoseRoot;
        Transform m_Nozzle;
        Transform m_Trigger;
        Transform m_HoseTip;
        ParticleSystem m_Water;
        float m_EmitBudget;
        float m_Tip;

        Vector3 m_SoilCenter;
        float m_SoilRadius;
        float m_TableHeight;

        public static WateringCan Create(Camera camera)
        {
            var go = new GameObject("WateringCan");
            go.transform.SetParent(camera.transform, false);
            var can = go.AddComponent<WateringCan>();
            can.Build(camera.transform);
            return can;
        }

        void Build(Transform cameraTransform)
        {
            m_Camera = cameraTransform;
            // Bottom-right of the view (the controller lowers the camera near plane so it is not clipped).
            transform.localPosition = new Vector3(0.035f, -0.1f, 0.3f);
            transform.localScale = Vector3.one * 0.8f;

            m_CanRoot = ArtKit.Node("Can", transform);
            m_Body = ArtKit.Node("Body", m_CanRoot);

            var green = ArtKit.Lit(new Color(0.18f, 0.55f, 0.35f), 0.6f);
            var dark = ArtKit.Lit(new Color(0.1f, 0.32f, 0.2f), 0.6f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Tank", m_Body, Vector3.zero, new Vector3(0.05f, 0.022f, 0.05f), green);
            ArtKit.Shape(PrimitiveType.Cylinder, "Lid", m_Body, new Vector3(0f, 0.023f, 0f), new Vector3(0.045f, 0.002f, 0.045f), dark);
            var handle = ArtKit.Shape(PrimitiveType.Cube, "Handle", m_Body, new Vector3(0.03f, 0.018f, 0f), new Vector3(0.008f, 0.04f, 0.008f), dark);
            handle.localRotation = Quaternion.Euler(0f, 0f, 25f);

            // Spout from the tank toward the center of the screen (up, left and into the scene).
            var spoutDirection = new Vector3(-0.5f, 0.55f, 0.67f).normalized;
            var spoutArm = ArtKit.Node("SpoutArm", m_Body, new Vector3(-0.018f, -0.006f, 0.01f));
            spoutArm.localRotation = Quaternion.FromToRotation(Vector3.up, spoutDirection);
            ArtKit.Shape(PrimitiveType.Cylinder, "Spout", spoutArm, new Vector3(0f, 0.032f, 0f), new Vector3(0.006f, 0.032f, 0.006f), green);
            ArtKit.Shape(PrimitiveType.Cylinder, "Rose", spoutArm, new Vector3(0f, 0.066f, 0f), new Vector3(0.014f, 0.004f, 0.014f), dark);
            m_Spout = ArtKit.Node("SpoutTip", spoutArm, new Vector3(0f, 0.07f, 0f));

            BuildHose();
            m_Water = CreateWater();
            SetMode(Mode.Hose);
        }

        void BuildHose()
        {
            m_HoseRoot = ArtKit.Node("Hose", transform, new Vector3(-0.01f, 0.005f, -0.02f));
            // Spray nozzle (pistol grip), barrel aimed toward the center of the screen.
            m_Nozzle = ArtKit.Node("Nozzle", m_HoseRoot);
            m_Nozzle.localRotation = Quaternion.LookRotation(new Vector3(-0.2f, 0.4f, 1f).normalized, Vector3.up);
            var yellow = ArtKit.Lit(new Color(0.98f, 0.68f, 0.1f), 0.5f);
            var grey = ArtKit.Lit(new Color(0.22f, 0.24f, 0.26f), 0.4f);
            var black = ArtKit.Lit(new Color(0.08f, 0.08f, 0.08f), 0.6f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Barrel", m_Nozzle, new Vector3(0f, 0f, 0.035f),
                new Vector3(0.016f, 0.035f, 0.016f), yellow).localRotation = Quaternion.Euler(90f, 0f, 0f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Head", m_Nozzle, new Vector3(0f, 0f, 0.072f),
                new Vector3(0.022f, 0.005f, 0.022f), black).localRotation = Quaternion.Euler(90f, 0f, 0f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Back", m_Nozzle, new Vector3(0f, 0f, -0.002f),
                new Vector3(0.02f, 0.004f, 0.02f), grey).localRotation = Quaternion.Euler(90f, 0f, 0f);
            var grip = ArtKit.Shape(PrimitiveType.Cube, "Grip", m_Nozzle, new Vector3(0f, -0.026f, 0.004f),
                new Vector3(0.014f, 0.05f, 0.018f), grey);
            grip.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            m_Trigger = ArtKit.Node("TriggerPivot", m_Nozzle, new Vector3(0f, -0.006f, 0.022f));
            ArtKit.Shape(PrimitiveType.Cube, "Trigger", m_Trigger, new Vector3(0f, -0.012f, 0f),
                new Vector3(0.005f, 0.024f, 0.006f), yellow);
            m_HoseTip = ArtKit.Node("NozzleTip", m_Nozzle, new Vector3(0f, 0f, 0.078f));

            // Green garden hose from the bottom of the grip, curving down out of view.
            var green = ArtKit.Lit(new Color(0.15f, 0.55f, 0.2f), 0.55f);
            var points = new[]
            {
                new Vector3(0f, -0.05f, -0.004f), new Vector3(0f, -0.075f, -0.014f), new Vector3(0.006f, -0.098f, -0.034f),
                new Vector3(0.014f, -0.12f, -0.06f), new Vector3(0.02f, -0.15f, -0.09f),
            };
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var segment = ArtKit.Shape(PrimitiveType.Cylinder, "Tube", m_Nozzle, (a + b) * 0.5f,
                    new Vector3(0.017f, (b - a).magnitude * 0.5f + 0.003f, 0.017f), green);
                segment.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            }
        }

        /// <summary>Selects the hose or the watering can (and shows/hides the whole tool).</summary>
        public void SetMode(Mode newMode)
        {
            mode = newMode;
            m_CanRoot.gameObject.SetActive(mode == Mode.Can);
            m_HoseRoot.gameObject.SetActive(mode == Mode.Hose);
            isPouring = false;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible)
                return;
            gameObject.SetActive(visible);
            isPouring = false;
            flow = 0f;
        }

        ParticleSystem CreateWater()
        {
            var go = new GameObject("Water");
            go.transform.SetParent(null, false); // world space, independent of the phone
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = streamGravity;
            main.maxParticles = 1500;
            var emission = ps.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = ArtKit.SphereMesh;
            renderer.sharedMaterial = ArtKit.Lit(new Color(0.35f, 0.7f, 1f), 0.95f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Splash: droplets jump up when a drop dies on the soil/table.
            var splashGo = new GameObject("Splash");
            splashGo.transform.SetParent(go.transform, false);
            var splash = splashGo.AddComponent<ParticleSystem>();
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var splashMain = splash.main;
            splashMain.playOnAwake = false;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.gravityModifier = 1f;
            splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.004f);
            splashMain.maxParticles = 2000;
            var splashEmission = splash.emission;
            splashEmission.rateOverTime = 0f;
            splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 4) });
            var shape = splash.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.004f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // hemisphere opens upward
            var splashRenderer = splashGo.GetComponent<ParticleSystemRenderer>();
            splashRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            splashRenderer.mesh = ArtKit.SphereMesh;
            splashRenderer.sharedMaterial = renderer.sharedMaterial;
            splashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var subEmitters = ps.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(splash, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

            ps.Play();
            return ps;
        }

        /// <summary>The pot's soil (a disc) and the table height around it.</summary>
        public void SetTarget(Vector3 soilCenter, float soilRadius, float tableHeight)
        {
            m_SoilCenter = soilCenter;
            m_SoilRadius = soilRadius;
            m_TableHeight = tableHeight;
        }

        static bool RayToHeight(Vector3 origin, Vector3 direction, float height, out Vector3 point)
        {
            point = origin;
            if (direction.y > -0.05f)
                return false; // looking level or up: never reaches the table
            float t = (height - origin.y) / direction.y;
            if (t <= 0f || t > 3f)
                return false;
            point = origin + direction * t;
            return true;
        }

        void UpdateAim()
        {
            var origin = m_Camera.position;
            var direction = m_Camera.forward;
            aimOnPot = false;
            hasAim = false;
            if (RayToHeight(origin, direction, m_SoilCenter.y, out var onSoil))
            {
                var flat = onSoil - m_SoilCenter;
                flat.y = 0f;
                if (flat.magnitude <= m_SoilRadius)
                {
                    aimOnPot = hasAim = true;
                    aimPoint = onSoil;
                    return;
                }
            }
            if (RayToHeight(origin, direction, m_TableHeight, out var onTable))
            {
                hasAim = true;
                aimPoint = onTable;
            }
        }

        void LateUpdate()
        {
            if (m_Camera == null)
                return;

            UpdateAim();

            // Sideways tilt of the phone around its viewing axis, relative to gravity (0 = held level).
            float roll = Mathf.Abs(Mathf.Asin(Mathf.Clamp(-m_Camera.right.y, -1f, 1f)) * Mathf.Rad2Deg) + Mathf.Abs(simulatedRoll);
            if (!hasAim && simulatedRoll > 0f)
            {
                // Editor test (no real table): pour onto the pot.
                hasAim = aimOnPot = true;
                aimPoint = m_SoilCenter;
            }
            if (mode == Mode.Hose)
            {
                isPouring = hasAim && (triggerHeld || simulatedRoll > startPourRoll);
                flow = isPouring ? 1f : 0f;
            }
            else
            {
                isPouring = hasAim && (isPouring ? roll > stopPourRoll : roll > startPourRoll);
                flow = isPouring ? Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(startPourRoll, fullPourRoll, roll)) : 0f;
            }

            // The can tips toward the center of the screen; the hose trigger is squeezed and the nozzle kicks back.
            m_Tip = Mathf.MoveTowards(m_Tip, isPouring ? 1f : 0f, Time.deltaTime * (mode == Mode.Hose ? 10f : 4f));
            if (mode == Mode.Can)
            {
                m_Body.localRotation = Quaternion.Euler(m_Tip * 20f, 0f, m_Tip * 25f);
            }
            else
            {
                m_Trigger.localRotation = Quaternion.Euler(-m_Tip * 25f, 0f, 0f);
                m_Nozzle.localPosition = new Vector3(0f, 0f, -0.006f * m_Tip) + Random.insideUnitSphere * (0.0006f * m_Tip);
            }

            if (!isPouring)
            {
                m_EmitBudget = 0f;
                return;
            }

            // Short flights with light gravity: an almost straight stream from the spout to the crosshair that
            // only sags a little. (With real gravity the drops had to be thrown up toward the lens first and the
            // stream looked like it made a loop.)
            bool hose = mode == Mode.Hose;
            var start = hose ? m_HoseTip.position : m_Spout.position;
            float distance = Vector3.Distance(start, aimPoint);
            // The hose is a fast, tight jet; the can a softer sprinkle.
            float flightTime = hose ? Mathf.Clamp(distance / 3.2f, 0.06f, 0.2f) : Mathf.Clamp(distance / streamSpeed, 0.1f, 0.3f);
            var gravity = Physics.gravity * streamGravity;
            float spread = hose ? 0.006f : 0.012f;
            float sizeScale = hose ? 0.8f : 1f;

            m_EmitBudget += dropletsPerSecond * (hose ? 1.6f : 1f) * flow * Time.deltaTime;
            while (m_EmitBudget >= 1f)
            {
                m_EmitBudget -= 1f;
                // Spread the landing spots a little (a watering-can rose sprinkles).
                var jitter = Random.insideUnitCircle * spread;
                var target = aimPoint + new Vector3(jitter.x, 0f, jitter.y);
                float time = flightTime * Random.Range(0.95f, 1.05f);
                var position = start + Random.insideUnitSphere * 0.002f;
                // Solve p(t) = p0 + v t + g t²/2 for v so the drop lands on the target at t = time.
                var velocity = (target - position - 0.5f * gravity * time * time) / time;
                m_Water.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    startSize = Random.Range(0.003f, 0.0055f) * sizeScale,
                    startLifetime = time, // dies exactly on the surface -> splash
                }, 1);
            }
        }

        void OnDestroy()
        {
            if (m_Water != null)
                Destroy(m_Water.gameObject);
        }
    }
}
