#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Pruebas EditMode del cliente del contrato API v1.1.0 (sección 11): sobres de /device/records y bandeja de salida.
/// Los mismos sobres se validaron contra los modelos y la base de datos del backend (NP_Back) el 2026-11-05.
/// </summary>
public class ContratoApiV1Tests
{
    private static readonly DateTime Now = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);
    private string tempRoot;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "np_outbox_tests_" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
    }

    [Test]
    public void SessionStart_TieneLosCamposDelContrato()
    {
        string json = DeviceRecordMapper.SessionStart("r1", "s1", Now, 3, "0.1.0", "Oculus Quest 3", true);
        StringAssert.Contains("\"type\":\"session_start\"", json);
        StringAssert.Contains("\"schemaVersion\":1", json);
        StringAssert.Contains("\"phobiaGrade\":3", json);
        StringAssert.Contains("\"deviceModel\":\"quest3\"", json);
        StringAssert.Contains("\"testSession\":true", json);
        StringAssert.Contains("\"startedAt\":\"2026-11-05T15:00:00.000Z\"", json);
    }

    [Test]
    public void LevelRecord_SinEstimulo_EnviaMinDistanceNull()
    {
        var metrics = new LevelMetrics { level = 2, proximityMin = -1f, sessionDuration = 30f };
        string json = DeviceRecordMapper.LevelRecord("r2", "s1", metrics, 1, Now);
        StringAssert.Contains("\"minDistanceM\":null", json);
        StringAssert.Contains("\"outcome\":\"criterion_not_met\"", json);
        StringAssert.Contains("\"startedAt\":\"2026-11-05T14:59:30.000Z\"", json);
    }

    [Test]
    public void LevelRecord_SalidaDeEmergencia_MarcaOutcomeYEvento()
    {
        var metrics = new LevelMetrics { level = 3, proximityMin = 2f, sessionDuration = 10f, outcome = DeviceRecordMapper.OutcomeSafeExit };
        string json = DeviceRecordMapper.LevelRecord("r3", "s1", metrics, 2, Now);
        StringAssert.Contains("\"outcome\":\"safe_exit\"", json);
        StringAssert.Contains("\"type\":\"safe_exit\"", json);
        StringAssert.Contains("\"attempt\":2", json);
    }

    [Test]
    public void LevelRecord_ValoresInvalidos_SeCorrigen()
    {
        var metrics = new LevelMetrics { level = 9, sessionDuration = float.NaN, avoidanceAttempts = -3, levelPassed = true };
        string json = DeviceRecordMapper.LevelRecord("r4", "s1", metrics, 0, Now);
        StringAssert.Contains("\"level\":5", json);
        StringAssert.Contains("\"attempt\":1", json);
        StringAssert.Contains("\"permanenceSeconds\":0.0", json);
        StringAssert.Contains("\"avoidanceCount\":0", json);
        StringAssert.Contains("\"outcome\":\"completed\"", json);
    }

    [Test]
    public void SessionEnd_MotivoYNivelAlcanzado()
    {
        var report = new SessionReport();
        report.levelMetrics.Add(new LevelMetrics { level = 1, levelPassed = true });
        Assert.AreEqual("user_finished", DeviceRecordMapper.ResolveEndReason(report, null));
        report.levelMetrics.Add(new LevelMetrics { level = 5, levelPassed = true });
        Assert.AreEqual("completed", DeviceRecordMapper.ResolveEndReason(report, null));
        Assert.AreEqual("safe_exit", DeviceRecordMapper.ResolveEndReason(report, "safe_exit"));
        Assert.AreEqual(5, DeviceRecordMapper.LevelReached(report));
        StringAssert.Contains("\"endReason\":\"app_closed\"", DeviceRecordMapper.SessionEnd("r5", "s1", Now, "app_closed", 5, 720));
    }

    [TestCase(201, OutboxAction.Delete)]
    [TestCase(200, OutboxAction.Delete)]
    [TestCase(409, OutboxAction.Quarantine)]
    [TestCase(422, OutboxAction.Quarantine)]
    [TestCase(401, OutboxAction.NeedsPairing)]
    [TestCase(429, OutboxAction.Retry)]
    [TestCase(500, OutboxAction.Retry)]
    [TestCase(503, OutboxAction.Retry)]
    [TestCase(0, OutboxAction.Retry)]
    public void Bandeja_ClasificaRespuestas(long status, OutboxAction expected)
    {
        Assert.AreEqual(expected, RecordOutbox.Classify(status));
    }

    [Test]
    public void Bandeja_EsperaCrecienteYRetryAfter()
    {
        Assert.AreEqual(2.0, RecordOutbox.RetryDelaySeconds(1, null));
        Assert.AreEqual(8.0, RecordOutbox.RetryDelaySeconds(3, null));
        Assert.AreEqual(300.0, RecordOutbox.RetryDelaySeconds(40, null));
        Assert.AreEqual(30.0, RecordOutbox.RetryDelaySeconds(1, "30"));
    }

    [Test]
    public void Bandeja_ConservaOrdenYSeparaPorEmparejamiento()
    {
        var outbox = new RecordOutbox(tempRoot);
        string first = outbox.Enqueue("dev-A", "session_start", "1", "{}");
        string second = outbox.Enqueue("dev-A", "level_record", "2", "{}");
        outbox.Enqueue("dev-B", "session_start", "3", "{}");

        List<string> pending = outbox.Pending("dev-A");
        Assert.AreEqual(new[] { first, second }, pending.ToArray());
        Assert.AreEqual(1, outbox.PendingCount("dev-B"));
        Assert.AreEqual(0, outbox.PendingCount(RecordOutbox.Unpaired));

        outbox.Apply(first, OutboxAction.Delete);
        outbox.Apply(second, OutboxAction.Quarantine);
        Assert.AreEqual(0, outbox.PendingCount("dev-A"));
        Assert.AreEqual(1, outbox.QuarantineCount);
    }

    [Test]
    public void Url_AgregaPrefijoApiV1()
    {
        Assert.AreEqual("http://192.168.1.50:8000/api/v1", FastAPIClient.ApiRootFrom("http://192.168.1.50:8000/"));
        Assert.AreEqual("https://x.test/api/v1", FastAPIClient.ApiRootFrom("https://x.test/api/v1"));
    }

    [Test]
    public void CodigoDeVinculacion_SeNormaliza()
    {
        Assert.AreEqual("K7Q-4MZ", FastAPIClient.NormalizePairingCode("k7q4mz"));
        Assert.AreEqual("K7Q-4MZ", FastAPIClient.NormalizePairingCode("K7Q-4MZ"));
        StringAssert.Contains("caducó", FastAPIClient.PairingError(410));
    }
}
#endif
