using System.Collections;
using System.Collections.Generic;
using CuidAR.Detection;
using CuidAR.UI;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace CuidAR
{
    /// <summary>
    /// AR scene flow (RF01 + RF02):
    /// checks AR support, waits for the camera, then every few frames sends the camera image to the
    /// leaf detector, stabilizes the result and draws the boxes and the status on screen.
    ///
    /// Finds the AR components by itself, so it only needs to be added to any object in ARScene.
    /// </summary>
    public class CuidARController : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("YOLOv8 ONNX model. If empty, loads Resources/Models/cuidar_yolov8n.")]
        [SerializeField] ModelAsset model;
        [Tooltip("One class name per line, in the model's order. If empty, loads Resources/Models/cuidar_labels.")]
        [SerializeField] TextAsset labels;
        [SerializeField] int inputSize = 640;

        [Header("Detection")]
        [Range(0.05f, 0.95f)] [SerializeField] float confidenceThreshold = 0.5f;
        [Range(0.1f, 0.9f)] [SerializeField] float iouThreshold = 0.45f;
        [Tooltip("Runs the model once every N frames (RF02: every 3-5 frames).")]
        [Range(1, 10)] [SerializeField] int frameInterval = 4;
        [Tooltip("Consecutive runs needed before a detection is shown.")]
        [Range(1, 10)] [SerializeField] int requiredHits = 3;

        [Header("Debug")]
        [Tooltip("Shows the image the model receives in the bottom-left corner.")]
        [SerializeField] bool showModelInput;
        [Tooltip("Turn on if the preview above appears upside down (depends on the graphics API).")]
        [SerializeField] bool flipModelInput;
        [Tooltip("Shows fake detections, to test the interface without a model (Editor / XR Simulation).")]
        [SerializeField] bool fakeDetections;

        ARSession m_Session;
        ARCameraBackground m_Background;
        ARCameraManager m_CameraManager;
        ARPlaneManager m_PlaneManager;

        DetectionOverlay m_Overlay;
        CameraFrameProvider m_Frames;
        LeafDetector m_Detector;
        readonly DetectionStabilizer m_Stabilizer = new();
        readonly List<LeafDetection> m_ScreenDetections = new();

        bool m_Running;
        bool m_Busy;
        bool m_HasCameraFrame;
        int m_FrameCounter;

        void Awake()
        {
            m_Session = FindAnyObjectByType<ARSession>();
            m_Background = FindAnyObjectByType<ARCameraBackground>();
            m_CameraManager = FindAnyObjectByType<ARCameraManager>();
            m_PlaneManager = FindAnyObjectByType<ARPlaneManager>();

            m_Overlay = gameObject.AddComponent<DetectionOverlay>();
            m_Overlay.Build(showModelInput);
            m_Stabilizer.requiredHits = requiredHits;
        }

        void OnEnable()
        {
            if (m_CameraManager != null)
                m_CameraManager.frameReceived += OnCameraFrame;
        }

        void OnDisable()
        {
            if (m_CameraManager != null)
                m_CameraManager.frameReceived -= OnCameraFrame;
        }

        void OnCameraFrame(ARCameraFrameEventArgs args) => m_HasCameraFrame = true;

        IEnumerator Start()
        {
            // RF01: check whether the device supports AR before anything else.
            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.Unsupported)
            {
                m_Overlay.SetStatus("This device does not support AR", false);
                m_Overlay.SetHintVisible(false);
                yield break;
            }
            if (m_Background == null)
            {
                Debug.LogError("[CuidAR] ARCameraBackground not found in the scene.");
                m_Overlay.SetStatus("AR camera not found", false);
                yield break;
            }

            if (!fakeDetections && !TryCreateDetector())
            {
                m_Overlay.SetStatus("Model not found", false);
                yield break;
            }

            m_Frames = new CameraFrameProvider(m_Background, inputSize) { flipVertical = flipModelInput };
            m_Overlay.SetPreview(m_Frames.ModelInput);
            m_Running = true;
        }

        bool TryCreateDetector()
        {
            if (model == null)
                model = Resources.Load<ModelAsset>("Models/cuidar_yolov8n");
            if (labels == null)
                labels = Resources.Load<TextAsset>("Models/cuidar_labels");
            if (model == null || labels == null)
            {
                Debug.LogError("[CuidAR] Put the model and labels in Assets/Resources/Models (see README).");
                return false;
            }

            var names = new List<string>();
            foreach (var line in labels.text.Split('\n'))
            {
                var entry = line.Trim();
                if (entry.Length > 0)
                    names.Add(entry);
            }
            m_Detector = new LeafDetector(model, names.ToArray(), inputSize)
            {
                confidenceThreshold = confidenceThreshold,
                iouThreshold = iouThreshold,
            };
            return true;
        }

        void Update()
        {
            if (!m_Running)
                return;

            m_Overlay.SetScanFrame(CameraFrameProvider.ComputeCropRegion(Screen.width, Screen.height));

            if (ARSession.state < ARSessionState.SessionTracking || !m_HasCameraFrame)
            {
                m_Overlay.SetStatus(ARSession.state == ARSessionState.Installing ? "Installing AR services..." : "Starting camera...", false);
                return;
            }

            if (++m_FrameCounter % frameInterval != 0 || m_Busy)
                return;

            if (fakeDetections)
                Show(FakeFrame());
            else
                RunDetection();
        }

        async void RunDetection()
        {
            if (!m_Frames.TryCapture())
                return;

            m_Busy = true;
            try
            {
                var crop = m_Frames.CropRegion;
                var found = await m_Detector.DetectAsync(m_Frames.ModelInput);
                if (!m_Running)
                    return;

                // Model input (0..1 inside the square) -> screen (0..1), both with a top-left origin.
                m_ScreenDetections.Clear();
                foreach (var detection in found)
                {
                    var mapped = detection;
                    mapped.box = new Rect(
                        crop.x + detection.box.x * crop.width,
                        crop.y + detection.box.y * crop.height,
                        detection.box.width * crop.width,
                        detection.box.height * crop.height);
                    m_ScreenDetections.Add(mapped);
                }
                Show(m_ScreenDetections);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                m_Busy = false;
            }
        }

        void Show(IReadOnlyList<LeafDetection> frame)
        {
            var stable = m_Stabilizer.Update(frame);
            m_Overlay.ShowDetections(stable);

            int problems = 0;
            foreach (var detection in stable)
            {
                if (LeafClasses.IsProblem(detection.label))
                    problems++;
            }

            if (stable.Count == 0)
            {
                bool surfaceFound = m_PlaneManager != null && m_PlaneManager.trackables.count > 0;
                m_Overlay.SetStatus(surfaceFound ? "Analyzing the leaves..." : "Looking for a surface...", false);
                m_Overlay.SetHintVisible(true);
            }
            else
            {
                m_Overlay.SetHintVisible(false);
                m_Overlay.SetStatus(problems == 0 ? "Healthy plant" : problems == 1 ? "1 problem found" : $"{problems} problems found",
                    problems == 0);
            }
        }

        List<LeafDetection> FakeFrame()
        {
            var crop = CameraFrameProvider.ComputeCropRegion(Screen.width, Screen.height);
            Rect Local(float x, float y, float w, float h) =>
                new(crop.x + x * crop.width, crop.y + y * crop.height, w * crop.width, h * crop.height);

            return new List<LeafDetection>
            {
                new() { box = Local(0.10f, 0.15f, 0.35f, 0.30f), classId = 1, label = "folha_amarelada", score = 0.82f },
                new() { box = Local(0.55f, 0.40f, 0.30f, 0.28f), classId = 2, label = "mancha_fungica", score = 0.74f },
                new() { box = Local(0.20f, 0.60f, 0.30f, 0.25f), classId = 0, label = "folha_saudavel", score = 0.91f },
            };
        }

        void OnDestroy()
        {
            m_Running = false;
            m_Detector?.Dispose();
            m_Frames?.Dispose();
        }
    }
}
