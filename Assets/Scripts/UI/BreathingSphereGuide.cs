using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Guia visual de respiracion 4-7-8.
/// Azul: inhala y crece.
/// Amarillo: mantiene EXACTAMENTE el tamano grande.
/// Verde: exhala y se reduce.
/// Si no tiene un texto asignado, muestra debajo de la esfera una tarjeta oscura (estilo de los menús) con la fase en
/// grande y una barra que se llena durante la fase; al cambiar de fase la palabra aparece con un fundido y los colores
/// pasan suavemente al de la fase. Tras los ciclos pedidos sigue respirando: IsCompleted indica que ya se hicieron.
/// </summary>
public class BreathingSphereGuide : MonoBehaviour
{
    [Header("Tiempos")]
    [SerializeField] private float inhaleSeconds = 4f;
    [SerializeField] private float holdSeconds = 7f;
    [SerializeField] private float exhaleSeconds = 8f;
    [SerializeField] private int cyclesToComplete = 3;

    [Header("Escala")]
    [SerializeField] private float minSize = 0.25f;
    [SerializeField] private float maxSize = 0.65f;

    [Header("Colores")]
    [SerializeField] private Color inhaleColor = new Color(0.25f, 0.55f, 1f, 1f);
    [SerializeField] private Color holdColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color exhaleColor = new Color(0.25f, 1f, 0.45f, 1f);
    [SerializeField] private Color completedColor = Color.white;

    [Header("Texto opcional")]
    [SerializeField] private TMP_Text instructionTMP;
    [SerializeField] private Text instructionText;


    // Etiqueta en metros percibidos (se multiplica por la escala del rig del nivel).
    private const float LabelGapBelowSphere = 0.12f;
    private const float LabelForward = 0.1f;
    // Tarjeta: unidades de lienzo y anchura en metros percibidos.
    private static readonly Vector2 CardSize = new Vector2(420f, 140f);
    private const float CardWidthMeters = 0.6f;
    private const float PhaseFadeSeconds = 0.35f;
    private const float ColorBlendSeconds = 0.45f;

    private Renderer sphereRenderer;
    private MaterialPropertyBlock materialBlock;
    private Coroutine routine;
    private int completedCycles;
    private bool completed;

    private Canvas card;
    private TMP_Text cardPhase;
    private Image cardFill;
    private string phaseName = "";
    private Color phaseColor = Color.white;
    private Color shownColor = Color.white;
    private float phaseStartedAt;
    private float phaseDuration = 1f;
    private float phaseEndsAt;
    private string cardPhaseShown;
    private float cardPhaseChangedAt;
    private float nextOnTopCheck;

    private Vector3 MinScale => Vector3.one * minSize;
    private Vector3 MaxScale => Vector3.one * maxSize;

    private void Awake()
    {
        sphereRenderer = GetComponent<Renderer>();
        materialBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        StartBreathing();
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (card != null) card.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (card != null) Destroy(card.gameObject);
    }

