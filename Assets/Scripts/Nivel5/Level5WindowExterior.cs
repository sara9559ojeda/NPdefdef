using UnityEngine;

/// <summary>
/// Exterior que se ve por la ventana del Nivel5: suelo, calle y una manzana de edificios con filas de ventanas, en vez
/// del skybox desnudo. Usa un material Unlit de Resources teñido con MaterialPropertyBlock (no crea shaders en tiempo
/// de ejecución que el build pudiera descartar). Sin colliders: es solo fondo.
/// </summary>
public static class Level5WindowExterior
{
    public const string RootName = "NP_L5_Exterior";

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColor = Shader.PropertyToID("_Color");

    public static GameObject Build(Transform window, Bounds opening, Vector3 outside)
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null) return existing;

        Material material = PickMaterial(window);
        GameObject root = new GameObject(RootName);
        Vector3 right = Vector3.Cross(Vector3.up, outside).normalized;
        float floorY = opening.min.y - 1.2f;
        Vector3 origin = new Vector3(opening.center.x, floorY, opening.center.z);
        root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(outside, Vector3.up));

        // Suelo (césped) y calle.
        Box(root.transform, "Suelo", new Vector3(0f, -0.6f, 30f), new Vector3(90f, 1f, 60f), new Color(.30f, .38f, .26f), material);
        Box(root.transform, "Calle", new Vector3(0f, -0.08f, 14f), new Vector3(90f, 0.1f, 7f), new Color(.22f, .22f, .24f), material);
        Box(root.transform, "Acera", new Vector3(0f, -0.04f, 9.8f), new Vector3(90f, 0.12f, 1.6f), new Color(.55f, .54f, .50f), material);

        // Edificios: alturas y colores variados, con filas de ventanas oscuras.
        Random.State saved = Random.state;
        Random.InitState(1105);
        float x = -40f;
        int index = 0;
        while (x < 40f)
        {
            float width = Random.Range(7f, 13f);
            float height = Random.Range(9f, 30f);
            float depth = Random.Range(8f, 14f);
            float z = 22f + Random.Range(0f, 6f) + depth / 2f;
            Color facade = Color.Lerp(new Color(.62f, .58f, .52f), new Color(.42f, .45f, .50f), Random.value);
            Transform building = Box(root.transform, "Edificio_" + (++index), new Vector3(x + width / 2f, height / 2f, z), new Vector3(width, height, depth), facade, material);
            Windows(building, root.transform, new Vector3(x + width / 2f, 0f, z - depth / 2f - 0.03f), width, height, material);
            x += width + Random.Range(1.5f, 4f);
        }

        // Fila de árboles bajos delante de la calle (cubos verdes): rompe la línea recta del suelo.
        for (int i = -6; i <= 6; i++)
        {
            float h = Random.Range(2.2f, 3.4f);
            Box(root.transform, "Copa_" + i, new Vector3(i * 6.5f + Random.Range(-1f, 1f), h, 7.5f), new Vector3(2.4f, 2f, 2.4f), new Color(.20f, .34f, .18f), material);
            Box(root.transform, "Tronco_" + i, new Vector3(i * 6.5f, h / 2f - 0.5f, 7.5f), new Vector3(.3f, h - 1f, .3f), new Color(.30f, .22f, .15f), material);
        }
        Random.state = saved;
        return root;
    }

    private static void Windows(Transform building, Transform root, Vector3 front, float width, float height, Material material)
    {
        int columns = Mathf.Max(2, Mathf.FloorToInt(width / 2.2f));
        int rows = Mathf.Max(2, Mathf.FloorToInt((height - 2f) / 3f));
        for (int r = 0; r < rows; r++)
        {
            Box(root, building.name + "_Fila_" + r,
                front + new Vector3(0f, 2.2f + r * 3f, 0f),
                new Vector3(width - 1.2f, 1.3f, 0.05f), new Color(.16f, .20f, .26f), material);
        }
        for (int c = 1; c < columns; c++)
        {
            Box(root, building.name + "_Pilar_" + c,
                front + new Vector3(-width / 2f + c * width / columns, height / 2f, -0.04f),
                new Vector3(0.35f, height, 0.06f), new Color(.66f, .63f, .58f), material);
        }
    }

    private static Transform Box(Transform root, string name, Vector3 localPosition, Vector3 size, Color color, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        Object.Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(root, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = size;
        MeshRenderer renderer = box.GetComponent<MeshRenderer>();
        if (material != null) renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, color);
        block.SetColor(LegacyColor, color);
        renderer.SetPropertyBlock(block);
        return box.transform;
    }

    // Material Unlit propio (Resources/NeuroPhobiaExterior): el exterior no tiene luces y con un material iluminado se
    // veía negro detrás del reflejo del cristal. Al estar en Resources, el build siempre incluye su shader.
    // Si faltara, se usa el material de la pared de la ventana.
    private static Material PickMaterial(Transform window)
    {
        Material unlit = Resources.Load<Material>("NeuroPhobiaExterior");
        if (unlit != null) return unlit;
        foreach (Renderer renderer in window.GetComponentsInChildren<Renderer>(true))
        {
            Material m = renderer.sharedMaterial;
            if (m != null && m.HasProperty(BaseColor) && m.renderQueue < 2500) return m;
        }
        return null;
    }
}
