using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Shared helpers for building UI at runtime. Used by the quiz mode (whose whole UI
// is generated in code) and by GameSceneManager for the title backdrop sprite.
public static class UiFactory
{
    private static Sprite roundedSprite;
    private static Sprite roundedOutlineSprite;

    // Alpha of a rounded-rect at the given pixel (1 inside, 0 outside, soft edge).
    static float RoundedAlpha(int x, int y, int size, int radius)
    {
        float cx = -1f, cy = -1f;
        if (x < radius && y < radius) { cx = radius; cy = radius; }
        else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
        else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
        else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }

        if (cx < 0f) return 1f;
        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        return Mathf.Clamp01(radius - dist + 0.5f);
    }

    static Sprite BuildRoundedSprite(bool outlineOnly)
    {
        const int size = 64;
        const int radius = 20;
        const int border = 6;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = RoundedAlpha(x, y, size, radius);
                if (outlineOnly)
                {
                    // Subtract the same shape inset by the border width, leaving a ring.
                    float inner = 0f;
                    if (x >= border && x < size - border && y >= border && y < size - border)
                    {
                        inner = RoundedAlpha(x - border, y - border, size - 2 * border, radius - border);
                    }
                    alpha *= 1f - inner;
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    // Procedurally generated rounded-rect sprite with 9-slice borders.
    // Built in code because Resources.GetBuiltinResource is not reliable in this Unity version.
    public static Sprite GetRoundedSprite()
    {
        if (roundedSprite == null) roundedSprite = BuildRoundedSprite(outlineOnly: false);
        return roundedSprite;
    }

    // Ring-only variant, used to mimic the outlined pill look of the NameInput graphics.
    public static Sprite GetRoundedOutlineSprite()
    {
        if (roundedOutlineSprite == null) roundedOutlineSprite = BuildRoundedSprite(outlineOnly: true);
        return roundedOutlineSprite;
    }

    public static Canvas CreateCanvas(string name)
    {
        GameObject canvasObj = new GameObject(name, typeof(RectTransform));
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        return canvas;
    }

    public static RectTransform CreatePanel(string name, Transform parent, Color color, bool rounded = false)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        if (rounded)
        {
            img.sprite = GetRoundedSprite();
            img.type = Image.Type.Sliced;
        }
        img.color = color;
        return (RectTransform)obj.transform;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        return tmp;
    }

    public static Button CreateButton(string name, Transform parent, string label, float fontSize,
        Color backgroundColor, Color textColor)
    {
        RectTransform rect = CreatePanel(name, parent, backgroundColor, rounded: true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();

        TextMeshProUGUI text = CreateText("Label", rect, label, fontSize, textColor);
        Stretch(text.rectTransform);

        return button;
    }

    public static TMP_InputField CreateInputField(string name, Transform parent, string placeholderText,
        float fontSize, Color? borderColor = null)
    {
        RectTransform rect = CreatePanel(name, parent, Color.white, rounded: true);
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();

        if (borderColor.HasValue)
        {
            RectTransform border = CreatePanel("Border", rect, borderColor.Value);
            Image borderImage = border.GetComponent<Image>();
            borderImage.sprite = GetRoundedOutlineSprite();
            borderImage.type = Image.Type.Sliced;
            borderImage.raycastTarget = false;
            Stretch(border);
        }

        RectTransform textArea = new GameObject("TextArea", typeof(RectTransform)).GetComponent<RectTransform>();
        textArea.SetParent(rect, false);
        Stretch(textArea, 14f, 6f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateText("Placeholder", textArea, placeholderText, fontSize,
            new Color(0f, 0f, 0f, 0.4f), TextAlignmentOptions.Left);
        Stretch(placeholder.rectTransform);
        placeholder.fontStyle = FontStyles.Italic;

        TextMeshProUGUI text = CreateText("Text", textArea, "", fontSize, Color.black, TextAlignmentOptions.Left);
        Stretch(text.rectTransform);

        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = rect.GetComponent<Image>();

        return input;
    }

    // Vertical scroll view whose content auto-grows with its children.
    // Returns the content transform to parent rows into.
    public static RectTransform CreateScrollView(string name, Transform parent, out ScrollRect scrollRect)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        scrollRect = obj.AddComponent<ScrollRect>();
        obj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        obj.AddComponent<RectMask2D>();

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(obj.transform, false);
        RectTransform content = (RectTransform)contentObj.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 12f;
        layout.padding = new RectOffset(8, 8, 8, 8);

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = content;
        scrollRect.viewport = (RectTransform)obj.transform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        return content;
    }

    public static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }
}
