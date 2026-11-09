using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Estética común de los menús (maqueta «tablero de corcho» aprobada el 2026-11-05):
/// fondo de corcho desenfocado, hojas de papel crema y acento verde salvia, sin rojo.
/// Las texturas se generan por código una sola vez (un atlas pequeño + el fondo), así que
/// no hay recursos de imagen que importar. Las fuentes están en Resources/NeuroPhobiaUI/Fonts;
/// si falta alguna se usa la fuente por defecto de TextMesh Pro.
/// Rendimiento en Quest: todos los Image usan el mismo atlas (se agrupan en pocas llamadas
/// de dibujo), sin máscaras, sin sombras en tiempo real ni post-proceso.
/// </summary>
public static class NeuroPhobiaUITheme
{
    // ───────────── colores ─────────────
    public static readonly Color Night = Hex(0x17130F);
    public static readonly Color Paper = Hex(0xEFE5CF);
    public static readonly Color PaperLight = Hex(0xF8F1E1);
    public static readonly Color Ink = Hex(0x2A2017);
    public static readonly Color InkSoft = Hex(0x5B4B3B);
    public static readonly Color Cream = Hex(0xF4ECDB);
    public static readonly Color Sepia = Hex(0xC8B08A);
    public static readonly Color Sage = Hex(0x93C4B2);
    public static readonly Color SageDeep = Hex(0x2F6C5C);
    public static readonly Color SageDeepDark = Hex(0x255A4C);
    public static readonly Color Amber = Hex(0xE3BB6E);
    public static readonly Color AmberInk = Hex(0x7D5212);
    public static readonly Color OnAccent = Hex(0xFBF6EA);

    /// <summary>Factor aplicado a todos los textos de los menús creados con AddText (base 1.3 × el ajuste de accesibilidad).</summary>
    public static float TextScale => 1.3f * NeuroPhobiaAccessibility.TextScale;

    public enum Face { Display, Type, TypeBold, Body }

    public enum ButtonKind { Primary, Secondary, Alert, Key }

    private const string FontFolder = "NeuroPhobiaUI/Fonts/";
    private static readonly Dictionary<Face, TMP_FontAsset> fonts = new Dictionary<Face, TMP_FontAsset>();

