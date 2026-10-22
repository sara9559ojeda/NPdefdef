#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Level1SequentialGazeInstaller
{
    private const string SetupName = "NP_Nivel1_SecuenciaMirada";
    private const string CanvasName = "NP_Nivel1_UI_Secuencia";

    [MenuItem("Tools/NeuroPhobia/Configurar cuadros 10 segundos Nivel 1")]
    public static void ConfigureLevel1Sequence()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "NeuroPhobia",
                "Detén el modo Play antes de ejecutar este comando.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "NeuroPhobia",
                "No hay una escena abierta.",
                "Aceptar");
            return;
        }

        List<GameObject> cards = new List<GameObject>();
        List<string> missingCards = new List<string>();

        for (int index = 1; index <= 4; index++)
        {
            string expectedName = $"Spider2D_Card_{index}";
            GameObject card = FindInScene(scene, expectedName);

            if (card == null)
            {
                missingCards.Add(expectedName);
            }
            else
            {
                cards.Add(card);
            }
        }

        if (missingCards.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "NeuroPhobia",
                "No se pudo configurar la secuencia porque faltan estos objetos:\n\n" +
                string.Join("\n", missingCards) +
                "\n\nNo cambies los nombres de los cuatro cuadros.",
                "Aceptar");
            return;
        }

        DisableOldLevel1Controller(scene);
        HideOldContinueButton(scene);

        GameObject setup = FindRoot(scene, SetupName);
        if (setup == null)
        {
            setup = new GameObject(SetupName);
            Undo.RegisterCreatedObjectUndo(setup, "Crear secuencia Nivel 1");
            SceneManager.MoveGameObjectToScene(setup, scene);
        }

        Level1SequentialGazeFlow flow =
            setup.GetComponent<Level1SequentialGazeFlow>();

        if (flow == null)
        {
            flow = Undo.AddComponent<Level1SequentialGazeFlow>(setup);
        }

        Canvas canvas = CreateOrResetCanvas(scene);
        TMP_Text progressText = CreateProgressUI(canvas.transform, out Image progressFill);
        GameObject completionPanel = CreateCompletionPanel(
            canvas.transform,
            out TMP_Text completionText);

        SerializedObject serializedFlow = new SerializedObject(flow);
        SetObjectArray(serializedFlow.FindProperty("cards"), cards);
        serializedFlow.FindProperty("playerCamera").objectReferenceValue = FindPreferredCamera(scene);
        serializedFlow.FindProperty("progressText").objectReferenceValue = progressText;
        serializedFlow.FindProperty("progressFill").objectReferenceValue = progressFill;
        serializedFlow.FindProperty("completionPanel").objectReferenceValue = completionPanel;
        serializedFlow.FindProperty("completionText").objectReferenceValue = completionText;
        serializedFlow.FindProperty("secondsPerCard").floatValue = 10f;
        serializedFlow.FindProperty("viewportRadius").floatValue = 0.13f;
        serializedFlow.FindProperty("lookAwayGraceSeconds").floatValue = 0.25f;
        serializedFlow.FindProperty("completionDelaySeconds").floatValue = 5f;
        serializedFlow.FindProperty("nextSceneName").stringValue = "Nivel2";
        serializedFlow.FindProperty("pauseGameplayDuringCompletion").boolValue = true;
        serializedFlow.ApplyModifiedPropertiesWithoutUndo();

        for (int index = 0; index < cards.Count; index++)
        {
            cards[index].SetActive(index == 0);
            EditorUtility.SetDirty(cards[index]);
        }

        completionPanel.SetActive(false);

        EditorUtility.SetDirty(flow);
        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = setup;

        EditorUtility.DisplayDialog(
            "NeuroPhobia",
            "Secuencia configurada correctamente.\n\n" +
            "Cada cuadro requiere 10 segundos de mirada continua.\n" +
            "Después del cuarto cuadro aparecerá una pantalla completa.\n" +
            "Cinco segundos después se cargará Nivel2.\n\n" +
            "Las posiciones y rotaciones que organizaste no fueron modificadas.",
            "Aceptar");
    }

    private static void DisableOldLevel1Controller(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour.GetType().Name == "Level1ExposureController")
                {
                    behaviour.enabled = false;
                    EditorUtility.SetDirty(behaviour);
                }
            }
        }
    }

    private static void HideOldContinueButton(Scene scene)
    {
        GameObject oldButton = FindInScene(scene, "Button_Continuar_Nivel2");
        if (oldButton != null)
        {
            oldButton.SetActive(false);
            EditorUtility.SetDirty(oldButton);
        }
    }

    private static Canvas CreateOrResetCanvas(Scene scene)
    {
        GameObject existing = FindInScene(scene, CanvasName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject canvasObject = new GameObject(
            CanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Undo.RegisterCreatedObjectUndo(canvasObject, "Crear UI Nivel 1");
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static TMP_Text CreateProgressUI(
        Transform canvasTransform,
        out Image progressFill)
    {
        GameObject panel = CreateUIObject(
            "Panel_Progreso",
            canvasTransform);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(30f, 30f);
        panelRect.sizeDelta = new Vector2(620f, 150f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.03f, 0.05f, 0.09f, 0.88f);

        GameObject textObject = CreateUIObject(
            "Texto_Progreso",
            panel.transform);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.38f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(25f, 5f);
        textRect.offsetMax = new Vector2(-25f, -10f);

        TextMeshProUGUI progressText =
            textObject.AddComponent<TextMeshProUGUI>();
        progressText.text = "Imagen 1 de 4\nMira el centro de la imagen: 0.0 / 10 s";
        progressText.fontSize = 31f;
        progressText.alignment = TextAlignmentOptions.Center;
        progressText.color = Color.white;
        progressText.enableWordWrapping = true;
        AssignDefaultFont(progressText);

        GameObject barBackground = CreateUIObject(
            "Barra_Fondo",
            panel.transform);

        RectTransform backgroundRect =
            barBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0f);
        backgroundRect.anchorMax = new Vector2(1f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0f);
        backgroundRect.anchoredPosition = new Vector2(0f, 18f);
        backgroundRect.sizeDelta = new Vector2(-50f, 22f);

        Image backgroundImage = barBackground.AddComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.22f);

        GameObject fillObject = CreateUIObject(
            "Barra_Progreso",
            barBackground.transform);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        progressFill = fillObject.AddComponent<Image>();
        progressFill.color = new Color(0.25f, 0.82f, 0.65f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;

        return progressText;
    }

    private static GameObject CreateCompletionPanel(
        Transform canvasTransform,
        out TMP_Text completionText)
    {
        GameObject panel = CreateUIObject(
            "Panel_Nivel1_Completado",
            canvasTransform);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.02f, 0.035f, 0.065f, 0.97f);

        GameObject card = CreateUIObject(
            "Tarjeta_Final",
            panel.transform);

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(980f, 520f);

        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.09f, 0.14f, 0.22f, 1f);

        GameObject titleObject = CreateUIObject(
            "Texto_Felicitaciones",
            card.transform);

        RectTransform titleRect =
            titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(70f, 60f);
        titleRect.offsetMax = new Vector2(-70f, -60f);

        TextMeshProUGUI text =
            titleObject.AddComponent<TextMeshProUGUI>();
        text.text =
            "¡Felicitaciones!\n\n" +
            "Completaste el Nivel 1\n\n" +
            "El Nivel 2 comenzará en 5 segundos.";
        text.fontSize = 54f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = true;
        AssignDefaultFont(text);

        completionText = text;
        panel.SetActive(false);
        return panel;
    }

    private static GameObject CreateUIObject(
        string name,
        Transform parent)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform));

        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void AssignDefaultFont(TMP_Text text)
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
    }

    private static Camera FindPreferredCamera(Scene scene)
    {
        Camera[] cameras = scene.GetRootGameObjects()
            .SelectMany(root =>
                root.GetComponentsInChildren<Camera>(true))
            .ToArray();

        Camera activeCamera = cameras.FirstOrDefault(cameraComponent =>
            cameraComponent != null &&
            cameraComponent.isActiveAndEnabled &&
            cameraComponent.CompareTag("MainCamera"));

        if (activeCamera != null)
        {
            return activeCamera;
        }

        activeCamera = cameras.FirstOrDefault(cameraComponent =>
            cameraComponent != null &&
            cameraComponent.isActiveAndEnabled);

        return activeCamera ?? cameras.FirstOrDefault();
    }

    private static void SetObjectArray(
        SerializedProperty arrayProperty,
        List<GameObject> values)
    {
        arrayProperty.arraySize = values.Count;

        for (int index = 0; index < values.Count; index++)
        {
            arrayProperty
                .GetArrayElementAtIndex(index)
                .objectReferenceValue = values[index];
        }
    }

    private static GameObject FindRoot(
        Scene scene,
        string exactName)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(root =>
                string.Equals(
                    root.name,
                    exactName,
                    StringComparison.Ordinal));
    }

    private static GameObject FindInScene(
        Scene scene,
        string exactName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform result = FindRecursive(
                root.transform,
                exactName);

            if (result != null)
            {
                return result.gameObject;
            }
        }

        return null;
    }

    private static Transform FindRecursive(
        Transform current,
        string exactName)
    {
        if (string.Equals(
            current.name,
            exactName,
            StringComparison.Ordinal))
        {
            return current;
        }

        foreach (Transform child in current)
        {
            Transform result = FindRecursive(
                child,
                exactName);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
#endif
