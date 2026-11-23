using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

namespace CuidAR.Watering
{
    /// <summary>
    /// AR watering experience (ARScene):
    ///  1. the camera looks for the printed QR marker on the table (image tracking);
    ///  2. a thirsty virtual plant appears on top of the marker;
    ///  3. the user brings the phone close and tips it like a watering can;
    ///  4. water pours from the virtual can, and while it reaches the pot the plant hydrates:
    ///     leaves rise and turn green, the soil darkens and a flower blooms; at 100% it celebrates.
    ///
    /// Finds the AR components by itself, so it only needs to be added to any object in ARScene.
    /// In the Editor (no marker): press P to place the plant in front of the camera and hold Space to tilt.
    /// </summary>
    public class PlantWateringController : MonoBehaviour
    {
        [Header("Marker")]
        [Tooltip("PNG of the printed marker (as .bytes). If empty, loads Resources/Markers/cuidar_marker.")]
        [SerializeField] TextAsset markerImage;
        [Tooltip("Printed width of the marker, in meters (the PDF prints it at 15 cm).")]
        [SerializeField] float markerWidth = 0.15f;
        [Tooltip("Width of the QR code square alone (white panel), in meters, for a marker cut around the QR code.")]
        [SerializeField] float qrOnlyWidth = 0.074f;

        [Header("Plant")]
        [Tooltip("Scale of the virtual plant (1 = pot of 10 cm, plant about 25 cm tall).")]
        [SerializeField] float plantScale = 1f;
        [Range(0f, 1f)] [SerializeField] float startHydration = 0.1f;
        [Tooltip("Hydration gained per second of watering (0.2 = 5 s from empty to full).")]
        [SerializeField] float hydrationPerSecond = 0.2f;
        [Tooltip("Hydration lost per second when not watering (the plant slowly dries again).")]
        [SerializeField] float dryingPerSecond = 0.004f;

        [Header("Interaction")]
        [Tooltip("The phone must be closer than this to the plant (meters) to water it.")]
        [SerializeField] float approachDistance = 0.7f;

        [Header("Bugs")]
        [Tooltip("Bugs per wave. The first wave comes after the plant is fully hydrated the first time.")]
        [SerializeField] int bugsPerWave = 4;
        [Tooltip("Seconds between waves once the previous one was removed (random in this range).")]
        [SerializeField] Vector2 waveInterval = new(25f, 40f);
        [Tooltip("Extra hydration lost per second for each bug on the plant (they eat it).")]
        [SerializeField] float dryingPerBug = 0.015f;

        [Header("Pruning")]
        [Tooltip("Seconds until another leaf dries out after all dead leaves were cut (random in this range).")]
        [SerializeField] Vector2 witherInterval = new(40f, 70f);

        static readonly PlantSpecies[] k_Species = { PlantSpecies.Daisy, PlantSpecies.Cactus, PlantSpecies.Sunflower };

        enum Tool { Hose, Can, Scissors }
        static readonly string[] k_ToolNames = { "Hose", "Can", "Scissors" };

        XROrigin m_Origin;
        Camera m_Camera;
        ARPlaneManager m_PlaneManager;

        WateringOverlay m_Overlay;
        MarkerTracker m_Tracker;
        WateringAudio m_Audio;
        Transform m_Anchor;
        VirtualPlant m_Plant;
        WateringCan m_Can;
        PlantPests m_Pests;
        PruningShears m_Shears;
        Tool m_Tool = Tool.Hose;     // the hose is the default tool
        bool m_PressConsumed;       // the current touch was used by a button, a bug or the scissors
        float m_NextWither = -1f;
        bool m_HadWithered;
        PlantSpecies m_Species = PlantSpecies.Daisy;
        float m_NextWave = -1f;   // < 0: no wave scheduled
        float m_MessageUntil;     // a short message (e.g. "Bugs gone!") wins over the normal status
        string m_Message;
        bool m_MessagePositive;

        float m_Hydration;
        bool m_Celebrated;
        float m_NextSplash;
        bool m_Ready;
        bool m_EditorMode;

