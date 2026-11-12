using UnityEngine;

/// <summary>
/// HUD informativo (sin botones) en World Space que sigue la vista con retardo: se queda quieto mientras la persona
/// mira cerca de él y solo se recoloca, suavemente, cuando la cabeza gira más de <see cref="followAngle"/> grados.
/// Va lejos (2 m) y desplazado abajo a la izquierda para no tapar el centro de la vista.
/// </summary>
public sealed class NeuroPhobiaHudFollow : MonoBehaviour
{
    public float distance = 2.1f;
    public Vector2 offset = new Vector2(-0.62f, -0.42f);
    public float widthMeters = 0.75f;
    public float followAngle = 28f;
    public float smoothTime = 0.35f;

    private Canvas canvas;
    private Camera head;
    private Vector3 velocity;
    private Quaternion anchorYaw;
    private bool following;
    private bool placed;

    /// <summary>Convierte un lienzo de HUD a World Space y le añade el seguimiento con retardo.</summary>
    public static NeuroPhobiaHudFollow Attach(Canvas canvas, Vector2 size, Vector2? offset = null, float widthMeters = 0.75f)
    {
        if (canvas == null) return null;
        Camera camera = NeuroPhobiaWorldPanel.GameplayCamera();
        if (canvas.transform.parent != null && camera != null && canvas.transform.IsChildOf(camera.transform))
            canvas.transform.SetParent(null, true);
        NeuroPhobiaWorldPanel.MakeWorldSpace(canvas, size, widthMeters, camera);
        NeuroPhobiaHudFollow follow = canvas.GetComponent<NeuroPhobiaHudFollow>();
        if (follow == null) follow = canvas.gameObject.AddComponent<NeuroPhobiaHudFollow>();
        follow.widthMeters = widthMeters;
        if (offset.HasValue) follow.offset = offset.Value;
        follow.placed = false;
        return follow;
    }

    /// <summary>
    /// HUD de pantalla completa (anclas en las esquinas): se convierte en una «pantalla» de 1280×720 a 2,4 m, centrada
    /// y algo baja, que sigue la vista con retardo. Los textos quedan en la periferia y el centro libre.
    /// </summary>
    public static NeuroPhobiaHudFollow AttachFullScreen(Canvas canvas)
    {
        NeuroPhobiaHudFollow follow = Attach(canvas, new Vector2(1280f, 720f), new Vector2(0f, -0.12f), 2.3f);
        if (follow != null) follow.distance = 2.4f;
        return follow;
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    private void LateUpdate()
    {
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;
        if (head == null || !head.isActiveAndEnabled)
        {
            head = NeuroPhobiaWorldPanel.GameplayCamera();
            if (head == null) return;
            canvas.worldCamera = head;
            placed = false;
        }

        Quaternion yaw = Yaw(head.transform);
        if (!placed)
        {
            anchorYaw = yaw;
            transform.position = Target(anchorYaw);
            transform.rotation = anchorYaw;
            placed = true;
            return;
        }

        float angle = Quaternion.Angle(anchorYaw, yaw);
        if (angle > followAngle) following = true;
        if (following)
        {
            anchorYaw = Quaternion.Slerp(anchorYaw, yaw, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.05f, smoothTime)));
            if (Quaternion.Angle(anchorYaw, yaw) < 2f) following = false;
        }

        Vector3 target = Target(anchorYaw);
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.rotation = Quaternion.LookRotation(transform.position - head.transform.position, Vector3.up);
    }

    private Vector3 Target(Quaternion facing)
    {
        return head.transform.position + facing * new Vector3(offset.x, offset.y, distance);
    }

    private static Quaternion Yaw(Transform t)
    {
        Vector3 forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(t.up, Vector3.up);
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
