using UnityEngine;

namespace CuidAR.Watering
{
    /// <summary>
    /// A cut leaf (or cactus arm) falling to the table: light gravity with air drag, a side-to-side flutter
    /// and a spin; it rests on the table for a moment, then shrinks away.
    /// </summary>
    public class FallingPiece : MonoBehaviour
    {
        Vector3 m_Velocity;
        Vector3 m_Spin;
        float m_Floor;
        float m_Flutter;
        float m_Time;
        float m_Landed = -1f;
        Vector3 m_Scale;

        public static void Drop(GameObject piece, float floorHeight, float scale)
        {
            var falling = piece.AddComponent<FallingPiece>();
            falling.m_Floor = floorHeight;
            falling.m_Velocity = (Random.insideUnitSphere * 0.05f + Vector3.up * 0.04f) * scale;
            falling.m_Spin = new Vector3(Random.Range(-200f, 200f), Random.Range(-120f, 120f), Random.Range(-200f, 200f));
            falling.m_Flutter = Random.value * 10f;
            falling.m_Scale = piece.transform.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_Time += dt;
            if (m_Landed < 0f)
            {
                m_Velocity += Physics.gravity * (0.45f * dt);
                m_Velocity *= 1f - Mathf.Clamp01(2.2f * dt); // air drag: leaves float down
                var flutter = new Vector3(Mathf.Sin(m_Time * 7f + m_Flutter), 0f, Mathf.Cos(m_Time * 5f + m_Flutter)) * 0.06f;
                transform.position += (m_Velocity + flutter) * dt;
                transform.Rotate(m_Spin * dt, Space.Self);
                if (transform.position.y <= m_Floor + 0.004f || m_Time > 4f)
                {
                    m_Landed = m_Time;
                    var p = transform.position;
                    transform.position = new Vector3(p.x, Mathf.Max(p.y, m_Floor + 0.004f), p.z);
                }
                return;
            }

            float fade = Mathf.Clamp01((m_Time - m_Landed - 1.5f) / 0.6f);
            transform.localScale = m_Scale * (1f - fade);
            if (fade >= 1f)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Materials were copied for the falling piece (so its color stays frozen); free them.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                Destroy(renderer.sharedMaterial);
        }
    }
}
