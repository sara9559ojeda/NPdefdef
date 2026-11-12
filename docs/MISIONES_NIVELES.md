# Misiones de cada nivel

Este documento es para Sara. Recoge qué tiene que hacer la persona en cada nivel para completarlo y qué script lo decide.

Los tiempos y distancias son los valores por defecto del código, salvo que se indique otra cosa. La escena puede ajustarlos en el Inspector. Todos los parámetros de exposición siguen **pendientes de validar clínicamente**.

## Reglas comunes a todos los niveles

- **Al empezar** aparece el panel de ansiedad SUDS «Antes de empezar» (0–100, o «Prefiero no responder»). El nivel está en pausa hasta que se responde.
- **Al cumplir la misión** aparece el SUDS «Al terminar» y después el panel «Nivel completado», con los botones «Siguiente nivel» y «Menú principal».
- **Durante el nivel**, el botón de menú del mando (o la B) abre la **pausa**, que ofrece continuar, opciones, volver al menú y **salida de emergencia**.
- **Cómo se ven los paneles.** Todos los paneles con botones están en el espacio del mundo, a unos 1,7 m delante de la persona y a la altura de los ojos.
  - Se recolocan al abrirse y siguen la vista si la persona gira la cabeza más de 50°.
  - Si hay una pared más cerca, el panel se acerca para no atravesarla.
  - Se pulsan con el rayo y el gatillo de cualquiera de los dos mandos.
- **Cómo se ven los HUD informativos.** Van a unos 2–2,4 m, fuera del centro de la vista, y siguen la vista con retardo.

## Nivel 1 · Observación de imágenes

- **Misión:** mirar de forma continua cada una de las ilustraciones de araña, en orden.
- **Para completarla:** cada ilustración se mantiene en el centro de la vista durante `secondsPerCard` (10 s). Hay un margen de 0,25 s si la mirada se aparta un momento.
- **Script:** `Level1SequentialGazeFlow`.

## Nivel 2 · Distancia segura (araña en el frasco)

- **Misión:** mantener la mirada en la araña del frasco desde una distancia segura.
- **Para completarla:** mirada estable durante `requiredSeconds` (20 s), dentro de un ángulo de 12°.
- **Script:** `NeuroPhobiaLevel2StableGaze`.

## Nivel 3 · Aproximación progresiva (cambia en esta entrega)

- **Misión** (se muestra en el HUD): *«Alcanza los orbes en orden. Acércate a cada orbe y mantenlo en la mirada.»*
- **Para completarla**, cada orbe se alcanza cuando se cumplen a la vez dos condiciones:
  1. la **cabeza** está a menos de 1,5 m del orbe (`reachDistance`);
  2. la persona lo **mantiene en la mirada** 3 s seguidos (`gazeSeconds`), con 14° de margen (`gazeAngle`).
- **Lo que no basta:** estar cerca sin mirarlo, mirarlo de lejos o llegar antes al segundo orbe.
- **Orden de los orbes:** solo se ve el orbe que toca. El nivel **solo termina** al alcanzar el último.
- **HUD:** muestra la misión, los orbes alcanzados, una barra de progreso y otra barra que se llena mientras se mantiene la mirada.
- **Un solo controlador** decide el progreso: `NPLevel3RealOrbs`. Al empezar se desactivan, sin borrarlos, `Level3ProgressControllerV2`, `Level3OrbSequenceFlow`, `Level3ProgressController` y `NeuroPhobiaLevel3OrbCompatibility`.
- **Arañas:** la cantidad total sale de `Resources/Level3Settings`, según el grado de fobia.

  | Grado | 1 | 2 | 3 | 4 | 5 |
  |---|---|---|---|---|---|
  | Arañas | 4 | 6 | 8 | 10 | 12 |

  - La mitad patrulla cerca de los orbes sin taparlos (a 2,2 m o más) y el resto se reparte por la sala.
  - Ninguna aparece a menos de 4 m del inicio del jugador.
  - **Decisión de Sara pendiente:** estos números son una propuesta.
- **Movimiento de las arañas:** recorren en bucle una ruta circular de radio 1,3 m.
  - Giran como mucho 70° por segundo, siempre en la dirección a la que miran.
  - Frenan en las curvas, aceleran con suavidad y a veces se detienen un momento.
- **Pausa de calma:** `Level3AdaptiveCalmV2` sigue activa. Si detecta mucho movimiento de cabeza o un alejamiento rápido, pausa a las arañas y muestra la respiración.

