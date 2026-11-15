using UnityEngine;

/// <summary>
/// Da vida sutil a la araña del Nivel 2 sin desplazar su raíz.
/// Si existe una animación Idle la reproduce lentamente; si no, usa un movimiento procedural mínimo.
/// </summary>
public sealed class Level2SpiderIdleLife : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimator;
    [SerializeField] private float animatorSpeed = 0.22f;
    [SerializeField] private float verticalAmplitude = 0.0035f;
    [SerializeField] private float rotationAmplitude = 0.7f;
    [SerializeField] private float frequency = 0.65f;
    [Tooltip("Degrees each leg swings in place when there is no animation clip.")]
    [SerializeField] private float legSwingDegrees = 5f;
    [SerializeField] private float legFrequency = 0.35f;

    // First segment of each leg and pedipalp of the Wolf Spider rig.
    private static readonly string[] LimbBonePrefixes = { "LegSegmentA.", "PedopalpSegmentA." };
    private readonly System.Collections.Generic.List<Transform> limbs = new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<Quaternion> limbRest = new System.Collections.Generic.List<Quaternion>();

    private Vector3 rootPosition;
    private Quaternion rootRotation;
    private Vector3 visualLocalPosition;
    private Quaternion visualLocalRotation;

    private void Start()
    {
        rootPosition = transform.position;
        rootRotation = transform.rotation;
        if (visualRoot == null && transform.childCount > 0) visualRoot = transform.GetChild(0);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (visualRoot != null)
        {
            visualLocalPosition = visualRoot.localPosition;
            visualLocalRotation = visualRoot.localRotation;
        }
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.speed = useAnimator ? animatorSpeed : 0f;
            animator.enabled = useAnimator;
        }
        if (!useAnimator) CollectLimbs();
    }

    private void CollectLimbs()
    {
        if (visualRoot == null) return;
        foreach (Transform bone in visualRoot.GetComponentsInChildren<Transform>(true))
        {
            foreach (string prefix in LimbBonePrefixes)
            {
                if (!bone.name.StartsWith(prefix)) continue;
                limbs.Add(bone);
                limbRest.Add(bone.localRotation);
                break;
            }
        }
    }

    // Each limb sways around its rest pose with its own rhythm, so the spider looks alive without leaving its spot.
    private void SwayLimbs()
    {
        for (int i = 0; i < limbs.Count; i++)
        {
            if (limbs[i] == null) continue;
            float phase = i * 1.7f;
            float t = Time.time * legFrequency * Mathf.PI * 2f;
            float lift = Mathf.Sin(t + phase) * legSwingDegrees;
            float reach = Mathf.Sin(t * 0.6f + phase * 1.3f) * legSwingDegrees * 0.5f;
            limbs[i].localRotation = limbRest[i] * Quaternion.Euler(lift, reach, 0f);
        }
    }

    private void LateUpdate()
    {
        transform.SetPositionAndRotation(rootPosition, rootRotation);
        if (useAnimator || visualRoot == null) return;
        float wave = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f);
        float slow = Mathf.Sin(Time.time * frequency * 0.43f * Mathf.PI * 2f);
        visualRoot.localPosition = visualLocalPosition + Vector3.up * (wave * verticalAmplitude);
        visualRoot.localRotation = visualLocalRotation * Quaternion.Euler(slow * rotationAmplitude, wave * rotationAmplitude * 0.35f, 0f);
        SwayLimbs();
    }
}
