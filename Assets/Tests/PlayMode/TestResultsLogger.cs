#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace NeuroPhobia.Tests
{
    /// <summary>
    /// Escribe el resultado de cada ejecución de pruebas en Temp/neurophobia_tests.txt
    /// para poder leerlo sin abrir la ventana del Test Runner (por ejemplo, desde el MCP).
    /// </summary>
    [InitializeOnLoad]
    internal static class TestResultsLogger
    {
        private const string FileName = "Temp/neurophobia_tests.txt";
        private static readonly TestRunnerApi Api = ScriptableObjectApi();

        static TestResultsLogger()
        {
            Api.RegisterCallbacks(new Callbacks());
        }

        private static TestRunnerApi ScriptableObjectApi()
        {
            return UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                File.WriteAllText(FileName, "INICIO " + System.DateTime.Now.ToString("HH:mm:ss") + "\n", Encoding.UTF8);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                var text = new StringBuilder();
                text.AppendLine(result.TestStatus + " | " + result.Test.Name + " | " + result.Duration.ToString("0.0") + " s");
                if (!string.IsNullOrEmpty(result.Message)) text.AppendLine("    " + result.Message.Replace("\n", "\n    "));
                File.AppendAllText(FileName, text.ToString(), Encoding.UTF8);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                File.AppendAllText(FileName,
                    "FIN | " + result.TestStatus + " | aprobadas=" + result.PassCount + " falladas=" + result.FailCount +
                    " omitidas=" + result.SkipCount + " inconclusas=" + result.InconclusiveCount + "\n", Encoding.UTF8);
            }
        }
    }
}
#endif
