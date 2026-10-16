# Auditoría preliminar — NeuroPhobia funcional

Fecha: 2026-10-11  
Raíz inspeccionada: `C:\Users\USUARIO\Documents\NeuroPhobia_FUNCIONAL`
Alcance: inspección estática de archivos y serialización YAML. No se abrieron ni guardaron escenas, no se ejecutaron builds ni pruebas y no se cambiaron configuraciones Unity.

## Identidad y control de versiones

- Raíz real del proyecto Unity: la indicada arriba; contiene `Assets/`, `Packages/` y `ProjectSettings/`.
- Unity: `6000.3.7f1`; revisión `696ec25a53d1` (`ProjectSettings/ProjectVersion.txt`).
- Git: rama `main`; último commit `4dce5ad137e5578801a521a5920779d100391746` (2026-09-30), “Ignora builds exportados y carpeta de respaldo IL2CPP”.
- Estado inicial: limpio, `## main...origin/main`.
- Git LFS está instalado (`3.7.1`). `git lfs ls-files` y `git lfs status` no listaron objetos ni pendientes de descarga/subida.

## Arquitectura observada

La implementación principal de juego/datos usa `Assets/Scripts`. También sobreviven sistemas paralelos en `Assets/NeuroPhobia_Level45`, `Assets/NeuroPhobia_Levels123`, `Assets/NeuroPhobia_Level23Polish` y `Assets/NeuroPhobia_PlayerFix`; no son intercambiables automáticamente. Las escenas serializan controladores distintos según nivel.

| Sistema existente | Ruta | Responsabilidad observada y uso |
|---|---|---|
| SessionManager | `Assets/Scripts/Control/SessionManager.cs` | Singleton persistente; genera SessionId, guarda PhobiaGrade y métricas por nivel, escribe reporte JSON local y solicita publicaciones a FastAPIClient. Lo invocan menú, managers/controladores de nivel y HUD. |
| LevelManager | `Assets/Scripts/Control/LevelManager.cs` | Carga `Nivel1`…`Nivel5`, acumula métricas, desbloquea el siguiente nivel y finaliza sesión al completar nivel 5. Lo invoca `LevelCompletionTrigger` y `TaskManager`. |
| GameStateManager | `Assets/Scripts/Control/GameStateManager.cs` | Estados MENU/PLAYING/PAUSED/BREATHING/LEVEL_COMPLETE/SESSION_END; pausa/reanuda `AranaController`. Lo usa el bootstrap y poller adaptativo. |
| MetricsCollector | `Assets/Scripts/Metricas/MetricsCollector.cs` | Acumula mirada a araña, intentos de evitación, frecuencia de movimiento de cabeza, proximidad mínima, tareas, respiración, intentos y contacto/retiro de mano. Se consulta desde controladores y managers de tareas. |
| SessionReport / LevelMetrics | `Assets/Scripts/Metricas/SessionReport.cs` | Tipos serializables del resumen de sesión y métricas por nivel. Usados por SessionManager/FastAPIClient. |
| TaskManager | `Assets/Scripts/Metricas/TaskManager.cs` | Lista predeterminada TASK_LAMP/DOOR/FRIDGE/WINDOW; recibe terminaciones y llama LevelManager al llegar al número requerido. Está serializado en Nivel5, aunque comparte escena con otro manager. |
| FastAPIClient | `Assets/Scripts/Comunicacion/FastAPIClient.cs` | HTTP JSON para inicio, métricas y fin de sesión; polling de comando adaptativo. URL por defecto `http://localhost:8000`. |
| AdaptiveCommandPoller | `Assets/Scripts/Control/AdaptiveCommandPoller.cs` | Consulta comando cada 5 s; reconoce `pause` y `resume`; pausa en BREATHING por 15 s. |
| LevelBootstrap / LevelCompletionTrigger | `Assets/Scripts/Control/LevelBootstrap.cs`, `LevelCompletionTrigger.cs` | Configura nivel/FPS objetivo y comunica finalización genérica a LevelManager. |
| XR bootstrap | `Assets/Scripts/Control/XRBootstrap.cs` | Inicialización explícita del loader XR Management al inicio. Está en código; su ejecución depende del punto de entrada/escena. |
| Selector PC/RV y controlador PC | `Assets/NeuroPhobia_PlayerFix/Scripts/Runtime/NeuroPhobiaControlModeSelector.cs`, `NeuroPhobiaDesktopController.cs` | Alterna cámaras/control de escritorio y origen XR; NPLevel5Controller busca referencias al selector para cambiar cámara/UI. En otras escenas hay rigs/controladores propios. |
| Araña común | `Assets/Scripts/Entidades/AranaController.cs` | Control general de araña y pausa/reanudación por estado. Algunas escenas usan patrullas de nivel en vez de depender de este control. |
| Nivel 2 (familia principal) | `Assets/Scripts/Nivel2/Level2GazeTimer.cs`, `Level2ExclusionZone.cs`, `Level2HUD.cs` | Mirada continua 30 s, zona de exclusión/distancia y HUD con avance/advertencia/continuación. Estos scripts existen, pero sus GUID no aparecen asociados a componentes propios en YAML de `Nivel2.unity`. Versiones alternativas están en `NeuroPhobia_Levels123` y `NeuroPhobia_Level23Polish`. |
| Nivel 5 (familia de tareas) | `Assets/Scripts/Nivel5/Level5DomesticTaskManager.cs`, `Level5TaskObject.cs`, `Level5SimpleInteractable.cs`, `Level5HUD.cs`, `Level5SpiderRoam.cs` | Cuatro interacciones domésticas llevan al mensaje de sesión completada y guardado de reporte; el HUD permite terminar/volver al menú. Serializadas en Nivel5. |
| Nivel 5 (familia fábrica) | `Assets/NeuroPhobia_Level45/Scripts/Runtime/NPLevel5Controller.cs`, `NPNaturalSpiderPatrol.cs`, `NPOrbPulse.cs` | Cuatro orbes secuenciales, zona final, mirada a araña central 15 s, panel de victoria y botones. El componente está habilitado y referencia cuatro orbes, zona final, araña central y tres patrullas; esto es evidencia estática, no prueba runtime. |
| Niveles 1–4 | `Assets/Scripts/Nivel3/*`, `Nivel4/*`, `NeuroPhobia_Level1Flow/Scripts/Runtime/Level1SequentialGazeFlow.cs`, `NeuroPhobia_Levels123/Scripts/Runtime/*`, `NeuroPhobia_Level45/Scripts/Runtime/NPLevel4Controller.cs` | Controladores específicos presentes; escenas 1, 3 y 4 serializan clases de esas familias. Verificar comportamiento en Play/Quest queda pendiente. |

