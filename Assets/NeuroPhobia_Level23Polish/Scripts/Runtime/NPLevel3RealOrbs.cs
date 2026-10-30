
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NPLevel3RealOrbs : MonoBehaviour
{
    public GameObject desktopPlayer;
    public GameObject xrOrigin;
    public GameObject orb1;
    public GameObject orb2;
    public float collectDistance = 1.6f;
    public float completionDelay = 5f;
    public string nextScene = "Nivel4";

    int count = 0;
    bool done = false;
    TMP_Text progressText;
    TMP_Text messageText;
    Image progressFill;
    Canvas hud;

    void Start()
    {
        ResolvePlayers();
        if (orb1) orb1.SetActive(true);
        if (orb2) orb2.SetActive(false);
        BuildHUD();
        UpdateHUD();
    }

    void Update()
    {
        if (done) return;
        ResolvePlayers();

        Transform p = ActivePlayer();
        if (p == null) return;

        GameObject target = count == 0 ? orb1 : orb2;
        if (target == null) return;

        Vector2 a = new Vector2(p.position.x, p.position.z);
        Vector2 b = new Vector2(target.transform.position.x, target.transform.position.z);

        if (Vector2.Distance(a, b) <= collectDistance)
            Collect(target);
    }

    void Collect(GameObject target)
    {
        target.SetActive(false);
        count++;

        if (count == 1)
        {
            if (orb2) orb2.SetActive(true);
            if (messageText) messageText.text = "Bien. Busca el segundo orbe.";
            UpdateHUD();
            return;
        }

        count = 2;
        UpdateHUD();
        StartCoroutine(Finish());
    }

    void ResolvePlayers()
    {
        if (!desktopPlayer)
        {
            desktopPlayer = GameObject.Find("NP_DesktopPlayer_Normalizado");
            if (!desktopPlayer) desktopPlayer = GameObject.Find("NP_DesktopPlayer_Niveles45");
        }

        if (!xrOrigin)
        {
            xrOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (!xrOrigin) xrOrigin = GameObject.Find("XR Origin");
        }
    }

    Transform ActivePlayer()
    {
        if (desktopPlayer && desktopPlayer.activeInHierarchy) return desktopPlayer.transform;
        if (xrOrigin && xrOrigin.activeInHierarchy) return xrOrigin.transform;
        return null;
    }

    void BuildHUD()
    {
        var root = new GameObject("NP_L3_HUD_OrbesReales");
        root.transform.SetParent(transform, false);

        hud = root.AddComponent<Canvas>();
        hud.renderMode = RenderMode.ScreenSpaceOverlay;
        hud.overrideSorting = true;
        hud.sortingOrder = 32000;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        root.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.zero;
        pr.pivot = Vector2.zero;
        pr.sizeDelta = new Vector2(520, 135);
        pr.anchoredPosition = new Vector2(18, 18);
        pr.localScale = Vector3.one * .75f;
        panel.GetComponent<Image>().color = new Color(.02f,.035f,.05f,.92f);

        var title = MakeText(panel.transform, "NIVEL 3 · APROXIMACIÓN PROGRESIVA", 22, FontStyles.Bold);
        SetRect(title.rectTransform, .04f,.72f,.96f,.96f);

        progressText = MakeText(panel.transform, "", 20, FontStyles.Bold);
        SetRect(progressText.rectTransform, .04f,.48f,.96f,.74f);

        messageText = MakeText(panel.transform, "Acércate al orbe luminoso.", 17, FontStyles.Normal);
        SetRect(messageText.rectTransform, .04f,.25f,.96f,.50f);

        var bg = new GameObject("BarraFondo", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(panel.transform, false);
        SetRect(bg.GetComponent<RectTransform>(), .07f,.08f,.93f,.17f);
        bg.GetComponent<Image>().color = new Color(.15f,.19f,.23f,1);

        var fill = new GameObject("Barra", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        var fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        progressFill = fill.GetComponent<Image>();
        progressFill.color = new Color(.1f,.8f,.72f,1);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
    }

    TMP_Text MakeText(Transform parent, string value, float size, FontStyles style)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = value; t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    void SetRect(RectTransform r, float x1,float y1,float x2,float y2)
    {
        r.anchorMin = new Vector2(x1,y1);
        r.anchorMax = new Vector2(x2,y2);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    void UpdateHUD()
    {
        if (progressText) progressText.text = "Orbes alcanzados: " + count + " / 2";
        if (progressFill) progressFill.fillAmount = count / 2f;
    }

    IEnumerator Finish()
    {
        done = true;
        GlobalSessionUI.CompleteLevel(3);
        yield break;
    }
}
