using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Escritor JSON mínimo para los sobres del contrato API v1.
/// JsonUtility no sabe escribir <c>null</c> ni diccionarios, y el contrato los necesita
/// (por ejemplo <c>minDistanceM: null</c> y el bloque <c>extra</c>).
/// Sin dependencias de UnityEngine para poder probarlo fuera del Editor.
/// </summary>
public sealed class NeuroPhobiaJson
{
    private readonly StringBuilder sb = new StringBuilder(512);
    private readonly Stack<bool> first = new Stack<bool>();

    public static string Iso(DateTime utc)
    {
        return utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    public NeuroPhobiaJson BeginObject() { Comma(); sb.Append('{'); first.Push(true); return this; }
    public NeuroPhobiaJson EndObject() { first.Pop(); sb.Append('}'); return this; }
    public NeuroPhobiaJson BeginArray() { Comma(); sb.Append('['); first.Push(true); return this; }
    public NeuroPhobiaJson EndArray() { first.Pop(); sb.Append(']'); return this; }

    public NeuroPhobiaJson Key(string name)
    {
        Comma();
        WriteString(name);
        sb.Append(':');
        // El valor que sigue a la clave no lleva coma.
        pendingValueAfterKey = true;
        return this;
    }

    public NeuroPhobiaJson Value(string value) { Comma(); if (value == null) sb.Append("null"); else WriteString(value); return this; }
    public NeuroPhobiaJson Value(bool value) { Comma(); sb.Append(value ? "true" : "false"); return this; }
    public NeuroPhobiaJson Value(int value) { Comma(); sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }

    public NeuroPhobiaJson Value(double value)
    {
        Comma();
        // El backend rechaza NaN e infinitos; se envían como null.
        if (double.IsNaN(value) || double.IsInfinity(value)) sb.Append("null");
        else sb.Append(value.ToString("0.0###", CultureInfo.InvariantCulture));
        return this;
    }

    public NeuroPhobiaJson Null() { Comma(); sb.Append("null"); return this; }

    public NeuroPhobiaJson Prop(string name, string value) { return Key(name).Value(value); }
    public NeuroPhobiaJson Prop(string name, bool value) { return Key(name).Value(value); }
    public NeuroPhobiaJson Prop(string name, int value) { return Key(name).Value(value); }
    public NeuroPhobiaJson Prop(string name, double value) { return Key(name).Value(value); }

    public NeuroPhobiaJson Prop(string name, double? value)
    {
        Key(name);
        return value.HasValue ? Value(value.Value) : Null();
    }

    public NeuroPhobiaJson Prop(string name, IEnumerable<string> values)
    {
        Key(name).BeginArray();
        if (values != null) foreach (string item in values) Value(item);
        return EndArray();
    }

    public override string ToString() { return sb.ToString(); }

    private bool pendingValueAfterKey;

    private void Comma()
    {
        if (pendingValueAfterKey) { pendingValueAfterKey = false; return; }
        if (first.Count == 0) return;
        if (first.Peek()) { first.Pop(); first.Push(false); }
        else sb.Append(',');
    }

    private void WriteString(string text)
    {
        sb.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
