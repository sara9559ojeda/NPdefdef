# Checklist final del nodo Unity

| Elemento | Estado | Evidencia y límite |
|---|---|---|
| Unity y Build Settings | CUMPLE | Unity 6000.3.7f1; MainMenu, Nivel1, Nivel2, Nivel3, Nivel4 y Nivel5 habilitados en ese orden. |
| Bienvenida y consentimiento | CUMPLE | MainMenu muestra bienvenida, aceptación y salida antes de la evaluación. |
| PhobiaGrade 1–5 | CUMPLE | La selección llama `StartSession(grade)` y llega a `SessionReport.phobiaGrade`; todo grado inicia en Nivel1; progresión acordada Nivel1 → Nivel3 → Nivel4 → Nivel5 (Nivel2 bloqueado). |
| Perfil visual | CUMPLE | Nombre y correo se validan y conservan solo en memoria durante la ejecución. |
| Controles y MainMenu | CUMPLE | Pantalla de información, navegación y salida disponibles. |
| Pausa global | CUMPLE | `GlobalSessionUI` usa `GameStateManager`; no hay `PauseManager` paralelo. Validación física pendiente. |
| Salida de emergencia | CUMPLE | Captura el nivel interrumpido con `levelPassed=false`, cierra la sesión y vuelve a MainMenu. |
| Resultado y resumen | CUMPLE | `CanvasLevelComplete` en niveles 1, 3 y 4, con Siguiente activo en los tres (1 → 3, 3 → 4, 4 → 5); `CanvasSessionSummary` lee el `SessionReport` real. Retroalimentación descriptiva, sin diagnóstico. |
| Nivel1 | CUMPLE | Flujo, HUD, pausa, métricas y resultado conectados; Siguiente lleva a Nivel3 (Nivel2 bloqueado). `breathingActivations` ya no suma 2 fijas por `BreathingGuide`. |
| Nivel2 | PENDIENTE NIVEL2 | El escenario nuevo no está disponible. `Nivel2.unity` de esta rama contiene un parque histórico; no se asume que sea el nuevo escenario aprobado y la escena no se modificó. |
| Nivel3 | CUMPLE (corregido) | Flujo, HUD, pausa, métricas, resultado y transición manual a Nivel4 conectados. Había dos MetricsCollector activos; queda solo el de `Nivel3_AutoSetup`, que usa `Level3AdaptiveCalmV2`. |
| Nivel4 | CUMPLE (corregido) | Interacción existente, HUD, pausa, resultado y transición manual a Nivel5 conectados. Antes `LevelManagers` y `Nivel4_AutoSetup` estaban inactivos y no había MetricsCollector, LevelBootstrap ni poller: ahora `LevelManagers` está activo con un MetricsCollector (nivel 4, araña `NP_Spider_Level4`). |
| Nivel5 visual | CUMPLE | Apartamento aprobado, sala, cocina, cuarto, baño, paredes URP, `08_TECHO`, lámpara, puerta, nevera, ventana y cinco spawn points conservados. |
| Nivel5 arañas | CUMPLE | PhobiaGrade 1–5 produce 1–5 CaveSpider; inválido usa 1; un spawn por punto, escala 0.3, `FREE_ROAM`, sin persecución y con pausa global. |
| Nivel5 tareas | CUMPLE | Solo `TASK_LAMP`, `TASK_DOOR`, `TASK_FRIDGE`, `TASK_WINDOW` cuentan para éxito. Lámpara cambia luz; puerta real y dos hojas de nevera tienen pivotes y apertura suave; ventana requiere 5 s de mirada continua. |
| Nivel5 HUD y éxito | CUMPLE | Un HUD activo con las cuatro tareas y progreso X/4; éxito requiere cuatro tareas y `avoidanceAttempts <= maxAvoidanceAttempts` (Inspector, 5 por defecto). |
| Nivel5 cierre | CUMPLE (corregido) | `LevelMetrics`, `SessionManager`, `SESSION_END`, `SessionReport`, JSON local y resumen conectados; backend opcional. `LevelManagers` activado (poller y LevelBootstrap, sin collector duplicado) y `NPLevel5Controller` (flujo fábrica) deshabilitado para que no active `L5_Orb_1`. |
| FastAPIClient | CUMPLE (corregido) | Llamadas no bloqueantes; la ausencia de servicio no bloquea el cierre local. Antes la URL era fija `http://localhost:8000` y `insecureHttpOption: 0` bloqueaba HTTP en Quest: ahora la URL se anula con `backend_url.txt` o PlayerPrefs, `insecureHttpOption: 2`, y la respuesta de comando tolera cuerpo vacío o inválido. Ver `PARAMETROS_IA.txt`. |
| Comandos adaptativos | CUMPLE (corregido) | Un `AdaptiveCommandPoller` activo en Nivel1, 3, 4 y 5. `pause` solo actúa en PLAYING (se ignora en LEVEL_COMPLETE y SESSION_END); `resume` solo desde BREATHING. |
| Parámetros para IA futura | CUMPLE | `PARAMETROS_IA.txt` enumera los campos reales, tipos, origen, actualización y transporte. `headMovementFrequency` es el promedio del nivel y `avoidanceAttempts` cuenta retrocesos ≥ 0,8 m/s (antes era la última muestra y un umbral inalcanzable). |
| Auditoría C# y escenas | CUMPLE | Batch de Unity: 0 errores C#, 0 Missing Scripts y 0 materiales ausentes en las seis escenas habilitadas; referencias críticas de Nivel5 conectadas. Tras la integración backend/IA, Assembly-CSharp compila sin errores con el Roslyn de Unity; la auditoría batch de escenas no se repitió. |
| Build Android | PENDIENTE PC | Un intento sin APK: excepción del validador OpenXR/MetaQuest y reimportación prolongada en esta máquina. No se alteró XR ni se repitió el build. |
| Validación PC | PENDIENTE PC | Play Mode, interacción visual, raycast, escritura JSON y mensajes XR sin headset pendientes. |
| Validación física Quest | PENDIENTE QUEST | Interacción XR, movimiento, colisiones, legibilidad, pausa y apertura de puerta/nevera pendientes en dispositivo. |
| Backend | FUERA DE ALCANCE | Sin implementación del servicio. |
| Base de datos | FUERA DE ALCANCE | Sin persistencia remota. |
| IA | FUERA DE ALCANCE | Sin módulo, modelo, análisis ni diagnóstico. |
| Persistencia real de perfil | FUERA DE ALCANCE | Solo estado local en memoria. |

Los resultados de la auditoría batch y del único intento de build Android se registran en `ESTADO_FINAL_ENTREGA.md`.
