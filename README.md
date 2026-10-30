# NEUROPHOBIA — App de realidad virtual

Aplicación de realidad virtual para apoyar el tratamiento de la aracnofobia mediante exposición gradual. Trabajo de grado de **Sara María Ojeda López**, Ingeniería de Software, Universidad Cooperativa de Colombia (Pasto, 2026).

> NEUROPHOBIA es un prototipo académico. No es un dispositivo médico ni sustituye a un terapeuta: se usa con el acompañamiento de un profesional de la salud.

## Plataforma

- **Unity 6000.3.7f1** (LTS), Universal Render Pipeline, OpenXR y XR Interaction Toolkit 3.3.1.
- **APK autónomo para Meta Quest 2, Quest 3 y Quest 3S** (Android, IL2CPP ARM64, minSdk 29). Objetivo: 72 FPS estables.
- Modo PC (teclado y ratón) disponible para pruebas en el Editor y en builds de desarrollo.

## Escenas del build

| Escena | Contenido |
|---|---|
| `MainMenu` | Bienvenida, consentimiento, grado de fobia y controles |
| `Nivel1` … `Nivel5` | Niveles de exposición gradual |

La progresión empieza siempre en el nivel 1. El selector de nivel solo aparece en el Editor y en builds de desarrollo, y las sesiones que se abren con él se marcan como sesiones de prueba.

## Abrir el proyecto

1. Instala Unity 6000.3.7f1 con el módulo **Android Build Support** (SDK, NDK y OpenJDK).
2. Abre esta carpeta desde Unity Hub. La primera importación tarda unos minutos.

## Generar el APK

Desde una terminal, con el Editor cerrado:

```
Unity.exe -batchmode -quiet -projectPath "<ruta del proyecto>" -buildTarget Android -executeMethod BuildQuest3.Build -logFile build.log
```

El APK queda en `Builds/Quest3/NeuroPhobia.apk`, que no se versiona. Para instalarlo: `adb install -r Builds/Quest3/NeuroPhobia.apk`.

## Datos y backend

La app guarda las variables de cada nivel en el visor y las envía al backend al terminar cada nivel, nunca durante la exposición. La comunicación sigue el contrato de la API, del que hay una copia fijada en [`docs/api/CONTRATO_API_v1.md`](docs/api/CONTRATO_API_v1.md); la fuente está en el repositorio del backend.

## Documentación

- `docs/api/`: copia fijada del contrato de la API.
- `docs/licencias/`: licencias y créditos de los assets de uso libre que se usan.
- `docs/historial/`: auditorías, revisiones y notas de versiones anteriores del proyecto.

## Repositorios relacionados

- Backend (FastAPI + PostgreSQL): https://github.com/sara9559ojeda/NP_Back
- Web (React + TypeScript): https://github.com/sara9559ojeda/NP_Front
