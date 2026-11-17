using UnityEngine;

namespace CuidAR.Detection
{
    /// <summary>One leaf found by the model.</summary>
    public struct LeafDetection
    {
        /// <summary>
        /// Bounding box, normalized (0..1) with the origin at the TOP-LEFT corner.
        /// Relative to the model input while decoding, and to the screen once mapped.
        /// </summary>
        public Rect box;
        public int classId;
        public string label;
        public float score;
    }
}
