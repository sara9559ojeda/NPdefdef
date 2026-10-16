# Revisión final de Nivel2 — 2026-10-11

> **Estado vigente: PENDIENTE NIVEL2 / BLOQUEADO.** Esta revisión describe un parque recuperado de un commit antiguo y presente en la rama de trabajo. El escenario cambió, pero el nuevo no está en el repositorio oficial entregado. El parque histórico no se puede tratar como ese escenario nuevo ni como validación final de gameplay. `Nivel2.unity` no se modificó en la auditoría final. Véanse `CHECKLIST_FINAL.md` y `ESTADO_FINAL_ENTREGA.md`.

## Escena guardada

`Assets/Scenes/Nivel2.unity` contiene el root `NP_ENV_NIVEL2_PARQUE_GRANDE_FINAL` recuperado de `9ed5dcb:Assets/Scenes/Nivel2.unity`. La escena histórica se abrió de forma aditiva; nunca sustituyó la escena actual. Se copió el root y se conservaron exclusivamente sus siete grupos ambientales: terreno y caminos, árboles, arbustos y flores, rocas, bancas y mobiliario, cercas e iluminación. El grupo histórico `08_ZONA_EXPOSICION` se excluyó porque contenía una araña y componentes antiguos. El parque generado localmente fue sustituido; quedó un solo entorno visual activo.

Se conservaron la luz actual, el prefab actual `XR Origin (XR Rig)`, el simulador XR según su estado existente y la configuración Quest/OpenXR del proyecto. No se modificaron `Packages`, `ProjectSettings` ni otras escenas. `ProjectSettings/ShaderGraphSettings.asset` ya tenía una modificación local previa y no se tocó.

## Jugabilidad conectada

- Araña: una sola `NP_WolfSpider_Nivel2_Static` actual, con modelo, collider y `Level2SpiderIdleLife`; ubicada en la plaza central del parque recuperado, a `root.TransformPoint(0, 0, 4.5)`.
- XR Origin: situado en `root.TransformPoint(0, 0, -11)` y orientado hacia la araña. Distancia horizontal inicial verificada: **15,50 m**.
- Distancia segura: un `NeuroPhobiaLevel2SafeDistance` con referencia a esa araña y al XR Origin; mínimo **8 m** y margen de 0,08 m.
- Mirada: un `NeuroPhobiaLevel2StableGaze` enlazado al renderer de la araña y a `MetricsCollector`. Requiere **30 s continuos**, ángulo de 12° y tolerancia de pérdida de 0,35 s.
- HUD: el controlador de mirada crea su Canvas World Space al iniciar y lo sitúa frente a la cámara activa. El aviso de distancia lo crea el controlador de distancia. No se recuperó ningún HUD histórico.
- Métricas: un `MetricsCollector`, nivel 2, enlazado a cámara XR y araña. El controlador de mirada registra intentos y, al completar, envía `LevelMetrics` si existe `SessionManager`.
- Transición: `nextScene = Nivel3`, tras la pantalla de finalización de 5 s. `Assets/Scenes/Nivel3.unity` está habilitada en Build Settings.

## Verificación acotada

Se ejecutó una migración y una verificación en Unity 6000.3.7f1, ambas en batchmode, sin build. La verificación abrió la escena **guardada** y confirmó: seis roots de escena, un XR Origin, un parque activo, una araña, un controlador de mirada, uno de distancia, un `MetricsCollector`, referencias funcionales válidas, 15,50 m iniciales, Nivel3 habilitado y **Missing Scripts = 0**. La compilación no mostró errores C#; solo advertencias de API obsoleta en otros scripts. La escena temporal, el script Editor temporal y sus archivos `.meta` se eliminaron después de la verificación.

## Prueba manual pendiente

En Unity/Quest: comprobar posición, escala y visibilidad del parque y la araña; cámara y controles XR; lectura del HUD; aviso y retorno al límite de 8 m; mirada continua, reinicio tras apartar la vista, registro de métricas y carga de Nivel3. No se ha ejecutado Play ni build Android en esta revisión.
