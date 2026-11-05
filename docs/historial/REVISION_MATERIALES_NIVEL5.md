# Revision de materiales del apartamento de Nivel5

## Alcance y diagnostico

Se inspeccionaron los 107 `Renderer` (incluidos inactivos) bajo `NP_L5_APARTAMENTO_VISUAL` en `Assets/Scenes/Nivel5.unity`. Antes de la conversion habia 21 materiales unicos y cero referencias `Missing Material`. Trece materiales del paquete `nappin/House Props Softpack` usaban el shader `Standard` del renderizador Built-in, incompatible con el proyecto URP. Los otros ocho materiales ya usaban `Universal Render Pipeline/Lit`.

| Material original (shader `Standard`) | Albedo/Base Map original | Normal | Metallic / Smoothness | Objetos del apartamento que lo usan |
| --- | --- | --- | --- | --- |
| `(Mat)EmissiveWarm` | Sin textura; color y emision | No | 0 / 0 | Lamparas, refrigerador |
| `(Mat)Glass` | Sin textura; color con alfa 0.035 | No | 0 / 0.246 | Estufa, espejo del bano |
| `(Mat)GradientBeige` | `(Txt)GradientBeige.png` | No | 0 / 0.3 | Sofa, lampara, armario, cama, comoda |
| `(Mat)GradientBlack` | `(Txt)GradientBlack.png` | No | 0 / 0.3 | Estanteria, mueble TV, estufa, fregadero, refrigerador, armario, cama, ducha, puerta |
| `(Mat)GradientBlue` | `(Txt)GradientBlue.png` | No | 0 / 0.3 | Gabinete de cocina, dispensador de jabon |
| `(Mat)GradientBrown` | `(Txt)GradientBrown.png` | No | 0 / 0.3 | Estanteria, mueble TV, gabinete, isla, sillas |
| `(Mat)GradientDarkBrown` | `(Txt)GradientDarkbrown.png` | No | 0 / 0.3 | Mueble TV, gabinete, sillas, armario |
| `(Mat)GradientDarkGreen` | `(Txt)GradientDarkgreen.png` | No | 0 / 0.3 | Sillas de cocina |
| `(Mat)GradientDarkGrey` | `(Txt)GradientDarkgrey.png` | No | 0 / 0.3 | Estufa |
| `(Mat)GradientGrey` | `(Txt)GradientGrey.png` | No | 0 / 0.3 | Mesa y mueble TV, estufa, fregadero, isla, refrigerador, cama, armario, comoda, inodoro, lavamanos, ducha, espejo, puerta |
| `(Mat)GradientOrange` | `(Txt)GradientOrange.png` | No | 0 / 0.3 | Cojines del sofa, manta de la cama |
| `(Mat)GradientYellow` | `(Txt)GradientYellow.png` | No | 0 / 0.3 | Lampara y mesas de noche |
| `(Mat)MetallicBlack` | Sin textura; color | No | 0 / 0.819 | Estanteria, mesa, mueble TV, sofa, estufa, fregadero, gabinete, isla, armario, comoda, lampara, inodoro, lavamanos, puerta |

Las diez texturas de la tabla permanecen en `Assets/nappin/House Props Softpack/Textures/`. No habia Normal Map ni Metallic Map en los trece materiales de origen. El material EmissiveWarm conserva su color de emision. Glass se convirtio a superficie transparente URP. No se detectaron materiales con shader ausente ni materiales rotos.

## Conversion aplicada

Se crearon trece copias con shader `Universal Render Pipeline/Lit` en `Assets/NeuroPhobia_Level5/Materials/Converted/`. Se conservaron las referencias a texturas originales, color, metallic, smoothness, emision y transparencia disponibles. Solo se reasignaron los `sharedMaterials` de los `Renderer` descendientes de `NP_L5_APARTAMENTO_VISUAL` en la escena: 146 slots. Los materiales originales del paquete no se editaron.

`NP_L5_FRIDGE_VISUAL`, `NP_L5_LAMP_VISUAL` y `NP_L5_DOOR_VISUAL` recibieron copias compatibles. `NP_L5_WINDOW_VISUAL` ya utilizaba materiales URP compatibles, por lo que no requirio conversion.

La herramienta `Assets/Editor/NeuroPhobiaNivel5MaterialFix.cs` ofrece `Tools > NeuroPhobia > Nivel 5 > Diagnosticar materiales del apartamento` y `Convertir materiales del apartamento a URP`. La conversion puede repetirse: los materiales compatibles se omiten y las copias existentes se reutilizan.

## Verificacion

La auditoria posterior reporto 107 renderizadores, 21 materiales unicos, **0 materiales incompatibles** y **0 slots Missing Material**. Las diez referencias a texturas Albedo originales siguen presentes en las copias. La herramienta solo cambia materiales de renderizadores del root; no modifica transforms, XR Origin, FactoryProps, scripts de gameplay, otras escenas, ProjectSettings ni Packages. No se hizo build.

Pendiente manual: abrir Nivel5 en Scene View o Game View con renderizado grafico para comprobar visualmente el acabado final, en especial la transparencia de Glass y la apariencia de la emision. La comprobacion automatica se hizo con Unity Editor en modo sin graficos.