No se encontró una clase llamada `PhobiaGrade` independiente: es una propiedad `int` del `SessionManager`, limitada a 1–5, serializada en el reporte/inicio backend.

## Escenas principales

Build Settings (`ProjectSettings/EditorBuildSettings.asset`) habilita las seis escenas requeridas. El control es estático; habilitada no equivale a verificada en ejecución.

| Escena | Controlador/sistemas encontrados en YAML | Cámara, XR y HUD — evidencia estática |
|---|---|---|
| `Assets/Scenes/MainMenu.unity` | `NeuroPhobiaMainMenuModern` y `NeuroPhobiaMenuBridge`; SessionManager/FastAPIClient referenciados. | UI de menú y parámetro `defaultPhobiaGrade: 1`; no se validó navegación ni selección en runtime. |
| `Assets/Scenes/Nivel1.unity` | `Level1SequentialGazeFlow`; rigs/código de PC presentes en el proyecto. | Hay DesktopCamera etiquetada MainCamera y DesktopTestRig serializado inactivo. HUD de secuencia/mirada. XR debe probarse en Quest. |
| `Assets/Scenes/Nivel2.unity` | No aparecen componentes YAML con identificador de los controladores de juego de Nivel2. Contiene prefab `XR Origin (XR Rig)` y un `XR Device Simulator` inactivo, más luz; instancia/ref de entorno no interpretada como juego probado. | Cámara viene del prefab XR. No se encontraron HUD, araña, temporizador, zona segura ni controlador propios en YAML. Ver detalle abajo. |
| `Assets/Scenes/Nivel3.unity` | `Level3SpiderPatrolV2`, `Level3OrbSequenceFlow`, `Level3SpiderVisibilityGuard`, `CalmPulseUI`; aparece un `AranaController` deshabilitado. | `NP_DesktopPlayer_Normalizado`; HUD/progreso y botón Continuar a Nivel4. No ejecución verificada. |
| `Assets/Scenes/Nivel4.unity` | `NPLevel4Controller`, `NPNaturalSpiderPatrol`, `Level4HUD`, `Level4SpiderHandCrawl`, además de araña común. | `NeuroPhobiaControlModeSelector`, HUD serializado inactivo en YAML, panel de finalización y botón Continuar a Nivel5. Requiere comprobar activación/config runtime. |
| `Assets/Scenes/Nivel5.unity` | `LevelBootstrap` (level 5), `MetricsCollector`, `Level5DomesticTaskManager`, cuatro `Level5TaskObject` e interactables; también `TaskManager`, `Level5HUD`, controlador `NPLevel5Controller` y arañas. | `NP_DesktopPlayer_Normalizado`/DesktopCamera; el controlador fábrica busca selector PC/RV y cámaras. `LevelManagers` está inactivo y contiene componentes de infraestructura en esa jerarquía. UI de tareas y de fábrica coexisten; prueba requerida. |

