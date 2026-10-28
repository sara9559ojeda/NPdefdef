using UnityEngine;

public class Level5SpawnManager : MonoBehaviour
{
    public GameObject spiderPrefab;
    public Transform[] spawnPoints = new Transform[5];
    public MetricsCollector metricsCollector;
    [Range(.05f, 1f)] public float spiderScale = .3f;
    [Tooltip("El modelo CaveSpider viene tumbado; Nivel4 lo pone de pie con -90 en X.")]
    public Vector3 uprightEuler = new Vector3(-90f, 0f, 0f);
    private readonly AranaController[] spiders = new AranaController[5];

    private void Start()
    {
        int grade = SessionManager.Instance != null ? SessionManager.Instance.PhobiaGrade : 0;
        if (grade < 1 || grade > 5)
        {
            grade = 1;
            Debug.LogWarning("Nivel5: PhobiaGrade ausente o inválido; se usa 1 araña.");
        }
        if (spiderPrefab == null) { Debug.LogError("Nivel5: falta el modelo de araña."); return; }
        for (int i = 0; i < grade && i < spawnPoints.Length && i < spiders.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            GameObject spider = Instantiate(spiderPrefab, spawnPoints[i].position, spawnPoints[i].rotation * Quaternion.Euler(uprightEuler), transform);
            spider.name = "L5_CaveSpider_" + (i + 1).ToString("00");
            spider.transform.localScale *= spiderScale;
            if (spider.GetComponentInChildren<Collider>() == null)
            {
                SphereCollider gazeCollider = spider.AddComponent<SphereCollider>();
                gazeCollider.radius = .12f / Mathf.Max(.001f, spider.transform.lossyScale.x);
            }
            AranaController controller = spider.GetComponent<AranaController>();
            if (controller == null) controller = spider.AddComponent<AranaController>();
            controller.defaultPattern = MovementPattern.FREE_ROAM;
            controller.SetMovementPattern(MovementPattern.FREE_ROAM);
            controller.moveSpeed = .15f;
            controller.patrolRadius = .4f;
            if (GameStateManager.CurrentState != GameState.PLAYING) controller.Pause();
            spiders[i] = controller;
        }
        if (metricsCollector == null) metricsCollector = FindFirstObjectByType<MetricsCollector>();
        if (metricsCollector != null) metricsCollector.SetSpiders(spiders);
    }
}
