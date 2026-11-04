using System;
using UnityEngine;

public class Level5WindowGaze : MonoBehaviour
{
    public Camera playerCamera;
    public Transform windowTarget;
    public TaskManager taskManager;
    public float requiredSeconds = 5f;
    public float maxDistance = 12f;
    private float continuousSeconds;
    private bool completed;

    private void Update()
    {
        if (completed || GameStateManager.CurrentState != GameState.PLAYING) return;
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null || windowTarget == null) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool looking = LooksAtWindow(ray);
        continuousSeconds = looking ? continuousSeconds + Time.deltaTime : 0f;
        if (continuousSeconds < requiredSeconds) return;
        completed = true;
        if (taskManager != null) taskManager.CompleteTask("TASK_WINDOW");
    }

    // La mirada atraviesa las arañas (y al propio jugador): sus colliders no deben tapar la ventana.
    private bool LooksAtWindow(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<NeuroPhobia.Level45.NPNaturalSpiderPatrol>() != null) continue;
            if (hit.collider.GetComponentInParent<AranaController>() != null) continue;
            if (playerCamera.transform.root != null && hit.collider.transform.IsChildOf(playerCamera.transform.root)) continue;
            return hit.transform == windowTarget || hit.transform.IsChildOf(windowTarget);
        }
        return false;
    }
}
