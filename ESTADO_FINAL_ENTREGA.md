# Estado final de entrega — nodo Unity NeuroPhobia

Fecha: 2026-10-12. Unity **6000.3.7f1**. Rama **`fix/nivel2-nivel5-final`**. El identificador del commit de entrega se consulta en `git log -1`; el documento forma parte de ese commit.

## Escenas y estado

Build Settings habilita, en orden, `MainMenu`, `Nivel1`, `Nivel2`, `Nivel3`, `Nivel4`, `Nivel5`.

| Escena | Estado de implementación | Pendiente |
|---|---|---|
| MainMenu | Bienvenida, consentimiento, aceptación, salida, evaluación PhobiaGrade 1–5, Mis Datos con validación local y controles. | Prueba de interacción PC/Quest. |
| Nivel1 | HUD, flujo de exposición, pausa, métricas y resultado conectados. El avance a Nivel2 queda bloqueado. | Validación física Quest. |
| Nivel2 | **PENDIENTE NIVEL2 / BLOQUEADO.** | El escenario cambió, pero el nuevo no está en la entrega oficial. Esta rama contiene un parque histórico recuperado; no se asume que corresponda al escenario nuevo. La escena quedó intacta en esta revisión. |
| Nivel3 | HUD, interacción existente, pausa, métricas, resultado y transición manual a Nivel4. | Validación física Quest. |
| Nivel4 | Terrario/interacción existente, HUD, pausa, métricas, resultado y transición manual a Nivel5. | Validación física Quest. |
| Nivel5 | Apartamento aprobado conservado: sala, cocina, cuarto, baño, paredes URP, techo `08_TECHO`, lámpara, puerta, refrigerador, ventana y cinco spawn points. Cuatro tareas oficiales y cierre de sesión conectados. | Validar interacción, apertura, movimiento, colisiones y legibilidad en Quest. |

## Interfaces y PhobiaGrade

La selección 1–5 llama `SessionManager.StartSession(phobiaGrade)`; el valor se conserva en `SessionReport.phobiaGrade`. Todos los grados inician en Nivel1 según la progresión actual. No hay una tabla inventada de grado a nivel inicial. En Nivel5, `Level5SpawnManager` instancia respectivamente 1, 2, 3, 4 o 5 CaveSpider; un grado inválido usa 1. Cada araña ocupa un punto distinto, a escala aproximada 0.3, con `MovementPattern.FREE_ROAM` y sin persecución del jugador.

Mis Datos valida nombre y correo en memoria y no afirma persistencia remota. La pausa y la salida de emergencia usan `GameStateManager` y `GlobalSessionUI`; no existe `PauseManager` duplicado. La emergencia registra `levelPassed=false` si el nivel no terminó. `CanvasLevelComplete` presenta resultados de Nivel1, Nivel3 y Nivel4; `CanvasSessionSummary` lee `SessionManager.GetReport()`. La retroalimentación se limita a descripciones de métricas y no es diagnóstico.

## Nivel5 funcional

`TASK_LAMP` alterna una luz; `TASK_DOOR` usa la puerta real del cuarto y abre su hoja suavemente 85° mediante un pivote auxiliar; `TASK_FRIDGE` usa el refrigerador real y abre sus dos hojas suavemente 85° en sentidos opuestos mediante dos pivotes auxiliares; `TASK_WINDOW` exige cinco segundos continuos de mirada y reinicia el contador al apartarla. Cada tarea se registra una sola vez. Las animaciones avanzan solo durante `PLAYING` y se detienen con `PAUSED`.

El HUD activo muestra `NIVEL 5`, las cuatro tareas y `Tareas completadas X / 4`, con acceso a pausa global. El éxito requiere las cuatro tareas y `avoidanceAttempts <= maxAvoidanceAttempts`; el valor inicial es 5 y puede editarse en Inspector. Superar ese umbral no marca éxito automático. Se corrigió un fallo hallado en esta revisión: el objeto `TaskManager` estaba desactivado en `Nivel5.unity`; quedó activado para recibir las tareas y cerrar el nivel.

## Métricas, JSON e integración

`MetricsCollector` registra `gazeTimeOnSpider`, `avoidanceAttempts`, `headMovementFrequency`, `proximityMin`, `taskCompletions`, `breathingActivations`, `levelPassed`, `sessionDuration` y los campos adicionales existentes de `LevelMetrics`. `SessionManager` añade las métricas, cierra el `SessionReport` con `sessionId`, `phobiaGrade`, `totalDuration` y `levelMetrics`, y escribe `Application.persistentDataPath/session_<id>.json`. `FastAPIClient` conserva los POST existentes de inicio, métricas y cierre; la indisponibilidad del backend no bloquea el cierre ni el JSON local. `PARAMETROS_IA.txt` enumera campos, tipos, origen, actualización y uso posible por un análisis externo futuro.

