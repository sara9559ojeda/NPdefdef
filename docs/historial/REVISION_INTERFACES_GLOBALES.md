# Revisión de interfaces globales

> Nota de cierre: el apartado histórico que indica que Nivel5 aún debe enlazarse ya quedó superado. Su estado vigente se describe en `CHECKLIST_FINAL.md`, `REVISION_NIVEL5_FINAL.md` y `ESTADO_FINAL_ENTREGA.md`.

## Implementado

- MainMenu: bienvenida/consentimiento, evaluación inicial 1–5, perfil visual con validación local, controles y salida. Los botones de acceso directo a niveles se retiran del menú reconstruido en runtime.
- `GlobalSessionUI`: una interfaz por escena jugable (Nivel1, Nivel3, Nivel4), con botón de pausa visible, menú de pausa, controles básicos, confirmación de salida, resultado de nivel y resumen de sesión. Se instancia al cargar la escena sin editarla. Conserva los HUD propios de los niveles y no añade otra barra de progreso.
- `GameStateManager` pausa arañas en PAUSED/LEVEL_COMPLETE/SESSION_END. La pausa usa `Time.timeScale = 0`; la UI usa eventos independientes del tiempo de juego.
- La salida de emergencia agrega métricas disponibles con `levelPassed = false`, cierra el reporte local y vuelve a MainMenu.
- Las finalizaciones conectadas agregan métricas una sola vez por nivel. Nivel1 y Nivel4 no avanzan automáticamente a los escenarios pendientes. Nivel3 ofrece avance manual a Nivel4. Actualización 2026-10-16 (rama `feature/integracion-backend-ia`): Siguiente sigue Nivel1 → Nivel3 → Nivel4 → Nivel5; Nivel2 sigue bloqueado.
- `SessionManager` conserva `phobiaGrade`, mide la duración total con reloj real, evita cierres y registros duplicados y expone `ResolveInitialLevel` como lugar explícito para la tabla futura de asignación. Por ahora retorna Nivel1 para todo grado válido.
- Se conserva `FastAPIClient` y su comportamiento no bloqueante ante fallos de red.

## Scripts nuevos

- `Assets/Scripts/UI/GlobalSessionUI.cs` y su `.meta`.

## Scripts existentes modificados

- `Assets/NeuroPhobia_MainMenuModern/Scripts/Runtime/NeuroPhobiaMainMenuModern.cs`
- `Assets/Scripts/Control/SessionManager.cs`, `GameStateManager.cs`, `LevelManager.cs`, `LevelCompletionTrigger.cs`
- `Assets/Scripts/Menu/NeuroPhobiaMenuBridge.cs`, `Assets/Scripts/UI/SceneButtonLoader.cs`
- `Assets/NeuroPhobia_Level1Flow/Scripts/Runtime/Level1SequentialGazeFlow.cs`
- `Assets/NeuroPhobia_Levels123/Scripts/Runtime/Level1ExposureController.cs`, `Level3ProgressControllerV2.cs`
- `Assets/NeuroPhobia_Level23Polish/Scripts/Runtime/Level3OrbSequenceFlow.cs`, `NPLevel3RealOrbs.cs`
- `Assets/Scripts/Nivel3/Level3ProgressController.cs`, `Assets/Scripts/Nivel4/Level4ContactTimer.cs`, `Level4HUD.cs`
- `Assets/NeuroPhobia_Level45/Scripts/Runtime/NPLevel4Controller.cs`

## Escenas tocadas

Ningún archivo `.unity`. La escena MainMenu ya contiene `NeuroPhobiaMainMenuModern`, que reconstruye su Canvas al iniciar. La interfaz global se crea al cargar Nivel1, Nivel3 o Nivel4. Nivel2 y Nivel5 permanecen intactos.

## Prueba manual pendiente

1. Abrir MainMenu en Play Mode. Iniciar experiencia, aceptar y seleccionar cada grado 1–5. Confirmar reporte con grado correcto y entrada a Nivel1.
2. Abrir Mis Datos. Probar nombre vacío/corto, correo incorrecto y datos válidos. Verificar que el texto solo habla de estado local.
3. En Nivel1, Nivel3 y Nivel4 abrir PAUSA con botón UI; en PC, probar Escape. Continuar y confirmar que el juego vuelve a avanzar.
4. Abrir salida de emergencia, cancelar y luego confirmar. Verificar retorno a MainMenu, `levelPassed = false` y archivo `session_<id>.json` en `Application.persistentDataPath`.
5. Completar cada nivel jugable. Revisar métricas y texto neutro. Nivel1 y Nivel4 deben mostrar sus bloqueos, Nivel3 debe permitir Nivel4. Abrir resumen desde el resultado.
6. En Quest comprobar puntero XR, legibilidad del Canvas World Space y acceso al botón PAUSA. No se reasignó ningún botón del sistema Meta.
7. En Editor comprobar Missing Scripts visualmente en MainMenu, Nivel1, Nivel3 y Nivel4.

## Pendiente y fuera de alcance

- Nivel2: bloqueado por escenario nuevo no presente en repositorio.
- Nivel5: escenario de apartamento y configuración visual posterior; después enlazar TaskManager, SpawnManager, PhobiaGrade, HUD, métricas y SessionReport.
- Tabla grado → nivel: pendiente de definición.
- Pruebas PC/Quest y selección de una acción física segura para pausa en Quest.
- Backend, base de datos, IA y persistencia real del perfil: fuera de alcance.
