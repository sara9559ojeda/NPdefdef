# Checklist preliminar de requisitos

Estados limitados a: CUMPLE, NO CUMPLE, PENDIENTE DE PRUEBA PC, PENDIENTE DE PRUEBA QUEST. CUMPLE se asigna solo ante evidencia estática concreta; donde el requisito exige ejecución, queda pendiente.

| REQUISITO | ESTADO | EVIDENCIA | ARCHIVO/ESCENA | PENDIENTE |
|---|---|---|---|---|
| MainMenu presente en Build | CUMPLE | Escena listada habilitada (índice 0) | `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scenes/MainMenu.unity` | Probar flujo de inicio/selección de grado |
| Nivel1 presente en Build y flujo secuencial | CUMPLE | Habilitada; YAML referencia `Level1SequentialGazeFlow` | `Assets/Scenes/Nivel1.unity`, `Assets/NeuroPhobia_Level1Flow/Scripts/Runtime/Level1SequentialGazeFlow.cs` | Mirada, transición, UI y métricas PC/Quest |
| Nivel2 jugabilidad para nuevo escenario | NO CUMPLE | YAML propio solo evidencia luz, prefab XR Origin y simulador inactivo; no componentes gameplay/escenario conectados | `Assets/Scenes/Nivel2.unity`, `Assets/Scripts/Nivel2/*` | Definir/vincular escenario y objetivo según `Assets/NeuroPhobia_Level2_ParkFinal/Documentation/INSTRUCCIONES.txt`; conectar spawn, spider, gaze, safe zone, HUD, métricas y transición |
| Nivel3 presente y componentes de flujo | CUMPLE | Habilitada; patrulla, secuencia de orbes, guardia de visibilidad, HUD progreso serializados | `Assets/Scenes/Nivel3.unity` | Probar checkpoints/orbes, salida, UI y controles PC/Quest |
| Nivel4 presente y controlador | CUMPLE | Habilitada; controlador Level45 y patrulla referenciados | `Assets/Scenes/Nivel4.unity`, `Assets/NeuroPhobia_Level45/Scripts/Runtime/NPLevel4Controller.cs` | Probar 3 puntos, transición a Nivel5, HUD/PC/Quest |
| Nivel5 cumple objetivo documental de fábrica/orbes | NO CUMPLE | Hay código y referencias activas a orbes, zona final 15 s y tres patrullas, pero el flujo no integra fin de sesión; lógica de tareas domésticas paralela | `Assets/Scenes/Nivel5.unity`, `Assets/NeuroPhobia_Level45/Documentation/INSTRUCCIONES_COMPLETAS.txt`, `NPLevel5Controller.cs` | Alinear flujo único y probar cuatro orbes, 3 arañas/rutas, mirada, botones, métricas/sesión |
| OpenXR/Quest configurado | CUMPLE | Paquetes XR 3.3.1/4.5.4/1.16.1, Assets/XR, XRBootstrap y referencia XR Origin | `Packages/manifest.json`, `Assets/XR/`, `Assets/Scripts/Control/XRBootstrap.cs` | La información disponible indica que la base funciona en Quest, pero esta copia/revisión no se probó aquí: verificar arranque y los 5 niveles en dispositivo sin alterar configuración protegida |
| UI/HUD niveles | PENDIENTE DE PRUEBA PC | HUD/controladores presentes en varias escenas; Nivel2 sin HUD propio visible en YAML; Nivel4 HUD inactivo serializado; Nivel5 HUDs duplicados por flujos | `Assets/Scenes/Nivel1.unity`…`Nivel5.unity`, `Assets/Scripts/*HUD.cs` | Comprobar visibilidad, mensajes, eventos y legibilidad en cada nivel |
| Métricas | CUMPLE | Esquema/collector y campos de captura existen; varios flujos llaman BuildMetrics | `Assets/Scripts/Metricas/MetricsCollector.cs`, `SessionReport.cs`, controladores de niveles | Verificar captura real/valores en los cinco niveles; Nivel2 no tiene conexión serializada clara. Actualización 2026-10-16: Nivel4 no tenía collector activo y Nivel3 tenía dos; corregido junto con headMovementFrequency, avoidanceAttempts y breathingActivations (ver `PARAMETROS_IA.txt`) |
| JSON local | CUMPLE | `JsonUtility.ToJson`, `File.WriteAllText` con `session_<id>.json` | `Assets/Scripts/Control/SessionManager.cs` | Completar sesión y validar fichero generado/valores/no nulos |
| Backend/interface | CUMPLE | FastAPIClient declara URL/endpoints y poller de comandos | `Assets/Scripts/Comunicacion/FastAPIClient.cs`, `Assets/Scripts/Control/AdaptiveCommandPoller.cs` | No se halló backend dentro del inventario Unity ni se probó servicio/red. Actualización 2026-10-16: URL configurable (`backend_url.txt`/PlayerPrefs) e `insecureHttpOption: 2`; falta probar contra un FastAPI en la LAN |
| Transiciones entre niveles/sesión | PENDIENTE DE PRUEBA PC | LevelManager ofrece carga N1-N5 y LevelCompletionTrigger; Nivel2 registra y muestra botón, pero su temporizador no carga escena; Nivel5 tiene dos rutas | `Assets/Scripts/Control/LevelManager.cs`, `LevelCompletionTrigger.cs`, `Assets/Scripts/Nivel2/Level2GazeTimer.cs`, `Assets/Scripts/Nivel5/*` | Seguir recorrido completo y confirmar una sola ruta válida en cada nivel, incluida finalización Nivel5 |
| Errores de compilación | PENDIENTE DE PRUEBA PC | Inspección estática no compila código | Proyecto Unity completo | Abrir Console/compilar scripts y guardar primer error si aparece |
| Build Android/Quest | PENDIENTE DE PRUEBA QUEST | No se encontró APK/AAB en inventario; no se ejecutó build | `ProjectSettings/EditorBuildSettings.asset`, `Assets/Editor/BuildQuest3.cs` | Build de validación y ejecución en Quest cuando se autorice fase de pruebas |
| Rendimiento/framerate | PENDIENTE DE PRUEBA QUEST | Hay targetFrameRate 72 en LevelBootstrap; no hay medición de rendimiento | `Assets/Scripts/Control/LevelBootstrap.cs` | Medir frame time/estabilidad en Quest, en especial vegetación/luces/arañas |
| Corrección visible Nivel2 (escenario cambiado) | NO CUMPLE | La escena activa no aporta evidencia serializada del escenario Parque descrito en documentación | `Assets/Scenes/Nivel2.unity`, docs Nivel2 | Confirmar escenario visual vigente y reconstruir/vincular lógica acorde a él |
| Corrección visible Nivel5 (requerimiento de documento) | NO CUMPLE | Se mezclan meta fábrica y tareas domésticas; desacuerdo menú Nivel1 vs menú; no se enlaza final fábrica al reporte | `Assets/Scenes/Nivel5.unity`, `Assets/NeuroPhobia_Level45/Documentation/INSTRUCCIONES_COMPLETAS.txt` | Unificar el nivel según requisito confirmado, activar flujo y asegurar reporte/fin de sesión |
| Grabación de video de jugabilidad | PENDIENTE DE PRUEBA PC | No hay build ni prueba de recorrido documentada | Todo el proyecto | Grabar luego de corregir y validar, mostrando menú, niveles y finalización; ideal incluir ejecución Quest |
| Tests automatizados existentes | NO CUMPLE | Búsqueda de archivos no encontró suites EditMode/PlayMode | Inventario del repo | No hay tests automatizados detectados; incorporar verificación manual por checklist |

## Conteo preliminar

- CUMPLE: 8 (incluye presencia/build de escenas, componentes de niveles 1/3/4 e infraestructura estática XR/métricas/JSON/backend).
- NO CUMPLE: 5 (Nivel2 gameplay, objetivo Nivel5, corrección visual Nivel2, corrección visible Nivel5 y ausencia de tests automatizados).
- PENDIENTE DE PRUEBA PC: 3.
- PENDIENTE DE PRUEBA QUEST: 2.

Estos conteos clasifican evidencia estática y no sustituyen ejecución.