        void Awake()
        {
            m_Origin = FindAnyObjectByType<XROrigin>();
            m_Camera = m_Origin != null && m_Origin.Camera != null ? m_Origin.Camera : Camera.main;
            if (m_Camera == null)
            {
                var cameraManager = FindAnyObjectByType<ARCameraManager>();
                if (cameraManager != null)
                    m_Camera = cameraManager.GetComponent<Camera>();
            }
            if (m_Origin != null && m_Origin.Camera == null && m_Camera != null)
                m_Origin.Camera = m_Camera;
            if (m_Camera != null)
                EnsurePoseDriver(m_Camera.gameObject);
            m_PlaneManager = FindAnyObjectByType<ARPlaneManager>();

            m_Overlay = gameObject.AddComponent<WateringOverlay>();
            m_Overlay.Build(System.Array.ConvertAll(k_Species, VirtualPlant.DisplayName), k_ToolNames);
            m_Overlay.toolPressed += index => SelectTool((Tool)index);
            m_Overlay.resetPressed += ResetPlant;
            m_Overlay.speciesPressed += index => ChangeSpecies(k_Species[index]);
            m_Overlay.settingsPressed += CameraPermission.OpenAppSettings;

            m_Tracker = gameObject.AddComponent<MarkerTracker>();
            m_Hydration = startHydration;

            if (m_Camera != null)
            {
                // The phone gets very close to the plant (and the can sits 30 cm ahead): default 0.3 m would clip.
                m_Camera.nearClipPlane = 0.02f;
                if (FindAnyObjectByType<AudioListener>() == null)
                    m_Camera.gameObject.AddComponent<AudioListener>();
            }
            m_Audio = WateringAudio.Create(transform);
        }

        IEnumerator Start()
        {
            if (m_Origin == null)
            {
                m_Overlay.SetStatus("XR Origin not found in the scene", false);
                yield break;
            }
            if (m_Camera == null)
            {
                m_Overlay.SetStatus("AR camera not found in the scene", false);
                yield break;
            }

            // Ask for the camera here too (not only in the menu), so the scene never runs without it.
            yield return CameraPermission.Request();
            if (!CameraPermission.IsGranted)
            {
                m_Overlay.SetStatus("Camera access denied", false);
                m_Overlay.SetHint("Allow the camera for CuidAR in Settings");
                m_Overlay.SetSettingsVisible(true);
                // Resume automatically when the user comes back from Settings with the camera allowed.
                while (!CameraPermission.IsGranted)
                    yield return new WaitForSeconds(0.5f);
                m_Overlay.SetSettingsVisible(false);
            }

            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.Unsupported)
            {
                if (Application.isEditor)
                {
                    m_EditorMode = true;
                    m_Ready = true;
                    yield break;
                }
                m_Overlay.SetStatus("This device does not support AR", false);
                yield break;
            }

            // Planes are not needed (the marker gives the table position) and would clutter the view.
            if (m_PlaneManager != null)
            {
                m_PlaneManager.SetTrackablesActive(false);
                m_PlaneManager.enabled = false;
            }

            // The full printed marker, and the QR code square alone (for a marker cut around the QR code).
            if (markerImage == null)
                markerImage = Resources.Load<TextAsset>("Markers/cuidar_marker");
            var images = new System.Collections.Generic.List<MarkerTracker.MarkerImage>
            {
                new() { texture = LoadImage(markerImage), name = "cuidar_marker", width = markerWidth },
                new() { texture = LoadImage(Resources.Load<TextAsset>("Markers/cuidar_qr")), name = "cuidar_qr", width = qrOnlyWidth },
            };
            if (images[0].texture == null && images[1].texture == null)
            {
                m_Overlay.SetStatus("Marker image not found", false);
                yield break;
            }

            m_Ready = true;
            yield return m_Tracker.Setup(m_Origin, images);
            if (m_Tracker.state != MarkerTracker.State.Searching && Application.isEditor)
                m_EditorMode = true; // e.g. XR Simulation without runtime image libraries
        }

        /// <summary>
        /// Makes the AR camera follow the phone's position and rotation (6DoF). Without it the virtual camera
        /// stays still and nothing can stay anchored to the marker. Added in code so the scene can't lose it.
        /// See <see cref="ARCameraPose"/> for the iOS 27 orientation safety check.
        /// </summary>
        static void EnsurePoseDriver(GameObject cameraObject)
        {
            var trackedPoseDriver = cameraObject.GetComponent<TrackedPoseDriver>();
            if (trackedPoseDriver != null)
                trackedPoseDriver.enabled = false; // ARCameraPose replaces it
            if (cameraObject.GetComponent<ARCameraPose>() == null)
                cameraObject.AddComponent<ARCameraPose>();
        }

