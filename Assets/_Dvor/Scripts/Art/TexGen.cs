using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Текстуры рисуются кодом: альбедо и карта нормалей из одной и той же
    /// карты высот. Нормали — главное, что отличает «плоскую заливку»
    /// от поверхности: кирпич начинает ловить свет рёбрами, гофра гаража бликует.
    /// </summary>
    public struct Surface
    {
        public Texture2D Albedo;
        public Texture2D Normal;
    }

    public static class TexGen
    {
        /// <summary>
        /// Сторона генерируемой текстуры. Фасады делаются вдвое крупнее:
        /// на них приходится больше всего экрана, и мыло там заметнее всего.
        /// </summary>
        private static int S = 256;

        // ---------------------------------------------------------- шум

        private static float Hash(int x, int y, int seed)
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695040;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        private static float Value(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, seed),     b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Fbm(float x, float y, int seed, int oct = 4)
        {
            float sum = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++) { sum += Value(x * f, y * f, seed + i) * amp; amp *= 0.5f; f *= 2f; }
            return sum;
        }

        // ---------------------------------------------------------- сборка

        private static Texture2D Tex(string name, Color[] px, bool linear = false)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true, linear) { name = name };
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Карта нормалей из высот. Кодировка DXT5nm — та же, что делает
        /// импортёр Unity на ПК: X лежит в альфе, Y в зелёном.
        /// </summary>
        private static Texture2D NormalFrom(string name, float[] h, float strength)
        {
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int xl = (x - 1 + S) % S, xr = (x + 1) % S;
                    int yd = (y - 1 + S) % S, yu = (y + 1) % S;

                    float dx = (h[y * S + xr] - h[y * S + xl]) * strength;
                    float dy = (h[yu * S + x] - h[yd * S + x]) * strength;

                    var n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * S + x] = new Color(1f, n.y * 0.5f + 0.5f, 1f, n.x * 0.5f + 0.5f);
                }
            return Tex(name, px, linear: true);
        }

        private static Surface Make(string name, Color[] albedo, float[] height, float bump)
            => new() { Albedo = Tex(name, albedo), Normal = NormalFrom(name + "_n", height, bump) };

        private static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, 1f);

        // ---------------------------------------------------------- поверхности

        public static Surface Concrete(Color tint, int seed = 7)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(x / 26f, y / 26f, seed) * 0.55f + Fbm(x / 6f, y / 6f, seed + 9) * 0.45f;
                    float pit = Hash(x, y, seed + 2) > 0.985f ? -0.4f : 0f;
                    float k = 0.74f + n * 0.5f;
                    if (Fbm(x / 90f, y / 9f, seed + 30) > 0.72f) k *= 0.86f;
                    px[y * S + x] = Shade(tint, k + pit * 0.3f);
                    h[y * S + x] = n + pit;
                }
            return Make("concrete", px, h, 2.2f);
        }

        public static Surface Asphalt(int seed = 11)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            var baseC = new Color(0.25f, 0.25f, 0.27f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float grain = Hash(x, y, seed) * 0.3f + Fbm(x / 10f, y / 10f, seed) * 0.4f;
                    float k = 0.70f + grain;
                    float crack = Mathf.Abs(Fbm(x / 34f, y / 34f, seed + 5) - 0.5f);
                    float cut = crack < 0.02f ? 1f : 0f;
                    if (cut > 0f) k *= 0.5f;
                    px[y * S + x] = Shade(baseC, k);
                    h[y * S + x] = grain - cut * 0.9f;
                }
            return Make("asphalt", px, h, 2.6f);
        }

        public static Surface Dirt(int seed = 19)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            var earth = new Color(0.32f, 0.29f, 0.22f);
            var grass = new Color(0.24f, 0.34f, 0.17f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float m = Fbm(x / 30f, y / 30f, seed);
                    float fine = Fbm(x / 4f, y / 4f, seed + 7);
                    var c = Color.Lerp(earth, grass, Mathf.SmoothStep(0.40f, 0.62f, m));
                    px[y * S + x] = Shade(c, 0.78f + fine * 0.4f);
                    h[y * S + x] = m * 0.5f + fine * 0.5f;
                }
            return Make("dirt", px, h, 1.6f);
        }

        public static Surface PanelWall(Color tint, int seed = 3, float litChance = 0.22f)
        {
            int prev = S; S = 512;
            try { return BuildPanel(tint, seed, litChance); }
            finally { S = prev; }
        }

        private static Surface BuildPanel(Color tint, int seed, float litChance)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            int cell = S / 2;
            var frame = new Color(0.84f, 0.82f, 0.76f);
            var glassDark = new Color(0.13f, 0.16f, 0.21f);
            var glassLit  = new Color(0.99f, 0.87f, 0.56f);

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Fbm(x / 22f, y / 22f, seed) * 0.5f + Fbm(x / 5f, y / 5f, seed + 4) * 0.5f;
                    var c = Shade(tint, 0.76f + n * 0.45f);
                    float hh = n * 0.35f;

                    int cx = x / cell, cy = y / cell;
                    int lx = x % cell, ly = y % cell;

                    if (lx < 3 || ly < 3) { c = Shade(c, 0.62f); hh -= 0.9f; }   // шов между плитами

                    // Пропорции окна хрущёвки: ~1.35 м в ширину, ~1.5 м в высоту,
                    // подоконник на 0.85 м от пола. В клетке 3.1 x 2.8 м это 42% и 54%.
                    int wx0 = Mathf.RoundToInt(cell * 0.29f), wx1 = Mathf.RoundToInt(cell * 0.71f);
                    int wy0 = Mathf.RoundToInt(cell * 0.30f), wy1 = Mathf.RoundToInt(cell * 0.84f);
                    int fr  = Mathf.Max(2, cell / 42);

                    // ржавые потёки вниз от подоконника
                    if (ly < wy0 && lx > wx0 - fr * 2 && lx < wx1 + fr * 2)
                    {
                        float run = Fbm(x / 3.5f, y / 50f, seed + 60);
                        float below = (wy0 - ly) / (float)Mathf.Max(1, wy0);
                        float amount = Mathf.Clamp01((run - 0.42f) * 2.4f) * (1f - below * 0.55f);
                        c = Color.Lerp(c, new Color(0.30f, 0.25f, 0.19f), amount * 0.6f);
                    }

                    // подоконник — светлая полка, её видно на любом фасаде
                    if (ly >= wy0 - fr * 2 && ly < wy0 && lx > wx0 - fr * 2 && lx < wx1 + fr * 2)
                    { c = Shade(frame, 1.06f); hh = 0.7f; }

                    if (lx > wx0 && lx < wx1 && ly > wy0 && ly < wy1)
                    {
                        bool border = lx < wx0 + fr || lx > wx1 - fr || ly < wy0 + fr || ly > wy1 - fr;
                        if (border) { c = frame; hh = 0.35f; }
                        else
                        {
                            bool lit = Hash(cx, cy, seed + 77) < litChance;
                            float sh = 1f - (ly - wy0) / (float)(wy1 - wy0) * 0.30f;
                            c = Shade(lit ? glassLit : glassDark, sh);
                            hh = -0.8f;
                            // переплёт: вертикаль посередине и форточка сверху
                            if (Mathf.Abs(lx - cell / 2) < fr) { c = Shade(frame, 0.92f); hh = 0.2f; }
                            if (Mathf.Abs(ly - (wy0 + (wy1 - wy0) * 3 / 4)) < fr) { c = Shade(frame, 0.92f); hh = 0.2f; }
                        }
                    }

                    px[y * S + x] = c;
                    h[y * S + x] = hh;
                }
            return Make("panel", px, h, 2.4f);
        }

        /// <summary>
        /// Фасад поверх настоящего бетона: основа берётся из скачанной текстуры,
        /// а окна, швы и потёки дорисовываются кодом.
        ///
        /// Фотографии бетона окон не содержат — если поставить такую текстуру
        /// на дом как есть, получится глухая стена. Композит даёт и зерно
        /// настоящего материала, и окна, попадающие на линии этажей.
        /// </summary>
        public static Surface PanelWallOver(Texture2D baseTex, int seed = 3, float litChance = 0.08f)
        {
            int prev = S; S = 512;
            try { return BuildPanelOver(baseTex, seed, litChance); }
            finally { S = prev; }
        }

        private static Surface BuildPanelOver(Texture2D baseTex, int seed, float litChance)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            var frame = new Color(0.80f, 0.78f, 0.73f);
            var glassDark = new Color(0.12f, 0.15f, 0.19f);
            var glassLit  = new Color(0.95f, 0.84f, 0.54f);
            int cell = S / 2;

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // основа — скачанный бетон, растянутый на плитку
                    var c = baseTex.GetPixelBilinear(x / (float)S, y / (float)S);
                    float hh = (c.r + c.g + c.b) * 0.33f - 0.5f;

                    int cx = x / cell, cy = y / cell;
                    int lx = x % cell, ly = y % cell;

                    if (lx < 3 || ly < 3) { c = Shade(c, 0.62f); hh -= 0.9f; }

                    // Пропорции окна хрущёвки: ~1.35 м в ширину, ~1.5 м в высоту,
                    // подоконник на 0.85 м от пола. В клетке 3.1 x 2.8 м это 42% и 54%.
                    int wx0 = Mathf.RoundToInt(cell * 0.29f), wx1 = Mathf.RoundToInt(cell * 0.71f);
                    int wy0 = Mathf.RoundToInt(cell * 0.30f), wy1 = Mathf.RoundToInt(cell * 0.84f);
                    int fr  = Mathf.Max(2, cell / 42);

                    // ржавые потёки вниз от подоконника
                    if (ly < wy0 && lx > wx0 - fr * 2 && lx < wx1 + fr * 2)
                    {
                        float run = Fbm(x / 3.5f, y / 50f, seed + 60);
                        float below = (wy0 - ly) / (float)Mathf.Max(1, wy0);
                        float amount = Mathf.Clamp01((run - 0.42f) * 2.4f) * (1f - below * 0.55f);
                        c = Color.Lerp(c, new Color(0.30f, 0.25f, 0.19f), amount * 0.6f);
                    }

                    // подоконник — светлая полка, её видно на любом фасаде
                    if (ly >= wy0 - fr * 2 && ly < wy0 && lx > wx0 - fr * 2 && lx < wx1 + fr * 2)
                    { c = Shade(frame, 1.06f); hh = 0.7f; }

                    if (lx > wx0 && lx < wx1 && ly > wy0 && ly < wy1)
                    {
                        bool border = lx < wx0 + fr || lx > wx1 - fr || ly < wy0 + fr || ly > wy1 - fr;
                        if (border) { c = frame; hh = 0.35f; }
                        else
                        {
                            bool lit = Hash(cx, cy, seed + 77) < litChance;
                            float sh = 1f - (ly - wy0) / (float)(wy1 - wy0) * 0.30f;
                            c = Shade(lit ? glassLit : glassDark, sh);
                            hh = -0.8f;
                            // переплёт: вертикаль посередине и форточка сверху
                            if (Mathf.Abs(lx - cell / 2) < fr) { c = Shade(frame, 0.92f); hh = 0.2f; }
                            if (Mathf.Abs(ly - (wy0 + (wy1 - wy0) * 3 / 4)) < fr) { c = Shade(frame, 0.92f); hh = 0.2f; }
                        }
                    }

                    px[y * S + x] = c;
                    h[y * S + x] = hh;
                }

            return Make("panel_over", px, h, 2.4f);
        }

        public static Surface Ribbed(Color tint, int seed = 23)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float rib = Mathf.Sin(x / (float)S * Mathf.PI * 16f) * 0.5f + 0.5f;
                    float k = 0.60f + rib * 0.45f;
                    var c = tint;
                    float rust = Fbm(x / 28f, y / 28f, seed);
                    if (rust > 0.58f) c = Color.Lerp(tint, new Color(0.45f, 0.24f, 0.12f), (rust - 0.58f) * 2.4f);
                    k *= 0.92f + Hash(x, y, seed) * 0.16f;
                    px[y * S + x] = Shade(c, k);
                    h[y * S + x] = rib + (rust > 0.58f ? -0.2f : 0f);
                }
            return Make("ribbed", px, h, 3.2f);
        }

        public static Surface Brick(int seed = 31)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            var brick = new Color(0.53f, 0.30f, 0.23f);
            var mortar = new Color(0.70f, 0.68f, 0.64f);
            int bh = S / 8, bw = S / 4;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int row = y / bh;
                    int xoff = (row % 2 == 0) ? 0 : bw / 2;
                    int lx = (x + xoff) % bw, ly = y % bh;
                    Color c; float hh;
                    if (lx < 4 || ly < 4) { c = mortar; hh = -0.8f; }
                    else
                    {
                        float v = 0.80f + Hash(x / bw, row, seed) * 0.38f + Fbm(x / 8f, y / 8f, seed) * 0.15f;
                        c = Shade(brick, v);
                        hh = 0.45f + Fbm(x / 8f, y / 8f, seed) * 0.2f;
                    }
                    px[y * S + x] = c;
                    h[y * S + x] = hh;
                }
            return Make("brick", px, h, 3.0f);
        }

        public static Surface Wood(Color tint, int seed = 41)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float grain = Mathf.Sin(y / 3.5f + Fbm(x / 40f, y / 12f, seed) * 9f) * 0.5f + 0.5f;
                    float k = 0.70f + grain * 0.35f + Hash(x, y, seed) * 0.06f;
                    float hh = grain * 0.5f;
                    if (x % (S / 4) < 2) { k *= 0.65f; hh = -0.8f; }
                    px[y * S + x] = Shade(tint, k);
                    h[y * S + x] = hh;
                }
            return Make("wood", px, h, 1.8f);
        }

        public static Surface Stripes(Color a, Color b, int width = 32)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var c = ((x / width) % 2 == 0) ? a : b;
                    float wrinkle = Fbm(x / 12f, y / 30f, 55);
                    px[y * S + x] = Shade(c, 0.84f + wrinkle * 0.3f);
                    h[y * S + x] = wrinkle;
                }
            return Make("stripes", px, h, 1.4f);
        }

        public static Surface Flat(Color c, float noise = 0.12f, int seed = 61)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Hash(x, y, seed);
                    px[y * S + x] = Shade(c, 1f - noise * 0.5f + n * noise);
                    h[y * S + x] = n * 0.2f;
                }
            return Make("flat", px, h, 0.8f);
        }

        /// <summary>Дворовая трава: клочковатая, с проплешинами — не газон.</summary>
        public static Surface Grass(int seed = 83)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            var green = new Color(0.22f, 0.32f, 0.16f);
            var dry   = new Color(0.37f, 0.35f, 0.22f);
            var bald  = new Color(0.29f, 0.26f, 0.20f);

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float patch = Fbm(x / 34f, y / 34f, seed);
                    float blade = Fbm(x / 2.5f, y / 5f, seed + 11);
                    var c = Color.Lerp(green, dry, Mathf.SmoothStep(0.45f, 0.75f, patch));
                    if (patch < 0.32f) c = Color.Lerp(bald, c, patch / 0.32f);   // вытоптано
                    px[y * S + x] = Shade(c, 0.72f + blade * 0.55f);
                    h[y * S + x] = blade * 0.8f + patch * 0.2f;
                }
            return Make("grass", px, h, 2.0f);
        }

        public static Surface Foliage(Color c, int seed = 71)
        {
            var px = new Color[S * S]; var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float leaf = Fbm(x / 7f, y / 7f, seed) * 0.6f + Hash(x, y, seed) * 0.4f;
                    px[y * S + x] = Shade(c, 0.58f + leaf * 0.8f);
                    h[y * S + x] = leaf;
                }
            return Make("foliage", px, h, 2.0f);
        }
    }
}