La pertenencia completa de cámara y HUD se determina por referencias a prefabs/componentes en YAML; esta auditoría no cargó las escenas en Unity y por ello marca comportamiento/visibilidad como pendiente.

Otras escenas .unity encontradas en Assets (muestras/demos, no incluidas en Build Settings): Assets/Scenes/SampleScene.unity; Assets/Fantasy Forest Environment Free Sample/Scenes/assets.unity; .../demoScene_free.unity; Assets/Modelosdetodo/ALP_Assets/Big Oak Tree FREE/Scenes/BigOakScene.unity; Assets/Modelosdetodo/furnituree/Mix_Furniture_Pack/Demo/Demo_Scene.unity; Assets/Modelosdetodo/Pictures/Pictures.unity; Assets/nappin/House Props Softpack/SampleScene.unity; Assets/Old Table&Chair/Scene01.unity; Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/DemoScene.unity.

## Nivel 2: contenido y brecha frente a documentación

**Evidencia actual:** `Nivel2.unity` (16.596 bytes) tiene una luz, instancia `XR Origin (XR Rig)` y una instancia del simulador XR desactivada. La parte YAML inspeccionada no incluye GameObjects ni referencias a `Level2GazeTimer`, `Level2ExclusionZone`, `Level2HUD`, `MetricsCollector`, araña, spawn/selector PC ni escenario parque. Los scripts `Assets/Scripts/Nivel2/*` sí existen, pero no están conectados en la escena por sus GUID; existen implementaciones alternativas en las carpetas Levels123/Level23Polish. No se puede afirmar cuál era el escenario visual previsto: la serialización por sí sola no acredita parque ni geometría jugable.

**Requerimiento documental:** `Assets/NeuroPhobia_Level2_ParkFinal/Documentation/INSTRUCCIONES.txt` dice que el parque grande debe conservar araña, jugador PC, XR, temporizador de mirada, límite de distancia, selector PC/RV y UI. `Assets/NeuroPhobia_MegaFix2345/Documentation/LEEME_MEGA_ARREGLO_2_5.txt` dice conservar araña, temporizador y límite de distancia, además del parque y HDR. La documentación prescribe prueba primero en computador (`Assets/NeuroPhobia_Level2_ParkFinal/Documentation/INSTRUCCIONES.txt`). Son instrucciones de paquetes dentro del repositorio; algunas describen un procedimiento de instalación ya realizado y no son pruebas de estado actual.

**Brecha concreta:** la escena actual no contiene evidencia serializada de gameplay ni de parque; falta vincular escenario, spawn/controlador PC/RV, araña/objetivo, timer de gaze, zona segura, HUD, métricas y transición, o confirmar que se generan en runtime por un componente presente. Temporizador documentado a contrastar: 30 s según los scripts Level2GazeTimer/Level2HUD; límite en script por defecto 8 m, no se halló valor configurado en la escena. Tampoco hay evidencia de que la finalización de Nivel2 invoque LevelManager: `Level2GazeTimer.CompleteLevel()` registra métricas directamente y muestra botón/hud; no se vio llamada de carga de escena en ese script. Falta probar colisiones/gaze PC y Quest.

## Nivel 5: contraste documental y estado

Documento de requisitos más directo: `Assets/NeuroPhobia_Level45/Documentation/INSTRUCCIONES_COMPLETAS.txt`; refuerzo `Assets/NeuroPhobia_MegaFix2345/Documentation/LEEME_MEGA_ARREGLO_2_5.txt`. Ambos describen fábrica, cuatro orbes en orden, círculo/zona final, mirada central de 15 s; el primero requiere tres arañas con rutas independientes, felicitación y botones “Volver al menú”/“Repetir Nivel 5”. Hay documentación paralela de las tareas domésticas en código/escena, pero no se encontró documento de requisitos que las declare objetivo final para Nivel5.

### Cumple (por evidencia estática; no prueba de ejecución)

- Entorno/objetivo fábrica: nombres de zona industrial y objetos de fábrica presentes en `Nivel5.unity`.
- Hay tres referencias a patrullas (`spiderPatrols`) enlazadas desde el controlador activo, además de `AranaController`/`Level5SpiderRoam` en escena. La cantidad de arañas visuales activas en runtime no está demostrada.
- Existe lógica de cuatro orbes en orden y exposición final de 15 s en `NPLevel5Controller.cs`; la escena contiene objetos `L5_Orb_*` y controlador referenciado.
- Existen textos/botones y lógica de victoria/reinicio/menú en `NPLevel5Controller`, y HUD alternativo de tareas en escena.
- Están serializados los cuatro nombres `TASK_LAMP`, `TASK_DOOR`, `TASK_FRIDGE`, `TASK_WINDOW` como tareas domésticas; `Level5DomesticTaskManager` requiere cuatro.