        static Texture2D LoadImage(TextAsset png)
        {
            if (png == null)
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return texture.LoadImage(png.bytes, false) ? texture : null; // stays readable
        }

        void Update()
        {
            if (!m_Ready)
                return;

            HandleEditorInput();

            if (!m_Tracker.hasPose)
            {
                ShowSearching();
                m_Overlay.SetSpecies(false, 0);
                m_Overlay.SetTool(false, 0);
                return;
            }

            if (m_Plant == null)
                SpawnPlant();

            FollowMarker();
            HandlePointer();
            UpdateWatering();
            UpdatePruning();
        }

        void ShowSearching()
        {
            switch (m_Tracker.state)
            {
                case MarkerTracker.State.Unsupported:
                    m_Overlay.SetStatus("This device does not support AR", false);
                    m_Overlay.SetHint(null);
                    return;
                case MarkerTracker.State.Failed when !m_EditorMode:
                    m_Overlay.SetStatus(m_Tracker.error, false);
                    m_Overlay.SetHint(null);
                    return;
            }

            if (!m_EditorMode && ARSession.state < ARSessionState.SessionTracking)
            {
                m_Overlay.SetStatus(ARSession.state == ARSessionState.Installing ? "Installing AR services..." : "Starting camera...", false);
                m_Overlay.SetHint(null);
                return;
            }

            m_Overlay.SetStatus("Point the camera at the QR code", false);
            m_Overlay.SetHint(m_EditorMode ? "Editor: press P to place the plant" : "Lay the printed marker flat on a table");
        }

