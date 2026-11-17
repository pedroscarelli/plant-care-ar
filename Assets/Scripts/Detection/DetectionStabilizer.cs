using System.Collections.Generic;

namespace CuidAR.Detection
{
    /// <summary>
    /// Keeps the UI from flickering: a detection only shows up after the same class is found
    /// at the same place in N consecutive model runs, and it only disappears after it is
    /// missed a few runs in a row.
    /// </summary>
    public class DetectionStabilizer
    {
        public int requiredHits = 3;
        public int maxMisses = 2;
        public float matchIoU = 0.3f;

        class Track
        {
            public LeafDetection detection;
            public int hits;
            public int misses;
            public bool confirmed;
        }

        readonly List<Track> m_Tracks = new();
        readonly List<LeafDetection> m_Confirmed = new();

        public IReadOnlyList<LeafDetection> Update(IReadOnlyList<LeafDetection> frame)
        {
            var used = new bool[frame.Count];

            foreach (var track in m_Tracks)
            {
                int best = -1;
                float bestIoU = matchIoU;
                for (int i = 0; i < frame.Count; i++)
                {
                    if (used[i] || frame[i].classId != track.detection.classId)
                        continue;
                    float iou = YoloDecoder.IoU(frame[i].box, track.detection.box);
                    if (iou > bestIoU)
                    {
                        bestIoU = iou;
                        best = i;
                    }
                }

                if (best >= 0)
                {
                    used[best] = true;
                    track.detection = frame[best];
                    track.hits++;
                    track.misses = 0;
                    if (track.hits >= requiredHits)
                        track.confirmed = true;
                }
                else
                {
                    track.misses++;
                    if (!track.confirmed)
                        track.hits = 0; // must be consecutive
                }
            }

            m_Tracks.RemoveAll(t => t.misses > maxMisses || (!t.confirmed && t.hits == 0));

            for (int i = 0; i < frame.Count; i++)
            {
                if (!used[i])
                    m_Tracks.Add(new Track { detection = frame[i], hits = 1, confirmed = requiredHits <= 1 });
            }

            m_Confirmed.Clear();
            foreach (var track in m_Tracks)
            {
                if (track.confirmed)
                    m_Confirmed.Add(track.detection);
            }
            return m_Confirmed;
        }

        public void Clear()
        {
            m_Tracks.Clear();
            m_Confirmed.Clear();
        }
    }
}
