using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Permite sentarse y pararse en Nivel2 con el botón A del mando derecho (C en teclado), pero solo
/// junto a un sillón de la sala (los objetos cuyo nombre contiene «sofa»); lejos de ellos no hace nada.
/// Sentado baja la vista, con una transición suave, hasta una altura de ojos de silla y bloquea el
/// movimiento con el joystick; al pararse restaura ambas cosas. Se crea solo en Nivel2.
/// </summary>
public sealed class Level2SitToggle : MonoBehaviour
{
    private const float SeatedEyeHeight = 1.1f;
    private const float MaxDrop = 0.9f;
    private const float SeatReach = 1.2f;
    private const float TransitionSeconds = 0.4f;

    private XROrigin origin;
    private Transform cameraOffset;
    private Vector3 standingOffset;
    private InputAction toggleAction;
    private bool seated;
    private Vector3 targetOffset;
    private bool transitioning;
    private readonly System.Collections.Generic.List<Renderer> sofas = new System.Collections.Generic.List<Renderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Nivel2") return;
        new GameObject("NP_Level2_SitToggle").AddComponent<Level2SitToggle>();
    }

    private void OnEnable()
    {
        toggleAction = new InputAction("NP_Level2_Sit", InputActionType.Button);
        toggleAction.AddBinding("<XRController>{RightHand}/primaryButton");
        toggleAction.AddBinding("<Keyboard>/c");
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        if (toggleAction != null)
        {
            toggleAction.Disable();
            toggleAction.Dispose();
            toggleAction = null;
        }

        StandUp();
    }

    private void Update()
    {
        if (origin == null)
        {
            origin = FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.CameraFloorOffsetObject == null) { origin = null; return; }
            cameraOffset = origin.CameraFloorOffsetObject.transform;
            standingOffset = cameraOffset.localPosition;
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.name.ToLowerInvariant().Contains("sofa")) sofas.Add(renderer);
            }
        }

        // Transición suave de la altura de ojos (antes era un salto de golpe). Fuera de una transición no se
        // toca el offset, para no pelear con el modo de seguimiento de piso del rig.
        if (transitioning)
        {
            cameraOffset.localPosition = Vector3.MoveTowards(
                cameraOffset.localPosition, targetOffset, MaxDrop / TransitionSeconds * Time.unscaledDeltaTime);
            transitioning = cameraOffset.localPosition != targetOffset;
        }

        if (toggleAction.WasPressedThisFrame())
        {
            if (seated) StandUp();
            else SitDown();
        }
    }

    // Verdadero si la cabeza está a menos de SeatReach (en planta) de algún sillón.
    private bool NearSofa()
    {
        Vector3 head = origin.Camera.transform.position;
        foreach (Renderer sofa in sofas)
        {
            if (sofa == null) continue;
            Bounds bounds = sofa.bounds;
            bounds.Expand(new Vector3(SeatReach * 2f, 0f, SeatReach * 2f));
            Vector3 flat = new Vector3(head.x, bounds.center.y, head.z);
            if (bounds.Contains(flat)) return true;
        }
        return false;
    }

    private void SitDown()
    {
        if (seated || cameraOffset == null || origin.Camera == null || !NearSofa()) return;

        // Altura actual de la cabeza sobre el piso del rig, para bajar solo lo necesario.
        float head = origin.CameraInOriginSpacePos.y;
        float drop = Mathf.Clamp(head - SeatedEyeHeight, 0f, MaxDrop);

        // El offset de pie se toma ahora, no al iniciar: al arrancar aún puede traer la altura de ojos
        // por defecto del prefab (1.36 × escala del rig) y al pararse el jugador quedaba "volando".
        standingOffset = cameraOffset.localPosition;
        Vector3 position = standingOffset;
        position.y -= drop;
        targetOffset = position;
        transitioning = true;

        seated = true;
        Level1ThumbstickMove.MovementLocked = true;
    }

    private void StandUp()
    {
        if (!seated) return;

        targetOffset = standingOffset;
        transitioning = true;
        seated = false;
        Level1ThumbstickMove.MovementLocked = false;
    }
}
