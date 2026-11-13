using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Black overlay fixed to the player's view, used to hide abrupt camera moves: the level start (shown as eyes
/// opening) and the short trips to and from a seat. It is a small world-space canvas parented to the gameplay
/// camera, so it works both in the headset and on a desktop camera. All animations use unscaled time because
/// the session panels pause the game with timeScale 0.
/// </summary>
public sealed class ScreenFader : MonoBehaviour
{
    private const float CanvasDistance = 0.35f;
    private const float CanvasSize = 2000f;
    private const float CanvasScale = 0.0015f;
    private const int SortingOrder = 32767;

    private static ScreenFader instance;

    private Canvas canvas;
    private RectTransform upperLid;
    private RectTransform lowerLid;
    private Image veil;
    private Camera boundCamera;

    /// <summary>True while the view is at least partly covered.</summary>
    public static bool IsCovering => instance != null && instance.canvas != null && instance.canvas.enabled;

    private static ScreenFader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("NP_ScreenFader", typeof(RectTransform)).AddComponent<ScreenFader>();
                instance.Build();
            }
            return instance;
        }
    }

    /// <summary>Covers the view completely, immediately.</summary>
    public static void CoverNow()
    {
        Instance.Apply(0f, 1f);
    }

    /// <summary>Uncovers the view like eyes opening: a first glimpse, a slow blink, then fully open.</summary>
    public static IEnumerator OpenEyes(float seconds = 1.6f)
    {
        ScreenFader fader = Instance;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float openness = EyeOpenness(Mathf.Clamp01(elapsed / seconds));
            fader.Apply(openness, 1f - Mathf.SmoothStep(0f, 1f, openness * 1.4f));
            yield return null;
        }
        fader.Hide();
    }

    /// <summary>Fades the whole view to black.</summary>
    public static IEnumerator FadeOut(float seconds = 0.25f)
    {
        yield return Instance.FadeVeil(0f, 1f, seconds);
    }

    /// <summary>Fades the whole view back from black.</summary>
    public static IEnumerator FadeIn(float seconds = 0.25f)
    {
        ScreenFader fader = Instance;
        yield return fader.FadeVeil(1f, 0f, seconds);
        fader.Hide();
    }

    // Openness over normalized time: opens a little, half closes (blink), then opens completely.
    private static float EyeOpenness(float t)
    {
        if (t < 0.3f) return Mathf.SmoothStep(0f, 0.35f, t / 0.3f);
        if (t < 0.45f) return Mathf.SmoothStep(0.35f, 0.15f, (t - 0.3f) / 0.15f);
        return Mathf.SmoothStep(0.15f, 1f, (t - 0.45f) / 0.55f);
    }

    private IEnumerator FadeVeil(float from, float to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            Apply(1f, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds))));
            yield return null;
        }
        Apply(1f, to);
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = SortingOrder;
        RectTransform rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(CanvasSize, CanvasSize);

        veil = CreateBlackImage("Veil", new Vector2(CanvasSize, CanvasSize)).GetComponent<Image>();
        upperLid = CreateBlackImage("UpperLid", new Vector2(CanvasSize, CanvasSize));
        lowerLid = CreateBlackImage("LowerLid", new Vector2(CanvasSize, CanvasSize));
        Hide();
    }

    private RectTransform CreateBlackImage(string objectName, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Image image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        return rect;
    }

    // openness: 0 = lids closed, 1 = lids fully out of view. veilAlpha: opacity of the full-view black layer.
    private void Apply(float openness, float veilAlpha)
    {
        AttachToCamera();
        canvas.enabled = true;
        float lidOffset = CanvasSize * 0.5f + openness * CanvasSize * 0.5f;
        upperLid.anchoredPosition = new Vector2(0f, lidOffset);
        lowerLid.anchoredPosition = new Vector2(0f, -lidOffset);
        veil.color = new Color(0f, 0f, 0f, Mathf.Clamp01(veilAlpha));
    }

    private void Hide()
    {
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (canvas.enabled) AttachToCamera();
    }

    // The gameplay camera can change (desktop rig or XR rig): the overlay always follows the active one.
    private void AttachToCamera()
    {
        Camera camera = NeuroPhobiaWorldPanel.GameplayCamera();
        if (camera == null || camera == boundCamera) return;
        boundCamera = camera;
        transform.SetParent(camera.transform, false);
        transform.localPosition = new Vector3(0f, 0f, CanvasDistance);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * CanvasScale;
        canvas.worldCamera = camera;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
