# Revisión final de Nivel5

**NIVEL5 = IMPLEMENTADO / PENDIENTE PRUEBA QUEST**

El apartamento aprobado conserva los Transform originales. Las paredes reciben materiales URP exclusivos de Nivel5 con la textura de yeso pintado ya presente en Assets. `08_TECHO` contiene cuatro piezas simples ajustadas a los pisos existentes, a la altura superior de los muros.

El flujo de fábrica, orbes, rutas y HUD antiguo queda inactivo en Nivel5. `TASK_DOOR` usa la única instancia existente llamada `puerta sala a cuarto`; no se creó otra puerta ni se cambió su nombre. La jerarquía contiene `door/door_body` separado de `door_frame`. Un pivote auxiliar `NP_L5_DOOR_HINGE`, situado en el origen de `door_body`, guía una apertura suave de 85°. La hoja gira alrededor de ese punto al activar la interacción XR.

`TASK_FRIDGE` usa la instancia `NP_L5_FRIDGE_VISUAL`. Su prefab tiene `fridge_body`, `fridge_leftDoor` y `fridge_rightDoor` separados. Se añadieron los pivotes auxiliares `NP_L5_FRIDGE_LEFT_HINGE` y `NP_L5_FRIDGE_RIGHT_HINGE` en los orígenes de ambas hojas. La interacción XR abre las dos hojas suavemente 85° en sentidos opuestos. El cuerpo permanece en su sitio y el prefab original no se modificó. Los pivotes son marcadores de giro: no fue necesario reparentar las mallas del prefab. Las matrices mundiales iniciales de ambas instancias y las tres hojas se conservaron. `Level5TaskInteraction` anima solo mientras `GameStateManager` está en `PLAYING` y registra cada tarea una sola vez. Validar el sentido y espacio libre de las aperturas en PC y Quest.

`TaskManager` registra una sola vez `TASK_LAMP`, `TASK_DOOR`, `TASK_FRIDGE` y `TASK_WINDOW`; esta última exige cinco segundos continuos de gaze. El éxito requiere las cuatro tareas y `avoidanceAttempts <= maxAvoidanceAttempts` (Inspector, valor inicial 5). `SessionManager` añade LevelMetrics, cierra la sesión, guarda JSON local y llama al `FastAPIClient` existente sin depender del backend. `GlobalSessionUI` presenta resumen, pausa y navegación a MainMenu.

`Level5SpawnManager` lee `SessionManager.PhobiaGrade` para activar de una a cinco instancias CaveSpider en `Spawn_01` a `Spawn_05`, a escala 0.3 y con `FREE_ROAM`. Un grado inválido o ausente usa 1. `MetricsCollector` recibe las arañas activas para distancia, alejamiento y gaze.

Pendiente: prueba de interacción, legibilidad, movimiento, colisiones y escritura JSON en PC y Quest. No se realizó build Android ni prueba física Quest.
