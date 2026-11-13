using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small floating badge that tells the player which button acts on the thing in front of them, for example
/// "A · Sentarte" next to an armchair or "Gatillo o grip" on a door being pointed at.
/// One badge is shared by the whole level: whoever shows it becomes its owner and only that owner can hide it.
/// The badge faces the head and keeps the same apparent size at any distance.
/// </summary>
public sealed class InteractionPrompt : MonoBehaviour
{
    private static readonly Vector2 BadgeSize = new Vector2(420f, 90f);
    // Badge width as a fraction of its distance to the head (about 10 degrees of the view).
    private const float WidthPerMeter = 0.18f;
    private const float MinDistance = 0.3f;
    private const int SortingOrder = 600;

    private static InteractionPrompt instance;

    private Canvas canvas;
    private TMP_Text label;
    private Object owner;
    private Vector3 anchor;

    /// <summary>Shows the badge at a world position. Replaces whatever another owner was showing.</summary>
    public static void Show(Object owner, Vector3 worldPosition, string text)
    {
        if (owner == null) return;
        if (instance == null)
        {
            instance = new GameObject("NP_InteractionPrompt", typeof(RectTransform)).AddComponent<InteractionPrompt>();
            instance.Build();
        }
        instance.owner = owner;
        instance.anchor = worldPosition;
        instance.label.text = text;
        instance.canvas.enabled = true;
        instance.Place();
    }

    /// <summary>Hides the badge if it belongs to this owner.</summary>
    public static void Hide(Object owner)
    {
        if (instance == null || instance.owner != owner) return;
        instance.owner = null;
        instance.canvas.enabled = false;
    }

    /// <summary>True while this owner's badge is visible.</summary>
    public static bool IsShownBy(Object owner)
    {
        return instance != null && instance.canvas.enabled && instance.owner == owner;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = SortingOrder;
        ((RectTransform)transform).sizeDelta = BadgeSize;

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0.03f, 0.04f, 0.05f, 0.9f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 44f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.86f, 0.4f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        label = text;
    }

    private void LateUpdate()
    {
        if (!canvas.enabled) return;
        // The owner was destroyed or the game is no longer in play: nothing to prompt.
        if (owner == null || GameStateManager.CurrentState != GameState.PLAYING)
        {
            canvas.enabled = false;
            owner = null;
            return;
        }
        Place();
    }

    private void Place()
    {
        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        if (head == null) return;
        Vector3 fromHead = anchor - head.transform.position;
        float distance = Mathf.Max(MinDistance, fromHead.magnitude);
        transform.position = anchor;
        transform.rotation = Quaternion.LookRotation(fromHead.sqrMagnitude > 0.0001f ? fromHead : head.transform.forward, Vector3.up);
        transform.localScale = Vector3.one * (distance * WidthPerMeter / BadgeSize.x);
        canvas.worldCamera = head;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
