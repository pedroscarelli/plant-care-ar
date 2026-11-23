using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CuidAR.Watering
{
    /// <summary>
    /// Image tracking of the printed QR marker.
    ///
    /// The reference image library is built at runtime from Resources/Markers/cuidar_marker.bytes
    /// (a PNG), so no editor-made library asset is needed: just print Marker/cuidar_marker_A4.pdf.
    /// Exposes the last known marker pose; it is kept when the marker leaves the camera view,
    /// so the plant stays on the table while the user brings the phone close to water it.
    /// </summary>
    public class MarkerTracker : MonoBehaviour
    {
        public enum State { Starting, Unsupported, Failed, Searching, Tracking, Lost }

        public State state { get; private set; } = State.Starting;
        public bool hasPose { get; private set; }
        public Pose pose { get; private set; }
        public string error { get; private set; }

        ARTrackedImageManager m_Manager;

        /// <summary>A reference image: the picture, a name and its printed width in meters.</summary>
        public struct MarkerImage
        {
            public Texture2D texture;
            public string name;
            public float width;
        }

        /// <summary>
        /// Creates the image manager on the XR Origin and registers the marker images
        /// (any of them makes the plant appear).
        /// </summary>
        public IEnumerator Setup(XROrigin origin, IReadOnlyList<MarkerImage> images)
        {
            // AddReferenceImage requires the session to be ready.
            while (ARSession.state < ARSessionState.SessionInitializing)
            {
                if (ARSession.state == ARSessionState.Unsupported)
                {
                    state = State.Unsupported;
                    yield break;
                }
                yield return null;
            }

            // Trackable managers must sit on the XR Origin object. Added while enabled without a library,
            // the manager disables itself; it is re-enabled below once the library has the marker.
            m_Manager = origin.GetComponent<ARTrackedImageManager>();
            if (m_Manager == null)
                m_Manager = origin.gameObject.AddComponent<ARTrackedImageManager>();
            m_Manager.enabled = false;

            RuntimeReferenceImageLibrary library;
            try
            {
                library = m_Manager.CreateRuntimeLibrary();
            }
            catch (System.Exception e)
            {
                Fail($"Image tracking is not supported: {e.Message}");
                yield break;
            }

            if (library is not MutableRuntimeReferenceImageLibrary mutable)
            {
                Fail("This device can't add marker images at runtime.");
                yield break;
            }

            m_Manager.referenceLibrary = mutable;
            int added = 0;
            foreach (var image in images)
            {
                if (image.texture == null)
                    continue;
                var job = mutable.ScheduleAddImageWithValidationJob(image.texture, image.name, image.width);
                while (!job.jobHandle.IsCompleted)
                    yield return null;
                job.jobHandle.Complete();
                if (job.status == AddReferenceImageJobStatus.Success)
                {
                    added++;
                    Debug.Log($"[CuidAR] Marker image '{image.name}' added ({image.width * 100f:0.#} cm).");
                }
                else
                {
                    Debug.LogWarning($"[CuidAR] Marker image '{image.name}' rejected by AR ({job.status}).");
                }
            }

            if (added == 0)
            {
                Fail("Marker images rejected by AR.");
                yield break;
            }

            m_Manager.requestedMaxNumberOfMovingImages = 1;
            m_Manager.trackablesChanged.AddListener(OnTrackablesChanged);
            m_Manager.enabled = true;
            state = State.Searching;
        }

        void Fail(string message)
        {
            error = message;
            state = State.Failed;
            Debug.LogError("[CuidAR] " + message);
        }

        void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (var image in args.added)
                Consider(image);
            foreach (var image in args.updated)
                Consider(image);
        }

        void Consider(ARTrackedImage image)
        {
            if (image.trackingState == TrackingState.Tracking)
            {
                if (state != State.Tracking)
                    Debug.Log($"[CuidAR] Marker found: '{image.referenceImage.name}'");
                pose = new Pose(image.transform.position, image.transform.rotation);
                hasPose = true;
                state = State.Tracking;
            }
            else if (state == State.Tracking)
            {
                // Limited / None: the marker left the view. Keep the last pose.
                state = State.Lost;
            }
        }

        /// <summary>Editor helper: pretends the marker was found at the given pose.</summary>
        public void ForcePose(Pose forced)
        {
            pose = forced;
            hasPose = true;
            state = State.Tracking;
        }

        void OnDestroy()
        {
            if (m_Manager != null)
                m_Manager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }
    }
}
