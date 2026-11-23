using UnityEngine;
using UnityEngine.InputSystem;

namespace CuidAR.Watering
{
    /// <summary>
    /// Moves the AR camera with the phone (6DoF), like a Tracked Pose Driver, plus a safety check.
    ///
    /// ARKit's pose is corrected for the screen orientation Unity reports. On iOS 27 Unity's iOS layer uses
    /// an orientation API that became a no-op, so that correction can be off by a multiple of 90°: the 3D
    /// content then looks rotated and the watering can "pours" while held level.
    /// To guard against it, the "down" direction of the virtual camera is compared with the real gravity
    /// from the phone's motion sensor; if they disagree by ~90/180/270° around the viewing axis, the pose is
    /// rotated by that amount. When everything is right (the normal case) the correction stays at 0°.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ARCameraPose : MonoBehaviour
    {
        InputAction m_Position;
        InputAction m_Rotation;
        float m_Correction;      // applied roll correction (multiple of 90°)
        float m_Candidate;
        float m_CandidateTime;
        ScreenOrientation m_LastOrientation = (ScreenOrientation)(-1);

        void OnEnable()
        {
            m_Position = new InputAction("CuidAR Camera Position", InputActionType.Value, expectedControlType: "Vector3");
            m_Position.AddBinding("<HandheldARInputDevice>/devicePosition");
            m_Position.AddBinding("<XRHMD>/centerEyePosition");
            m_Rotation = new InputAction("CuidAR Camera Rotation", InputActionType.Value, expectedControlType: "Quaternion");
            m_Rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            m_Rotation.AddBinding("<XRHMD>/centerEyeRotation");
            m_Position.Enable();
            m_Rotation.Enable();

            if (GravitySensor.current != null)
                InputSystem.EnableDevice(GravitySensor.current);
            else if (Accelerometer.current != null)
                InputSystem.EnableDevice(Accelerometer.current);

            Application.onBeforeRender += Apply;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= Apply;
            m_Position?.Dispose();
            m_Rotation?.Dispose();
        }

        void Update()
        {
            if (Screen.orientation != m_LastOrientation)
            {
                Debug.Log($"[CuidAR] Screen.orientation = {Screen.orientation} ({Screen.width}x{Screen.height})");
                m_LastOrientation = Screen.orientation;
            }
            // Only iOS has the orientation problem; elsewhere the AR pose is used as is.
            if (Application.platform == RuntimePlatform.IPhonePlayer && TryReadPose(out _, out var rotation))
                EstimateCorrection(rotation);
            Apply();
        }

        bool TryReadPose(out Vector3 position, out Quaternion rotation)
        {
            position = m_Position.ReadValue<Vector3>();
            rotation = m_Rotation.ReadValue<Quaternion>();
            // No AR device yet: the action returns an all-zero quaternion.
            return rotation.x != 0f || rotation.y != 0f || rotation.z != 0f || rotation.w != 0f;
        }

        void Apply()
        {
            if (!TryReadPose(out var position, out var rotation))
                return;
            transform.localPosition = position;
            transform.localRotation = rotation * Quaternion.Euler(0f, 0f, m_Correction);
        }

        void EstimateCorrection(Quaternion rotation)
        {
            Vector3 gravity;
            if (GravitySensor.current != null && GravitySensor.current.enabled)
                gravity = GravitySensor.current.gravity.ReadValue();
            else if (Accelerometer.current != null && Accelerometer.current.enabled)
                gravity = Accelerometer.current.acceleration.ReadValue();
            else
                return;

            // Real "down" on the screen plane (device x = right, y = up in portrait) vs the virtual camera's.
            var real = new Vector2(gravity.x, gravity.y);
            var down = Quaternion.Inverse(rotation) * Vector3.down;
            var virtualDown = new Vector2(down.x, down.y);
            // Only measurable when the phone isn't pointing straight at the floor/ceiling.
            if (real.magnitude < 0.5f || virtualDown.magnitude < 0.5f)
                return;

            float angle = Vector2.SignedAngle(real, virtualDown);
            float snapped = Mathf.Round(angle / 90f) * 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, snapped)) > 25f)
                return; // ambiguous reading (phone moving fast)
            snapped = Mathf.Repeat(snapped, 360f);

            if (Mathf.Approximately(snapped, m_Correction))
            {
                m_CandidateTime = 0f;
                return;
            }
            // Require the same reading for a moment before changing the correction.
            if (!Mathf.Approximately(snapped, m_Candidate))
            {
                m_Candidate = snapped;
                m_CandidateTime = 0f;
            }
            m_CandidateTime += Time.unscaledDeltaTime;
            if (m_CandidateTime > 0.5f)
            {
                Debug.Log($"[CuidAR] Camera roll correction {m_Correction}° -> {snapped}° (AR pose vs gravity sensor)");
                m_Correction = snapped;
                m_CandidateTime = 0f;
            }
        }
    }
}