    public void StartBreathing()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        completedCycles = 0;
        completed = false;
        transform.localScale = MinScale;
        if (isActiveAndEnabled) routine = StartCoroutine(BreathingRoutine());
    }

    public bool IsCompleted()
    {
        return completed;
    }

    public int GetCompletedCycles()
    {
        return completedCycles;
    }

    private IEnumerator BreathingRoutine()
    {
        while (true)
        {
            SetVisual("INHALA", inhaleColor, inhaleSeconds);
            yield return ScaleOverTime(MinScale, MaxScale, inhaleSeconds);

            // Clave: forzamos el tamano grande ANTES y DURANTE todo el tiempo amarillo.
            SetVisual("MANTÉN", holdColor, holdSeconds);
            transform.localScale = MaxScale;
            yield return HoldLarge(holdSeconds);

            SetVisual("EXHALA", exhaleColor, exhaleSeconds);
            yield return ScaleOverTime(MaxScale, MinScale, exhaleSeconds);

            completedCycles++;
            if (completedCycles >= cyclesToComplete) completed = true;
        }
    }

    private IEnumerator HoldLarge(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            transform.localScale = MaxScale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = MaxScale;
    }

    private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            float t = Mathf.Clamp01(elapsed / seconds);
            float smoothT = t * t * (3f - 2f * t);
            transform.localScale = Vector3.Lerp(from, to, smoothT);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = to;
    }

    private void SetVisual(string text, Color color, float seconds)
    {
        phaseName = text;
        phaseColor = color;
        phaseStartedAt = Time.time;
        phaseDuration = Mathf.Max(0.01f, seconds);
        phaseEndsAt = Time.time + seconds;
        SetInstruction(text);
        SetColor(color);
    }

    private void SetInstruction(string text)
    {
        if (instructionTMP != null) instructionTMP.text = text;
        if (instructionText != null) instructionText.text = text;
    }

    private void SetColor(Color color)
    {
        if (sphereRenderer == null) return;

        sphereRenderer.GetPropertyBlock(materialBlock);
        materialBlock.SetColor("_BaseColor", color);
        materialBlock.SetColor("_Color", color);
        sphereRenderer.SetPropertyBlock(materialBlock);
    }

    // ------------------------------------------------------------------ tarjeta

    private void LateUpdate()
    {
        if (instructionTMP != null || instructionText != null) return;   // la escena ya trae su propio texto
        if (card == null) CreateCard();
        if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);

        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        float rigScale = NeuroPhobiaWorldPanel.RigScale(head != null ? head.transform : null);

        // Debajo del tamaño máximo de la esfera (encima chocaba con la primera imagen, que está justo arriba) y
        // adelantada hacia la persona, para que el alféizar o la pared de detrás no la tapen.
        float parentScale = transform.parent != null ? transform.parent.lossyScale.y : 1f;
        float maxRadius = maxSize * parentScale * 0.5f;
        Transform t = card.transform;
        Vector3 towardViewer = Vector3.zero;
        if (head != null)
        {
            towardViewer = head.transform.position - transform.position;
            towardViewer.y = 0f;
            towardViewer = towardViewer.sqrMagnitude > 0.0001f ? towardViewer.normalized : Vector3.zero;
        }
        t.position = transform.position + Vector3.down * (maxRadius + LabelGapBelowSphere * rigScale) +
                     towardViewer * (maxRadius + LabelForward * rigScale);
        t.localScale = Vector3.one * (CardWidthMeters * rigScale / CardSize.x);
        if (head != null)
        {
            Vector3 toCard = t.position - head.transform.position;
            toCard.y = 0f;
            if (toCard.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(toCard, Vector3.up);
        }

        // Fase: la palabra nueva aparece con un fundido y un pequeño crecimiento; los colores pasan suavemente.
        if (cardPhaseShown != phaseName)
        {
            cardPhaseShown = phaseName;
            cardPhaseChangedAt = Time.time;
            cardPhase.text = phaseName;
        }
        float appear = Mathf.Clamp01((Time.time - cardPhaseChangedAt) / PhaseFadeSeconds);
        float eased = 1f - (1f - appear) * (1f - appear);
        shownColor = Color.Lerp(shownColor, phaseColor, Mathf.Clamp01(Time.deltaTime / ColorBlendSeconds) * 3f);
        Color word = Color.Lerp(NeuroPhobiaUITheme.Cream, shownColor, 0.55f);
        word.a = eased;
        cardPhase.color = word;
        cardPhase.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, eased);

        float progress = Mathf.Clamp01((Time.time - phaseStartedAt) / phaseDuration);
        cardFill.fillAmount = Mathf.SmoothStep(0f, 1f, progress);
        cardFill.color = shownColor;

        // Por encima de la escena, como los menús (la silla y la planta junto a las esferas la tapaban).
        if (Time.unscaledTime >= nextOnTopCheck)
        {
            nextOnTopCheck = Time.unscaledTime + 0.5f;
            NeuroPhobiaWorldPanel.DrawOnTop(card.transform);
        }
    }

    private void CreateCard()
    {
        GameObject root = new GameObject(name + "_Tarjeta", typeof(RectTransform));
        card = root.AddComponent<Canvas>();
        card.renderMode = RenderMode.WorldSpace;
        ((RectTransform)root.transform).sizeDelta = CardSize;

        GameObject background = NeuroPhobiaUITheme.Node(root.transform, "Fondo");
        NeuroPhobiaUITheme.Stretch(background);
        NeuroPhobiaUITheme.AddImage(background, NeuroPhobiaUITheme.Solid, NeuroPhobiaUITheme.WithAlpha(NeuroPhobiaUITheme.Night, 0.86f));
        GameObject border = NeuroPhobiaUITheme.Node(root.transform, "Borde");
        NeuroPhobiaUITheme.Stretch(border);
        NeuroPhobiaUITheme.AddImage(border, NeuroPhobiaUITheme.Frame, NeuroPhobiaUITheme.WithAlpha(NeuroPhobiaUITheme.Cream, 0.25f)).fillCenter = false;

        GameObject phase = NeuroPhobiaUITheme.Node(root.transform, "Fase");
        RectTransform phaseRect = (RectTransform)phase.transform;
        phaseRect.anchorMin = new Vector2(0f, 0.32f);
        phaseRect.anchorMax = new Vector2(1f, 1f);
        phaseRect.offsetMin = phaseRect.offsetMax = Vector2.zero;
        cardPhase = NeuroPhobiaUITheme.AddText(phase, "", NeuroPhobiaUITheme.Face.TypeBold, 46f, NeuroPhobiaUITheme.Cream, TextAlignmentOptions.Center);

        GameObject bar = NeuroPhobiaUITheme.Node(root.transform, "Barra");
        RectTransform barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(0.1f, 0.14f);
        barRect.anchorMax = new Vector2(0.9f, 0.24f);
        barRect.offsetMin = barRect.offsetMax = Vector2.zero;
        NeuroPhobiaUITheme.AddImage(bar, NeuroPhobiaUITheme.Solid, NeuroPhobiaUITheme.WithAlpha(NeuroPhobiaUITheme.Cream, 0.15f));
        GameObject fill = NeuroPhobiaUITheme.Node(bar.transform, "Relleno");
        NeuroPhobiaUITheme.Stretch(fill);
        cardFill = NeuroPhobiaUITheme.AddImage(fill, NeuroPhobiaUITheme.Solid, Color.white);
        cardFill.type = Image.Type.Filled;
        cardFill.fillMethod = Image.FillMethod.Horizontal;
        cardFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        cardFill.fillAmount = 0f;

        NeuroPhobiaWorldPanel.DrawOnTop(card.transform);
    }
}