    /// <summary>Fuente TMP de cada papel tipográfico (creada una vez en modo dinámico).</summary>
    public static TMP_FontAsset Font(Face face)
    {
        if (fonts.TryGetValue(face, out TMP_FontAsset cached) && cached != null) return cached;
        string file = face == Face.Display ? "SpecialElite-Regular"
            : face == Face.Type ? "CourierPrime-Regular"
            : face == Face.TypeBold ? "CourierPrime-Bold"
            : "SourceSerif4-Variable";
        TMP_FontAsset asset = null;
        Font source = Resources.Load<Font>(FontFolder + file);
        if (source != null)
        {
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset != null)
            {
                asset.name = file + " (NeuroPhobia)";
                if (TMP_Settings.defaultFontAsset != null)
                    asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            }
        }
        if (asset == null) asset = TMP_Settings.defaultFontAsset;
        fonts[face] = asset;
        return asset;
    }

    /// <summary>Aplica fuente, tamaño y color. Si falta la versión negrita se simula con FontStyles.Bold.</summary>
    public static void Style(TMP_Text text, Face face, float size, Color color)
    {
        TMP_FontAsset asset = Font(face);
        text.font = asset;
        text.fontSize = size;
        text.color = color;
        bool realBold = face == Face.TypeBold && asset != null && asset != TMP_Settings.defaultFontAsset;
        text.fontStyle = face == Face.TypeBold && !realBold ? FontStyles.Bold : FontStyles.Normal;
    }

    // ───────────── sprites generados ─────────────
    private const int AtlasSize = 256;
    private static Texture2D atlas;
    private static Sprite solid, frame, ink, shadow, line, dot, paper, strip, tape, background;

    public static Sprite Solid { get { EnsureAtlas(); return solid; } }
    public static Sprite Frame { get { EnsureAtlas(); return frame; } }
    public static Sprite InkMark { get { EnsureAtlas(); return ink; } }
    public static Sprite Shadow { get { EnsureAtlas(); return shadow; } }
    public static Sprite RoughLine { get { EnsureAtlas(); return line; } }
    public static Sprite Dot { get { EnsureAtlas(); return dot; } }
    public static Sprite PaperSheet { get { EnsureAtlas(); return paper; } }
    public static Sprite TornStrip { get { EnsureAtlas(); return strip; } }
    public static Sprite Tape { get { EnsureAtlas(); return tape; } }

    public static Sprite CorkBackground
    {
        get
        {
            if (background == null)
            {
                Texture2D tex = BuildBackground(256, 160);
                background = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            }
            return background;
        }
    }

    private static void EnsureAtlas()
    {
        if (atlas != null) return;
        var px = new Color32[AtlasSize * AtlasSize];
        var rnd = new System.Random(11);

        // Bloque sólido (8×8) en (0,0): se usa recortado 1 px para evitar sangrado.
        Fill(px, 0, 0, 8, 8, (x, y) => 1f);
        // Marco de 3 px (24×24) en (12,0).
        Fill(px, 12, 0, 24, 24, (x, y) => (x < 3 || y < 3 || x > 20 || y > 20) ? 1f : 0f);
        // Marca de tinta (64×32) en (40,0): mancha alargada de bordes suaves.
        Fill(px, 40, 0, 64, 32, (x, y) =>
        {
            float u = (x + .5f) / 64f, v = (y + .5f) / 32f - .5f;
            float half = .42f * Mathf.Sin(Mathf.PI * Mathf.Pow(u, .8f)) * (1f - .25f * u);
            float d = Mathf.Abs(v + .04f * Mathf.Sin(u * 9f)) - half;
            return Mathf.Clamp01(.5f - d * 20f);
        });
        // Sombra suave (64×64) en (108,0).
        Fill(px, 108, 0, 64, 64, (x, y) =>
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x + .5f - 32f) - 4f) / 28f;
            float dy = Mathf.Max(0f, Mathf.Abs(y + .5f - 32f) - 4f) / 28f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
        });
        // Filo vertical rugoso (8×128) en (176,0).
        float[] jitter = Noise1D(rnd, 128, 2.6f, 10);
        Fill(px, 176, 0, 8, 128, (x, y) =>
        {
            float center = 4f + jitter[y];
            return Mathf.Clamp01(1.2f - Mathf.Abs(x + .5f - center));
        });
        // Punto (32×32) en (188,0).
        Fill(px, 188, 0, 32, 32, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16f, 16f));
            return Mathf.Clamp01(14.5f - d);
        });
        // Hoja de papel (128×128) en (0,128): borde superior rasgado.
        float[] top = Noise1D(rnd, 128, 9f, 5);
        Fill(px, 0, 128, 128, 128, (x, y) => (127 - y) >= top[x] + 2f ? 1f : 0f);
        // Franja rasgada del título (128×64) en (128,128): extremos irregulares.
        float[] leftEnd = Noise1D(rnd, 64, 16f, 4);
        float[] rightEnd = Noise1D(rnd, 64, 18f, 4);
        Fill(px, 128, 128, 128, 64, (x, y) => (x >= leftEnd[y] + 2f && x <= 125f - rightEnd[y]) ? 1f : 0f);
        // Cinta adhesiva (64×32) en (128,196): extremos cortados a mano.
        float[] tapeL = Noise1D(rnd, 32, 5f, 3);
        float[] tapeR = Noise1D(rnd, 32, 5f, 3);
        Fill(px, 128, 196, 64, 32, (x, y) => (x >= tapeL[y] + 1f && x <= 62f - tapeR[y]) ? 1f : 0f);

        atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
        {
            name = "NeuroPhobiaUI_Atlas",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        atlas.SetPixels32(px);
        atlas.Apply(false, true);

        solid = Make(new Rect(1, 1, 6, 6), Vector4.zero);
        frame = Make(new Rect(12, 0, 24, 24), new Vector4(6, 6, 6, 6));
        ink = Make(new Rect(40, 0, 64, 32), Vector4.zero);
        shadow = Make(new Rect(108, 0, 64, 64), new Vector4(28, 28, 28, 28));
        line = Make(new Rect(176, 0, 8, 128), Vector4.zero);
        dot = Make(new Rect(188, 0, 32, 32), Vector4.zero);
        paper = Make(new Rect(0, 128, 128, 128), new Vector4(4, 4, 4, 18));
        strip = Make(new Rect(128, 128, 128, 64), new Vector4(26, 8, 26, 8));
        tape = Make(new Rect(128, 196, 64, 32), new Vector4(8, 4, 8, 4));
    }

    private static Sprite Make(Rect rect, Vector4 border)
    {
        return Sprite.Create(atlas, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private delegate float AlphaAt(int x, int y);

    private static void Fill(Color32[] px, int ox, int oy, int w, int h, AlphaAt alpha)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f);
                px[(oy + y) * AtlasSize + ox + x] = new Color32(255, 255, 255, a);
            }
    }

    // Ruido 1D suave (puntos aleatorios interpolados) entre 0 y amplitude.
    private static float[] Noise1D(System.Random rnd, int length, float amplitude, int step)
    {
        var values = new float[length];
        int knots = length / step + 2;
        var k = new float[knots];
        for (int i = 0; i < knots; i++) k[i] = (float)rnd.NextDouble() * amplitude;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / step;
            int a = Mathf.FloorToInt(t);
            values[i] = Mathf.Lerp(k[a], k[a + 1], t - a);
        }
        return values;
    }

    // Fondo: tablero de corcho con papeles, notas y fotos de paisaje (sin arañas), desenfocado y
    // oscurecido a la izquierda para que el menú se lea bien. Se genera a baja resolución:
    // al estirarlo a 1440×900 el filtrado bilineal completa el desenfoque.
    private static Texture2D BuildBackground(int w, int h)
    {
        var rnd = new System.Random(42);
        var c = new Color[w * h];
        Color cork = Hex(0x6B5440);
        for (int i = 0; i < c.Length; i++)
        {
            float n = (float)rnd.NextDouble() * .10f - .05f;
            c[i] = new Color(cork.r + n, cork.g + n * .9f, cork.b + n * .8f, 1f);
        }

        // Papeles en coordenadas del lienzo 1440×900 (origen arriba a la izquierda).
        Color doc = Hex(0xE9DFC8), photoFrame = Hex(0xF2ECE0), note = Hex(0xE6DCA6), noteSage = Hex(0xBCD6C7);
        Rect[] rects =
        {
            R(560, 40, 230, 300), R(830, 0, 260, 210), R(1130, 70, 120, 120), R(1230, 220, 240, 320),
            R(610, 380, 280, 220), R(940, 290, 130, 120), R(940, 480, 220, 280), R(1210, 580, 250, 200),
            R(530, 660, 140, 130), R(730, 660, 180, 240)
        };
        Color[] colors = { doc, photoFrame, note, doc, photoFrame, noteSage, doc, photoFrame, note, doc };
        Vector2[] pins = new Vector2[rects.Length];
        for (int r = 0; r < rects.Length; r++)
        {
            DrawRect(c, w, h, rects[r], colors[r], .35f);
            bool photo = colors[r] == photoFrame;
            if (photo)
            {
                Rect inner = new Rect(rects[r].x + 10, rects[r].y + 10, rects[r].width - 20, rects[r].height - 44);
                PaintLandscape(c, w, h, inner, r);
            }
            else if (colors[r] == doc)
            {
                for (float ly = rects[r].y + 26; ly < rects[r].yMax - 20; ly += 16)
                    DrawRect(c, w, h, new Rect(rects[r].x + 18, ly, rects[r].width - 36, 2), Hex(0x8C7A62), 0f);
            }
            pins[r] = new Vector2(rects[r].center.x, rects[r].y + 14);
        }

        int[,] threads = { { 0, 1 }, { 1, 3 }, { 0, 4 }, { 4, 5 }, { 5, 6 }, { 3, 7 }, { 6, 9 }, { 2, 3 } };
        for (int t = 0; t < threads.GetLength(0); t++)
            DrawLine(c, w, h, pins[threads[t, 0]], pins[threads[t, 1]], Hex(0xD9C7A3), .5f);
        foreach (Vector2 p in pins) DrawRect(c, w, h, new Rect(p.x - 7, p.y - 7, 14, 14), Hex(0x7AA594), 0f);

        BoxBlur(c, w, h, 2);
        BoxBlur(c, w, h, 2);

        // Luz cálida arriba a la derecha, oscurecido a la izquierda y abajo (como la maqueta).
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1), vTop = 1f - y / (float)(h - 1);
                int i = y * w + x;
                Color col = c[i] * .72f;
                float lamp = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, vTop), new Vector2(.82f, .13f)) / .55f);
                col += new Color(1f, .84f, .63f) * (.20f * lamp * lamp);
                float dark = u < .33f ? 1f : u < .42f ? Mathf.Lerp(1f, .86f, (u - .33f) / .09f)
                    : u < .62f ? Mathf.Lerp(.86f, .25f, (u - .42f) / .20f) : Mathf.Lerp(.25f, .10f, (u - .62f) / .38f);
                float bottom = Mathf.Clamp01((vTop - .70f) / .30f) * .75f;
                dark = 1f - (1f - dark) * (1f - bottom);
                col = Color.Lerp(col, Night, dark);
                col.a = 1f;
                c[i] = col;
            }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "NeuroPhobiaUI_Corcho",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(c);
        tex.Apply(false, true);
        return tex;

        Rect R(float x, float y, float rw, float rh) => new Rect(x / 1440f * w, y / 900f * h, rw / 1440f * w, rh / 900f * h);
    }

    private static void PaintLandscape(Color[] c, int w, int h, Rect r, int seed)
    {
        Color sky = seed % 2 == 0 ? Hex(0xE9C99A) : Hex(0xB9CFC6);
        Color land = seed % 2 == 0 ? Hex(0x7FA296) : Hex(0x9A8571);
        for (int y = Mathf.FloorToInt(r.y); y < Mathf.CeilToInt(r.yMax); y++)
            for (int x = Mathf.FloorToInt(r.x); x < Mathf.CeilToInt(r.xMax); x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float u = (x - r.x) / r.width, v = (y - r.y) / r.height;
                float hill = .55f + .12f * Mathf.Sin(u * 5f + seed);
                c[(h - 1 - y) * w + x] = v < hill ? Color.Lerp(sky, Color.white, .2f * (1f - v)) : land;
            }
    }

    private static void DrawRect(Color[] c, int w, int h, Rect r, Color color, float shadowAlpha)
    {
        if (shadowAlpha > 0f)
            DrawRect(c, w, h, new Rect(r.x + 1.5f, r.y + 2f, r.width, r.height), Color.Lerp(Color.black, c[0], .3f), 0f);
        for (int y = Mathf.FloorToInt(r.y); y < Mathf.CeilToInt(r.yMax); y++)
            for (int x = Mathf.FloorToInt(r.x); x < Mathf.CeilToInt(r.xMax); x++)
                if (x >= 0 && y >= 0 && x < w && y < h) c[(h - 1 - y) * w + x] = color;
    }

    private static void DrawLine(Color[] c, int w, int h, Vector2 a, Vector2 b, Color color, float alpha)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
        for (int s = 0; s <= steps; s++)
        {
            Vector2 p = Vector2.Lerp(a, b, s / (float)Mathf.Max(1, steps));
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            int i = (h - 1 - y) * w + x;
            c[i] = Color.Lerp(c[i], color, alpha);
        }
    }

    private static void BoxBlur(Color[] c, int w, int h, int radius)
    {
        var tmp = new Color[c.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear; int n = 0;
                for (int k = -radius; k <= radius; k++) { int xx = Mathf.Clamp(x + k, 0, w - 1); sum += c[y * w + xx]; n++; }
                tmp[y * w + x] = sum / n;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear; int n = 0;
                for (int k = -radius; k <= radius; k++) { int yy = Mathf.Clamp(y + k, 0, h - 1); sum += tmp[yy * w + x]; n++; }
                c[y * w + x] = sum / n;
            }
    }

    // ───────────── construcción de elementos ─────────────

    public static GameObject Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Coloca un rect en píxeles del lienzo, medidos desde la esquina superior izquierda del padre.</summary>
    public static RectTransform Place(GameObject go, float x, float y, float w, float h)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    public static void Stretch(GameObject go)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static Image AddImage(GameObject go, Sprite sprite, Color color, bool raycast = false)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TMP_Text AddText(GameObject go, string value, Face face, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        Style(text, face, size * TextScale, color);
        // Letra más grande para leer en el visor; si algún texto no cabe en su caja, vuelve a su tamaño original.
        text.enableAutoSizing = true;
        text.fontSizeMax = size * TextScale;
        text.fontSizeMin = size;
        text.text = value;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// Hoja de papel con sombra y cinta. El objeto devuelto es la hoja (su hijo «Contenido» no existe:
    /// se añade todo directamente sobre la hoja en coordenadas desde su esquina superior izquierda).
    /// </summary>
    public static GameObject Sheet(Transform parent, string name, float x, float y, float w, float h)
    {
        GameObject shade = Node(parent, name + "Sombra");
        Place(shade, x - 22f, y - 4f, w + 44f, h + 52f);
        AddImage(shade, Shadow, new Color(0f, 0f, 0f, .55f));

        GameObject sheet = Node(parent, name);
        Place(sheet, x, y, w, h);
        AddImage(sheet, PaperSheet, Paper, true);

        GameObject tapeGo = Node(sheet.transform, "Cinta");
        RectTransform tapeRect = Place(tapeGo, w / 2f - 70f, -14f, 140f, 32f);
        tapeRect.localEulerAngles = new Vector3(0f, 0f, 1.5f);
        AddImage(tapeGo, Tape, new Color(Sage.r, Sage.g, Sage.b, .6f));
        return sheet;
    }

    /// <summary>Botón con el estilo del papel. Devuelve la etiqueta para poder cambiar su texto.</summary>
    public static TMP_Text Button(Transform parent, string name, string label, ButtonKind kind, float fontSize,
        UnityEngine.Events.UnityAction action, out Button button)
    {
        GameObject go = Node(parent, name);
        Color fill = kind == ButtonKind.Primary ? SageDeep : PaperLight;
        Image background = AddImage(go, Solid, Color.white, true);
        button = go.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = kind == ButtonKind.Primary ? SageDeepDark : Hex(0xDCEBE3);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = kind == ButtonKind.Primary ? Hex(0x1D4A3E) : Hex(0xC7DDD2);
        colors.disabledColor = new Color(fill.r, fill.g, fill.b, .45f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        if (action != null) button.onClick.AddListener(action);

        if (kind != ButtonKind.Primary)
        {
            GameObject border = Node(go.transform, "Borde");
            Stretch(border);
            Color borderColor = kind == ButtonKind.Alert ? AmberInk : kind == ButtonKind.Key ? new Color(Ink.r, Ink.g, Ink.b, .4f) : Ink;
            AddImage(border, Frame, borderColor).fillCenter = false;
        }

        GameObject textGo = Node(go.transform, "Texto");
        Stretch(textGo);
        Color textColor = kind == ButtonKind.Primary ? OnAccent : kind == ButtonKind.Alert ? AmberInk : Ink;
        TMP_Text text = AddText(textGo, label, Face.TypeBold, fontSize, textColor, TextAlignmentOptions.Center);
        text.margin = new Vector4(14f, 0f, 14f, 0f);
        return text;
    }

    /// <summary>Etiqueta «SOLO DESARROLLO» para funciones que solo existen en Editor y builds de desarrollo.</summary>
    public static GameObject DevTag(Transform parent, float x, float centerY)
    {
        GameObject tag = Node(parent, "SoloDesarrollo");
        Place(tag, x, centerY - 13f, 168f, 26f);
        AddImage(tag, Solid, Sepia);
        GameObject t = Node(tag.transform, "Texto");
        Stretch(t);
        TMP_Text text = AddText(t, "SOLO DESARROLLO", Face.Type, 14f, Ink, TextAlignmentOptions.Center);
        text.characterSpacing = 8f;
        return tag;
    }

    public static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
