using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuidAR.Watering
{
    /// <summary>
    /// Screen UI of the watering experience: status chip (top), hydration bar, step-by-step hint (bottom)
    /// and a "Dry again" button to replay the demo. Everything is created in code.
    /// </summary>
    public class WateringOverlay : MonoBehaviour
    {
        static readonly Color k_Dark = new Color32(34, 34, 34, 230);
        static readonly Color k_Green = new Color32(30, 140, 70, 235);
        static readonly Color k_Blue = new Color32(40, 140, 235, 255);
        static readonly Color k_Orange = new Color32(220, 140, 40, 255);

        Image m_StatusBackground;
        TextMeshProUGUI m_Status;
        GameObject m_HintRoot;
        TextMeshProUGUI m_Hint;
        GameObject m_BarRoot;
        RectTransform m_BarFill;
        Image m_BarFillImage;
        TextMeshProUGUI m_BarLabel;
        GameObject m_ResetButton;
        GameObject m_SettingsButton;
        GameObject m_MoveButton;
        GameObject m_Reticle;
        Image[] m_ReticleBars;
        GameObject m_SpeciesRoot;
        Image[] m_SpeciesButtons;
        GameObject m_ToolRoot;
        Image[] m_ToolButtons;

        public event Action resetPressed;
        public event Action settingsPressed;
        /// <summary>"Move" button: place the plant somewhere else.</summary>
        public event Action movePressed;
        /// <summary>A plant model button was tapped (index into the names given to Build).</summary>
        public event Action<int> speciesPressed;
        /// <summary>A tool button was tapped (index into the tool names given to Build).</summary>
        public event Action<int> toolPressed;

        public void Build(string[] speciesNames, string[] toolNames)
        {
            var canvasGo = new GameObject("WateringOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1; // below the scene's own canvas (Back button stays on top)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            // Status chip (top center, below the Back button).
            var chip = CreateRect("Status", root);
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -200f);
            m_StatusBackground = chip.gameObject.AddComponent<Image>();
            m_StatusBackground.color = k_Dark;
            m_StatusBackground.raycastTarget = false;
            AddAutoSize(chip, 40, 18);
            m_Status = CreateText("Text", chip, 44, FontStyles.Bold);

            // Hydration bar.
            var bar = CreateRect("HydrationBar", root);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -310f);
            bar.sizeDelta = new Vector2(760f, 70f);
            var barBackground = bar.gameObject.AddComponent<Image>();
            barBackground.color = new Color(0f, 0f, 0f, 0.55f);
            barBackground.raycastTarget = false;
            m_BarFill = CreateRect("Fill", bar);
            m_BarFill.anchorMin = Vector2.zero;
            m_BarFill.anchorMax = new Vector2(0f, 1f);
            m_BarFill.offsetMin = new Vector2(6f, 6f);
            m_BarFill.offsetMax = new Vector2(-6f, -6f);
            m_BarFillImage = m_BarFill.gameObject.AddComponent<Image>();
            m_BarFillImage.raycastTarget = false;
            m_BarLabel = CreateText("Label", bar, 36, FontStyles.Bold);
            var labelRect = (RectTransform)m_BarLabel.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            m_BarRoot = bar.gameObject;

            // Hint (bottom center).
            var hint = CreateRect("Hint", root);
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            hint.anchoredPosition = new Vector2(0f, 330f);
            var hintBackground = hint.gameObject.AddComponent<Image>();
            hintBackground.color = new Color(0f, 0f, 0f, 0.5f);
            hintBackground.raycastTarget = false;
            AddAutoSize(hint, 36, 18);
            m_Hint = CreateText("Text", hint, 38, FontStyles.Normal);
            m_HintRoot = hint.gameObject;

            // "Dry again" button (bottom right) to replay the demo.
            var button = CreateRect("ResetButton", root);
            button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 0f);
            button.anchoredPosition = new Vector2(-40f, 120f);
            button.sizeDelta = new Vector2(320f, 110f);
            var buttonImage = button.gameObject.AddComponent<Image>();
            buttonImage.color = k_Dark;
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => resetPressed?.Invoke());
            var buttonText = CreateText("Text", button, 38, FontStyles.Bold);
            buttonText.text = "Dry again";
            var buttonTextRect = (RectTransform)buttonText.transform;
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = buttonTextRect.offsetMax = Vector2.zero;
            m_ResetButton = button.gameObject;

            // "Move" button (top right, the scene's Back button is top left).
            var move = CreateRect("MoveButton", root);
            move.anchorMin = move.anchorMax = move.pivot = new Vector2(1f, 1f);
            move.anchoredPosition = new Vector2(-40f, -60f);
            move.sizeDelta = new Vector2(240f, 100f);
            move.gameObject.AddComponent<Image>().color = k_Dark;
            move.gameObject.AddComponent<Button>().onClick.AddListener(() => movePressed?.Invoke());
            var moveText = CreateText("Text", move, 38, FontStyles.Bold);
            moveText.text = "Move";
            var moveTextRect = (RectTransform)moveText.transform;
            moveTextRect.anchorMin = Vector2.zero;
            moveTextRect.anchorMax = Vector2.one;
            moveTextRect.offsetMin = moveTextRect.offsetMax = Vector2.zero;
            m_MoveButton = move.gameObject;
            m_MoveButton.SetActive(false);

            // "Open Settings" button (center), shown only when camera access was denied.
            var settings = CreateRect("SettingsButton", root);
            settings.anchorMin = settings.anchorMax = settings.pivot = new Vector2(0.5f, 0.5f);
            settings.sizeDelta = new Vector2(520f, 130f);
            settings.gameObject.AddComponent<Image>().color = k_Green;
            settings.gameObject.AddComponent<Button>().onClick.AddListener(() => settingsPressed?.Invoke());
            var settingsText = CreateText("Text", settings, 42, FontStyles.Bold);
            settingsText.text = "Open Settings";
            var settingsTextRect = (RectTransform)settingsText.transform;
            settingsTextRect.anchorMin = Vector2.zero;
            settingsTextRect.anchorMax = Vector2.one;
            settingsTextRect.offsetMin = settingsTextRect.offsetMax = Vector2.zero;
            m_SettingsButton = settings.gameObject;
            m_SettingsButton.SetActive(false);

            // "+" crosshair at the center: where the water will land.
            var reticle = CreateRect("Reticle", root);
            reticle.anchorMin = reticle.anchorMax = reticle.pivot = new Vector2(0.5f, 0.5f);
            reticle.sizeDelta = new Vector2(80f, 80f);
            m_ReticleBars = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var line = CreateRect("Bar", reticle);
                line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
                line.sizeDelta = i == 0 ? new Vector2(80f, 10f) : new Vector2(10f, 80f);
                m_ReticleBars[i] = line.gameObject.AddComponent<Image>();
                m_ReticleBars[i].raycastTarget = false;
            }
            m_Reticle = reticle.gameObject;
            m_Reticle.SetActive(false);

            // Plant model selector (row under the hydration bar).
            var row = CreateButtonRow("SpeciesSelector", root, speciesNames, i => speciesPressed?.Invoke(i), out m_SpeciesButtons);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -400f);
            row.sizeDelta = new Vector2(780f, 90f);
            m_SpeciesRoot = row.gameObject;
            m_SpeciesRoot.SetActive(false);

            // Tool selector (bottom left; "Dry again" is at the bottom right).
            var tools = CreateButtonRow("ToolSelector", root, toolNames, i => toolPressed?.Invoke(i), out m_ToolButtons);
            tools.anchorMin = tools.anchorMax = tools.pivot = Vector2.zero;
            tools.anchoredPosition = new Vector2(40f, 120f);
            tools.sizeDelta = new Vector2(630f, 110f);
            m_ToolRoot = tools.gameObject;
            m_ToolRoot.SetActive(false);

            SetStatus("Starting camera...", false);
            SetHint(null);
            SetHydration(-1f, false);
            SetResetVisible(false);
        }

        public void SetStatus(string text, bool positive)
        {
            m_Status.text = text;
            m_StatusBackground.color = positive ? k_Green : k_Dark;
        }

        public void SetHint(string text)
        {
            m_HintRoot.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text))
                m_Hint.text = text;
        }

        /// <summary>Hydration 0..1; negative hides the bar.</summary>
        public void SetHydration(float value, bool watering)
        {
            m_BarRoot.SetActive(value >= 0f);
            if (value < 0f)
                return;
            m_BarFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            m_BarFillImage.color = Color.Lerp(k_Orange, k_Blue, Mathf.InverseLerp(0.2f, 0.7f, value));
            m_BarLabel.text = (watering ? "Watering... " : "Hydration ") + Mathf.RoundToInt(value * 100f) + "%";
        }

        public void SetResetVisible(bool visible) => m_ResetButton.SetActive(visible);

        /// <summary>Shows the plant model selector with <paramref name="selected"/> highlighted.</summary>
        public void SetSpecies(bool visible, int selected) => Highlight(m_SpeciesRoot, m_SpeciesButtons, visible, selected);

        /// <summary>Shows the tool selector with <paramref name="selected"/> highlighted.</summary>
        public void SetTool(bool visible, int selected) => Highlight(m_ToolRoot, m_ToolButtons, visible, selected);

        static void Highlight(GameObject root, Image[] buttons, bool visible, int selected)
        {
            root.SetActive(visible);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].color = i == selected ? k_Green : new Color(0f, 0f, 0f, 0.55f);
        }

        static RectTransform CreateButtonRow(string name, RectTransform parent, string[] labels, Action<int> onPressed, out Image[] images)
        {
            var row = CreateRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            images = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var item = CreateRect(labels[i], row);
                images[i] = item.gameObject.AddComponent<Image>();
                item.gameObject.AddComponent<Button>().onClick.AddListener(() => onPressed(index));
                var label = CreateText("Text", item, 36, FontStyles.Bold);
                label.text = labels[i];
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            }
            return row;
        }

        public void SetSettingsVisible(bool visible) => m_SettingsButton.SetActive(visible);

        public void SetMoveVisible(bool visible) => m_MoveButton.SetActive(visible);

        /// <summary>Center crosshair; blue when it is on the pot.</summary>
        public void SetReticle(bool visible, bool onTarget)
        {
            m_Reticle.SetActive(visible);
            var color = onTarget ? k_Blue : new Color(1f, 1f, 1f, 0.85f);
            foreach (var bar in m_ReticleBars)
                bar.color = color;
        }

        // ---------- building blocks ----------

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
    }
}
