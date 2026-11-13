using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;

/// <summary>
/// Permite sentarse y pararse en Nivel2 con el botón A del mando derecho (C en teclado), pero solo
/// dentro del trigger de un sillón de la sala (una caja invisible alrededor de cada objeto cuyo nombre
/// contiene «sofa»); fuera de ellos no hace nada. En Nivel2 el botón A ya no salta: el salto del rig se apaga.
/// Sentado baja la vista, con una transición suave, hasta una altura de ojos de silla y bloquea el
/// movimiento con el joystick; al pararse restaura ambas cosas. Se crea solo en Nivel2.
/// </summary>
public sealed class Level2SitToggle : MonoBehaviour
{
    private const float SeatedEyeHeight = 1.1f;
    private const float MaxDrop = 0.9f;
    // Margen (m) del trigger alrededor del sillón, en planta.
    private const float TriggerMargin = 0.6f;
    // Alto (m) del trigger con el rig a escala 1 (se multiplica por la escala del rig): cubre la cabeza de pie.
    private const float TriggerHeight = 2.6f;
    private const float TransitionSeconds = 0.4f;
    private const int IgnoreRaycastLayer = 2;

    private XROrigin origin;
    private Transform cameraOffset;
    private Vector3 standingOffset;
    private InputAction toggleAction;
    private bool seated;
    private Vector3 targetOffset;
    private bool transitioning;
    private readonly List<BoxCollider> seatTriggers = new List<BoxCollider>();

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
            DisableJump(origin);
            BuildSeatTriggers();
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

    // En Nivel2 el botón A es para sentarse: el salto del rig (también en A) se apaga.
    private static void DisableJump(XROrigin rig)
    {
        foreach (JumpProvider jump in rig.GetComponentsInChildren<JumpProvider>(true))
        {
            jump.enabled = false;
        }
    }

    // Un trigger (caja invisible) por sillón. Las mallas del sillón se llaman «Plane.010», «cloth.015»…; el nombre
    // con «sofa» lo lleva el objeto raíz del mueble, así que se busca ahí y se abarcan todas sus mallas.
    private void BuildSeatTriggers()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsSofaRoot(t)) continue;
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            // El rig de Nivel2 está escalado (×2,6): la cabeza de pie queda a más de 3 m, así que el alto acompaña la escala.
            float height = TriggerHeight * Mathf.Max(1f, origin.transform.lossyScale.y);
            GameObject zone = new GameObject("NP_Sillon_Trigger_" + t.name);
            zone.layer = IgnoreRaycastLayer;   // no tapa el rayo de los mandos ni la mirada
            zone.transform.position = new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(bounds.size.x + TriggerMargin * 2f, height, bounds.size.z + TriggerMargin * 2f);
            seatTriggers.Add(box);
        }
    }

    private static bool IsSofaRoot(Transform t)
    {
        if (!t.name.ToLowerInvariant().Contains("sofa")) return false;
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.name.ToLowerInvariant().Contains("sofa")) return false;
        return true;
    }

    // Verdadero si la cabeza está dentro del trigger de algún sillón.
    private bool InSeatTrigger()
    {
        Vector3 head = origin.Camera.transform.position;
        foreach (BoxCollider trigger in seatTriggers)
        {
            if (trigger != null && trigger.bounds.Contains(head)) return true;
        }
        return false;
    }

    private void SitDown()
    {
        if (seated || cameraOffset == null || origin.Camera == null || !InSeatTrigger()) return;

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
