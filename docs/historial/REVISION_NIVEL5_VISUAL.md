# Revisión visual de Nivel5

## Resultado

`Assets/Scenes/Nivel5.unity` se abrió y guardó mediante la herramienta de Unity Editor `Tools > NeuroPhobia > Nivel 5 > Preparar apartamento visual`. La herramienta reutiliza el root existente en ejecuciones posteriores y solo añade detalles faltantes; no duplica el apartamento.

## Paquetes y prefabs útiles encontrados

| Paquete en Assets | Hallazgos útiles |
|---|---|
| `nappin/House Props Softpack` (57 prefabs) | Sofá, mesa de centro, consola multimedia, lámparas, estantería; refrigerador, estufa, fregadero, isla, sillas, almacenamiento; cama, mesas de noche, armario, cómoda; inodoro, lavamanos, espejos y componentes de ducha; puerta y accesorios. |
| `UnityTechnologies/Basic Asset Pack Interior` (49 prefabs) | Pisos modulares, muros, marco de puerta, muros con ventana, lámparas, alfombras, plantas, mesas, sofá, cama, sillas y estanterías. |
| `Modelosdetodo/furnituree` (25 prefabs) | Sofás, mesas, muebles para TV, cajones, estanterías y piso alternativos. |
| `Old Table&Chair` (2 prefabs) | Mesa y silla alternativas. |
| `Modelosdetodo/Pictures` (2 prefabs) | Cuadros para decoración posterior. |

No se encontró un televisor separado, microondas, bañera ni cabina completa de ducha entre los prefabs inspeccionados. Se usó consola multimedia como mueble equivalente a TV y solo accesorios reales de ducha.

## Prefabs utilizados

- **SALA:** `(Prb)Sofa`, `(Prb)CoffeTable`, `(Prb)MediaConsole`, `(Prb)Lamp`, `(Prb)Shelf1`, `RugRectangleMedium`, `PlantPotMedium`.
- **COCINA:** `(Prb)Fridge`, `(Prb)Stove`, `(Prb)KitchenSink`, `(Prb)KitchenIsland`, `(Prb)DiningChair` ×2, `(Prb)Storage1` como gabinete.
- **CUARTO:** `(Prb)DoubleBed`, `(Prb)BedsideTable` ×2, `(Prb)Wardrobe`, `(Prb)Dresser`, `(Prb)RoomLight`.
- **BAÑO:** `(Prb)Toilet`, `(Prb)BathroomSink`, `(Prb)Mirror1`, `(Prb)ShowerFace`, `(Prb)ShowerTemperature`, `(Prb)SoapDispenser`.
- **ESTRUCTURA:** `Floor4m` ×4, `WallWindow2m`, `(Prb)Door` y muros simples editables de Cube. Se dejó sin techo nuevo para facilitar la edición y conservar altura libre.

## Objetos y distribución creados

- `NP_L5_APARTAMENTO_VISUAL` contiene `01_SALA`, `02_COCINA`, `03_CUARTO`, `04_BANO`, `05_PUERTAS_VENTANAS`, `06_DECORACION` y `07_MOBILIARIO_EXTRA`.
- Cuatro habitaciones de aproximadamente **6 × 6 m**, en una cuadrícula de **12 × 12 m**. Sala: x −3..3, z −12..−6; cocina: x 3..9, z −12..−6; cuarto: x −3..3, z −6..0; baño: x 3..9, z −6..0. El punto inicial PC existente `(0, 0, −7.8)` queda en sala.
- Muros de 3.2 m con aberturas interiores de 1.6–2 m. Puerta principal y ventana al fondo. Cuatro luces puntuales sencillas de ambientación.
- Prefabs escalados según sus bounds y apoyados en el piso; todos los colliders de instancias nuevas quedan deshabilitados. El piso de fábrica antiguo sigue activo como soporte físico existente.
- `NP_L5_FRIDGE_VISUAL`, `NP_L5_DOOR_VISUAL`, `NP_L5_LAMP_VISUAL` y `NP_L5_WINDOW_VISUAL` son nombres de **instancias de escena**, sin renombrar assets originales ni añadirles scripts.
- `NP_L5_SPAWN_POINTS_FUTUROS` contiene cinco transforms vacíos: `Spawn_01` esquina de sala, `Spawn_02` cocina, `Spawn_03` cuarto, `Spawn_04` baño, `Spawn_05` cerca de un mueble en sala. No contienen modelos ni componentes de spawn.

## Objetos antiguos

Se conservaron todos los objetos y scripts de la escena original. Solo se desactivaron `FactoryProps` y los muros/techo antiguos sin scripts que ocultaban el apartamento. `LUCES` quedó activo porque contiene scripts; el piso de fábrica permanece activo. `NP_LEVEL5_FINAL`, su controller, rutas, orbes y arañas **antiguos** permanecen intactos; no se añadieron arañas nuevas. No se creó `NP_L5_ANTIGUO_PENDIENTE_REVISION` porque no fue necesario reagrupar nada.

## Ajuste manual posterior

Revisar en Scene View la orientación fina de los muebles, los materiales de muros, la decoración, la altura/posición del espejo y los accesorios de ducha, el encaje de puerta y ventana, y la transitabilidad real con el rig VR. La colocación es una primera propuesta editable. Los visuales y scripts antiguos de fábrica que permanecen activos requieren una revisión aparte antes de utilizar Nivel5 como nivel doméstico funcional.

## Lógica no implementada

No se añadieron TaskManager, SpawnManager, tareas, arañas, PhobiaGrade, métricas, HUD funcional, SessionReport, botones, triggers, colliders funcionales, scripts de interacción ni animaciones de tareas. No se modificaron XR Origin, Input Actions, ProjectSettings, Packages, Android ni otras escenas. No se hizo build, commit ni push.

## Verificación

Unity Editor compiló la herramienta y guardó Nivel5. En la comparación de la escena serializada antes/después: **0 GameObjects antiguos eliminados, 0 MonoBehaviours antiguos eliminados y 0 GUID de script cambiado**. Los únicos cambios de `m_IsActive` antiguos fueron visuales de fábrica; cámara, control de modo y managers conservaron su estado. Los roots visuales nuevos pasaron la comprobación de 0 MonoBehaviours y sus colliders quedaron deshabilitados. La inspección visual en Scene View y en headset queda para el ajuste manual.
