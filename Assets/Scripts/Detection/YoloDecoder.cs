using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuidAR.Detection
{
    /// <summary>
    /// Turns the raw YOLOv8 output into a short list of detections.
    ///
    /// The exported model returns one tensor shaped [1, 4 + classes, anchors] (anchors = 8400 at 640x640).
    /// For each anchor: rows 0..3 hold the box (center x, center y, width, height, in input pixels)
    /// and the next rows hold one score per class (already 0..1).
    /// </summary>
    public static class YoloDecoder
    {
        public static void Decode(
            ReadOnlySpan<float> output, int numClasses, int numAnchors, int inputSize,
            float confidenceThreshold, float iouThreshold, int maxDetections,
            List<LeafDetection> results)
        {
            results.Clear();
            var candidates = new List<LeafDetection>();

            for (int i = 0; i < numAnchors; i++)
            {
                // Best class for this anchor.
                int bestClass = -1;
                float bestScore = confidenceThreshold;
                for (int c = 0; c < numClasses; c++)
                {
                    float score = output[(4 + c) * numAnchors + i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }
                if (bestClass < 0)
                    continue;

                float cx = output[i];
                float cy = output[numAnchors + i];
                float w = output[2 * numAnchors + i];
                float h = output[3 * numAnchors + i];

                candidates.Add(new LeafDetection
                {
                    box = new Rect((cx - w / 2f) / inputSize, (cy - h / 2f) / inputSize, w / inputSize, h / inputSize),
                    classId = bestClass,
                    score = bestScore,
                });
            }

            // Non-maximum suppression: keep the strongest box and drop the ones overlapping it.
            // It ignores the class on purpose, so each leaf gets a single label.
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var candidate in candidates)
            {
                bool overlaps = false;
                foreach (var kept in results)
                {
                    if (IoU(kept.box, candidate.box) > iouThreshold)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps)
                    continue;

                results.Add(candidate);
                if (results.Count >= maxDetections)
                    break;
            }
        }

        /// <summary>Intersection over union of two boxes (0 = apart, 1 = identical).</summary>
        public static float IoU(Rect a, Rect b)
        {
            float x1 = Mathf.Max(a.xMin, b.xMin);
            float y1 = Mathf.Max(a.yMin, b.yMin);
            float x2 = Mathf.Min(a.xMax, b.xMax);
            float y2 = Mathf.Min(a.yMax, b.yMax);
            float intersection = Mathf.Max(0f, x2 - x1) * Mathf.Max(0f, y2 - y1);
            float union = a.width * a.height + b.width * b.height - intersection;
            return union <= 0f ? 0f : intersection / union;
        }
    }
}
