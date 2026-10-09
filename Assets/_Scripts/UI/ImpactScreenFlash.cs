using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ImpactScreenFlash : MonoBehaviour
{
    [SerializeField] private Image flashImage;

    private Coroutine flashRoutine;

    public static ImpactScreenFlash CreateDefaultOverlay()
    {
        GameObject canvasObject = new GameObject("ImpactFlashCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject overlayObject = new GameObject("ImpactFlashOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ImpactScreenFlash));
        overlayObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = overlayObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;

        return overlayObject.GetComponent<ImpactScreenFlash>();
    }

    private void Awake()
    {
        ResolveImage();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        ResolveImage();
        SetAlpha(0f);
    }

    public void PlayFlash(float peakAlpha = 1f, float holdTime = 0.08f, float fadeOutTime = 0.3f)
    {
        ResolveImage();
        if (flashImage == null)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine(
            Mathf.Clamp01(peakAlpha),
            Mathf.Max(0f, holdTime),
            Mathf.Max(0.01f, fadeOutTime)));
    }

    private IEnumerator FlashRoutine(float peakAlpha, float holdTime, float fadeOutTime)
    {
        SetAlpha(peakAlpha);

        if (holdTime > 0f)
        {
            yield return new WaitForSecondsRealtime(holdTime);
        }

        float elapsed = 0f;
        float startAlpha = peakAlpha;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutTime);
            SetAlpha(Mathf.Lerp(startAlpha, 0f, t));
            yield return null;
        }

        SetAlpha(0f);
        flashRoutine = null;
    }

    private void ResolveImage()
    {
        if (flashImage == null)
        {
            flashImage = GetComponent<Image>();
        }

        if (flashImage != null)
        {
            flashImage.raycastTarget = false;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (flashImage == null)
        {
            return;
        }

        Color color = flashImage.color;
        color.r = 1f;
        color.g = 1f;
        color.b = 1f;
        color.a = Mathf.Clamp01(alpha);
        flashImage.color = color;
    }
}
