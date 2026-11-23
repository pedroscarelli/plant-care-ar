using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Look of the main menu, built in code (no image files): green gradient background with soft circles,
/// the "CuidAR" title, a drawn potted plant that gently bobs, a styled Start button and a footer tip.
/// Added by <see cref="MainMenuController"/>; it only adds/restyles UI, the button keeps its behaviour.
/// </summary>
public class MainMenuStyle : MonoBehaviour
{
    static readonly Color k_Green = new Color32(36, 140, 78, 255);
    static readonly Color k_DarkGreen = new Color32(18, 70, 45, 255);

    RectTransform m_Plant;
    RectTransform m_Button;

    public static void Apply(Canvas canvas, Button startButton)
    {
        var style = canvas.gameObject.AddComponent<MainMenuStyle>();
        style.Build((RectTransform)canvas.transform, startButton);
    }

    void Build(RectTransform root, Button startButton)
    {
        // Leftover placeholder (white square without a sprite) from the old scene.
        foreach (Transform child in root)
            if (child.name == "Image" && child.TryGetComponent<Image>(out var image) && image.sprite == null)
                child.gameObject.SetActive(false);

        // Background: vertical gradient, behind everything.
        var background = Stretch(Rect("Background", root));
        background.SetAsFirstSibling();
        var gradient = background.gameObject.AddComponent<RawImage>();
        gradient.texture = GradientTexture();
        gradient.raycastTarget = false;

        // Soft decorative circles.
        var circle = CircleSprite();
        AddCircle(background, circle, new Vector2(0f, 1f), new Vector2(120f, -260f), 620f, 0.07f);
        AddCircle(background, circle, new Vector2(1f, 1f), new Vector2(-80f, -620f), 420f, 0.06f);
        AddCircle(background, circle, new Vector2(1f, 0f), new Vector2(-160f, 320f), 700f, 0.10f);
        AddCircle(background, circle, new Vector2(0f, 0f), new Vector2(60f, 700f), 300f, 0.08f);

        // Title.
        var title = Text("Title", background, "CuidAR", 170, FontStyles.Bold, Color.white);
        Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1000f, 220f));
        var subtitle = Text("Subtitle", background, "plant-care-ar", 48, FontStyles.Normal, new Color(1f, 1f, 1f, 0.85f));
        subtitle.characterSpacing = 8f;
        Place(subtitle, new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(1000f, 70f));
        var tagline = Text("Tagline", background, "Water, prune and protect a virtual plant in augmented reality", 36,
            FontStyles.Italic, new Color(1f, 1f, 1f, 0.75f));
        tagline.textWrappingMode = TextWrappingModes.Normal;
        Place(tagline, new Vector2(0.5f, 1f), new Vector2(0f, -610f), new Vector2(820f, 110f));

        // Illustration.
        m_Plant = Rect("PlantArt", background);
        var art = m_Plant.gameObject.AddComponent<RawImage>();
        art.texture = PlantTexture(512);
        art.raycastTarget = false;
        m_Plant.anchorMin = m_Plant.anchorMax = new Vector2(0.5f, 0.5f);
        m_Plant.sizeDelta = new Vector2(560f, 560f);
        m_Plant.anchoredPosition = new Vector2(0f, 20f);

        // Start button: bigger, green, lower on the screen.
        if (startButton != null)
        {
            m_Button = (RectTransform)startButton.transform;
            m_Button.anchoredPosition = new Vector2(0f, -470f);
            m_Button.sizeDelta = new Vector2(660f, 150f);
            if (startButton.TryGetComponent<Image>(out var buttonImage))
                buttonImage.color = k_Green;
            var colors = startButton.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            startButton.colors = colors;
            var label = startButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = Color.white;
                label.fontSize = 60;
                label.fontStyle = FontStyles.Bold;
            }
        }

        // Footer tip.
        var footer = Text("Footer", background, "Lay the printed QR marker on a table and point the camera at it", 32,
            FontStyles.Normal, k_DarkGreen);
        footer.textWrappingMode = TextWrappingModes.Normal;
        Place(footer, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(860f, 100f));
    }

    void Update()
    {
        if (m_Plant != null)
        {
            m_Plant.anchoredPosition = new Vector2(0f, 20f + Mathf.Sin(Time.time * 1.6f) * 12f);
            m_Plant.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.1f) * 2.5f);
        }
        if (m_Button != null)
            m_Button.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 3f) * 0.02f);
    }

    // ---------- UI helpers ----------

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static void Place(TMP_Text text, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string value, float size, FontStyles style, Color color)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    static void AddCircle(RectTransform parent, Sprite sprite, Vector2 anchor, Vector2 position, float size, float alpha)
    {
        var rect = Rect("Circle", parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;
    }

    // ---------- procedural textures ----------

    static Texture2D GradientTexture()
    {
        const int height = 256;
        var texture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var top = new Color(0.06f, 0.30f, 0.22f);
        var middle = new Color(0.22f, 0.58f, 0.36f);
        var bottom = new Color(0.86f, 0.95f, 0.80f);
        for (int y = 0; y < height; y++)
        {
            float t = 1f - y / (height - 1f); // 0 = top
            var color = t < 0.5f ? Color.Lerp(top, middle, t / 0.5f) : Color.Lerp(middle, bottom, Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.5f));
            texture.SetPixel(0, y, color);
        }
        texture.Apply();
        return texture;
    }

    static Sprite CircleSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * size / 2f)));
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>A potted plant with a flower and a water drop, drawn with signed distances (anti-aliased).</summary>
    static Texture2D PlantTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float aa = 1.5f / size;

        var terracotta = new Color(0.86f, 0.43f, 0.25f);
        var rim = new Color(0.74f, 0.33f, 0.19f);
        var soil = new Color(0.33f, 0.2f, 0.12f);
        var stem = new Color(0.2f, 0.45f, 0.18f);
        Color[] leafColors = { new(0.25f, 0.68f, 0.3f), new(0.18f, 0.58f, 0.26f), new(0.33f, 0.75f, 0.35f) };
        var petal = new Color(0.97f, 0.5f, 0.68f);
        var center = new Color(1f, 0.83f, 0.2f);
        var drop = new Color(0.3f, 0.62f, 1f);
        var shadow = new Color(0f, 0f, 0f, 0.18f);

        // Leaves: center, half length, half width, angle (degrees, 0 = pointing right).
        var leaves = new[]
        {
            (new Vector2(0.33f, 0.55f), 0.16f, 0.055f, 150f), (new Vector2(0.67f, 0.58f), 0.16f, 0.055f, 30f),
            (new Vector2(0.37f, 0.70f), 0.12f, 0.045f, 140f), (new Vector2(0.63f, 0.73f), 0.11f, 0.042f, 40f),
            (new Vector2(0.45f, 0.82f), 0.07f, 0.03f, 115f),
        };

        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            var p = new Vector2((px + 0.5f) / size, (py + 0.5f) / size);
            var color = Color.clear;

            // Ground shadow.
            Over(ref color, shadow, Ellipse(p, new Vector2(0.5f, 0.06f), 0.24f, 0.03f), aa);
            // Pot body (trapezoid narrower at the bottom).
            float k = Mathf.InverseLerp(0.06f, 0.36f, p.y);
            float halfWidth = Mathf.Lerp(0.16f, 0.22f, k);
            float potDistance = Mathf.Max(Mathf.Abs(p.x - 0.5f) - halfWidth, Mathf.Max(0.06f - p.y, p.y - 0.36f));
            Over(ref color, terracotta, potDistance, aa);
            Over(ref color, rim, Box(p, new Vector2(0.5f, 0.375f), new Vector2(0.25f, 0.035f)), aa);
            Over(ref color, soil, Ellipse(p, new Vector2(0.5f, 0.41f), 0.21f, 0.022f), aa);
            // Stem.
            Over(ref color, stem, Segment(p, new Vector2(0.5f, 0.41f), new Vector2(0.5f, 0.84f)) - 0.011f, aa);
            // Leaves.
            for (int i = 0; i < leaves.Length; i++)
            {
                var (c, length, width, angle) = leaves[i];
                Over(ref color, leafColors[i % leafColors.Length], Leaf(p, c, length, width, angle), aa);
            }
            // Flower.
            var flower = new Vector2(0.5f, 0.86f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f;
                Over(ref color, petal, Vector2.Distance(p, flower + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.05f) - 0.04f, aa);
            }
            Over(ref color, center, Vector2.Distance(p, flower) - 0.033f, aa);
            // Water drop (circle + pointed top).
            var dropCenter = new Vector2(0.8f, 0.82f);
            float dropDistance = Mathf.Min(Vector2.Distance(p, dropCenter) - 0.045f,
                Segment(p, dropCenter, dropCenter + new Vector2(0f, 0.08f)) - Mathf.Lerp(0.04f, 0.004f, Mathf.InverseLerp(dropCenter.y, dropCenter.y + 0.08f, p.y)));
            Over(ref color, drop, dropDistance, aa);
            Over(ref color, new Color(1f, 1f, 1f, 0.7f), Vector2.Distance(p, dropCenter + new Vector2(-0.015f, 0.012f)) - 0.011f, aa);

            pixels[py * size + px] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static void Over(ref Color destination, Color source, float distance, float aa)
    {
        float alpha = source.a * Mathf.Clamp01(0.5f - distance / aa);
        if (alpha <= 0f)
            return;
        float outAlpha = alpha + destination.a * (1f - alpha);
        var rgb = (new Vector3(source.r, source.g, source.b) * alpha
                   + new Vector3(destination.r, destination.g, destination.b) * destination.a * (1f - alpha)) / Mathf.Max(outAlpha, 1e-5f);
        destination = new Color(rgb.x, rgb.y, rgb.z, outAlpha);
    }

    static float Box(Vector2 p, Vector2 center, Vector2 half)
    {
        var d = new Vector2(Mathf.Abs(p.x - center.x) - half.x, Mathf.Abs(p.y - center.y) - half.y);
        return Mathf.Max(d.x, d.y);
    }

    static float Ellipse(Vector2 p, Vector2 center, float rx, float ry)
    {
        var d = p - center;
        return (new Vector2(d.x / rx, d.y / ry).magnitude - 1f) * Mathf.Min(rx, ry);
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Pointed leaf shape (vesica): intersection of two circles, rotated.</summary>
    static float Leaf(Vector2 p, Vector2 center, float halfLength, float halfWidth, float angle)
    {
        float r = Mathf.Deg2Rad * angle;
        var d = p - center;
        var local = new Vector2(d.x * Mathf.Cos(r) + d.y * Mathf.Sin(r), -d.x * Mathf.Sin(r) + d.y * Mathf.Cos(r));
        // Circle radius / offset so the lens spans ±halfLength along x and ±halfWidth along y.
        float radius = (halfLength * halfLength + halfWidth * halfWidth) / (2f * halfWidth);
        float offset = radius - halfWidth;
        local.y = Mathf.Abs(local.y);
        return Vector2.Distance(local, new Vector2(0f, -offset)) - radius;
    }
}
