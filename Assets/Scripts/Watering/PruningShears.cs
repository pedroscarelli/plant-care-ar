using System;
using UnityEngine;

namespace CuidAR.Watering
{
    /// <summary>
    /// Pruning scissors held at the bottom-right of the view. <see cref="Snip"/> flies them to a point in the
    /// scene (the leaf that was tapped), opens, snaps closed (the cut happens at that moment) and flies back.
    /// </summary>
    public class PruningShears : MonoBehaviour
    {
        static readonly Vector3 k_Rest = new(0.04f, -0.095f, 0.28f);
        static readonly Quaternion k_RestRotation = Quaternion.Euler(-25f, 10f, 28f);

        Transform m_Camera;
        Transform m_BladeA;
        Transform m_BladeB;

        float m_Time = -1f;        // >= 0 while snipping
        Vector3 m_Target;          // world point to cut
        Action m_OnCut;
        bool m_CutDone;

        const float k_Out = 0.2f, k_Close = 0.08f, k_Back = 0.25f;

        public bool busy => m_Time >= 0f;

        public static PruningShears Create(Camera camera)
        {
            var go = new GameObject("PruningShears");
            go.transform.SetParent(camera.transform, false);
            var shears = go.AddComponent<PruningShears>();
            shears.Build(camera.transform);
            return shears;
        }

        void Build(Transform cameraTransform)
        {
            m_Camera = cameraTransform;
            transform.localPosition = k_Rest;
            transform.localRotation = k_RestRotation;

            var steel = ArtKit.Lit(new Color(0.82f, 0.84f, 0.88f), 0.9f);
            var red = ArtKit.Lit(new Color(0.85f, 0.15f, 0.12f), 0.5f);
            var dark = ArtKit.Lit(new Color(0.15f, 0.15f, 0.17f), 0.6f);
            m_BladeA = BuildHalf("BladeA", steel, red, 1f);
            m_BladeB = BuildHalf("BladeB", steel, red, -1f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Screw", transform, Vector3.zero, new Vector3(0.008f, 0.004f, 0.008f), dark)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>One half: a blade above the pivot (pointing +Y) and a ring handle below it.</summary>
        Transform BuildHalf(string name, Material steel, Material handle, float side)
        {
            var half = ArtKit.Node(name, transform, new Vector3(0f, 0f, side * 0.0012f));
            // The blade of one half continues into the handle on the other side (that is how scissors cross).
            var blade = ArtKit.Shape(PrimitiveType.Cube, "Blade", half, new Vector3(side * 0.002f, 0.026f, 0f),
                new Vector3(0.007f, 0.052f, 0.0016f), steel);
            blade.localRotation = Quaternion.Euler(0f, 0f, side * 3f);
            ArtKit.Shape(PrimitiveType.Sphere, "Point", half, new Vector3(side * 0.0035f, 0.052f, 0f),
                new Vector3(0.006f, 0.012f, 0.0016f), steel);
            ArtKit.Shape(PrimitiveType.Cube, "Shank", half, new Vector3(-side * 0.006f, -0.013f, 0f),
                new Vector3(0.005f, 0.026f, 0.003f), handle).localRotation = Quaternion.Euler(0f, 0f, side * 20f);
            ArtKit.Shape(PrimitiveType.Cylinder, "Ring", half, new Vector3(-side * 0.013f, -0.032f, 0f),
                new Vector3(0.022f, 0.002f, 0.016f), handle).localRotation = Quaternion.Euler(90f, 0f, 0f);
            return half;
        }

        /// <summary>Flies to <paramref name="worldPoint"/>, cuts (calls <paramref name="onCut"/>) and comes back.</summary>
        public bool Snip(Vector3 worldPoint, Action onCut)
        {
            if (busy)
                return false;
            m_Target = worldPoint;
            m_OnCut = onCut;
            m_CutDone = false;
            m_Time = 0f;
            return true;
        }

        void SetOpen(float degrees)
        {
            m_BladeA.localRotation = Quaternion.Euler(0f, 0f, degrees * 0.5f);
            m_BladeB.localRotation = Quaternion.Euler(0f, 0f, -degrees * 0.5f);
        }

        void LateUpdate()
        {
            if (m_Camera == null)
                return;

            if (!busy)
            {
                // Idle: slightly open, gently "breathing".
                transform.localPosition = k_Rest + new Vector3(0f, Mathf.Sin(Time.time * 2f) * 0.0015f, 0f);
                transform.localRotation = k_RestRotation;
                SetOpen(22f + Mathf.Sin(Time.time * 2.5f) * 4f);
                return;
            }

            m_Time += Time.deltaTime;
            // Stop a little in front of the target, so the blades straddle it instead of sinking into it.
            var local = m_Camera.InverseTransformPoint(m_Target);
            local -= local.normalized * 0.03f;
            var aimRotation = Quaternion.LookRotation(Vector3.forward, (local - k_Rest).normalized) * Quaternion.Euler(-20f, 0f, 0f);

            if (m_Time < k_Out)
            {
                float k = Ease(m_Time / k_Out);
                transform.localPosition = Vector3.Lerp(k_Rest, local, k);
                transform.localRotation = Quaternion.Slerp(k_RestRotation, aimRotation, k);
                SetOpen(Mathf.Lerp(22f, 60f, k));
            }
            else if (m_Time < k_Out + k_Close)
            {
                float k = (m_Time - k_Out) / k_Close;
                transform.localPosition = local;
                transform.localRotation = aimRotation;
                SetOpen(Mathf.Lerp(60f, 0f, k * k));
            }
            else
            {
                if (!m_CutDone)
                {
                    m_CutDone = true;
                    m_OnCut?.Invoke();
                }
                float k = Ease(Mathf.Clamp01((m_Time - k_Out - k_Close) / k_Back));
                transform.localPosition = Vector3.Lerp(local, k_Rest, k);
                transform.localRotation = Quaternion.Slerp(aimRotation, k_RestRotation, k);
                SetOpen(Mathf.Lerp(0f, 22f, k));
                if (m_Time >= k_Out + k_Close + k_Back)
                    m_Time = -1f;
            }
        }

        static float Ease(float t) => 1f - (1f - t) * (1f - t);
    }
}