## Nivel 4 · Contacto virtual

- **Misión:** acercarse, uno a uno, a los puntos de exposición junto a la araña y mantener la mirada en cada uno.
- **Para completarla:** en cada punto, estar a menos de `activationRadius` (3 m) y mirarlo durante `secondsPerPoint` (10 s). Se tolera 1 s de pérdida de la mirada.
- **Script:** `NPLevel4Controller`.

## Nivel 5 · Desafío final en casa (cambia en esta entrega)

- **Misión:** cuatro tareas domésticas en el apartamento, en cualquier orden, con arañas en la casa.
- **Ninguna tarea se completa por proximidad, por mirar ni por atravesar un trigger.** Cada tarea exige una acción física con el mando.

  | Tarea (HUD) | Qué hay que hacer | Cuándo cuenta |
  |---|---|---|
  | Apagar lámpara | Pulsar el **interruptor** amarillo del pie de la lámpara, con el dedo del mando (poke) o apuntando y apretando el gatillo | Cuando la luz queda apagada. La luz se apaga de verdad. |
  | Abrir puerta | **Agarrar la hoja** de la puerta entre la sala y el cuarto (grip o gatillo) y **girar la mano** como al abrir una puerta | Cuando la hoja está abierta al menos al 60 % |
  | Abrir nevera | Agarrar una de las dos puertas de la nevera y girarla | Cuando está abierta al menos al 60 % |
  | Abrir ventana | Agarrar el **cristal** y deslizarlo hacia arriba | Cuando está abierta al menos al 60 % |

- **Puertas y cajones:** todas las puertas de muebles (armario, nevera, gabinetes, fregadero, horno) y todos los cajones (armario, isla, cómoda, gabinetes) **empiezan cerrados**.
  - Tienen collider sólido y se pueden abrir con el mando.
  - Las puertas de muebles giran sobre su bisagra y los cajones se deslizan.
- **Puertas de paso:** sala–cuarto (la de la tarea) y cuarto–baño se abren igual, agarrando y girando.
  - El marco ya no es una caja maciza: cerrada, la hoja bloquea el paso; abierta, el hueco queda libre.
  - La **puerta principal** queda cerrada y sólida, porque es la salida del apartamento.
- **Ventana:** por la ventana se ve un exterior (calle, árboles y edificios) en lugar del skybox.
  - Una araña está posada por fuera del cristal y sube con él al abrir la ventana. Se activa con `windowSpider` en `Level5SpiderSettings`. **Es decisión de Sara y está pendiente de validar clínicamente.**
  - Una barrera invisible impide salir por el hueco.
- **Movimiento:** desplazamiento continuo con el stick izquierdo y giro por pasos con el derecho, del rig de XR Interaction Toolkit.
- **Arañas:** la cantidad sigue el grado de fobia (grado N → N arañas). La distancia mínima es de 1,5 m.
  - Ninguna araña entra por su cuenta en ese radio.
  - Si es la persona la que se acerca, la araña **no se desplaza ni se puede empujar**: se queda quieta.
- **Fin del nivel:** al completar las cuatro tareas aparece el resumen de la sesión.
  - Si se superaron los intentos de alejamiento (5, valor heredado y sin fuente), se ofrece **repetir el nivel**.
- **Scripts:**
  - `Level5TaskInteraction` evalúa el estado físico de cada tarea;
  - `Level5InteractiveHome` monta la casa interactiva;
  - `Level5HingeHandle`, `Level5SlideHandle` y `Level5LampSwitch` son las piezas que se agarran o se pulsan;
  - `Level5WindowExterior` crea el exterior;
  - `TaskManager` lleva la cuenta de las tareas.

## Pendiente de validar en las gafas (Sara)

Lo comprobado hasta ahora se ha hecho en el Editor de Unity con pruebas PlayMode y capturas renderizadas. **Nada de esto está validado todavía en Quest 3.** En las gafas hay que comprobar:

1. **Paneles:** se leen bien a 1,7 m y se pulsan con el rayo de los dos mandos.
2. **Nivel 3:** la misión es comprensible y el número de arañas y su movimiento son adecuados.
3. **Bisagras y cajones:** responden con naturalidad a la mano, tanto de cerca como con el rayo, y el sentido de apertura de cada puerta es correcto.
4. **Lámpara:** el interruptor es fácil de encontrar y el apagado se nota.
5. **Ventana:** el exterior y la araña se ven bien.
6. **Navegación:** se puede recorrer la casa sin quedarse atascado.
