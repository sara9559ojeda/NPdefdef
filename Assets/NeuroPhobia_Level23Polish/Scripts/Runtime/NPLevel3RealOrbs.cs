using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Único controlador del progreso del Nivel 3 (aproximación progresiva).
/// Misión: agarrar los orbes en orden. Un orbe se agarra con el mando (grip, o gatillo mientras se apunta o se toca)
/// estando la cabeza a menos de <see cref="Level3Settings.reachDistance"/>. Mirarlo, o agarrarlo de lejos con el rayo,
/// no cuenta. El nivel solo termina al agarrar el último orbe. Otros controladores antiguos del nivel se desactivan al empezar.
/// </summary>
public class NPLevel3RealOrbs : MonoBehaviour
{
    public GameObject desktopPlayer;
    public GameObject xrOrigin;
    public GameObject orb1;
    public GameObject orb2;
    [Tooltip("Heredado: ya no se usa. La distancia sale de Level3Settings.reachDistance.")]
    public float collectDistance = 1.6f;
    public float completionDelay = 5f;
    public string nextScene = "Nivel4";
    public AudioClip collectClip;

    public const string MissionText = "Misión: agarra los orbes en orden. Acércate a cada orbe y agárralo con el mando.";

    private GameObject[] orbs;
    private int count;
    private bool done;
    private Level3Settings settings;
    private string hintMessage = "Camina hacia el orbe luminoso.";

    public int ReachedOrbs => count;
    public int TotalOrbs => orbs != null ? orbs.Length : 0;
    public bool IsComplete => done;
    public GameObject CurrentOrb => orbs != null && count < orbs.Length ? orbs[count] : null;

    private void Awake()
    {
        DisableCompetingControllers();
    }

    private void Start()
    {
        settings = Level3Settings.Load();
        orbs = new[] { orb1, orb2 };
        for (int i = 0; i < orbs.Length; i++)
        {
            if (orbs[i] == null) continue;
            PrepareGrab(orbs[i]);
            orbs[i].SetActive(i == 0);
        }
        UpdateHUD();
    }

    // Un solo controlador decide el fin del Nivel 3: los demás quedan desactivados (no se borran).
    private void DisableCompetingControllers()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == this) continue;
            string type = behaviour.GetType().Name;
            if (type == "Level3ProgressControllerV2" || type == "Level3ProgressController" ||
                type == "Level3OrbSequenceFlow" || type == "NeuroPhobiaLevel3OrbCompatibility" ||
                type == "Level3Checkpoint" || type == "Level3CheckpointV2" || type == "LevelCompletionTrigger")
            {
                behaviour.enabled = false;
            }
        }
    }

    // El orbe se puede agarrar con el mando: el rayo y la mano de XRI no detectan colliders «trigger», así que el suyo
    // pasa a sólido, pero el cuerpo del jugador lo ignora (no tropieza con él).
    private void PrepareGrab(GameObject orb)
    {
        Collider collider = orb.GetComponent<Collider>();
        if (collider == null) collider = orb.AddComponent<SphereCollider>();
        collider.isTrigger = false;
        foreach (CharacterController body in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Physics.IgnoreCollision(collider, body);

        XRSimpleInteractable interactable = orb.GetComponent<XRSimpleInteractable>();
        if (interactable == null) interactable = orb.AddComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(_ => TryGrab(orb));
        // Button badge and a short vibration while a controller points at or touches the orb.
        interactable.hoverEntered.AddListener(args =>
        {
            InteractionPrompt.Show(orb, orb.transform.position + Vector3.up * 0.45f, "Grip o gatillo");
            ControllerHaptics.Pulse(ControllerHaptics.HandOf(args.interactorObject.transform));
        });
        interactable.hoverExited.AddListener(_ => InteractionPrompt.Hide(orb));
    }

    private void Update()
    {
        if (done || GameStateManager.CurrentState != GameState.PLAYING) return;
        GameObject target = CurrentOrb;
        if (target == null) return;
        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        if (head == null) return;

        // Gatillo mientras el mando apunta o toca el orbe: también lo agarra (el grip llega por selectEntered).
        if (TriggerPressedOn(target)) TryGrab(target);
        if (done || CurrentOrb != target) return;

        SetHint(Hint(head.transform, target.transform.position));
    }

    private void SetHint(string message)
    {
        if (hintMessage == message) return;
        hintMessage = message;
        UpdateHUD();
    }

    /// <summary>Agarra el orbe si es el que toca y la cabeza está lo bastante cerca. Devuelve true si cuenta.</summary>
    public bool TryGrab(GameObject orb)
    {
        if (done || orb == null || orb != CurrentOrb || GameStateManager.CurrentState != GameState.PLAYING) return false;
        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        if (head == null || !IsWithinReach(head.transform, orb.transform.position, settings)) return false;
        Reach(orb);
        return true;
    }

    /// <summary>Cabeza a menos de la distancia de alcance del orbe.</summary>
    public static bool IsWithinReach(Transform head, Vector3 orb, Level3Settings settings)
    {
        return Vector3.Distance(head.position, orb) <= settings.reachDistance;
    }

    private static bool TriggerPressedOn(GameObject orb)
    {
        XRSimpleInteractable interactable = orb.GetComponent<XRSimpleInteractable>();
        if (interactable == null || !interactable.isHovered) return false;
        foreach (var hovering in interactable.interactorsHovering)
        {
            if (hovering is XRBaseInputInteractor input && input.activateInput != null && input.activateInput.ReadWasPerformedThisFrame())
                return true;
        }
        return false;
    }

    private string Hint(Transform head, Vector3 orb)
    {
        float distance = Vector3.Distance(head.position, orb);
        return distance > settings.reachDistance
            ? "Camina hacia el orbe luminoso (" + distance.ToString("0") + " m)."
            : "Agarra el orbe con el grip o el gatillo.";
    }

    private void Reach(GameObject target)
    {
        InteractionPrompt.Hide(target);
        target.SetActive(false);
        count++;
        if (collectClip != null) AudioSource.PlayClipAtPoint(collectClip, target.transform.position);

        if (count < orbs.Length)
        {
            if (orbs[count] != null) orbs[count].SetActive(true);
            hintMessage = "Bien. Busca el siguiente orbe.";
            UpdateHUD();
            return;
        }

        UpdateHUD();
        StartCoroutine(Finish());
    }

    // Mission, progress and the current hint on the shared wrist HUD (LevelHud).
    private void UpdateHUD()
    {
        int total = TotalOrbs;
        string body = MissionText + "\nOrbes agarrados: " + count + " / " + total + "\n" + hintMessage;
        LevelHud.SetMission("NIVEL 3 · APROXIMACIÓN", body, total > 0 ? count / (float)total : 0f);
    }

    private IEnumerator Finish()
    {
        done = true;
        hintMessage = "Misión cumplida.";
        UpdateHUD();
        GlobalSessionUI.CompleteLevel(3);
        yield break;
    }
}
