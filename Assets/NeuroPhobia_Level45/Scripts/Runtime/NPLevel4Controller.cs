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

        // Entering the orb radius once arms the point; after that only the look is counted and the
        // spiders keep moving naturally.
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

            // Phase 2: once the point is armed the distance no longer matters; only the look is counted.
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

            // Exposure: the spiders closest to this orb stop roaming and walk slowly towards it.
            if (exposurePoints != null && currentPoint < exposurePoints.Length && exposurePoints[currentPoint] != null)
                Level4SpiderApproach.Begin(exposurePoints[currentPoint].position, currentPoint);

            PlayClip(checkpointClip);
        }

        // Every spider of the level counts for the look: the one on the route and the ones that roam on their
        // own (Level4IndependentSpiders gives each of them its own patrol before this runs).
        private void FindSpiderVisuals()
        {
            spiderVisuals.Clear();

            foreach (NPNaturalSpiderPatrol patrol in FindObjectsByType<NPNaturalSpiderPatrol>(FindObjectsSortMode.None))
            {
                if (patrol.visualRoot != null && !spiderVisuals.Contains(patrol.visualRoot))
                    spiderVisuals.Add(patrol.visualRoot);
            }

            if (spiderTarget == null)
                return;

            foreach (Transform child in spiderTarget)
            {
                if (child != null && child.name.StartsWith("Visual_WolfSpider") && !spiderVisuals.Contains(child))
                    spiderVisuals.Add(child);
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

        // The full-screen panel saved in the scene (title, timer and a block of debug text in the middle of the
        // view) is replaced by the shared wrist HUD (LevelHud).
        private void ConfigureLevelHud()
        {
            if (titleText == null)
                return;

            levelHudCanvas = titleText.canvas != null ? titleText.canvas.rootCanvas : null;
            if (levelHudCanvas != null)
                levelHudCanvas.gameObject.SetActive(false);
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
            Level4SpiderApproach.End();
            if (exposurePoints != null &&
                currentPoint < exposurePoints.Length &&
                exposurePoints[currentPoint] != null)
            {
                exposurePoints[currentPoint]
                    .gameObject.SetActive(false);
            }

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

            string instruction;

            if (!pointArmed)
            {
                instruction = float.IsInfinity(distance)
                    ? "Acércate al orbe luminoso."
                    : "Acércate al orbe luminoso (" + distance.ToString("0") + " m).";
            }
            else if (!lookingAtSpider)
            {
                instruction = "Punto activado: las arañas se acercan despacio y se detienen a distancia segura. Mira a una de ellas.";
            }
            else
            {
                instruction = "Mantén la mirada. Puedes moverte.";
            }

            if (promptText != null)
                promptText.text = instruction;

            float lookProgress = secondsPerPoint > 0f
                ? Mathf.Clamp01(currentLookTime / secondsPerPoint)
                : 0f;

            LevelHud.SetMission(
                "NIVEL 4 · EXPOSICIÓN CERCANA",
                "Acércate a cada orbe y mira a una araña " + secondsPerPoint.ToString("0") + " s.\n" +
                "Punto " + displayedPoint + " de " + Mathf.Max(total, 1) + "\n" + instruction,
                total > 0 ? (currentPoint + lookProgress) / total : 0f);
            LevelHud.SetGaze(lookProgress, pointArmed && (lookingAtSpider || currentLookTime > 0f));
        }

        private void BeginCompletion()
        {
            Level4SpiderApproach.End();
            if (completing)
                return;

            completing = true;
            LevelHud.SetGaze(0f, false);
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

