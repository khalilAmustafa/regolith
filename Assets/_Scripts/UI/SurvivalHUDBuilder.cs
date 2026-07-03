using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SurvivalHUDBuilder
{
#if UNITY_EDITOR
    [MenuItem("Tools/Regolith/Create Survival HUD")]
    public static SurvivalHUD CreateSurvivalHUDMenu()
    {
        SurvivalHUD existingHud = UnityEngine.Object.FindAnyObjectByType<SurvivalHUD>();
        if (existingHud != null)
        {
            Selection.activeGameObject = existingHud.gameObject;
            Debug.Log("Survival HUD already exists. Existing HUD selected.");
            return existingHud;
        }

        SurvivalHUD hud = CreateSurvivalHUD();
        Selection.activeGameObject = hud.gameObject;
        return hud;
    }
#endif

    public static SurvivalHUD CreateSurvivalHUD()
    {
        GameObject canvasObject = new GameObject("SurvivalHUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject resourcePanel = new GameObject("ResourcePanel", typeof(RectTransform));
        resourcePanel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = resourcePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(28f, -28f);
        panelRect.sizeDelta = new Vector2(420f, 196f);

        Component oxygenText = CreateText("OxygenText", resourcePanel.transform, "Suit O2: 100 / 100", new Vector2(0f, -0f), TextAnchor.UpperLeft, 24);
        Component storedOxygenText = CreateText("StoredOxygenText", resourcePanel.transform, "Stored O2: 0 / 100", new Vector2(0f, -36f), TextAnchor.UpperLeft, 24);
        Component regolithText = CreateText("RegolithText", resourcePanel.transform, "Regolith: 0 / 10", new Vector2(0f, -72f), TextAnchor.UpperLeft, 24);
        Component concreteText = CreateText("ConcreteText", resourcePanel.transform, "Concrete: 0 / 20", new Vector2(0f, -108f), TextAnchor.UpperLeft, 24);
        Component powerText = CreateText("PowerText", resourcePanel.transform, "Power: OFFLINE", new Vector2(0f, -144f), TextAnchor.UpperLeft, 24);

        Component promptText = CreateText("PromptText", canvasObject.transform, string.Empty, Vector2.zero, TextAnchor.MiddleCenter, 30);
        RectTransform promptRect = ((Component)promptText).GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 92f);
        promptRect.sizeDelta = new Vector2(900f, 52f);

        Component messageText = CreateText("MessageText", canvasObject.transform, string.Empty, Vector2.zero, TextAnchor.MiddleCenter, 34);
        RectTransform messageRect = ((Component)messageText).GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.5f, 1f);
        messageRect.anchorMax = new Vector2(0.5f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(0f, -90f);
        messageRect.sizeDelta = new Vector2(1000f, 64f);

        SurvivalHUD hud = canvasObject.AddComponent<SurvivalHUD>();
        hud.player = UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalResources>();
        hud.Bind(oxygenText, storedOxygenText, regolithText, concreteText, powerText, promptText, messageText);

        promptText.gameObject.SetActive(false);
        messageText.gameObject.SetActive(false);

        return hud;
    }

    private static Component CreateText(string name, Transform parent, string text, Vector2 anchoredPosition, TextAnchor alignment, int fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(900f, 34f);

        Component textComponent = TryAddTextMeshPro(textObject);
        if (textComponent != null)
        {
            SetReflectedProperty(textComponent, "text", text);
            SetReflectedProperty(textComponent, "fontSize", (float)fontSize);
            SetReflectedProperty(textComponent, "color", Color.white);
            SetReflectedProperty(textComponent, "enableWordWrapping", false);
            SetTMPAlignment(textComponent, alignment);
            return textComponent;
        }

        Text uiText = textObject.AddComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiText.font == null)
        {
            uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        uiText.fontSize = fontSize;
        uiText.color = Color.white;
        uiText.alignment = alignment;
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        return uiText;
    }

    private static Component TryAddTextMeshPro(GameObject target)
    {
        Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        if (tmpType == null)
        {
            return null;
        }

        return target.AddComponent(tmpType) as Component;
    }

    private static void SetReflectedProperty(Component component, string propertyName, object value)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(component, value);
        }
    }

    private static void SetTMPAlignment(Component component, TextAnchor alignment)
    {
        Type alignmentType = Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
        if (alignmentType == null)
        {
            return;
        }

        string valueName = alignment == TextAnchor.MiddleCenter ? "Center" : "TopLeft";
        object alignmentValue = Enum.Parse(alignmentType, valueName);
        SetReflectedProperty(component, "alignment", alignmentValue);
    }
}