        void SpawnPlant()
        {
            m_Anchor = new GameObject("PlantAnchor").transform;
            m_Anchor.localScale = Vector3.one * plantScale;
            SnapAnchor();
            CreatePlant();
            m_Can = WateringCan.Create(m_Camera);
            m_Shears = PruningShears.Create(m_Camera);
            SelectTool(m_Tool);
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        void CreatePlant()
        {
            m_Plant = VirtualPlant.Create(m_Anchor, m_Species);
            m_Plant.hydration = m_Hydration;
            m_Pests = PlantPests.Create(m_Plant, m_Camera);
            m_Pests.bugRemoved += _ =>
            {
                m_Audio.PlayPop();
                m_Plant.Splash();
            };
            m_Pests.allRemoved += () =>
            {
                m_Plant.Celebrate(30);
                m_Audio.PlayChime();
                ShowMessage("Bugs gone! The plant is safe");
                m_NextWave = Time.time + Random.Range(waveInterval.x, waveInterval.y);
            };
            m_Overlay.SetSpecies(true, System.Array.IndexOf(k_Species, m_Species));
            m_HadWithered = true;
            m_NextWither = -1f;
        }

        void SelectTool(Tool tool)
        {
            m_Tool = tool;
            m_Overlay.SetTool(true, (int)tool);
            if (m_Can == null)
                return;
            m_Can.SetVisible(tool != Tool.Scissors);
            if (tool != Tool.Scissors)
                m_Can.SetMode(tool == Tool.Hose ? WateringCan.Mode.Hose : WateringCan.Mode.Can);
            m_Shears.gameObject.SetActive(tool == Tool.Scissors);
        }

        /// <summary>
        /// Touches on the 3D scene: a tap on a bug removes it (any tool); with the scissors a tap cuts the leaf
        /// under the finger; with the hose, holding the finger down sprays.
        /// </summary>
        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null)
                return;
            if (pointer.press.wasPressedThisFrame)
            {
                var screen = pointer.position.ReadValue();
                m_PressConsumed = PlantPests.IsOverButton(screen) || m_Pests.TryRemoveAt(screen);
                if (!m_PressConsumed && m_Tool == Tool.Scissors)
                {
                    m_PressConsumed = true;
                    TrySnip(screen);
                }
            }
            bool held = pointer.press.isPressed && !m_PressConsumed;
            m_Can.triggerHeld = held && m_Tool == Tool.Hose;
        }

        void TrySnip(Vector2 screen)
        {
            if (m_Shears.busy)
                return;
            float radius = Mathf.Max(70f, Screen.height * 0.06f);
            int part = m_Plant.FindCuttable(m_Camera, screen, radius, out var point);
            if (part < 0)
            {
                // Missed: snip in the air where the finger is, as feedback.
                var ray = m_Camera.ScreenPointToRay(screen);
                m_Shears.Snip(ray.GetPoint(0.25f), m_Audio.PlaySnip);
                return;
            }
            var plant = m_Plant;
            m_Shears.Snip(point, () =>
            {
                if (plant != m_Plant)
                    return; // the species changed meanwhile
                m_Audio.PlaySnip();
                bool wasDead = m_Plant.Cut(part);
                ShowMessage(wasDead ? "Nice cut! Dry leaf removed" : "That leaf was healthy! It will grow back", wasDead);
            });
        }

        void UpdatePruning()
        {
            int withered = m_Plant.witheredCount;
            if (withered == 0 && m_HadWithered)
            {
                m_HadWithered = false;
                m_Plant.Celebrate(30);
                m_Audio.PlayChime();
                ShowMessage("Pruned! The plant looks tidy");
                m_NextWither = Time.time + Random.Range(witherInterval.x, witherInterval.y);
            }
            else if (withered > 0)
            {
                m_HadWithered = true;
            }
            if (m_NextWither >= 0f && Time.time >= m_NextWither)
            {
                m_NextWither = -1f;
                m_Plant.WitherRandom(1);
            }
        }

        /// <summary>Swaps the plant model (it starts thirsty again).</summary>
        void ChangeSpecies(PlantSpecies species)
        {
            if (m_Plant == null || species == m_Species)
                return;
            m_Species = species;
            Destroy(m_Plant.gameObject);
            ResetPlant();
            CreatePlant();
            string need = species switch
            {
                PlantSpecies.Cactus => "needs little water",
                PlantSpecies.Sunflower => "needs a lot of water",
                _ => "needs regular water",
            };
            ShowMessage($"{VirtualPlant.DisplayName(species)}: {need}");
        }

        void ShowMessage(string text, bool positive = true)
        {
            m_Message = text;
            m_MessagePositive = positive;
            m_MessageUntil = Time.time + 2.5f;
        }

        Pose TargetPose()
        {
            // Keep the plant upright (gravity), using only the marker's heading.
            var pose = m_Tracker.pose;
            var forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(pose.rotation * Vector3.up, Vector3.up);
            return new Pose(pose.position, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        void SnapAnchor()
        {
            var target = TargetPose();
            m_Anchor.SetPositionAndRotation(target.position, target.rotation);
        }

        void FollowMarker()
        {
            // Smooth out the small jitter of image tracking.
            var target = TargetPose();
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            m_Anchor.SetPositionAndRotation(
                Vector3.Lerp(m_Anchor.position, target.position, k),
                Quaternion.Slerp(m_Anchor.rotation, target.rotation, k));
        }

        void UpdateWatering()
        {
            float dt = Time.deltaTime;
            var soil = m_Plant.SoilCenter;
            m_Can.SetTarget(soil, VirtualPlant.PotRadius * plantScale * 1.15f, m_Anchor.position.y);

            float distance = Vector3.Distance(m_Camera.transform.position, soil);
            bool close = distance <= approachDistance;
            bool pouring = m_Can.isPouring;
            bool hitsSoil = m_Can.aimOnPot;
            if (m_EditorMode && pouring)
                hitsSoil = true; // no real geometry to aim with in the Editor
            bool scissors = m_Tool == Tool.Scissors;
            m_Overlay.SetReticle(!scissors, hitsSoil);

            bool watering = pouring && hitsSoil && close;
            // Bugs: the first wave arrives after the first full watering, then again some time after each removal.
            if (m_NextWave >= 0f && Time.time >= m_NextWave && m_Pests.aliveCount == 0)
            {
                m_Pests.SpawnWave(bugsPerWave);
                m_NextWave = -1f;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                Handheld.Vibrate();
#endif
            }
            int bugs = m_Pests.aliveCount;

            if (watering)
            {
                m_Hydration = Mathf.Min(1f, m_Hydration + hydrationPerSecond * VirtualPlant.WaterSpeed(m_Species) * m_Can.flow * dt);
                if (Time.time >= m_NextSplash)
                {
                    m_Plant.Splash();
                    m_NextSplash = Time.time + 0.4f;
                }
            }
            else
            {
                m_Hydration = Mathf.Max(0f, m_Hydration - (dryingPerSecond + dryingPerBug * bugs) * dt);
            }
            m_Plant.hydration = m_Hydration;
            m_Audio.SetFlow(pouring ? m_Can.flow : 0f);

            if (!m_Celebrated && m_Hydration >= 1f)
            {
                m_Celebrated = true;
                if (m_NextWave < 0f && bugs == 0)
                    m_NextWave = Time.time + 4f; // first bugs come to the happy plant
                m_Plant.Celebrate();
                m_Audio.PlayChime();
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                Handheld.Vibrate();
#endif
            }
            else if (m_Celebrated && m_Hydration < 0.8f)
            {
                m_Celebrated = false;
            }

            m_Overlay.SetHydration(m_Hydration, watering);
            m_Overlay.SetResetVisible(m_Hydration > 0.5f);

            int withered = m_Plant.witheredCount;
            if (Time.time < m_MessageUntil)
            {
                m_Overlay.SetStatus(m_Message, m_MessagePositive);
            }
            else if (bugs > 0 && !watering)
            {
                m_Overlay.SetStatus(bugs == 1 ? "1 bug on the plant!" : bugs + " bugs on the plant!", false);
                m_Overlay.SetHint("Tap the bugs on the screen to remove them");
            }
            else if (scissors)
            {
                m_Overlay.SetStatus(withered == 0 ? "The plant looks tidy" : withered == 1 ? "1 dry leaf to prune" : withered + " dry leaves to prune", withered == 0);
                m_Overlay.SetHint(withered > 0 ? "Tap a brown leaf to cut it" : "Tap a leaf to prune it (it grows back)");
            }
            else if (m_Celebrated)
            {
                m_Overlay.SetStatus("Plant hydrated!", true);
                m_Overlay.SetHint(pouring ? "That's enough water :)"
                    : withered > 0 ? "Some leaves are dry: try the Scissors"
                    : "Well done! Tap \"Dry again\" to replay");
            }
            else if (watering)
            {
                m_Overlay.SetStatus("Watering...", true);
                m_Overlay.SetHint(m_Tool == Tool.Hose ? "Keep spraying the pot!" : "Keep pouring on the pot!");
            }
            else if (pouring && !close)
            {
                m_Overlay.SetStatus("Too far from the plant", false);
                m_Overlay.SetHint("Bring the phone closer to the pot");
            }
            else if (pouring)
            {
                m_Overlay.SetStatus("Missing the pot", false);
                m_Overlay.SetHint("Put the + on the pot");
            }
            else
            {
                m_Overlay.SetStatus(m_Hydration < 0.5f ? "The plant is thirsty!" : "The plant is happy", m_Hydration >= 0.5f);
                bool hose = m_Tool == Tool.Hose;
                m_Overlay.SetHint(!close ? "Bring the phone closer to the plant"
                    : m_EditorMode ? (hose ? "Editor: hold Space (or the mouse) to spray" : "Editor: hold Space to tilt the can")
                    : hitsSoil ? (hose ? "Now hold your finger on the screen to spray" : "Now tilt the phone sideways to pour")
                    : hose ? "Put the + on the pot, then hold the screen" : "Put the + on the pot, then tilt the phone");
            }
        }

        void ResetPlant()
        {
            m_Hydration = startHydration;
            m_Celebrated = false;
            if (m_Pests != null)
                m_Pests.Clear();
            m_NextWave = -1f;
        }

        void HandleEditorInput()
        {
            if (!Application.isEditor || Keyboard.current == null)
                return;

            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                // Place the "marker" on an imaginary table in front of and below the camera.
                m_EditorMode = true;
                var cam = m_Camera.transform;
                var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 1e-4f)
                    forward = Vector3.forward;
                m_Tracker.ForcePose(new Pose(cam.position + forward * 0.35f + Vector3.down * 0.3f,
                    Quaternion.LookRotation(forward, Vector3.up)));
                if (m_Anchor != null)
                    SnapAnchor();
            }

            if (Keyboard.current.bKey.wasPressedThisFrame && m_Pests != null)
                m_Pests.SpawnWave(bugsPerWave);

            if (m_Can != null)
            {
                float target = Keyboard.current.spaceKey.isPressed ? 55f : 0f;
                m_Can.simulatedRoll = Mathf.MoveTowards(m_Can.simulatedRoll, target, 120f * Time.deltaTime);
            }
        }

        void OnDestroy()
        {
            if (m_Anchor != null)
                Destroy(m_Anchor.gameObject);
        }
    }
}
