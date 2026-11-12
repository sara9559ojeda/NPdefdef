using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Único controlador del progreso del Nivel 3 (aproximación progresiva).
/// Misión: alcanzar los orbes en orden. Un orbe se alcanza cuando la cabeza está a menos de
/// <see cref="Level3Settings.reachDistance"/> y la persona lo mantiene en la mirada
/// <see cref="Level3Settings.gazeSeconds"/> segundos seguidos. Estar cerca sin mirarlo, o mirarlo de lejos, no cuenta.
/// El nivel solo termina al alcanzar el último orbe. Otros controladores antiguos del nivel se desactivan al empezar.
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

    public const string MissionText = "Misión: alcanza los orbes en orden. Acércate a cada orbe y mantenlo en la mirada.";

    private GameObject[] orbs;
    private int count;
    private bool done;
    private float gazeTimer;
    private Level3Settings settings;
    private TMP_Text progressText;
    private TMP_Text messageText;
    private Image progressFill;
    private Image gazeFill;
    private Canvas hud;

    public int ReachedOrbs => count;
    public int TotalOrbs => orbs != null ? orbs.Length : 0;
    public bool IsComplete => done;
    public GameObject CurrentOrb => orbs != null && count < orbs.Length ? orbs[count] : null;
    public float GazeProgress => settings != null ? Mathf.Clamp01(gazeTimer / Mathf.Max(0.01f, settings.gazeSeconds)) : 0f;

    private void Awake()
    {
        DisableCompetingControllers();
    }

    private void Start()
    {
        settings = Level3Settings.Load();
        orbs = new[] { orb1, orb2 };
        for (int i = 0; i < orbs.Length; i++) if (orbs[i] != null) orbs[i].SetActive(i == 0);
        BuildHUD();
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

    private void Update()
    {
        if (done || GameStateManager.CurrentState != GameState.PLAYING) return;
        GameObject target = CurrentOrb;
        if (target == null) return;
        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        if (head == null) return;

        bool looking = IsReachingAndLooking(head.transform, target.transform.position, settings);
        gazeTimer = looking ? gazeTimer + Time.deltaTime : 0f;
        if (messageText != null && looking) messageText.text = "Mantén la mirada en el orbe…";
        else if (messageText != null) messageText.text = Hint(head.transform, target.transform.position);
        if (gazeFill != null) gazeFill.fillAmount = GazeProgress;

        if (gazeTimer >= settings.gazeSeconds) Reach(target);
    }

    /// <summary>Cabeza cerca del orbe y orbe en el centro de la mirada.</summary>
    public static bool IsReachingAndLooking(Transform head, Vector3 orb, Level3Settings settings)
    {
        Vector3 toOrb = orb - head.position;
        if (toOrb.magnitude > settings.reachDistance) return false;
        return Vector3.Angle(head.forward, toOrb) <= settings.gazeAngle;
    }

    private string Hint(Transform head, Vector3 orb)
    {
        float distance = Vector3.Distance(head.position, orb);
        return distance > settings.reachDistance
            ? "Camina hacia el orbe luminoso (" + distance.ToString("0") + " m)."
            : "Mira el orbe para alcanzarlo.";
    }

    private void Reach(GameObject target)
    {
        target.SetActive(false);
        gazeTimer = 0f;
        count++;
        if (collectClip != null) AudioSource.PlayClipAtPoint(collectClip, target.transform.position);

        if (count < orbs.Length)
        {
            if (orbs[count] != null) orbs[count].SetActive(true);
            if (messageText != null) messageText.text = "Bien. Busca el siguiente orbe.";
            UpdateHUD();
            return;
        }

        UpdateHUD();
        StartCoroutine(Finish());
    }

    private void BuildHUD()
    {
        var root = new GameObject("NP_L3_HUD_Mision", typeof(RectTransform));
        hud = root.AddComponent<Canvas>();
        hud.sortingOrder = 500;
        root.AddComponent<CanvasScaler>();

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        Image background = panel.GetComponent<Image>();
        background.color = new Color(.02f, .035f, .05f, .92f);
        background.raycastTarget = false;

        var title = MakeText(panel.transform, "NIVEL 3 · APROXIMACIÓN PROGRESIVA", 30, FontStyles.Bold);
        SetRect(title.rectTransform, .04f, .80f, .96f, .97f);
        var mission = MakeText(panel.transform, MissionText, 22, FontStyles.Normal);
        SetRect(mission.rectTransform, .04f, .56f, .96f, .80f);
        progressText = MakeText(panel.transform, "", 26, FontStyles.Bold);
        SetRect(progressText.rectTransform, .04f, .40f, .96f, .56f);
        messageText = MakeText(panel.transform, "Camina hacia el orbe luminoso.", 22, FontStyles.Normal);
        SetRect(messageText.rectTransform, .04f, .24f, .96f, .40f);

        progressFill = Bar(panel.transform, "Barra", .07f, .13f, .93f, .19f, new Color(.1f, .8f, .72f, 1));
        gazeFill = Bar(panel.transform, "Mirada", .07f, .05f, .93f, .09f, new Color(.95f, .78f, .35f, 1));

        NeuroPhobiaHudFollow.Attach(hud, new Vector2(760f, 330f), new Vector2(-0.55f, -0.45f), 0.8f);
    }

    private static Image Bar(Transform parent, string name, float x1, float y1, float x2, float y2, Color color)
    {
        var bg = new GameObject(name + "Fondo", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        SetRect(bg.GetComponent<RectTransform>(), x1, y1, x2, y2);
        bg.GetComponent<Image>().color = new Color(.15f, .19f, .23f, 1);
        bg.GetComponent<Image>().raycastTarget = false;
        var fill = new GameObject(name, typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        var fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        Image image = fill.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        // Sin sprite, Image.Type.Filled no recorta: se usa la escala horizontal.
        image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 0f;
        return image;
    }

    private static TMP_Text MakeText(Transform parent, string value, float size, FontStyles style)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = value; t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static void SetRect(RectTransform r, float x1, float y1, float x2, float y2)
    {
        r.anchorMin = new Vector2(x1, y1);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    private void UpdateHUD()
    {
        int total = TotalOrbs;
        if (progressText) progressText.text = "Orbes alcanzados: " + count + " / " + total;
        if (progressFill) progressFill.fillAmount = total > 0 ? count / (float)total : 0f;
        if (gazeFill) gazeFill.fillAmount = 0f;
    }

    private IEnumerator Finish()
    {
        done = true;
        if (messageText) messageText.text = "Misión cumplida.";
        GlobalSessionUI.CompleteLevel(3);
        yield break;
    }
}
