using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NeuroPhobia.Level45
{
    [DisallowMultipleComponent]
    public sealed class NPLevel45RuntimeFix : MonoBehaviour
    {
        [Header("Jugador")]
        public Camera preferredCamera;
        public bool forceDesktopInsideEditor = true;
        public float desktopMoveSpeed = 3.2f;

        [Header("Diagnóstico")]
        public string configuredLevel = "Nivel 4 o 5";

        private bool desktopConfigured;
        private Camera activeDesktopCamera;

        private void Awake()
        {
            ApplyMode();
        }

        private void Start()
        {
            ApplyMode();
        }

        private void Update()
        {
            bool desktop = IsDesktopMode();

            if (desktop && !desktopConfigured)
            {
                ConfigureDesktopPlayer();
            }

            if (!desktop)
            {
                desktopConfigured = false;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyMode();
            }
        }

        private void ApplyMode()
        {
            if (IsDesktopMode())
            {
                ConfigureDesktopPlayer();
                ConfigureInputModule(false);
            }
            else
            {
                ConfigureInputModule(true);
            }
        }

        private bool IsDesktopMode()
        {
            if (Application.isEditor &&
                forceDesktopInsideEditor)
            {
                return true;
            }

            return !XRSettings.isDeviceActive;
        }

        private void ConfigureDesktopPlayer()
        {
            Camera cameraComponent =
                FindDesktopCamera();

            if (cameraComponent == null)
            {
                Debug.LogWarning(
                    "NeuroPhobia: no se encontró una cámara " +
                    "para activar el movimiento de escritorio en " +
                    configuredLevel + ".");

                return;
            }

            ActivateHierarchy(cameraComponent.transform);

            foreach (Camera otherCamera in
                     FindObjectsByType<Camera>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (otherCamera == null ||
                    otherCamera == cameraComponent)
                {
                    continue;
                }

                if (otherCamera.enabled)
                {
                    otherCamera.enabled = false;
                }

                AudioListener otherListener =
                    otherCamera.GetComponent<AudioListener>();

                if (otherListener != null)
                {
                    otherListener.enabled = false;
                }
            }

            cameraComponent.enabled = true;

            try
            {
                cameraComponent.gameObject.tag = "MainCamera";
            }
            catch
            {
                // MainCamera es una etiqueta integrada. El catch evita
                // detener el nivel si el proyecto cambió las etiquetas.
            }

            AudioListener listener =
                cameraComponent.GetComponent<AudioListener>();

            if (listener == null)
            {
                listener =
                    cameraComponent.gameObject
                        .AddComponent<AudioListener>();
            }

            listener.enabled = true;

            Transform playerRoot =
                FindPlayerRoot(cameraComponent.transform);

            ActivateHierarchy(playerRoot);

            CharacterController characterController =
                playerRoot.GetComponent<CharacterController>();

            if (characterController == null)
            {
                characterController =
                    playerRoot.gameObject
                        .AddComponent<CharacterController>();

                characterController.height = 1.75f;
                characterController.radius = 0.28f;
                characterController.center =
                    new Vector3(0f, 0.88f, 0f);
            }

            characterController.enabled = true;

            NPDesktopFallbackPlayer movement =
                playerRoot.GetComponent<
                    NPDesktopFallbackPlayer>();

            if (movement == null)
            {
                movement =
                    playerRoot.gameObject
                        .AddComponent<
                            NPDesktopFallbackPlayer>();
            }

            movement.enabled = true;
            movement.playerCamera = cameraComponent;
            movement.moveSpeed = desktopMoveSpeed;
            movement.allowArrowKeys = true;
            movement.lockCursorOnStart = true;

            activeDesktopCamera = cameraComponent;
            desktopConfigured = true;
        }

        private Camera FindDesktopCamera()
        {
            Camera[] cameras =
                FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            Camera desktopNamed =
                cameras.FirstOrDefault(cameraComponent =>
                    cameraComponent != null &&
                    HierarchyNameContains(
                        cameraComponent.transform,
                        "np_desktopplayer_normalizado"));

            if (desktopNamed != null)
            {
                return desktopNamed;
            }

            desktopNamed =
                cameras.FirstOrDefault(cameraComponent =>
                    cameraComponent != null &&
                    HierarchyNameContains(
                        cameraComponent.transform,
                        "desktop"));

            if (desktopNamed != null)
            {
                return desktopNamed;
            }

            if (preferredCamera != null)
            {
                return preferredCamera;
            }

            Camera tagged = Camera.main;
            if (tagged != null)
            {
                return tagged;
            }

            return cameras.FirstOrDefault(cameraComponent =>
                       cameraComponent != null &&
                       cameraComponent.enabled &&
                       cameraComponent.gameObject
                           .activeInHierarchy) ??
                   cameras.FirstOrDefault();
        }

        private static Transform FindPlayerRoot(
            Transform cameraTransform)
        {
            Transform current = cameraTransform;
            Transform namedCandidate = null;
            Transform controllerCandidate = null;

            while (current != null)
            {
                string lowerName =
                    current.name.ToLowerInvariant();

                if (lowerName.Contains("desktopplayer") ||
                    lowerName.Contains("desktop_player") ||
                    lowerName.Contains("np_desktop"))
                {
                    namedCandidate = current;
                }

                if (current.GetComponent<
                        CharacterController>() != null)
                {
                    controllerCandidate = current;
                }

                current = current.parent;
            }

            if (namedCandidate != null)
            {
                return namedCandidate;
            }

            if (controllerCandidate != null)
            {
                return controllerCandidate;
            }

            return cameraTransform.parent != null
                ? cameraTransform.parent
                : cameraTransform;
        }

        private static bool HierarchyNameContains(
            Transform current,
            string value)
        {
            while (current != null)
            {
                if (current.name.IndexOf(
                        value,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void ActivateHierarchy(
            Transform target)
        {
            if (target == null)
            {
                return;
            }

            Transform[] chain =
                target.GetComponentsInParent<
                    Transform>(true);

            for (int index = chain.Length - 1;
                 index >= 0;
                 index--)
            {
                if (!chain[index].gameObject.activeSelf)
                {
                    chain[index].gameObject.SetActive(true);
                }
            }
        }

        private static void ConfigureInputModule(
            bool xrMode)
        {
            EventSystem eventSystem =
                EventSystem.current;

            if (eventSystem == null)
            {
                eventSystem =
                    FindObjectsByType<EventSystem>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault();
            }

            if (eventSystem == null)
            {
                GameObject eventObject =
                    new GameObject("EventSystem");

                eventSystem =
                    eventObject.AddComponent<EventSystem>();
            }

            eventSystem.gameObject.SetActive(true);
            eventSystem.enabled = true;

            Type xrModuleType =
                Type.GetType(
                    "UnityEngine.XR.Interaction.Toolkit.UI." +
                    "XRUIInputModule, " +
                    "Unity.XR.Interaction.Toolkit");

            Type inputSystemType =
                Type.GetType(
                    "UnityEngine.InputSystem.UI." +
                    "InputSystemUIInputModule, " +
                    "Unity.InputSystem");

            BaseInputModule[] modules =
                eventSystem.GetComponents<
                    BaseInputModule>();

            if (xrMode && xrModuleType != null)
            {
                Component xrModule =
                    modules.FirstOrDefault(module =>
                        module != null &&
                        xrModuleType.IsInstanceOfType(module));

                if (xrModule == null)
                {
                    xrModule =
                        eventSystem.gameObject
                            .AddComponent(xrModuleType);
                }

                EnableModule(xrModule, true);

                foreach (BaseInputModule module in
                         eventSystem.GetComponents<
                             BaseInputModule>())
                {
                    if (module != null &&
                        module != xrModule)
                    {
                        module.enabled = false;
                    }
                }

                return;
            }

            Component desktopModule = null;

            if (inputSystemType != null)
            {
                desktopModule =
                    eventSystem.GetComponents<Component>()
                    .FirstOrDefault(component =>
                        component != null &&
                        inputSystemType
                            .IsInstanceOfType(component));

                if (desktopModule == null)
                {
                    desktopModule =
                        eventSystem.gameObject
                            .AddComponent(inputSystemType);
                }
            }

            if (desktopModule == null)
            {
                StandaloneInputModule standalone =
                    eventSystem.GetComponent<
                        StandaloneInputModule>();

                if (standalone == null)
                {
                    standalone =
                        eventSystem.gameObject
                            .AddComponent<
                                StandaloneInputModule>();
                }

                desktopModule = standalone;
            }

            foreach (BaseInputModule module in
                     eventSystem.GetComponents<
                         BaseInputModule>())
            {
                module.enabled =
                    module == desktopModule;
            }

            EnableModule(desktopModule, true);
        }

        private static void EnableModule(
            Component component,
            bool enabledValue)
        {
            Behaviour behaviour =
                component as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = enabledValue;
            }
        }
    }

    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class NPAdaptiveHUDCanvas : MonoBehaviour
    {
        public Camera preferredCamera;
        public Vector2 referenceResolution =
            new Vector2(960f, 540f);
        public float matchWidthOrHeight = 0.45f;

        private Canvas targetCanvas;
        private CanvasScaler scaler;
        private bool lastXRState;

        private void Awake()
        {
            targetCanvas = GetComponent<Canvas>();
            scaler = GetComponent<CanvasScaler>();

            if (scaler == null)
            {
                scaler =
                    gameObject.AddComponent<
                        CanvasScaler>();
            }

            Apply(true);
        }

        private void LateUpdate()
        {
            bool xrState = XRSettings.isDeviceActive;

            if (xrState != lastXRState ||
                (xrState &&
                 targetCanvas.worldCamera == null))
            {
                Apply(false);
            }
        }

        private void Apply(bool force)
        {
            if (targetCanvas == null)
            {
                return;
            }

            bool xrState = XRSettings.isDeviceActive;
            lastXRState = xrState;

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode
                    .ScaleWithScreenSize;

            scaler.referenceResolution =
                referenceResolution;

            scaler.matchWidthOrHeight =
                matchWidthOrHeight;

            targetCanvas.pixelPerfect = true;
            targetCanvas.sortingOrder = 500;

            if (xrState)
            {
                // En el visor, el HUD va en el mundo, lejos y siguiendo la vista con retardo (antes, pegado a 0,72 m).
                NeuroPhobiaHudFollow.AttachFullScreen(targetCanvas);
            }
            else
            {
                targetCanvas.renderMode =
                    RenderMode.ScreenSpaceOverlay;

                targetCanvas.worldCamera = null;
            }
        }
    }
}
