# Checklist final del nodo Unity

| Elemento | Estado | Evidencia y límite |
|---|---|---|
| Unity y Build Settings | CUMPLE | Unity 6000.3.7f1; MainMenu, Nivel1, Nivel2, Nivel3, Nivel4 y Nivel5 habilitados en ese orden. |
| Bienvenida y consentimiento | CUMPLE | MainMenu muestra bienvenida, aceptación y salida antes de la evaluación. |
| PhobiaGrade 1–5 | CUMPLE | La selección llama `StartSession(grade)`, llega a `SessionReport.phobiaGrade` y se guarda en el backend con `PUT /device/phobia-grade` (BPMN «Ajustar grado de fobia»); se precarga el último grado. Nivel inicial = `allowedStartLevel` del backend, Nivel1 si no hay contexto. Progresión Nivel1 → Nivel2 → Nivel3 → Nivel4 → Nivel5. |
| Perfil visual | CUMPLE | Nombre y correo se validan y conservan solo en memoria durante la ejecución. |
| Controles y MainMenu | CUMPLE | Pantalla de información, navegación y salida disponibles. |
| Pausa global | CUMPLE | `GlobalSessionUI` usa `GameStateManager`; no hay `PauseManager` paralelo. Validación física pendiente. |
| Salida de emergencia | CUMPLE | Captura el nivel interrumpido con `levelPassed=false`, cierra la sesión y vuelve a MainMenu. |
| Resultado y resumen | CUMPLE | `CanvasLevelComplete` en niveles 1, 3 y 4, con Siguiente activo en los tres (1 → 3, 3 → 4, 4 → 5); `CanvasSessionSummary` lee el `SessionReport` real. Retroalimentación descriptiva, sin diagnóstico. |
| Nivel1 | CUMPLE | Flujo, HUD, pausa, métricas y resultado conectados; Siguiente lleva a Nivel2. `breathingActivations` ya no suma 2 fijas por `BreathingGuide`. El HUD de progreso (`NP_Nivel1_UI_Secuencia`) era `ScreenSpaceOverlay` y no se veía en las gafas: con visor XR activo pasa a `ScreenSpaceCamera` a 0,72 m, como en Nivel4. |
| Nivel2 | CUMPLE (pendiente PC/Quest) | Desde el 2026-10-28 `Assets/Scenes/Nivel2.unity` es el escenario nuevo (antes `Nivel2Nuevo.unity`); el parque anterior quedó en `Nivel2_Parque_Anterior.unity`, fuera de Build Settings. Araña `NP_WolfSpider_Nivel2_Static` a 4,5 m (`Level2SpawnAlign`), mirada continua de 20 s (`NeuroPhobiaLevel2StableGaze`), un MetricsCollector (nivel 2), panel común y Siguiente a Nivel3. No hay límite de distancia mínima (el parque anterior usaba 8 m y 30 s). Sara aprobó los 20 s y la ausencia de límite el 2026-11-05. |
| Nivel3 | CUMPLE (corregido) | Flujo, HUD, pausa, métricas, resultado y transición manual a Nivel4 conectados. Había dos MetricsCollector activos; queda solo el de `Nivel3_AutoSetup`, que usa `Level3AdaptiveCalmV2`. |
| Nivel4 | CUMPLE (corregido) | Interacción existente, HUD, pausa, resultado y transición manual a Nivel5 conectados. Antes `LevelManagers` y `Nivel4_AutoSetup` estaban inactivos y no había MetricsCollector, LevelBootstrap ni poller: ahora `LevelManagers` está activo con un MetricsCollector (nivel 4, araña `NP_Spider_Level4`). |
| Nivel5 visual | CUMPLE | Apartamento aprobado, sala, cocina, cuarto, baño, paredes URP, `08_TECHO`, lámpara, puerta, nevera, ventana y cinco spawn points conservados. |
| Nivel5 arañas | CUMPLE | PhobiaGrade 1–5 produce 1–5 CaveSpider; inválido usa 1; un spawn por punto, escala 0.3, `FREE_ROAM`, sin persecución y con pausa global. |
| Nivel5 tareas | CUMPLE | Solo `TASK_LAMP`, `TASK_DOOR`, `TASK_FRIDGE`, `TASK_WINDOW` cuentan para éxito. Lámpara cambia luz; puerta real y dos hojas de nevera tienen pivotes y apertura suave; ventana requiere 5 s de mirada continua. |
| Nivel5 HUD y éxito | CUMPLE | Un HUD activo con las cuatro tareas y progreso X/4; éxito requiere cuatro tareas y `avoidanceAttempts <= maxAvoidanceAttempts` (Inspector, 5 por defecto). |
| Nivel5 cierre | CUMPLE (corregido) | `LevelMetrics`, `SessionManager`, `SESSION_END`, `SessionReport`, JSON local y resumen conectados; backend opcional. `LevelManagers` activado (poller y LevelBootstrap, sin collector duplicado) y `NPLevel5Controller` (flujo fábrica) deshabilitado para que no active `L5_Orb_1`. |
| FastAPIClient | CUMPLE (contrato v1.1.0, pendiente PC/Quest) | Hasta el 2026-11-05 enviaba a `/api/session/*`, rutas que el backend no tiene: ningún dato llegaba. Ahora usa `/api/v1/device/*`: vinculación del visor, contexto, grado de fobia, bandeja de salida con reintentos (`outbox/`), `session_start`/`level_record`/`session_end` y cierre de sesión del visor. Sobres validados contra el backend real con PostgreSQL efímero (retroalimentación guardada). URL con `backend_url.txt` o PlayerPrefs, `insecureHttpOption: 2`. Ver `PARAMETROS_IA.txt`. |
| Inicio de sesión del visor | CUMPLE (pendiente PC/Quest) | Tras el consentimiento, si el visor no está vinculado, pide el código de 6 caracteres de la web con teclado en pantalla; código erróneo → mensaje y se vuelve a escribir (BPMN). En Editor/desarrollo se puede seguir sin vincular (sesión de prueba). |
| Comandos adaptativos | RETIRADO | El contrato v1 retira la consulta de comandos (D13). `AdaptiveCommandPoller` queda inerte en las escenas para no dejar Missing Script. |
| Parámetros para IA futura | CUMPLE | `PARAMETROS_IA.txt` enumera los campos reales, tipos, origen, actualización y transporte. `headMovementFrequency` es el promedio del nivel y `avoidanceAttempts` cuenta retrocesos ≥ 0,8 m/s (antes era la última muestra y un umbral inalcanzable). |
| Auditoría C# y escenas | CUMPLE | Batch de Unity: 0 errores C#, 0 Missing Scripts y 0 materiales ausentes en las seis escenas habilitadas; referencias críticas de Nivel5 conectadas. Tras la integración backend/IA, Assembly-CSharp compila sin errores con el Roslyn de Unity; la auditoría batch de escenas no se repitió. |
| Build Android | PENDIENTE PC | Un intento sin APK: excepción del validador OpenXR/MetaQuest y reimportación prolongada en esta máquina. No se alteró XR ni se repitió el build. |
| Validación PC | PENDIENTE PC | Play Mode, interacción visual, raycast, escritura JSON y mensajes XR sin headset pendientes. |
| Validación física Quest | PENDIENTE QUEST | Interacción XR, movimiento, colisiones, legibilidad, pausa y apertura de puerta/nevera pendientes en dispositivo. |
| Backend | OTRO REPOSITORIO | Implementado en `NP_Back` (FastAPI + PostgreSQL, contrato v1.1.0). Unity lo consume desde la rama `feature/unity-contrato-v1.1`. |
| Base de datos | FUERA DE ALCANCE | Sin persistencia remota. |
| IA | FUERA DE ALCANCE | Sin módulo, modelo, análisis ni diagnóstico. |
| Persistencia real de perfil | FUERA DE ALCANCE | Solo estado local en memoria. |

Los resultados de la auditoría batch y del único intento de build Android se registran en `ESTADO_FINAL_ENTREGA.md`.
