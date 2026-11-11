using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildQuest3
{
    [Serializable]
    private class ReleaseManifest
    {
        public string filename;
        public string version;
        public int versionCode;
        public string createdAt;
        public long bytes;
        public string sha256;
        public string sourceCommit;
        public bool sourceDirty;
        public string backendUrl;
        public bool signedForDistribution;
        public bool debuggable;
        public bool headsetTested;
        public bool distributionReady;
    }

    public static void Build()
    {
        string commit = Git("rev-parse HEAD");
        bool dirty = !string.IsNullOrWhiteSpace(Git("status --porcelain --untracked-files=no"));
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        FastAPIClient client = UnityEngine.Object.FindFirstObjectByType<FastAPIClient>();
        if (client == null) throw new InvalidOperationException("MainMenu has no API client");
        string backendUrl = new SerializedObject(client).FindProperty("baseUrl").stringValue;
        if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != "https")
            throw new InvalidOperationException("The delivery APK requires an HTTPS backend");

        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        string keystoreName = PlayerSettings.Android.keystoreName;
        string keystorePassword = PlayerSettings.Android.keystorePass;
        string aliasName = PlayerSettings.Android.keyaliasName;
        string aliasPassword = PlayerSettings.Android.keyaliasPass;
        string configuredKeystore = Environment.GetEnvironmentVariable("NP_ANDROID_KEYSTORE_PATH");
        bool signedForDistribution = !string.IsNullOrWhiteSpace(configuredKeystore) && File.Exists(configuredKeystore);
        try
        {
            if (signedForDistribution)
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = configuredKeystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("NP_ANDROID_KEYSTORE_PASSWORD");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("NP_ANDROID_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("NP_ANDROID_KEY_PASSWORD");
            }
            string outputDirectory = "Builds/Quest3";
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "NeuroPhobia.apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("Quest build failed with " + report.summary.totalErrors + " errors");
                EditorApplication.Exit(1);
                return;
            }
            string digest;
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(outputPath))
                digest = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            var manifest = new ReleaseManifest
            {
                filename = "NeuroPhobia.apk",
                version = PlayerSettings.bundleVersion,
                versionCode = PlayerSettings.Android.bundleVersionCode,
                createdAt = DateTime.UtcNow.ToString("o"),
                bytes = new FileInfo(outputPath).Length,
                sha256 = digest,
                sourceCommit = commit,
                sourceDirty = dirty,
                backendUrl = backendUrl,
                signedForDistribution = signedForDistribution,
                debuggable = false,
                headsetTested = false,
                distributionReady = false
            };
            File.WriteAllText(Path.Combine(outputDirectory, "release.json"), JsonUtility.ToJson(manifest, true));
            Debug.Log("Quest APK and release manifest created; physical headset validation is pending");
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            PlayerSettings.Android.keystoreName = keystoreName;
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = aliasName;
            PlayerSettings.Android.keyaliasPass = aliasPassword;
        }
    }

    private static string Git(string arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var process = System.Diagnostics.Process.Start(start))
        {
            string value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Cannot identify the source revision");
            return value;
        }
    }
}
