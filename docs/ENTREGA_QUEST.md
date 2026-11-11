# Build para la demostración académica

## Cambios

- URL de la API: `https://d2jh1px5hj3b01.cloudfront.net`; las builds de distribución requieren HTTPS.
- Consulta del resultado final desde el Refugio, con explicación por reglas y resumen opcional guardado por el servidor. Durante la exposición no se consulta Groq desde las gafas.
- El resultado recién cerrado se identifica por sesión para evitar presentar el anterior como actual.
- Correcciones del nivel 5 incorporadas desde `5e6c0e3`: colisiones, puertas y movimiento. El simulador XR del nivel 5 queda inactivo, como en las demás escenas del build.
- Pruebas PlayMode adaptadas para batchmode: se sustituyen dos esperas de fin de render, incompatibles con ese modo, por el siguiente frame.

## Editor y paquetes

El proyecto indicaba Unity 6000.3.7f1. Este PC dispone de 6000.3.25f1 con Android SDK/NDK/OpenJDK. Al importar con ese editor, Unity actualizó paquetes compatibles, ajustes XR/URP, importadores y metadatos que faltaban. La revisión local y las pruebas se realizaron con 6000.3.25f1; usar esa versión para reproducir esta entrega.

## Compilación

`BuildQuest3.Build` genera `Builds/Quest3/NeuroPhobia.apk` y `release.json`. Android ARM64, IL2CPP, sin Development Build. Registra revisión Git, estado del árbol, tamaño, SHA-256 y backend.

Para compilar en este PC con firma privada y entorno aislado:

```powershell
./tools/BuildQuestDelivery.ps1 -CreateSigningKey
# En compilaciones posteriores, reutilizar la firma:
./tools/BuildQuestDelivery.ps1
```

El script crea una clave local en `Builds/Signing`, restringe su acceso al usuario actual y guarda la contraseña protegida por Windows DPAPI. El archivo DPAPI solo puede descifrarse en el contexto de ese usuario/equipo: conservar la clave y exportar su contraseña a un gestor seguro fuera del chat antes de trasladarse a otra máquina. El JSON temporal de firma se elimina al leerlo en Unity y también al terminar el proceso. Unity recibe únicamente su ruta en `NP_ANDROID_SIGNING_CONFIG`; no recibe contraseñas ni claves de otros servicios en el entorno.

Los archivos de firma y Builds están ignorados por Git. Conservar una copia segura del almacén y sus claves; son necesarios para actualizar instalaciones firmadas con él. No compartir contraseñas en informes. Si el APK anterior usa otra firma, no se puede actualizar encima: primero sincronizar sus registros pendientes y decidir cómo retirar esa instalación sin perder datos.

No usar simultáneamente otro editor sobre el proyecto. La entrega usa SDK target 34, instalado con este editor, y mínimo 29; no requiere instalar SDK 33. Versión de la aplicación 0.2.0, código 2.

## Prueba física pendiente

Las pruebas automáticas PC no verifican render estereoscópico, mandos reales, confort ni instalación en Quest. El manifiesto se genera siempre con `headsetTested=false` y `distributionReady=false`. No habilitar la descarga web hasta revisar firma, ausencia de depuración y recorrido físico.

En Quest: arranque HTTPS, vinculación por código, inicio desde Refugio, SUDS, intentos, nivel 5 y puertas, pausa/salida segura, sincronización del cierre y mismo resultado en Refugio y web. Groq necesita configuración privada del backend actualizado; sin ello se conserva la explicación por reglas.

No se debe crear `backend_url.txt` para la distribución nueva, salvo que la persona responsable necesite una anulación explícita. El valor HTTPS correcto ya viene configurado. Revisar que no quede un archivo antiguo en el directorio persistente del visor que sustituya ese valor.