### Falta / discrepancias observables

- El GameObject `NP_Level5_Controller` y su componente están activos en YAML, con referencias a cuatro orbes, zona final, araña central y tres patrullas. Esto acredita el cableado, no el funcionamiento. Su `menuSceneName` por código es `Nivel1`, distinto del destino “Volver al menú” del documento.
- Dos rutas de finalización compiten: tareas domésticas registran nivel 5 en SessionManager; `TaskManager` llama a `LevelManager.OnLevelComplete`, que finaliza sesión. El controlador fábrica muestra su propio panel, pero `NPLevel5Controller.CompleteLevel()` no registra métricas ni llama a SessionManager/LevelManager. Resultado final/backend para la ruta fábrica no queda integrado por código visible.
- El requerimiento dice tres arañas con rutas independientes predecibles; hay componentes de araña y rutas con nombres `L5_Route_*`, pero el número activo y vínculo de las tres rutas al controlador no se certifica por lectura parcial/estado de ancestros. La rutina que crea arañas extra en el código no es evidencia de que se ejecute.
- `TASK_*` existe como lógica y objetos, pero implementa objetivo distinto de la fábrica-orbes descrita por los documentos.
- HUD de tareas y HUD/control de fábrica coexisten; qué HUD se ve y recibe cámara/eventos en PC/RV requiere Play/Quest.

### Pendiente de prueba

- Conteo de arañas activas en runtime; rutas y patrulla; referencias exactas de cuatro orbes y círculo final.
- Secuencia de orbes, mirada de 15 s, panel y ambos botones, en PC y Quest.
- Completar sesión: reporte local, final de sesión, JSON y envío backend; verificar qué ruta de Nivel5 se usa.
- Visibilidad HUD/Canvas y control/cámara PC/RV.

## Sistemas de XR

`Packages/manifest.json` declara XR Interaction Toolkit 3.3.1, XR Management 4.5.4 y OpenXR 1.16.1. Existen `Assets/XR/` y `Assets/XRI/Settings/`, escena contiene prefab XR Origin al menos en Nivel2, y hay código XRBootstrap. Esto acredita configuración/activos presentes, no lanzamiento probado en Quest. No se inspeccionaron ni modificaron configuraciones protegidas; hace falta prueba en el dispositivo y verificar cada escena.

## Datos para análisis posterior

El esquema serializable contiene **17 campos**: 5 en `SessionReport` y 12 en `LevelMetrics`. La tabla completa está en `PARAMETROS_IA_BORRADOR.txt`. El código ya permite guardar `session_<SessionId>.json` bajo `Application.persistentDataPath` y enviar POST a `/api/session/start`, `/api/session/metrics`, `/api/session/end`; el poller solicita GET `/api/session/command/{sessionId}`. Es una interfaz preparada, no evidencia de backend activo ni de IA. La URL por defecto localhost suele apuntar al propio dispositivo durante ejecución Quest y no acredita conectividad remota.

## Tests, herramientas, logs y builds existentes

- No se encontraron carpetas/archivos de tests EditMode o PlayMode ni `.asmdef` de tests en los resultados de inventario.
- Sí hay herramientas de Editor/menús para construir o reparar niveles: `Assets/Editor/*`, y las carpetas Editor de `NeuroPhobia_Level45`, `NeuroPhobia_Level2_ParkFinal`, `NeuroPhobia_Level3_BasementFinal`, `NeuroPhobia_PlayerFix`, entre otras. Esta fase no las ejecutó.
- Hay escenas de muestra de assets/paquetes; ninguna identificada como prueba automatizada.
- No se encontraron logs de Unity compilación ni builds finales `.apk`, `.aab`, `.exe` en el inventario buscado. Existe una carpeta `ProyectoFinal_BurstDebugInformation_DoNotShip` con artefactos de Burst, que no demuestra una build de entrega.
- Sin abrir Unity/compilar no se puede declarar ausencia o presencia de errores de compilación.

## Límites de esta auditoría

YAML evidencia referencias y valores serializados; no prueba resultados en ejecución, visibilidad efectiva, dependencias resueltas, errores de compilación, framerate, interacción de manos/controladores ni compatibilidad Quest. Pendientes concretos quedan en `CHECKLIST_PRELIMINAR.md`.

