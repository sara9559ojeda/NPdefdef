using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>Qué hacer con un sobre según la respuesta del backend (contrato API v1, tabla de la sección 6.3).</summary>
public enum OutboxAction
{
    /// <summary>201 stored / 200 duplicate: guardado; se borra el archivo.</summary>
    Delete,
    /// <summary>409 RECORD_CONFLICT / 422 VALIDATION_ERROR / 400 / 413: nunca se aceptará; se aparta a cuarentena.</summary>
    Quarantine,
    /// <summary>401: token inválido o revocado; se conserva y hay que volver a emparejar.</summary>
    NeedsPairing,
    /// <summary>429, 5xx (incluido 503 STORAGE_UNAVAILABLE), sin red: se reintenta con espera creciente.</summary>
    Retry
}

/// <summary>
/// Bandeja de salida del visor (D12): cada registro es un archivo en <c>persistentDataPath/outbox/&lt;propietario&gt;</c>
/// que se borra solo cuando el backend confirma que lo guardó. Sobrevive a cierres de la app y a la falta de red.
/// El propietario es el deviceId del emparejamiento activo al crear el registro: así un registro nunca se envía
/// con el token de otro paciente que vincule el visor después. Sin vincular, el propietario es <see cref="Unpaired"/>
/// y esos registros (sesiones de prueba) se quedan solo en el visor.
/// Sin dependencias de UnityEngine para poder probarla fuera del Editor.
/// </summary>
public sealed class RecordOutbox
{
    public const double MinRetrySeconds = 2.0;
    public const double MaxRetrySeconds = 300.0;
    public const string Unpaired = "sin_vincular";

    private static long lastSequence;

    public string OutboxDir { get; }
    public string QuarantineDir { get; }

    public RecordOutbox(string rootDirectory)
    {
        OutboxDir = Path.Combine(rootDirectory, "outbox");
        QuarantineDir = Path.Combine(rootDirectory, "outbox_quarantine");
        Directory.CreateDirectory(OutboxDir);
        Directory.CreateDirectory(QuarantineDir);
    }

    /// <summary>Guarda el sobre en disco. El nombre empieza por una secuencia para conservar el orden de envío.</summary>
    public string Enqueue(string owner, string type, string recordId, string json)
    {
        long sequence = Math.Max(DateTime.UtcNow.Ticks, lastSequence + 1);
        lastSequence = sequence;
        string name = sequence.ToString("D19", CultureInfo.InvariantCulture) + "_" + type + "_" + recordId + ".json";
        string dir = OwnerDir(owner);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        string temp = path + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
        return path;
    }

    public List<string> Pending(string owner)
    {
        string dir = OwnerDir(owner);
        var files = Directory.Exists(dir) ? new List<string>(Directory.GetFiles(dir, "*.json")) : new List<string>();
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    public int PendingCount(string owner) { return Pending(owner).Count; }

    private string OwnerDir(string owner)
    {
        string safe = string.IsNullOrEmpty(owner) ? Unpaired : owner;
        foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        return Path.Combine(OutboxDir, safe);
    }
    public int QuarantineCount => Directory.GetFiles(QuarantineDir, "*.json").Length;

    public void Apply(string path, OutboxAction action)
    {
        if (!File.Exists(path)) return;
        if (action == OutboxAction.Delete) File.Delete(path);
        else if (action == OutboxAction.Quarantine)
        {
            string target = Path.Combine(QuarantineDir, Path.GetFileName(path));
            if (File.Exists(target)) File.Delete(target);
            File.Move(path, target);
        }
    }

    /// <param name="httpStatus">Código HTTP, o 0 si no hubo respuesta (sin red, tiempo agotado).</param>
    public static OutboxAction Classify(long httpStatus)
    {
        if (httpStatus == 200 || httpStatus == 201) return OutboxAction.Delete;
        if (httpStatus == 401) return OutboxAction.NeedsPairing;
        if (httpStatus == 400 || httpStatus == 409 || httpStatus == 413 || httpStatus == 422) return OutboxAction.Quarantine;
        // 403 (rol sin permiso) tampoco se arregla reintentando el mismo sobre.
        if (httpStatus == 403) return OutboxAction.Quarantine;
        return OutboxAction.Retry;
    }

    /// <summary>Espera antes del siguiente intento: respeta Retry-After y si no, duplica desde 2 s hasta 5 min.</summary>
    public static double RetryDelaySeconds(int consecutiveFailures, string retryAfterHeader)
    {
        if (!string.IsNullOrEmpty(retryAfterHeader) &&
            double.TryParse(retryAfterHeader.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
            seconds > 0)
            return Math.Min(MaxRetrySeconds, Math.Max(1.0, seconds));
        int exponent = Math.Max(0, Math.Min(consecutiveFailures - 1, 16));
        return Math.Min(MaxRetrySeconds, MinRetrySeconds * Math.Pow(2, exponent));
    }
}
