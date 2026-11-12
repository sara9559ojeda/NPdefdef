using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// APK de DESARROLLO para probar en las gafas por USB (adb install -r). No es el APK de entrega:
/// - Development Build y depurable;
/// - firmado con la clave de depuración de Unity (NO con la clave académica de Sara), por lo que instalarlo encima
///   del APK 0.2.0 obliga a desinstalar antes la app (y se pierde la vinculación del visor);
/// - no modifica PlayerSettings de forma permanente (restaura la firma al terminar).
/// Uso en batch: -executeMethod BuildQuest3Development.Build  →  Builds/Quest3_dev/NeuroPhobia_dev.apk
/// </summary>
public static class BuildQuest3Development
{
    public const string OutputDirectory = "Builds/Quest3_dev";

    public static void Build()
    {
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android);
        AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
        try
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Directory.CreateDirectory(OutputDirectory);
            string output = Path.Combine(OutputDirectory, "NeuroPhobia_dev.apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("El build de desarrollo falló con " + report.summary.totalErrors + " errores");
            Debug.Log("APK de desarrollo: " + Path.GetFullPath(output) + " (" + new FileInfo(output).Length / (1024 * 1024) + " MB). Sin validar en las gafas.");
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, backend);
            PlayerSettings.Android.targetArchitectures = architectures;
        }
    }
}
