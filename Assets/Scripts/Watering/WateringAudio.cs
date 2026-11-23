using UnityEngine;

namespace CuidAR.Watering
{
    /// <summary>
    /// Sounds generated in code (no audio files): a looping water-pouring noise whose volume follows the flow,
    /// and a short chime when the plant is fully hydrated.
    /// </summary>
    public class WateringAudio : MonoBehaviour
    {
        const int k_Rate = 44100;

        AudioSource m_Pour;
        AudioSource m_Fx;
        AudioClip m_Chime;
        AudioClip m_Pop;
        AudioClip m_Snip;

        public static WateringAudio Create(Transform parent)
        {
            var go = new GameObject("WateringAudio");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<WateringAudio>();
            audio.Build();
            return audio;
        }

        void Build()
        {
            m_Pour = gameObject.AddComponent<AudioSource>();
            m_Pour.clip = PourClip();
            m_Pour.loop = true;
            m_Pour.volume = 0f;
            m_Pour.playOnAwake = false;
            m_Pour.Play();

            m_Fx = gameObject.AddComponent<AudioSource>();
            m_Fx.playOnAwake = false;
            m_Chime = ChimeClip();
            m_Pop = PopClip();
            m_Snip = SnipClip();
        }

        /// <summary>0 = silent, 1 = full stream.</summary>
        public void SetFlow(float flow)
        {
            m_Pour.volume = Mathf.MoveTowards(m_Pour.volume, flow * 0.6f, Time.deltaTime * 3f);
            m_Pour.pitch = 0.9f + flow * 0.2f;
        }

        public void PlayChime()
        {
            m_Fx.pitch = 1f;
            m_Fx.PlayOneShot(m_Chime, 0.8f);
        }

        /// <summary>Short "squish/pop" when a bug is tapped away.</summary>
        public void PlayPop()
        {
            m_Fx.pitch = Random.Range(0.9f, 1.15f);
            m_Fx.PlayOneShot(m_Pop, 0.9f);
        }

        /// <summary>Metallic "snip" of the scissors.</summary>
        public void PlaySnip()
        {
            m_Fx.pitch = Random.Range(0.95f, 1.08f);
            m_Fx.PlayOneShot(m_Snip, 0.9f);
        }

        static AudioClip SnipClip()
        {
            // Two very short bright clicks (blades meeting) with a ringing metallic tail.
            int length = (int)(k_Rate * 0.18f);
            var data = new float[length];
            var random = new System.Random(5);
            foreach (float start in new[] { 0f, 0.035f })
            {
                int offset = (int)(start * k_Rate);
                for (int i = offset; i < length; i++)
                {
                    float t = (i - offset) / (float)k_Rate;
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 180f);
                    float ring = (Mathf.Sin(2f * Mathf.PI * 3200f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 5100f * t)) * Mathf.Exp(-t * 45f);
                    data[i] += noise * 0.45f + ring * 0.12f;
                }
            }
            var clip = AudioClip.Create("Snip", length, 1, k_Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip PopClip()
        {
            // Fast downward pitch sweep plus a burst of noise.
            int length = (int)(k_Rate * 0.16f);
            var data = new float[length];
            var random = new System.Random(11);
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)k_Rate;
                float freq = Mathf.Lerp(900f, 180f, t / 0.16f);
                phase += 2f * Mathf.PI * freq / k_Rate;
                float env = Mathf.Exp(-t * 28f) * Mathf.Clamp01(t * 800f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 60f);
                data[i] = Mathf.Sin(phase) * env * 0.5f + noise * 0.25f;
            }
            var clip = AudioClip.Create("Pop", length, 1, k_Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip PourClip()
        {
            // Low-passed noise ("shhh") plus random short rising tones (bubbles).
            int length = k_Rate * 2;
            var data = new float[length];
            var random = new System.Random(7);
            float low = 0f, band = 0f;
            float bubbleFreq = 0f, bubblePhase = 0f, bubbleEnv = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += 0.18f * (white - low);
                band += 0.5f * (low - band);
                if (bubbleEnv < 0.01f && random.NextDouble() < 0.0009)
                {
                    bubbleFreq = 500f + (float)random.NextDouble() * 900f;
                    bubbleEnv = 1f;
                }
                bubbleFreq *= 1.00004f;
                bubblePhase += 2f * Mathf.PI * bubbleFreq / k_Rate;
                bubbleEnv *= 0.9992f;
                data[i] = (low - band * 0.5f) * 0.9f + Mathf.Sin(bubblePhase) * bubbleEnv * 0.25f;
            }
            // Crossfade the ends so the loop has no click.
            int fade = k_Rate / 10;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[i] = data[i] * t + data[length - fade + i] * (1f - t);
            }
            var clip = AudioClip.Create("Pour", length - fade, 1, k_Rate, false);
            var trimmed = new float[length - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }

        static AudioClip ChimeClip()
        {
            // Rising major arpeggio (C6 E6 G6 C7) with soft decay.
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            float step = 0.11f;
            int length = (int)(k_Rate * (step * notes.Length + 0.6f));
            var data = new float[length];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * k_Rate);
                for (int i = start; i < length; i++)
                {
                    float t = (i - start) / (float)k_Rate;
                    float env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 200f);
                    data[i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * env * 0.22f
                             + Mathf.Sin(4f * Mathf.PI * notes[n] * t) * env * 0.05f;
                }
            }
            var clip = AudioClip.Create("Chime", length, 1, k_Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
