using System;
using UnityEngine;

/// <summary>
/// Ajustes de accesibilidad del jugador (tamaño de letra, tamaño del personaje, velocidad, giro y
/// distancia del menú). Se eligen en «Accesibilidad» del menú principal y se guardan en PlayerPrefs,
/// así que valen para todas las sesiones en este visor. Cada ajuste es un índice dentro de una lista
/// corta de valores para que sea fácil de cambiar con los controladores.
/// </summary>
public static class NeuroPhobiaAccessibility
{
    public enum Setting { TextSize, PlayerSize, MoveSpeed, TurnAngle, MenuDistance, TurnMode }

    private static readonly float[][] Values =
    {
        new[] { 1f, 1.15f, 1.3f, 1.5f },          // letra (se multiplica por la base de NeuroPhobiaUITheme)
        new[] { 0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.5f }, // personaje (se multiplica por la escala del rig de cada nivel)
        new[] { 0.5f, 0.75f, 1f, 1.25f },          // velocidad de movimiento
        new[] { 15f, 30f, 45f, 60f },              // grados por giro a saltos
        new[] { 1.1f, 1.5f, 2f },                  // metros entre el menú y la cabeza (solo en visor)
        new[] { 0f, 1f }                           // turn mode: 0 = continuous (smooth), 1 = snap
    };

    private static readonly string[][] Labels =
    {
        new[] { "Normal", "Grande", "Muy grande", "Enorme" },
        new[] { "80 %", "90 %", "100 %", "110 %", "125 %", "150 %" },
        new[] { "Lenta", "Suave", "Normal", "Rápida" },
        new[] { "15°", "30°", "45°", "60°" },
        new[] { "Cerca", "Normal", "Lejos" },
        new[] { "Continuo", "A saltos" }
    };

    private static readonly int[] Defaults = { 0, 2, 2, 1, 1, 0 };
    private static readonly int[] Index = new int[Defaults.Length];
    private static bool loaded;

    /// <summary>Se dispara cada vez que cambia un ajuste.</summary>
    public static event Action Changed;

    public static float TextScale => Value(Setting.TextSize);
    public static float PlayerScale => Value(Setting.PlayerSize);
    public static float MoveSpeedScale => Value(Setting.MoveSpeed);
    public static float SnapTurnDegrees => Value(Setting.TurnAngle);
    public static float MenuDistance => Value(Setting.MenuDistance);
    /// <summary>True when the right stick turns the player smoothly; false when it turns in fixed steps.</summary>
    public static bool ContinuousTurn => Value(Setting.TurnMode) < 0.5f;

    public static int OptionCount(Setting setting) => Values[(int)setting].Length;

    public static string Label(Setting setting)
    {
        Load();
        return Labels[(int)setting][Index[(int)setting]];
    }

    /// <summary>Mueve el ajuste una opción (+1 o −1) sin salirse de la lista.</summary>
    public static void Step(Setting setting, int direction)
    {
        Load();
        int i = (int)setting;
        int next = Mathf.Clamp(Index[i] + direction, 0, Values[i].Length - 1);
        if (next == Index[i]) return;
        Index[i] = next;
        PlayerPrefs.SetInt(Key(setting), next);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        Load();
        for (int i = 0; i < Index.Length; i++)
        {
            Index[i] = Defaults[i];
            PlayerPrefs.DeleteKey(Key((Setting)i));
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ cambios pendientes (botón Aplicar)
    // Los paneles de ajustes cambian una copia; nada cambia en el juego hasta pulsar «Aplicar».
    private static readonly int[] Pending = new int[Defaults.Length];

    /// <summary>Empieza a editar: la copia pendiente parte de los valores actuales.</summary>
    public static void BeginEdit()
    {
        Load();
        Array.Copy(Index, Pending, Index.Length);
    }

    /// <summary>Mueve el valor pendiente una opción (+1 o −1) sin salirse de la lista.</summary>
    public static void StepPending(Setting setting, int direction)
    {
        Load();
        int i = (int)setting;
        Pending[i] = Mathf.Clamp(Pending[i] + direction, 0, Values[i].Length - 1);
    }

    /// <summary>Lleva los valores pendientes a los de fábrica (se aplican al pulsar «Aplicar»).</summary>
    public static void ResetPending()
    {
        Array.Copy(Defaults, Pending, Defaults.Length);
    }

    public static string PendingLabel(Setting setting) => Labels[(int)setting][Pending[(int)setting]];

    public static bool HasPendingChanges
    {
        get
        {
            Load();
            for (int i = 0; i < Index.Length; i++) if (Pending[i] != Index[i]) return true;
            return false;
        }
    }

    /// <summary>Guarda los valores pendientes en el visor y avisa a quien los aplica.</summary>
    public static void ApplyPending()
    {
        Load();
        Array.Copy(Pending, Index, Index.Length);
        for (int i = 0; i < Index.Length; i++) PlayerPrefs.SetInt(Key((Setting)i), Index[i]);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static float Value(Setting setting)
    {
        Load();
        return Values[(int)setting][Index[(int)setting]];
    }

    private static string Key(Setting setting) => "NP_Accesibilidad_" + setting;

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        for (int i = 0; i < Index.Length; i++)
        {
            int saved = PlayerPrefs.GetInt(Key((Setting)i), Defaults[i]);
            Index[i] = Mathf.Clamp(saved, 0, Values[i].Length - 1);
        }
    }
}
