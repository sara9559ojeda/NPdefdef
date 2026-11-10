using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NeuroPhobia.Level45
{
    public sealed class NPLevel4Controller : MonoBehaviour
    {
        [Header("Exposición")]
        public Transform spiderTarget;
        public NPNaturalSpiderPatrol spiderPatrol;
        public Transform[] exposurePoints;
        public float activationRadius = 3.0f;
        public float viewportLookRadius = 0.20f;
        public float lostLookTolerance = 1.0f;
        public float secondsPerPoint = 10f;

        [Header("Interfaz fija")]
        public TMP_Text titleText;
        public TMP_Text objectiveText;
        public TMP_Text timerText;
        public TMP_Text bestText;
        public TMP_Text promptText;
        public Image progressFill;

        [Header("Final")]
        public GameObject completionPanel;
        public TMP_Text completionTitle;
        public TMP_Text completionSubtitle;
        public TMP_Text countdownText;
        public string nextSceneName = "Nivel5";
        public float transitionDelay = 5f;

        [Header("Audio")]
        public AudioSource uiAudio;
        public AudioClip checkpointClip;
        public AudioClip completionClip;

        private Camera playerCamera;
        private int currentPoint;
        private float currentLookTime;
        private float bestAttempt;
        private float lostLookTime;
        private bool completing;
        private float completionRemaining;
        private readonly List<Transform> spiderVisuals =
            new List<Transform>();
        private string lookedSpiderName = "Ninguna";
        private float lookAngle;
        private Canvas levelHudCanvas;

        // NUEVO:
        // al entrar al radio del orbe, el punto queda activado.
        // Ya no hay que permanecer encima del orbe durante la mirada.
        private bool pointArmed;

        private void Start()
        {
            FindSpiderVisuals();
            playerCamera = FindActivePlayerCamera();
            ConfigureLevelHud();

            if (titleText != null)
                titleText.text = "NIVEL 4 · EXPOSICIÓN CERCANA";

            if (completionPanel != null)
                completionPanel.SetActive(false);

            pointArmed = false;
            ActivateOnlyCurrentPoint();
            RefreshUI();
        }

        private void Update()
        {
            if (completing)
            {
                UpdateCompletion();
                return;
            }

            Camera activeCamera = FindActivePlayerCamera();
            if (activeCamera != null && activeCamera != playerCamera)
            {
                playerCamera = activeCamera;
                ConfigureLevelHud();
            }
            else if (playerCamera == null ||
                     !playerCamera.gameObject.activeInHierarchy)
            {
                playerCamera = activeCamera;
            }

            if (exposurePoints == null ||
                exposurePoints.Length == 0 ||
                currentPoint >= exposurePoints.Length)
            {
                BeginCompletion();
                return;
            }

            Transform point = exposurePoints[currentPoint];

            if (point == null || playerCamera == null)
            {
                RefreshUI();
                return;
            }

            float distance = HorizontalDistance(
                playerCamera.transform.position,
                point.position);
            bool lookingAtSpider = IsLookingAtAnySpider();

            // FASE 1:
            // basta con entrar una vez en el radio del orbe.
            if (!pointArmed)
            {
                if (distance <= activationRadius)
                {
                    ArmCurrentPoint();
                }

                RefreshUI(distance, false);
                return;
            }

            // FASE 2:
            // ya activado el orbe, la distancia deja de importar.
            // La araña queda detenida y solo cuenta la mirada.
            if (lookingAtSpider)
            {
                currentLookTime += Time.deltaTime;
                lostLookTime = 0f;
                bestAttempt = Mathf.Max(
                    bestAttempt,
                    currentLookTime);

                if (currentLookTime >= secondsPerPoint)
                {
                    CompleteCurrentPoint();
                    return;
                }
            }
            else
            {
                lostLookTime += Time.deltaTime;

                // Se da 1 segundo de tolerancia.
                // Si vuelve a mirar antes, conserva el progreso.
                if (lostLookTime > lostLookTolerance)
                    currentLookTime = 0f;
            }

            RefreshUI(distance, lookingAtSpider);
        }

        private void ArmCurrentPoint()
        {
            if (pointArmed)
                return;

            pointArmed = true;
            currentLookTime = 0f;
            lostLookTime = 0f;
            bestAttempt = 0f;

            if (spiderPatrol != null)
                spiderPatrol.PausePatrol();

            PlayClip(checkpointClip);
        }

        private void FindSpiderVisuals()
        {
            spiderVisuals.Clear();
            if (spiderTarget == null)
                return;

            foreach (Transform child in spiderTarget)
            {
                if (child != null &&
                    (child.name == "Visual_WolfSpider" ||
                     child.name == "Visual_WolfSpider (1)" ||
                     child.name == "Visual_WolfSpider (2)" ||
                     child.name == "Visual_WolfSpider (3)"))
                {
                    spiderVisuals.Add(child);
                }
            }
        }

        private bool IsLookingAtAnySpider()
        {
            lookedSpiderName = "Ninguna";
            lookAngle = 180f;
            bool looking = false;

            foreach (Transform spiderVisual in spiderVisuals)
            {
                if (spiderVisual == null ||
                    !spiderVisual.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 targetCenter =
                    NPLevel45Camera.TargetCenter(spiderVisual);
                Vector3 direction =
                    targetCenter - playerCamera.transform.position;
                float candidateAngle = Vector3.Angle(
                    playerCamera.transform.forward,
                    direction);

                if (candidateAngle < lookAngle)
                    lookAngle = candidateAngle;

                if (!NPLevel45Camera.IsLookingAt(
                        playerCamera,
                        spiderVisual,
                        viewportLookRadius))
                {
                    continue;
                }

                looking = true;
                lookedSpiderName = "Sí";
                lookAngle = candidateAngle;
                break;
            }

            return looking;
        }

        private Camera FindActivePlayerCamera()
        {
            NPDesktopFallbackPlayer desktopPlayer =
                FindFirstObjectByType<NPDesktopFallbackPlayer>();

            if (desktopPlayer != null &&
                desktopPlayer.enabled &&
                desktopPlayer.gameObject.activeInHierarchy &&
                desktopPlayer.playerCamera != null &&
                desktopPlayer.playerCamera.enabled)
            {
                return desktopPlayer.playerCamera;
            }

            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            string preferredName =
                XRSettings.isDeviceActive ? "main camera" : "desktop";

            foreach (Camera cameraComponent in cameras)
            {
                if (cameraComponent != null &&
                    cameraComponent.enabled &&
                    cameraComponent.name.ToLowerInvariant()
                        .Contains(preferredName))
                {
                    return cameraComponent;
                }
            }

            return null;
        }

        private void ConfigureLevelHud()
        {
            if (titleText == null)
                return;

            levelHudCanvas = titleText.canvas;
            if (levelHudCanvas == null)
                return;

            CanvasScaler scaler =
                levelHudCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution =
                    new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            RectTransform canvasRect =
                levelHudCanvas.transform as RectTransform;
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.anchoredPosition = Vector2.zero;
                canvasRect.sizeDelta = Vector2.zero;
            }

            levelHudCanvas.pixelPerfect = true;
            levelHudCanvas.sortingOrder = 500;

            if (promptText != null)
            {
                promptText.enableAutoSizing = true;
                promptText.fontSizeMin = 16f;
                promptText.fontSizeMax = 24f;

                RectTransform promptRect =
                    promptText.rectTransform;
                promptRect.anchorMin = new Vector2(0.5f, 0f);
                promptRect.anchorMax = new Vector2(0.5f, 0f);
                promptRect.anchoredPosition = new Vector2(0f, 35f);
                promptRect.sizeDelta = new Vector2(900f, 230f);
            }

            if (XRSettings.isDeviceActive)
            {
                levelHudCanvas.renderMode =
                    RenderMode.ScreenSpaceCamera;
                levelHudCanvas.worldCamera = playerCamera;
                levelHudCanvas.planeDistance = 0.72f;
            }
            else
            {
                levelHudCanvas.renderMode =
                    RenderMode.ScreenSpaceOverlay;
                levelHudCanvas.worldCamera = null;
            }
        }

        private static float HorizontalDistance(
            Vector3 a,
            Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void CompleteCurrentPoint()
        {
            if (exposurePoints != null &&
                currentPoint < exposurePoints.Length &&
                exposurePoints[currentPoint] != null)
            {
                exposurePoints[currentPoint]
                    .gameObject.SetActive(false);
            }

            // Volver a mover la araña al terminar el punto.
            if (spiderPatrol != null)
                spiderPatrol.ResumePatrol();

            PlayClip(checkpointClip);

            currentPoint++;
            pointArmed = false;
            currentLookTime = 0f;
            lostLookTime = 0f;
            bestAttempt = 0f;

            if (currentPoint >= exposurePoints.Length)
            {
                BeginCompletion();
                return;
            }

            ActivateOnlyCurrentPoint();
            RefreshUI();
        }

        private void ActivateOnlyCurrentPoint()
        {
            if (exposurePoints == null)
                return;

            for (int index = 0;
                 index < exposurePoints.Length;
                 index++)
            {
                if (exposurePoints[index] != null)
                {
                    exposurePoints[index]
                        .gameObject.SetActive(
                            index == currentPoint);
                }
            }
        }

        private void RefreshUI(
            float distance = float.PositiveInfinity,
            bool lookingAtSpider = false)
        {
            int total =
                exposurePoints != null
                    ? exposurePoints.Length
                    : 0;

            int displayedPoint =
                Mathf.Clamp(
                    currentPoint + 1,
                    1,
                    Mathf.Max(total, 1));

            if (objectiveText != null)
            {
                objectiveText.text =
                    "Punto de exposición: " +
                    displayedPoint +
                    " / " +
                    Mathf.Max(total, 1);
            }

            if (timerText != null)
            {
                timerText.text =
                    "Mirada continua: " +
                    currentLookTime.ToString("0.0") +
                    " / " +
                    secondsPerPoint.ToString("0") +
                    " s";
            }

            if (bestText != null)
            {
                bestText.text =
                    "Mejor intento: " +
                    bestAttempt.ToString("0.0") +
                    " s";
            }

            if (progressFill != null)
            {
                progressFill.fillAmount =
                    secondsPerPoint > 0f
                        ? Mathf.Clamp01(
                            currentLookTime /
                            secondsPerPoint)
                        : 0f;
            }

            if (promptText == null)
                return;

            string instruction;

            if (!pointArmed)
            {
                instruction =
                    "Acércate al orbe luminoso\n" +
                    (float.IsInfinity(distance)
                        ? string.Empty
                        : "Distancia: " +
                          distance.ToString("0.0") +
                          " m");
            }
            else if (!lookingAtSpider)
            {
                instruction =
                    "Punto activado · La araña está detenida\n" +
                    "Mira directamente a la araña";
            }
            else
            {
                instruction =
                    "Mantén la mirada · Puedes moverte fuera del orbe";
            }

            int pausedCount = spiderPatrol != null
                ? spiderPatrol.PausedAnimatorCount
                : 0;

            promptText.text = instruction + "\n" +
                "Arañas detectadas: " + spiderVisuals.Count + "\n" +
                "Arañas pausadas: " + pausedCount + "\n" +
                "Araña mirada: " + lookedSpiderName + "\n" +
                "Ángulo de mirada: " + lookAngle.ToString("0.0") +
                "°\n" +
                "Mirada: " + currentLookTime.ToString("0.0") +
                " / " + secondsPerPoint.ToString("0") + " s";
        }

        private void BeginCompletion()
        {
            if (completing)
                return;

            completing = true;
            GlobalSessionUI.CompleteLevel(4);
        }

        private void UpdateCompletion()
        {
            completionRemaining -=
                Time.unscaledDeltaTime;

            UpdateCompletionText();

            if (completionRemaining <= 0f)
            {
                completing = false;

                if (!string.IsNullOrWhiteSpace(
                        nextSceneName))
                {
                    GlobalSessionUI.CompleteLevel(4);
                }
            }
        }

        private void UpdateCompletionText()
        {
            if (countdownText != null)
            {
                countdownText.text =
                    "El Nivel 5 comenzará en " +
                    Mathf.CeilToInt(
                        Mathf.Max(
                            0f,
                            completionRemaining)) +
                    " segundos";
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (uiAudio != null &&
                clip != null)
            {
                uiAudio.PlayOneShot(clip);
            }
        }
    }
}

