using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dvor
{
    /// <summary>Материалы из сгенерированных поверхностей: альбедо плюс карта нормалей.</summary>
    public static class MatLib
    {
        private static readonly Dictionary<string, Material> Cache = new();
        private static readonly Dictionary<string, Surface> Surf = new();
        private static Shader _shader;

        /// <summary>
        /// Шейдер выбирается по реально включённому конвейеру рендера.
        /// Шейдер URP во встроенном конвейере красит всё в розовый.
        /// </summary>
        private static Shader Lit
        {
            get
            {
                if (_shader != null) return _shader;

                bool srp = GraphicsSettings.currentRenderPipeline != null;
                if (srp)
                    _shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit");

                if (_shader == null)
                    _shader = Shader.Find("Standard")
                           ?? Shader.Find("Legacy Shaders/Diffuse")
                           ?? Shader.Find("Diffuse");

                if (_shader == null)
                {
                    Debug.LogError("[Двор] Не нашёл пригодного шейдера — всё будет розовым");
                    _shader = Shader.Find("Sprites/Default");
                }
                else Debug.Log($"[Двор] Рендер: {(srp ? "SRP" : "встроенный")}, шейдер «{_shader.name}»");

                return _shader;
            }
        }

        private static Surface S(string key, System.Func<Surface> make)
        {
            if (Surf.TryGetValue(key, out var s) && s.Albedo != null) return s;
            s = make();
            Surf[key] = s;
            return s;
        }

        public static Material Get(string kind, Vector2 tiling, float smoothness = 0.10f,
                                   float metallic = 0f, float bump = 1f)
        {
            string key = $"{kind}|{tiling.x:0.##}x{tiling.y:0.##}|{smoothness:0.##}|{bump:0.##}";
            if (Cache.TryGetValue(key, out var m) && m != null) return m;

            var surf = SurfaceFor(kind);
            m = new Material(Lit) { name = key };

            SetTex(m, "_BaseMap", surf.Albedo, tiling);
            SetTex(m, "_MainTex", surf.Albedo, tiling);

            if (surf.Normal != null)
            {
                SetTex(m, "_BumpMap", surf.Normal, tiling);
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }

            if (m.HasProperty("_BaseColor"))  m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color"))      m.SetColor("_Color", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", metallic);

            m.mainTextureScale = tiling;
            Cache[key] = m;
            return m;
        }

        private static void SetTex(Material m, string prop, Texture2D t, Vector2 tiling)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, t);
            m.SetTextureScale(prop, tiling);
        }

        /// <summary>
        /// Сначала ищем настоящую текстуру в Resources/Textures, и только если её
        /// нет — рисуем процедурную. Положил файлы в папку — игра стала красивее,
        /// ни строчки кода менять не надо.
        /// </summary>
        private static Surface SurfaceFor(string kind)
        {
            if (Surf.TryGetValue(kind, out var cached) && cached.Albedo != null) return cached;

            // Листва, стекло и крашеный ларёк остаются рисованными: фотоматериал
            // на них выглядит хуже, чем стилизованный.
            if (kind is "foliage" or "glass" or "kiosk") return Procedural(kind);

            var color = Resources.Load<Texture2D>($"Textures/{kind}_color");

            // стена с окнами: фото бетона окон не содержит, поэтому дорисовываем
            if (kind is "panel_a" or "panel_b")
            {
                var basis = color ?? Resources.Load<Texture2D>("Textures/panel_b_color")
                                  ?? Resources.Load<Texture2D>("Textures/panel_a_color");
                if (basis != null && basis.isReadable)
                {
                    int seed = kind == "panel_a" ? 3 : 12;
                    float lit = kind == "panel_a" ? 0.07f : 0.10f;
                    var built = TexGen.PanelWallOver(basis, seed, lit);
                    Surf[kind] = built;
                    Debug.Log($"[Двор] Фасад «{kind}»: бетон из файла + окна кодом");
                    return built;
                }
                if (basis != null)
                    Debug.LogWarning($"[Двор] «{kind}»: текстура не помечена Read/Write, рисую процедурно");
                return Procedural(kind);
            }

            if (color != null)
            {
                var loaded = new Surface
                {
                    Albedo = color,
                    Normal = Resources.Load<Texture2D>($"Textures/{kind}_normal")
                };
                Surf[kind] = loaded;
                Debug.Log($"[Двор] Материал «{kind}» взят из файла" +
                          (loaded.Normal != null ? " (с картой нормалей)" : " (без нормалей)"));
                return loaded;
            }

            return Procedural(kind);
        }

        private static Surface Procedural(string kind) => kind switch
        {
            // Палитра приглушённая: пасмурный день съедает насыщенность,
            // а яркие цвета мгновенно ломают вайб позднесоветского двора.
            "panel_a"   => S(kind, () => TexGen.PanelWall(new Color(0.70f, 0.67f, 0.61f), 3, 0.07f)),
            "panel_b"   => S(kind, () => TexGen.PanelWall(new Color(0.62f, 0.60f, 0.57f), 12, 0.10f)),
            "panel_end" => S(kind, () => TexGen.Concrete(new Color(0.64f, 0.62f, 0.58f), 17)),
            "concrete"  => S(kind, () => TexGen.Concrete(new Color(0.56f, 0.56f, 0.54f), 21)),
            "concrete_d"=> S(kind, () => TexGen.Concrete(new Color(0.40f, 0.40f, 0.39f), 29)),
            "asphalt"   => S(kind, () => TexGen.Asphalt()),
            "dirt"      => S(kind, () => TexGen.Dirt()),
            "grass"     => S(kind, () => TexGen.Grass()),
            "garage"    => S(kind, () => TexGen.Ribbed(new Color(0.26f, 0.31f, 0.42f), 23)),
            "garage_2"  => S(kind, () => TexGen.Ribbed(new Color(0.35f, 0.37f, 0.33f), 44)),
            "brick"     => S(kind, () => TexGen.Brick()),
            "wood"      => S(kind, () => TexGen.Wood(new Color(0.42f, 0.31f, 0.19f))),
            "wood_blue" => S(kind, () => TexGen.Wood(new Color(0.26f, 0.35f, 0.40f), 47)),
            "tarp"      => S(kind, () => TexGen.Stripes(new Color(0.66f, 0.62f, 0.32f), new Color(0.60f, 0.31f, 0.28f))),
            "tarp_2"    => S(kind, () => TexGen.Stripes(new Color(0.30f, 0.44f, 0.58f), new Color(0.74f, 0.74f, 0.72f))),
            "foliage"   => S(kind, () => TexGen.Foliage(new Color(0.21f, 0.33f, 0.17f))),
            "kiosk"     => S(kind, () => TexGen.Flat(new Color(0.16f, 0.47f, 0.50f), 0.10f)),
            "rust"      => S(kind, () => TexGen.Ribbed(new Color(0.42f, 0.27f, 0.17f), 91)),
            "glass"     => S(kind, () => TexGen.Flat(new Color(0.30f, 0.38f, 0.42f), 0.05f)),
            _           => S("flat_" + kind, () => TexGen.Flat(Color.grey))
        };

        public static Material Solid(Color c, float smoothness = 0.1f, float metallic = 0f)
        {
            string key = $"solid|{ColorUtility.ToHtmlStringRGB(c)}|{smoothness:0.##}|{metallic:0.##}";
            if (Cache.TryGetValue(key, out var m) && m != null) return m;

            m = new Material(Lit) { name = key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic"))  m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }
    }
}
