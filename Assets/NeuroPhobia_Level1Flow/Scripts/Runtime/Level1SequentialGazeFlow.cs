using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla la secuencia de cuatro ilustraciones del Nivel 1.
/// El usuario debe mirar cada ilustración de forma continua durante el tiempo configurado.
/// Al completar la última, muestra una pantalla completa y carga Nivel2 después de una pausa.
/// </summary>
public sealed class Level1SequentialGazeFlow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private List<GameObject> cards = new List<GameObject>();
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private TMP_Text completionText;

    [Header("Secuencia")]
    [SerializeField, Min(0.5f)] private float secondsPerCard = 10f;
    [SerializeField, Range(0.02f, 0.40f)] private float viewportRadius = 0.13f;
    [SerializeField, Min(0f)] private float lookAwayGraceSeconds = 0.25f;

    [Header("Final del nivel")]
    [SerializeField, Min(0f)] private float completionDelaySeconds = 5f;
    [SerializeField] private string nextSceneName = "Nivel2";
    [SerializeField] private bool pauseGameplayDuringCompletion = true;

    private Canvas hudCanvas;
    private int currentCardIndex;
    private float gazeTimer;
    private float lastValidLookTime = float.NegativeInfinity;
    private bool sequenceFinished;
    private bool completionStarted;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        previousTimeScale = Time.timeScale;
    }

    private void Start()
    {
        FindPlayerCamera();
        PrepareCards();

        // The floating progress panel saved in the scene is replaced by the shared wrist HUD (LevelHud).
        if (progressText != null && progressText.canvas != null)
        {
            hudCanvas = progressText.canvas.rootCanvas;
            hudCanvas.gameObject.SetActive(false);
        }

        if (completionPanel != null)
        {
            completionPanel.SetActive(false);
        }

        UpdateProgressUI(false);
    }

    private void Update()
    {
        if (sequenceFinished)
        {
            return;
        }

        if (playerCamera == null || !playerCamera.isActiveAndEnabled)
        {
            FindPlayerCamera();
            UpdateProgressUI(false);
            return;
        }

        if (cards == null || cards.Count == 0 || currentCardIndex >= cards.Count)
        {
            BeginCompletion();
            return;
        }

        // En pausa (o respiración) no corre el tiempo de mirada, igual que en Nivel2.
        if (GameStateManager.CurrentState != GameState.PLAYING)
        {
            lastValidLookTime = Time.unscaledTime;
            UpdateProgressUI(false);
            return;
        }

        GameObject currentCard = cards[currentCardIndex];
        bool isLooking = IsLookingAtCard(currentCard);

        if (isLooking)
        {
            lastValidLookTime = Time.unscaledTime;
            gazeTimer += Time.unscaledDeltaTime;
        }
        else if (Time.unscaledTime - lastValidLookTime > lookAwayGraceSeconds)
        {
            gazeTimer = 0f;
        }

        if (gazeTimer >= secondsPerCard)
        {
            CompleteCurrentCard();
        }

        UpdateProgressUI(isLooking);
    }

    private void PrepareCards()
    {
        cards = cards
            .Where(card => card != null)
            .Distinct()
            .ToList();

        currentCardIndex = 0;
        gazeTimer = 0f;

        for (int index = 0; index < cards.Count; index++)
        {
            // Las imágenes no deben empujar al jugador: la 4.ª está a 0,31 m de la pared y su collider sólido dejaba la
            // cápsula del jugador atrapada entre imagen y pared, y la física la sacaba de la oficina. Como trigger el rayo
            // de las métricas de mirada la sigue detectando (Queries Hit Triggers activado) y la mirada del flujo usa el
            // dibujo (Renderer), no el collider.
            foreach (Collider cardCollider in cards[index].GetComponentsInChildren<Collider>(true))
            {
                cardCollider.isTrigger = true;
            }

            cards[index].SetActive(index == 0);
        }
    }

    private void CompleteCurrentCard()
    {
        if (currentCardIndex < cards.Count && cards[currentCardIndex] != null)
        {
            cards[currentCardIndex].SetActive(false);
        }

        currentCardIndex++;
        gazeTimer = 0f;
        lastValidLookTime = float.NegativeInfinity;

        if (currentCardIndex < cards.Count)
        {
            GameObject nextCard = cards[currentCardIndex];
            if (nextCard != null)
            {
                nextCard.SetActive(true);
            }

            UpdateProgressUI(false);
            return;
        }

        BeginCompletion();
    }

    private void BeginCompletion()
    {
        if (completionStarted)
        {
            return;
        }

        completionStarted = true;
        sequenceFinished = true;

        foreach (GameObject card in cards)
        {
            if (card != null)
            {
                card.SetActive(false);
            }
        }

        // El HUD de progreso ya no aplica: se oculta para que no tape el menú de fin de nivel.
        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(false);
        }

        LevelHud.SetGaze(0f, false);
        GlobalSessionUI.CompleteLevel(1);
    }

    private IEnumerator CompletionCountdown()
    {
        float remaining = completionDelaySeconds;

        while (remaining > 0f)
        {
            if (completionText != null)
            {
                completionText.text =
                    "¡Felicitaciones!\n\n" +
                    "Completaste el Nivel 1\n\n" +
                    $"El Nivel 2 comenzará en {Mathf.CeilToInt(remaining)} segundos.";
            }

            yield return null;
            remaining -= Time.unscaledDeltaTime;
        }

        if (completionText != null)
        {
            completionText.text =
                "¡Felicitaciones!\n\n" +
                "Completaste el Nivel 1\n\n" +
                "Cargando Nivel 2...";
        }

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName);
        if (loadOperation == null)
        {
            Debug.LogError(
                $"No fue posible cargar la escena '{nextSceneName}'. " +
                "Verifica que esté incluida en Build Profiles/Build Settings.");
        }
    }

    private bool IsLookingAtCard(GameObject card)
    {
        if (card == null || !card.activeInHierarchy || playerCamera == null)
        {
            return false;
        }

        Vector3 targetPoint = GetCardTargetPoint(card);
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPoint);

        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        Vector2 offsetFromCenter = new Vector2(
            viewportPoint.x - 0.5f,
            viewportPoint.y - 0.5f);

        return offsetFromCenter.magnitude <= viewportRadius;
    }

    private static Vector3 GetCardTargetPoint(GameObject card)
    {
        Renderer[] renderers = card.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return card.transform.position;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            combinedBounds.Encapsulate(renderers[index].bounds);
        }

        return combinedBounds.center;
    }

    private void FindPlayerCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            playerCamera = Camera.main;
            return;
        }

        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        playerCamera = cameras.FirstOrDefault(cameraComponent =>
            cameraComponent != null &&
            cameraComponent.isActiveAndEnabled);
    }

    private void UpdateProgressUI(bool isLooking)
    {
        int totalCards = cards != null ? cards.Count : 0;
        int visibleNumber = totalCards == 0
            ? 0
            : Mathf.Clamp(currentCardIndex + 1, 1, totalCards);

        float normalizedProgress = secondsPerCard <= 0f
            ? 0f
            : Mathf.Clamp01(gazeTimer / secondsPerCard);

        if (progressFill != null)
        {
            progressFill.fillAmount = normalizedProgress;
        }

        PublishToHud(totalCards, visibleNumber, normalizedProgress, isLooking);

        if (progressText == null)
        {
            return;
        }

        if (totalCards == 0)
        {
            progressText.text = "No se encontraron los cuadros del Nivel 1.";
            return;
        }

        string instruction = isLooking
            ? "Mantén la mirada"
            : "Mira el centro de la imagen";

        progressText.text =
            $"Imagen {visibleNumber} de {totalCards}\n" +
            $"{instruction}: {gazeTimer:0.0} / {secondsPerCard:0} s";
    }

    // Mission and gaze progress on the shared HUD: the wrist panel shows which image is next, and the ring near
    // the centre of the view fills while the look is being counted.
    private void PublishToHud(int totalCards, int visibleNumber, float normalizedProgress, bool isLooking)
    {
        if (sequenceFinished)
        {
            return;
        }

        string mission = totalCards == 0
            ? "No se encontraron las imágenes del nivel."
            : $"Mira el centro de cada imagen durante {secondsPerCard:0} s.
Imagen {visibleNumber} de {totalCards}";

        float overall = totalCards == 0 ? 0f : (currentCardIndex + normalizedProgress) / totalCards;
        LevelHud.SetMission("NIVEL 1 · OBSERVACIÓN", mission, overall);
        LevelHud.SetGaze(normalizedProgress, isLooking || normalizedProgress > 0f);
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        RestoreTimeScale();
    }

    private void RestoreTimeScale()
    {
        if (pauseGameplayDuringCompletion && Time.timeScale == 0f)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }
    }
}