## Verificación y límites

Auditoría estática y batch: se revisaron las seis escenas habilitadas, sus referencias de scripts y materiales, los controladores, HUD, spawns, tareas y referencias críticas de Nivel5, además de Build Settings y scripts C#. La auditoría inicial detectó el `TaskManager` desactivado y motivó la corrección puntual. La segunda auditoría registró **0 errores C#**, **0 Missing Scripts**, **0 materiales ausentes**, un TaskManager activo, un HUD activo, un SpawnManager y un MetricsCollector activos en Nivel5. Se intentó un único build Android.

En PC sin gafas, el aviso de que no se pudo iniciar un XR Loader es esperable cuando falta headset/runtime. El prefab XR Origin incluye componentes `Gravity` y `Jump`; el mensaje de Jump Provider sobre Gravity no demuestra una referencia incompleta en el prefab guardado. No se modificó XR, Packages, ProjectSettings ni configuración Android para suprimir esos mensajes. Queda **PENDIENTE PC** reproducir y estudiar el aviso en Play Mode sin alterar la configuración Quest.

**PENDIENTE QUEST:** prueba física de controles XR, raycast de UI, pausa, emergencia, todas las transiciones, tareas y aperturas de Nivel5, arañas, métricas y escritura JSON. Esta revisión no afirma prueba física en Quest.

**FUERA DE ALCANCE:** backend FastAPI, base de datos, autenticación nueva, frontend web, persistencia real del perfil, módulo/modelo/análisis de IA y diagnóstico.

## Build Android de prueba

Resultado: **no completado; no se generó APK**. La única ejecución, en batchmode con BuildPipeline para Android y la configuración existente, superó la compilación C# y la auditoría de escenas. El validador de `com.unity.xr.openxr` 1.16.1 lanzó una `NullReferenceException` en `MetaQuestFeature.CreateValidationRules` durante la validación previa. Unity continuó importando activos durante más de diez minutos sin producir resultado de build; se detuvo el proceso para evitar una espera indefinida en esta máquina de 7,5 GB de RAM. La excepción pertenece al paquete/configuración XR protegida, no al código de gameplay revisado. No se cambió Android/OpenXR ni se repitió el build. Los archivos de ajustes que Unity reescribió automáticamente durante el intento se restauraron a su contenido inicial; la verificación del APK queda **PENDIENTE PC/QUEST** en un entorno de build adecuado.

## Integración backend e IA (rama `feature/integracion-backend-ia`)

Fecha: 2026-10-16. Se corrigieron partes declaradas como CUMPLE que no entregaban datos útiles al backend FastAPI. Detalle y uso en `PARAMETROS_IA.txt`.

- **Progresión:** antes solo se jugaba Nivel1. Ahora Siguiente sigue Nivel1 → Nivel3 → Nivel4 → Nivel5; Nivel2 sigue bloqueado y todo grado inicia en Nivel1.
- **Nivel3:** había dos MetricsCollector activos; queda el de `Nivel3_AutoSetup`.
- **Nivel4:** no había MetricsCollector, LevelBootstrap ni poller activos; `LevelManagers` quedó activo y su collector apunta a `NP_Spider_Level4`.
- **Nivel5:** `LevelManagers` activo (poller y LevelBootstrap) sin collector duplicado; `NPLevel5Controller` deshabilitado.
- **Métricas:** `headMovementFrequency` es el promedio del nivel; `avoidanceAttempts` cuenta episodios de retroceso ≥ 0,8 m/s; `BreathingGuide` ya no suma `breathingActivations` en Start; el respaldo de `GlobalSessionUI` usa `proximityMin = -1`.
- **Red:** la URL del backend se anula con `backend_url.txt` en `persistentDataPath` o PlayerPrefs `NeuroPhobia.BackendUrl`; `insecureHttpOption` pasó de 0 a 2 (HTTP siempre permitido) con aprobación de la usuaria. Es el único cambio en ProjectSettings; XR y Packages no se tocaron.
- **Comandos:** `pause` solo actúa en PLAYING (se ignora en LEVEL_COMPLETE y SESSION_END) y `resume` solo desde BREATHING; un cuerpo vacío o JSON inválido se trata como `none`.

Verificación: Assembly-CSharp compila sin errores con el Roslyn de Unity 6000.3.7f1. Batchmode no se pudo usar porque el Editor tenía el proyecto abierto. **PENDIENTE PC/QUEST:** Play Mode, recorrido 1 → 3 → 4 → 5 y envío real a un FastAPI en la LAN.
