using System.Collections.Generic;
using CuidAR.Detection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuidAR.UI
{
    /// <summary>
    /// Screen UI for scanning (screens 2, 3 and 5 of the prototype): a status chip at the top,
    /// the scan frame (the square the model analyzes), a hint at the bottom and one colored box
    /// with a label per detected leaf.
    ///
    /// Everything is created in code, so the scene only needs this component.
    /// Boxes are placed with anchors in normalized screen coordinates, so they work at any resolution.
    /// </summary>
    public class DetectionOverlay : MonoBehaviour
    {
        const float k_BoxLine = 6f;
        const float k_CornerLength = 70f;
        const float k_CornerLine = 10f;

        static readonly Color k_Dark = new Color32(34, 34, 34, 230);
        static readonly Color k_Green = new Color32(30, 140, 70, 235);

        class BoxView
        {
            public RectTransform root;
            public Image[] edges;
            public Image labelBackground;
            public TextMeshProUGUI label;
        }

        RectTransform m_Root;
        RectTransform m_BoxesRoot;
        RectTransform m_Frame;
        Image m_StatusBackground;
        TextMeshProUGUI m_Status;
        TextMeshProUGUI m_Hint;
        RawImage m_Preview;
        readonly List<BoxView> m_Boxes = new();

        public void Build(bool withPreview)
        {
            var canvasGo = new GameObject("DetectionOverlay", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1; // below the scene's own canvas (Back button stays clickable)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            m_Root = (RectTransform)canvasGo.transform;

            m_BoxesRoot = CreateRect("Boxes", m_Root);
            Stretch(m_BoxesRoot, Vector2.zero, Vector2.one);

            m_Frame = CreateRect("ScanFrame", m_Root);
            BuildCorners(m_Frame);

            // Status chip (top center).
            var chip = CreateRect("Status", m_Root);
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -170f);
            m_StatusBackground = chip.gameObject.AddComponent<Image>();
            m_StatusBackground.color = k_Dark;
            AddAutoSize(chip, 40, 18);
            m_Status = CreateText("Text", chip, 44, FontStyles.Bold);

            // Hint (bottom center).
            var hint = CreateRect("Hint", m_Root);
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            hint.anchoredPosition = new Vector2(0f, 200f);
            hint.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            AddAutoSize(hint, 32, 14);
            m_Hint = CreateText("Text", hint, 34, FontStyles.Normal);
            m_Hint.text = "Fit the plant inside the square";

            if (withPreview)
            {
                // Debug: shows exactly what the model receives (check orientation and colors).
                var preview = CreateRect("ModelInputPreview", m_Root);
                preview.anchorMin = preview.anchorMax = preview.pivot = new Vector2(0f, 0f);
                preview.anchoredPosition = new Vector2(24f, 24f);
                preview.sizeDelta = new Vector2(300f, 300f);
                m_Preview = preview.gameObject.AddComponent<RawImage>();
            }

            SetStatus("Starting camera...", false);
        }

        public void SetStatus(string text, bool positive)
        {
            m_Status.text = text;
            m_StatusBackground.color = positive ? k_Green : k_Dark;
        }

        public void SetHintVisible(bool visible) => m_Hint.transform.parent.gameObject.SetActive(visible);

        public void SetPreview(Texture texture)
        {
            if (m_Preview != null)
                m_Preview.texture = texture;
        }

        /// <summary>Square of the screen analyzed by the model (normalized, origin at the top-left).</summary>
        public void SetScanFrame(Rect region)
        {
            PlaceNormalized(m_Frame, region);
        }

        /// <summary>Boxes in normalized screen coordinates (origin at the top-left).</summary>
        public void ShowDetections(IReadOnlyList<LeafDetection> detections)
        {
            while (m_Boxes.Count < detections.Count)
                m_Boxes.Add(CreateBox());

            for (int i = 0; i < m_Boxes.Count; i++)
            {
                var view = m_Boxes[i];
                bool active = i < detections.Count;
                view.root.gameObject.SetActive(active);
                if (!active)
                    continue;

                var detection = detections[i];
                var color = LeafClasses.ColorOf(detection.label);
                PlaceNormalized(view.root, detection.box);
                foreach (var edge in view.edges)
                    edge.color = color;
                view.labelBackground.color = color;
                view.label.text = $"{LeafClasses.DisplayName(detection.label)} {Mathf.RoundToInt(detection.score * 100f)}%";
            }
        }

        // ---------- building blocks ----------

        static void PlaceNormalized(RectTransform rect, Rect box)
        {
            // UI anchors use a bottom-left origin; the boxes use top-left.
            rect.anchorMin = new Vector2(box.xMin, 1f - box.yMax);
            rect.anchorMax = new Vector2(box.xMax, 1f - box.yMin);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        BoxView CreateBox()
        {
            var root = CreateRect("LeafBox", m_BoxesRoot);
            var view = new BoxView
            {
                root = root,
                edges = new[]
                {
                    Edge(root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, k_BoxLine)), // top
                    Edge(root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, k_BoxLine)), // bottom
                    Edge(root, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(k_BoxLine, 0)), // left
                    Edge(root, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(k_BoxLine, 0)), // right
                },
            };

            // Label sits just above the box, aligned to its left edge.
            var labelRect = CreateRect("Label", root);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = Vector2.zero;
            view.labelBackground = labelRect.gameObject.AddComponent<Image>();
            AddAutoSize(labelRect, 14, 6);
            view.label = CreateText("Text", labelRect, 32, FontStyles.Bold);
            return view;
        }

        static Image Edge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            var rect = CreateRect("Edge", parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect.gameObject.AddComponent<Image>();
        }

        static void BuildCorners(RectTransform frame)
        {
            var white = new Color(1f, 1f, 1f, 0.9f);
            // For each corner: one horizontal and one vertical bar.
            foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
            {
                Edge(frame, corner, corner, corner, new Vector2(k_CornerLength, k_CornerLine)).color = white;
                Edge(frame, corner, corner, corner, new Vector2(k_CornerLine, k_CornerLength)).color = white;
            }
        }

        static void AddAutoSize(RectTransform rect, int horizontalPadding, int verticalPadding)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static TextMeshProUGUI CreateText(string name, RectTransform parent, float size, FontStyles style)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
