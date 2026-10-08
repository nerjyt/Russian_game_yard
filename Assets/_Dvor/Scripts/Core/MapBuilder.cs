using System.Collections.Generic;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Карта строится кодом, одинаково у всех — синхронизировать её не нужно.
    ///
    /// Планировка П-образная, как в настоящем квартале: два длинных дома с севера
    /// и юга, торцевой дом замыкает двор с запада, восток открыт. Всё стоит по
    /// одной сетке и привязано к дороге — иначе объекты выглядят раскиданными.
    ///
    /// Свет пасмурный: солнца нет, тени мягкие, туман густой. Это и есть вайб
    /// позднесоветского двора, а не текстуры.
    /// </summary>
    public static class MapBuilder
    {
        // ---- ключевые точки, на которые ссылается игровая логика ----
        public static Vector3 VanPoint    = new(46f, 1f, 38f);
        public static Vector3 BanishPoint = new(44f, 1f, 6f);
        public static Vector3 GazeboPoint = new(16f, 0f, -6f);

        /// <summary>Цвет пасмурной мути. Им же красится фон камеры.</summary>
        public static Color Overcast = new(0.60f, 0.62f, 0.63f);

        private static readonly List<Vector3> Spawns = new();
        private static Transform _root;

        public static Vector3 SpawnPoint(int i) =>
            Spawns.Count == 0 ? new Vector3(-10f, 1.2f, 0f) : Spawns[Mathf.Abs(i) % Spawns.Count];

        public static void Build()
        {
            if (_root != null) return;
            _root = new GameObject("Карта").transform;
            Spawns.Clear();
            SurfaceMap.Clear();

            Sky();
            Ground();
            Street();
            HouseNorth();
            HouseSouth();
            HouseWest();
            Yard();
            Kiosk();
            Garages();
            Dump();
            Construction();
            Perimeter();

            // спавн вдоль центральной дорожки, а не кольцом в пустоте
            for (int i = 0; i < 12; i++)
                Spawns.Add(new Vector3(-46f + i * 8f, 1.3f, (i % 2 == 0) ? -3f : 3f));
        }

        /// <summary>Фон камеры вместо неба — так получается ровная пасмурная муть.</summary>
        public static void ApplySky(Camera cam)
        {
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Overcast;
            cam.farClipPlane = 400f;
        }

        // ============================================================ примитивы

        private static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Box(string name, Vector3 pos, Vector3 size, Color c, bool collider = true)
            => Box(name, pos, size, MatLib.Solid(c), collider);

        /// <summary>
        /// Стена с текстурой. Для панелей шаг подобран так, чтобы ряд окон
        /// приходился ровно на этаж — иначе окна из текстуры и балконы из
        /// геометрии живут на разной высоте, и фасад выглядит кашей.
        /// </summary>
        private static GameObject Wall(string name, Vector3 pos, Vector3 size, string kind, float unitsPerTile = 0f)
        {
            Vector2 tiling;
            if (unitsPerTile > 0f)
                tiling = new Vector2(Mathf.Max(1f, size.x / unitsPerTile), Mathf.Max(1f, size.y / unitsPerTile));
            else
                tiling = new Vector2(Mathf.Max(1f, Mathf.Round(size.x / 6.4f)),
                                     Mathf.Max(1f, Mathf.Round(size.y / (FloorHeight * 2f))));
            return Box(name, pos, size, MatLib.Get(kind, tiling));
        }

        /// <summary>Явный тайлинг: нужен там, где длинная сторона дома идёт по Z.</summary>
        private static GameObject WallTiled(string name, Vector3 pos, Vector3 size, string kind, Vector2 tiling)
            => Box(name, pos, size, MatLib.Get(kind, tiling));

        /// <summary>Высота этажа. От неё пляшут окна, пояса и балконы.</summary>
        private const float FloorHeight = 2.8f;

        private static GameObject Ball(string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cyl(string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static void Spot(string tag, Vector3 pos, float radius = 3f)
        {
            var go = new GameObject($"Точка_{tag}");
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var s = go.AddComponent<DeedSpot>();
            s.Tag = tag;
            s.UseRadius = radius;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "маркер";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localScale = new Vector3(radius * 0.55f, 0.015f, radius * 0.55f);
            marker.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            marker.GetComponent<Renderer>().sharedMaterial = MatLib.Solid(new Color(0.95f, 0.72f, 0.20f), 0.5f);
            Object.Destroy(marker.GetComponent<Collider>());
            s.Marker = marker.GetComponent<Renderer>();
            s.Marker.enabled = false;
        }

        private static void Pavement(string name, Vector3 center, float sx, float sz, string kind,
                                     SurfaceMap.Kind surface, float tileScale = 4f)
        {
            Box(name, center + Vector3.up * 0.06f, new Vector3(sx, 0.06f, sz),
                MatLib.Get(kind, new Vector2(sx / tileScale, sz / tileScale)), false);
            SurfaceMap.Add(center, sx, sz, surface);
        }

        // ============================================================ атмосфера

        private static void Sky()
        {
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.pixelLightCount = 4;

            // неба как такового нет — ровная пасмурная муть, в которую уходит туман
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.50f, 0.52f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.40f, 0.40f);
            RenderSettings.ambientGroundColor  = new Color(0.19f, 0.18f, 0.17f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Overcast;
            RenderSettings.fogDensity = 0.0095f;   // край карты тонет в мути

            // в пасмурный день солнца нет: свет рассеянный, тени еле намечены
            var sun = new GameObject("Пасмурный свет");
            sun.transform.SetParent(_root, false);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 0.72f;
            l.color = new Color(0.92f, 0.93f, 0.97f);
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.34f;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.5f;
            sun.transform.rotation = Quaternion.Euler(58f, 30f, 0f);
            RenderSettings.sun = l;

            var fill = new GameObject("Подсветка");
            fill.transform.SetParent(_root, false);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.intensity = 0.22f;
            fl.color = new Color(0.72f, 0.76f, 0.85f);
            fl.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, -150f, 0f);

            ApplySky(Camera.main);
        }

        private static void Ground()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Земля";
            g.transform.SetParent(_root, false);
            g.transform.localScale = new Vector3(34f, 1f, 30f);
            g.GetComponent<Renderer>().sharedMaterial = MatLib.Get("grass", new Vector2(85f, 75f));
        }

        // ============================================================ улица

        private static void Street()
        {
            // проезжая часть вдоль северной стороны квартала
            Pavement("Дорога", new Vector3(0f, 0f, 40f), 260f, 11f, "asphalt", SurfaceMap.Kind.Asphalt, 6f);
            Box("Разделительная", new Vector3(0f, 0.11f, 40f), new Vector3(260f, 0.02f, 0.25f),
                MatLib.Solid(new Color(0.72f, 0.70f, 0.62f)), false);

            // тротуар между дорогой и домом
            Pavement("Тротуар", new Vector3(0f, 0f, 32.5f), 260f, 4f, "concrete", SurfaceMap.Kind.Concrete, 3f);
            Curb(new Vector3(0f, 0f, 34.6f), 260f);

            // второй проезд на восток, к гаражам и вглубь квартала
            Pavement("Проезд восточный", new Vector3(56f, 0f, -10f), 9f, 110f, "asphalt", SurfaceMap.Kind.Asphalt, 6f);

            // столбы с проводами вдоль дороги — вертикаль, которой не хватает
            for (float x = -104f; x <= 104f; x += 26f) Pole(new Vector3(x, 0f, 46f));

            Bus(new Vector3(24f, 0f, 46f));

            // УАЗ стоит у въезда во двор, а не посреди пустыря
            var van = MatLib.Solid(new Color(0.27f, 0.32f, 0.25f));
            Box("УАЗ кузов",   VanPoint + new Vector3(0f, 0.55f, 0f), new Vector3(5.4f, 1.5f, 2.3f), van);
            Box("УАЗ кабина",  VanPoint + new Vector3(-1.6f, 1.75f, 0f), new Vector3(2.2f, 1.1f, 2.2f), van);
            Box("УАЗ будка",   VanPoint + new Vector3(1.2f, 1.9f, 0f), new Vector3(3f, 1.4f, 2.2f),
                MatLib.Solid(new Color(0.23f, 0.27f, 0.21f)));
            Box("УАЗ мигалка", VanPoint + new Vector3(-1.6f, 2.45f, 0f), new Vector3(0.6f, 0.25f, 0.4f),
                MatLib.Solid(new Color(0.62f, 0.14f, 0.11f)));
            Wheels(VanPoint, 2.0f, 1.15f);
        }

        private static void Curb(Vector3 at, float length)
        {
            Box("Бордюр", at + Vector3.up * 0.12f, new Vector3(length, 0.24f, 0.32f),
                MatLib.Get("concrete", new Vector2(length / 2f, 1f)), false);
        }

        private static void Pole(Vector3 at)
        {
            var m = MatLib.Solid(new Color(0.46f, 0.45f, 0.43f), 0.15f);
            Cyl("Столб", at + Vector3.up * 4.5f, new Vector3(0.28f, 4.5f, 0.28f), m);
            Box("Траверса", at + new Vector3(0f, 8.6f, 0f), new Vector3(1.8f, 0.12f, 0.12f), m, false);
            Box("Кронштейн", at + new Vector3(-1.1f, 8.2f, 0f), new Vector3(2.2f, 0.1f, 0.1f), m, false);
            Box("Плафон", at + new Vector3(-2f, 8.05f, 0f), new Vector3(0.75f, 0.28f, 0.5f),
                MatLib.Solid(new Color(0.78f, 0.78f, 0.74f)), false);
            // провод к следующему столбу
            Box("Провод", at + new Vector3(13f, 8.45f, 0f), new Vector3(26f, 0.05f, 0.05f),
                MatLib.Solid(new Color(0.14f, 0.14f, 0.14f)), false);
        }

        private static void Bus(Vector3 at)
        {
            Box("Остановка крыша",  at + new Vector3(0f, 3.0f, 0f), new Vector3(10f, 0.22f, 4f),
                MatLib.Solid(new Color(0.28f, 0.33f, 0.34f)));
            Box("Остановка задняя", at + new Vector3(0f, 1.6f, 1.9f), new Vector3(10f, 2.9f, 0.2f),
                MatLib.Get("rust", new Vector2(5f, 2f)));
            Box("Остановка стойка л", at + new Vector3(-4.8f, 1.5f, -1.8f), new Vector3(0.16f, 3f, 0.16f),
                MatLib.Solid(new Color(0.33f, 0.35f, 0.36f)));
            Box("Остановка стойка п", at + new Vector3(4.8f, 1.5f, -1.8f), new Vector3(0.16f, 3f, 0.16f),
                MatLib.Solid(new Color(0.33f, 0.35f, 0.36f)));
            Box("Скамья остановки", at + new Vector3(0f, 0.5f, 1.3f), new Vector3(7f, 0.12f, 0.6f),
                MatLib.Get("wood", new Vector2(4f, 1f)));
            Box("Расписание", at + new Vector3(3.4f, 1.9f, 1.75f), new Vector3(1.2f, 0.9f, 0.1f),
                MatLib.Solid(new Color(0.80f, 0.79f, 0.72f)), false);

            Box("Таксофон корпус", at + new Vector3(-7f, 1.2f, 0f), new Vector3(0.7f, 1.1f, 0.5f),
                MatLib.Solid(new Color(0.62f, 0.54f, 0.18f)));
            Box("Таксофон стойка", at + new Vector3(-7f, 0.4f, 0f), new Vector3(0.18f, 0.8f, 0.18f),
                MatLib.Solid(new Color(0.32f, 0.32f, 0.32f)));
            Box("Таксофон козырёк", at + new Vector3(-7f, 1.85f, 0f), new Vector3(0.9f, 0.25f, 0.7f),
                MatLib.Solid(new Color(0.27f, 0.27f, 0.29f)));
            Spot("phone", at + new Vector3(-7f, 0f, -2.2f), 3f);

            Urn(at + new Vector3(5.6f, 0f, 1.2f));
        }

        private static void Urn(Vector3 at)
        {
            var m = MatLib.Get("rust", new Vector2(1f, 1f));
            Cyl("Урна", at + Vector3.up * 0.75f, new Vector3(0.42f, 0.3f, 0.42f), m);
            Cyl("Урна нога", at + Vector3.up * 0.22f, new Vector3(0.1f, 0.22f, 0.1f),
                MatLib.Solid(new Color(0.3f, 0.3f, 0.3f)), false);
        }

        private static void Wheels(Vector3 at, float dx, float dz)
        {
            var m = MatLib.Solid(new Color(0.08f, 0.08f, 0.09f));
            for (int w = 0; w < 4; w++)
            {
                var p = at + new Vector3(w < 2 ? -dx : dx, 0.33f, (w % 2 == 0) ? -dz : dz);
                var c = Cyl($"Колесо {w}", p, new Vector3(0.66f, 0.12f, 0.66f), m, false);
                c.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        // ============================================================ дома

        /// <summary>
        /// Хрущёвка: пять этажей, подъезды с козырьками, застеклённые вразнобой
        /// балконы, подвальные окошки, потёки под окнами.
        /// </summary>
        /// <summary>Шаг окна по фасаду. Вместе с высотой этажа задаёт сетку дома.</summary>
        private const float WindowStep = 3.1f;

        /// <summary>
        /// Хрущёвка. Вся геометрия привязана к одной сетке: шаг окна 3.1 м,
        /// этаж 2.8 м. Балконы стоят колоннами снизу доверху — вразнобой
        /// они бывают только у новостроек, и фасад от этого разваливается.
        /// </summary>
        private static void Block(string name, Vector3 center, Vector3 size, string kind,
                                  float faceZ, float[] entrances, int seed)
        {
            // тайлинг ровно по сетке: клетка текстуры = одно окно на одном этаже
            WallTiled(name, center, size, kind,
                      new Vector2(size.x / (WindowStep * 2f), size.y / (FloorHeight * 2f)));

            float bottom = center.y - size.y * 0.5f;
            float left   = center.x - size.x * 0.5f;
            float face   = center.z + faceZ * (size.z * 0.5f);
            int   modules = Mathf.Max(1, Mathf.RoundToInt(size.x / WindowStep));
            int   floors  = Mathf.Max(1, Mathf.FloorToInt(size.y / FloorHeight));
            var   rnd = new System.Random(seed);

            var trim  = MatLib.Get("concrete", new Vector2(size.x / 5f, 1f));
            var slab  = MatLib.Get("concrete", new Vector2(2f, 1f));
            var rail  = MatLib.Solid(new Color(0.40f, 0.38f, 0.34f));
            var glaze = MatLib.Get("glass", new Vector2(2f, 1f));
            var roof  = MatLib.Solid(new Color(0.21f, 0.21f, 0.20f));

            // цоколь
            Box(name + " цоколь", new Vector3(center.x, bottom + 0.8f, center.z),
                new Vector3(size.x + 0.5f, 1.6f, size.z + 0.5f),
                MatLib.Get("concrete_d", new Vector2(size.x / 4f, 1f)));
            Box(name + " отлив цоколя", new Vector3(center.x, bottom + 1.62f, face + faceZ * 0.3f),
                new Vector3(size.x + 0.6f, 0.1f, 0.34f), trim, false);

            // карниз и крыша
            Box(name + " карниз", new Vector3(center.x, center.y + size.y * 0.5f - 0.2f, center.z),
                new Vector3(size.x + 0.9f, 0.3f, size.z + 0.9f), trim, false);
            Box(name + " крыша", center + new Vector3(0f, size.y * 0.5f + 0.2f, 0f),
                new Vector3(size.x + 0.4f, 0.4f, size.z + 0.4f), roof);
            Box(name + " парапет", new Vector3(center.x, center.y + size.y * 0.5f + 0.55f, face + faceZ * 0.1f),
                new Vector3(size.x + 0.4f, 0.4f, 0.28f), roof, false);

            // межэтажные пояса — тонкие, как настоящие
            for (int fl = 1; fl < floors; fl++)
                Box(name + " пояс", new Vector3(center.x, bottom + fl * FloorHeight, face + faceZ * 0.1f),
                    new Vector3(size.x, 0.12f, 0.2f), trim, false);

            // торцевые пилястры прячут срез текстуры на углах
            Box(name + " угол л", new Vector3(left + 0.3f, bottom + size.y * 0.5f, face + faceZ * 0.12f),
                new Vector3(0.6f, size.y, 0.24f), trim, false);
            Box(name + " угол п", new Vector3(left + size.x - 0.3f, bottom + size.y * 0.5f, face + faceZ * 0.12f),
                new Vector3(0.6f, size.y, 0.24f), trim, false);

            // водостоки по углам и через каждые 6 модулей
            for (int m = 0; m <= modules; m += 6)
            {
                float px = left + Mathf.Clamp(m * WindowStep, 0.5f, size.x - 0.5f);
                Cyl(name + " водосток", new Vector3(px, bottom + size.y * 0.5f, face + faceZ * 0.25f),
                    new Vector3(0.2f, size.y * 0.5f, 0.2f),
                    MatLib.Solid(new Color(0.36f, 0.35f, 0.33f), 0.2f), false);
            }

            // ---- балконы: колоннами, узкие и неглубокие ----
            const float bw = 2.7f;    // ширина балкона
            const float bd = 0.85f;   // вылет: у хрущёвки он маленький
            for (int m = 1; m < modules - 1; m += 2)
            {
                if (rnd.NextDouble() < 0.25) continue;          // колонка без балконов
                float bx = left + (m + 0.5f) * WindowStep;

                for (int fl = 1; fl < floors; fl++)             // первый этаж без балкона
                {
                    float by = bottom + fl * FloorHeight + 0.12f;
                    float mid  = faceZ * (bd * 0.5f);
                    float edge = faceZ * bd;

                    // потолок балкона: плита следующего этажа, а у верхнего — карниз
                    float ceilY = (fl + 1 < floors) ? by + FloorHeight
                                                    : bottom + size.y - 0.35f;
                    float head  = ceilY - (by + 1.0f) - 0.12f;   // место над глухим низом

                    Box("Балкон плита", new Vector3(bx, by, face + mid),
                        new Vector3(bw, 0.16f, bd), slab, false);
                    Box("Балкон бок л", new Vector3(bx - bw * 0.5f, by + 0.52f, face + mid),
                        new Vector3(0.1f, 1.05f, bd), rail, false);
                    Box("Балкон бок п", new Vector3(bx + bw * 0.5f, by + 0.52f, face + mid),
                        new Vector3(0.1f, 1.05f, bd), rail, false);

                    if (head > 0.7f && rnd.NextDouble() < 0.55)
                    {
                        // застеклён: глухой низ, стекло ровно до перекрытия выше
                        Box("Балкон низ", new Vector3(bx, by + 0.5f, face + edge),
                            new Vector3(bw, 1f, 0.1f), rail, false);
                        Box("Балкон стекло", new Vector3(bx, by + 1f + head * 0.5f, face + edge),
                            new Vector3(bw, head, 0.08f), glaze, false);
                        Box("Балкон рама", new Vector3(bx, by + 1f + head + 0.06f, face + edge),
                            new Vector3(bw, 0.12f, 0.12f), rail, false);
                    }
                    else
                    {
                        // открытый: сплошная панель ограждения, а не решётка
                        Box("Балкон ограда", new Vector3(bx, by + 0.52f, face + edge),
                            new Vector3(bw, 1.05f, 0.1f), rail, false);
                        Box("Балкон поручень", new Vector3(bx, by + 1.06f, face + edge),
                            new Vector3(bw + 0.08f, 0.08f, 0.16f), rail, false);
                    }
                }
            }

            // подвальные окошки
            for (int m = 0; m < modules; m += 2)
                Box("Окошко подвала", new Vector3(left + (m + 0.5f) * WindowStep, 0.75f, face + faceZ * 0.1f),
                    new Vector3(1f, 0.45f, 0.1f), MatLib.Solid(new Color(0.08f, 0.09f, 0.10f)), false);

            // кусты под окнами первого этажа
            for (int m = 1; m < modules; m += 3)
                Bush(new Vector3(left + (m + 0.5f) * WindowStep, 0f, face + faceZ * 2.4f),
                     0.75f + (float)rnd.NextDouble() * 0.3f);

            // ---- подъезды ----
            for (int i = 0; i < entrances.Length; i++)
            {
                var door = new Vector3(entrances[i], 0f, face);
                Box($"{name} портал {i}", door + new Vector3(0f, 1.35f, faceZ * 0.14f),
                    new Vector3(2.4f, 2.7f, 0.28f), MatLib.Get("concrete", new Vector2(1f, 1f)), false);
                Box($"{name} дверь {i}", door + new Vector3(0f, 1.1f, faceZ * 0.3f),
                    new Vector3(1.5f, 2.2f, 0.2f), MatLib.Get("rust", new Vector2(1f, 1f)));
                Box($"{name} козырёк {i}", door + new Vector3(0f, 2.55f, faceZ * 0.9f),
                    new Vector3(2.8f, 0.18f, 1.6f), MatLib.Solid(new Color(0.30f, 0.31f, 0.28f)));
                Box($"{name} опора л {i}", door + new Vector3(-1.25f, 1.25f, faceZ * 1.5f),
                    new Vector3(0.12f, 2.5f, 0.12f), rail, false);
                Box($"{name} опора п {i}", door + new Vector3(1.25f, 1.25f, faceZ * 1.5f),
                    new Vector3(0.12f, 2.5f, 0.12f), rail, false);
                Box($"{name} ступени {i}", door + new Vector3(0f, 0.12f, faceZ * 1.1f),
                    new Vector3(2.4f, 0.24f, 1.5f), MatLib.Get("concrete", new Vector2(1f, 1f)));
                Box($"{name} номер {i}", door + new Vector3(0.95f, 2.3f, faceZ * 0.3f),
                    new Vector3(0.42f, 0.34f, 0.06f), MatLib.Solid(new Color(0.78f, 0.76f, 0.70f)), false);

                Spot("entrance", door + new Vector3(0f, 0f, faceZ * 3.2f));
                Bench(door + new Vector3(3.4f, 0f, faceZ * 2.4f), faceZ);
            }
        }

        /// <summary>Куст: три сплюснутых шара. Прячет стык стены с землёй.</summary>
        private static void Bush(Vector3 at, float scale = 1f)
        {
            var f = MatLib.Get("foliage", new Vector2(1.5f, 1.5f));
            Ball("Куст 1", at + new Vector3(0f, 0.75f * scale, 0f), new Vector3(2.2f, 1.5f, 1.8f) * scale, f);
            Ball("Куст 2", at + new Vector3(0.8f * scale, 0.55f * scale, 0.3f * scale),
                 new Vector3(1.5f, 1.1f, 1.3f) * scale, f);
            Ball("Куст 3", at + new Vector3(-0.7f * scale, 0.6f * scale, -0.2f * scale),
                 new Vector3(1.4f, 1.2f, 1.2f) * scale, f);
        }

        private static void Bench(Vector3 at, float faceZ)
        {
            Spot("bench", at + new Vector3(0f, 0f, -faceZ * 1.8f), 2.6f);
            var w = MatLib.Get("wood", new Vector2(3f, 1f));
            Box("Лавка сиденье", at + new Vector3(0f, 0.45f, 0f), new Vector3(2.8f, 0.12f, 0.7f), w);
            Box("Лавка спинка",  at + new Vector3(0f, 0.85f, faceZ * 0.35f), new Vector3(2.8f, 0.5f, 0.1f), w);
            Box("Лавка нога л",  at + new Vector3(-1.2f, 0.22f, 0f), new Vector3(0.14f, 0.45f, 0.6f),
                MatLib.Solid(new Color(0.22f, 0.23f, 0.22f)));
            Box("Лавка нога п",  at + new Vector3(1.2f, 0.22f, 0f), new Vector3(0.14f, 0.45f, 0.6f),
                MatLib.Solid(new Color(0.22f, 0.23f, 0.22f)));
        }

        private static readonly float[] NorthEntrances = { -50f, -28f, -6f, 26f };
        private static readonly float[] SouthEntrances = { -50f, -28f, -6f, 26f };

        private static void HouseNorth()
        {
            // дом с севера, аркой во двор с улицы
            Block("Дом 12", new Vector3(-16f, 7.5f, 22f), new Vector3(68f, 15f, 12f), "panel_a",
                  -1f, new[] { -50f, -28f, -6f }, 101);
            Block("Дом 12к2", new Vector3(30f, 7.5f, 22f), new Vector3(20f, 15f, 12f), "panel_a",
                  -1f, new[] { 28f }, 102);
            // проём между ними = арка (x ≈ 18..20)
            Box("Перемычка арки", new Vector3(19f, 13.2f, 22f), new Vector3(3f, 3.6f, 12f),
                MatLib.Get("panel_end", new Vector2(1f, 1f)));
            Pavement("Арка проезд", new Vector3(19f, 0f, 24f), 3f, 20f, "asphalt", SurfaceMap.Kind.Asphalt, 3f);

            Box("Вход в подвал 12", new Vector3(-38f, 0.4f, 15.8f), new Vector3(2.4f, 1.2f, 1.8f),
                MatLib.Solid(new Color(0.10f, 0.10f, 0.11f)));
            Spot("basement", new Vector3(-38f, 0f, 13.6f), 2.6f);
        }

        private static void HouseSouth()
        {
            Block("Дом 14", new Vector3(-16f, 7.5f, -22f), new Vector3(68f, 15f, 12f), "panel_b",
                  1f, SouthEntrances, 201);

            // опорный пункт — пристройка на торце, кирпичная, её видно издалека
            Wall("Опорный пункт", new Vector3(30f, 2.2f, -22f), new Vector3(20f, 4.4f, 12f), "brick", 2f);
            Box("Крыша опорного", new Vector3(30f, 4.65f, -22f), new Vector3(21f, 0.4f, 13f),
                MatLib.Solid(new Color(0.20f, 0.20f, 0.21f)));
            Box("Вывеска опорного", new Vector3(30f, 3.5f, -15.9f), new Vector3(6f, 1f, 0.18f),
                MatLib.Solid(new Color(0.16f, 0.22f, 0.42f)));
            Box("Дверь опорного", new Vector3(30f, 1.15f, -15.9f), new Vector3(1.6f, 2.3f, 0.22f),
                MatLib.Get("wood", new Vector2(1f, 1f)));
            Box("Ступени опорного", new Vector3(30f, 0.13f, -14.9f), new Vector3(2.8f, 0.26f, 1.8f),
                MatLib.Get("concrete", new Vector2(1f, 1f)));
            Spot("police", new Vector3(30f, 0f, -13f), 4f);

            // квартира и окно бабки с первого этажа
            Box("Окно бабки", new Vector3(-40f, 1.7f, -15.95f), new Vector3(2.4f, 1.6f, 0.18f),
                MatLib.Get("glass", new Vector2(1f, 1f)));
            Box("Рама окна", new Vector3(-40f, 1.7f, -16.03f), new Vector3(2.7f, 1.9f, 0.12f),
                MatLib.Solid(new Color(0.72f, 0.70f, 0.64f)), false);
            Box("Банки на подоконнике", new Vector3(-40f, 2.4f, -15.85f), new Vector3(1.7f, 0.32f, 0.3f),
                MatLib.Solid(new Color(0.62f, 0.58f, 0.38f)), false);
            Spot("window", new Vector3(-40f, 0f, -13.4f));
            Spot("flat",   new Vector3(-44f, 0f, -13.4f));

            Box("Вход в подвал 14", new Vector3(8f, 0.4f, -15.8f), new Vector3(2.4f, 1.2f, 1.8f),
                MatLib.Solid(new Color(0.10f, 0.10f, 0.11f)));
            Spot("basement", new Vector3(8f, 0f, -13.6f), 2.6f);

            Box("Почтовые ящики", new Vector3(-18.5f, 1.2f, -15.9f), new Vector3(1.3f, 1f, 0.18f),
                MatLib.Solid(new Color(0.30f, 0.34f, 0.30f)), false);
            Spot("post", new Vector3(-18.5f, 0f, -13.6f), 2.6f);
        }

        private static void HouseWest()
        {
            // торцевой дом замыкает двор с запада — двор становится двором, а не полем
            WallTiled("Дом 10", new Vector3(-66f, 7.5f, 0f), new Vector3(12f, 15f, 44f), "panel_end",
                      new Vector2(44f / 6.4f, 15f / 5.6f));
            Box("Крыша 10", new Vector3(-66f, 15.25f, 0f), new Vector3(13f, 0.5f, 45f),
                MatLib.Solid(new Color(0.21f, 0.21f, 0.20f)));
            Box("Парапет 10", new Vector3(-59.8f, 15.9f, 0f), new Vector3(0.3f, 0.7f, 45f),
                MatLib.Solid(new Color(0.21f, 0.21f, 0.20f)), false);

            for (int i = 0; i < 3; i++)
            {
                float z = -14f + i * 14f;
                Box($"Дверь 10-{i}", new Vector3(-59.9f, 1.15f, z), new Vector3(0.22f, 2.3f, 1.6f),
                    MatLib.Get("rust", new Vector2(1f, 1f)));
                Box($"Козырёк 10-{i}", new Vector3(-58.9f, 2.6f, z), new Vector3(2f, 0.22f, 3.4f),
                    MatLib.Solid(new Color(0.30f, 0.31f, 0.28f)));
                Box($"Ступени 10-{i}", new Vector3(-58.8f, 0.13f, z), new Vector3(1.8f, 0.26f, 2.8f),
                    MatLib.Get("concrete", new Vector2(1f, 1f)));
                Spot("entrance", new Vector3(-56.6f, 0f, z));
            }

            Box("Трансформаторная", new Vector3(-54f, 1.6f, 24f), new Vector3(5f, 3.2f, 4f),
                MatLib.Get("brick", new Vector2(2.5f, 1.6f)));
            Box("Дверь ТП", new Vector3(-51.4f, 1.1f, 24f), new Vector3(0.2f, 2.2f, 1.4f),
                MatLib.Get("rust", new Vector2(1f, 1f)));
            Box("Знак ТП", new Vector3(-51.3f, 2.4f, 24f), new Vector3(0.1f, 0.5f, 0.4f),
                MatLib.Solid(new Color(0.78f, 0.68f, 0.12f)), false);
        }

        // ============================================================ двор

        private static void Yard()
        {
            // двор — газон, асфальт только по дорожкам и проезду
            Pavement("Газон двора", new Vector3(-10f, 0f, 0f), 102f, 32f, "grass", SurfaceMap.Kind.Grass, 8f);

            // внутридворовой проезд вдоль дома 12 с карманами под машины
            Pavement("Проезд двора", new Vector3(-12f, 0f, 11.5f), 94f, 6.5f, "asphalt", SurfaceMap.Kind.Asphalt, 6f);
            Curb(new Vector3(-12f, 0f, 8.1f), 94f);

            // центральная дорожка вдоль двора и отростки к подъездам
            Pavement("Дорожка центр", new Vector3(-10f, 0f, 0f), 102f, 2.6f, "concrete", SurfaceMap.Kind.Concrete, 3f);
            foreach (var x in new[] { -50f, -28f, -6f, 28f })
                Pavement("Дорожка к 12", new Vector3(x, 0f, 6.5f), 2.2f, 13f, "concrete", SurfaceMap.Kind.Concrete, 3f);
            foreach (var x in SouthEntrances)
                Pavement("Дорожка к 14", new Vector3(x, 0f, -6.5f), 2.2f, 13f, "concrete", SurfaceMap.Kind.Concrete, 3f);
            Pavement("Дорожка к арке", new Vector3(19f, 0f, 8f), 2.2f, 16f, "concrete", SurfaceMap.Kind.Concrete, 3f);

            // машины в карманах
            var carColors = new[]
            {
                new Color(0.60f, 0.58f, 0.54f), new Color(0.23f, 0.33f, 0.25f),
                new Color(0.44f, 0.15f, 0.13f), new Color(0.21f, 0.26f, 0.37f),
                new Color(0.66f, 0.64f, 0.58f)
            };
            float[] cars = { -48f, -40f, -32f, 2f, 10f };
            for (int i = 0; i < cars.Length; i++)
            {
                Car(new Vector3(cars[i], 0f, 11.5f), carColors[i % carColors.Length], i);
                Spot("car", new Vector3(cars[i], 0f, 9.2f), 2.6f);
            }

            // беседка
            var wood = MatLib.Get("wood", new Vector2(3f, 1f));
            Box("Беседка пол",   GazeboPoint + new Vector3(0f, 0.22f, 0f), new Vector3(7.5f, 0.44f, 7.5f), wood);
            SurfaceMap.Add(GazeboPoint, 7.5f, 7.5f, SurfaceMap.Kind.Wood);
            Box("Беседка крыша", GazeboPoint + new Vector3(0f, 3.05f, 0f), new Vector3(8.6f, 0.3f, 8.6f),
                MatLib.Solid(new Color(0.26f, 0.32f, 0.24f)));
            for (int i = 0; i < 4; i++)
                Box($"Столб беседки {i}",
                    GazeboPoint + new Vector3((i % 2 == 0) ? -3.5f : 3.5f, 1.7f, (i < 2) ? -3.5f : 3.5f),
                    new Vector3(0.22f, 2.9f, 0.22f), wood);
            Box("Стол беседки", GazeboPoint + new Vector3(0f, 0.88f, 0f), new Vector3(2.2f, 0.12f, 1.2f), wood);
            Box("Труба", GazeboPoint + new Vector3(3.2f, 1.4f, -3.2f), new Vector3(0.12f, 2.3f, 0.12f),
                MatLib.Solid(new Color(0.48f, 0.48f, 0.50f), 0.5f, 0.6f));
            Spot("yard", GazeboPoint + new Vector3(0f, 0f, 5.5f), 4f);

            Playground(new Vector3(-34f, 0f, -4f));
            Sport(new Vector3(-4f, 0f, 5f));
            Drying(new Vector3(-20f, 0f, -8f));
            CarpetBeater(new Vector3(-56f, 0f, -8f));

            Spot("yard", new Vector3(-12f, 0f, 0f), 4f);
            Spot("yard", new Vector3(34f, 0f, 2f), 4f);

            // тополя во дворе — ровным рядом вдоль дорожки, как сажали
            for (float x = -52f; x <= 32f; x += 14f)
                Tree(new Vector3(x, 0f, -11f), 0.9f + (x % 3) * 0.05f);

            Urn(new Vector3(-6f, 0f, -8f));
            Urn(new Vector3(26f, 0f, -8f));

            // бетонные вазоны вдоль дорожки
            var pot = MatLib.Get("concrete", new Vector2(1f, 1f));
            for (float x = -44f; x <= 28f; x += 18f)
            {
                Cyl("Вазон", new Vector3(x, 0.28f, 2.4f), new Vector3(0.95f, 0.28f, 0.95f), pot);
                Cyl("Земля в вазоне", new Vector3(x, 0.55f, 2.4f), new Vector3(0.8f, 0.04f, 0.8f),
                    MatLib.Get("dirt", new Vector2(1f, 1f)), false);
            }

            Bush(new Vector3(-58f, 0f, -14f), 1.1f);
            Bush(new Vector3(-58f, 0f, 12f), 1.0f);
            Bush(new Vector3(38f, 0f, -10f), 1.15f);
            Bush(new Vector3(38f, 0f, 10f), 0.95f);
        }

        /// <summary>
        /// Машина: низ кузова, капот с багажником, кабина со стёклами, бампера.
        /// Одна коробка на колёсах читается как кирпич — нужен силуэт.
        /// </summary>
        private static void Car(Vector3 at, Color color, int seed)
        {
            var body = MatLib.Solid(color, 0.45f, 0.2f);
            var glass = MatLib.Get("glass", new Vector2(1f, 1f));
            var trim = MatLib.Solid(new Color(0.24f, 0.24f, 0.25f), 0.4f, 0.4f);

            Box($"Кузов низ {seed}",  at + new Vector3(0f, 0.62f, 0f), new Vector3(4.3f, 0.62f, 1.82f), body);
            Box($"Капот {seed}",      at + new Vector3(1.55f, 0.98f, 0f), new Vector3(1.3f, 0.2f, 1.7f), body, false);
            Box($"Багажник {seed}",   at + new Vector3(-1.6f, 0.98f, 0f), new Vector3(1.2f, 0.2f, 1.7f), body, false);

            Box($"Кабина {seed}",     at + new Vector3(-0.1f, 1.28f, 0f), new Vector3(2.1f, 0.62f, 1.72f), body);
            Box($"Лобовое {seed}",    at + new Vector3(0.96f, 1.26f, 0f), new Vector3(0.12f, 0.5f, 1.6f), glass, false);
            Box($"Заднее {seed}",     at + new Vector3(-1.16f, 1.26f, 0f), new Vector3(0.12f, 0.5f, 1.6f), glass, false);
            Box($"Боковое л {seed}",  at + new Vector3(-0.1f, 1.26f, -0.87f), new Vector3(1.9f, 0.46f, 0.1f), glass, false);
            Box($"Боковое п {seed}",  at + new Vector3(-0.1f, 1.26f, 0.87f), new Vector3(1.9f, 0.46f, 0.1f), glass, false);
            Box($"Крыша {seed}",      at + new Vector3(-0.1f, 1.6f, 0f), new Vector3(2.1f, 0.1f, 1.72f), body, false);

            Box($"Бампер перед {seed}", at + new Vector3(2.2f, 0.55f, 0f), new Vector3(0.22f, 0.4f, 1.9f), trim, false);
            Box($"Бампер зад {seed}",   at + new Vector3(-2.2f, 0.55f, 0f), new Vector3(0.22f, 0.4f, 1.9f), trim, false);
            Box($"Фара л {seed}",       at + new Vector3(2.18f, 0.85f, -0.6f), new Vector3(0.18f, 0.24f, 0.4f),
                MatLib.Solid(new Color(0.82f, 0.80f, 0.70f), 0.7f), false);
            Box($"Фара п {seed}",       at + new Vector3(2.18f, 0.85f, 0.6f), new Vector3(0.18f, 0.24f, 0.4f),
                MatLib.Solid(new Color(0.82f, 0.80f, 0.70f), 0.7f), false);

            Wheels(at, 1.45f, 0.92f);
        }

        private static void Playground(Vector3 at)
        {
            // вкопанные покрышки — бордюром вдоль одной стороны, наполовину в земле,
            // а не кольцом поперёк дорожек
            var tyre = MatLib.Solid(new Color(0.11f, 0.11f, 0.12f));
            for (int i = 0; i < 9; i++)
                Cyl($"Покрышка {i}", at + new Vector3(-8f + i * 2f, 0.12f, -5.5f),
                    new Vector3(0.95f, 0.16f, 0.95f), tyre, false);

            Box("Песочница", at + new Vector3(0f, 0.2f, 0f), new Vector3(4.5f, 0.4f, 4.5f),
                MatLib.Solid(new Color(0.62f, 0.57f, 0.40f)), false);
            SurfaceMap.Add(at, 4.5f, 4.5f, SurfaceMap.Kind.Dirt);
            Box("Борт песочницы", at + new Vector3(0f, 0.38f, -2.3f), new Vector3(4.8f, 0.3f, 0.25f),
                MatLib.Get("wood", new Vector2(2f, 1f)));

            Cyl("Грибок ножка", at + new Vector3(0f, 1f, 0f), new Vector3(0.24f, 1f, 0.24f),
                MatLib.Get("wood", new Vector2(1f, 2f)));
            Box("Грибок шляпа", at + new Vector3(0f, 2.1f, 0f), new Vector3(3f, 0.24f, 3f),
                MatLib.Solid(new Color(0.55f, 0.24f, 0.20f)));

            var frame = MatLib.Solid(new Color(0.28f, 0.40f, 0.52f), 0.2f);
            Box("Качели рама",    at + new Vector3(-6f, 2.2f, 2f), new Vector3(3.4f, 0.16f, 0.16f), frame);
            Box("Качели стойка л", at + new Vector3(-7.6f, 1.1f, 2f), new Vector3(0.16f, 2.2f, 0.16f), frame);
            Box("Качели стойка п", at + new Vector3(-4.4f, 1.1f, 2f), new Vector3(0.16f, 2.2f, 0.16f), frame);
            Box("Качели сиденье",  at + new Vector3(-6f, 0.7f, 2f), new Vector3(0.9f, 0.08f, 0.3f),
                MatLib.Get("wood", new Vector2(1f, 1f)), false);
            Box("Качели цепь л",   at + new Vector3(-6.4f, 1.45f, 2f), new Vector3(0.05f, 1.5f, 0.05f), frame, false);
            Box("Качели цепь п",   at + new Vector3(-5.6f, 1.45f, 2f), new Vector3(0.05f, 1.5f, 0.05f), frame, false);

            // лазалка: рама из стоек и перекладин, а не плита, воткнутая в газон
            var climb = MatLib.Solid(new Color(0.55f, 0.38f, 0.15f));
            for (int i = 0; i < 2; i++)
            {
                float cx = 5f + i * 2.6f;
                Box($"Лазалка стойка {i}л", at + new Vector3(cx, 1.1f, -0.9f), new Vector3(0.14f, 2.2f, 0.14f), climb);
                Box($"Лазалка стойка {i}п", at + new Vector3(cx, 1.1f, 0.9f), new Vector3(0.14f, 2.2f, 0.14f), climb);
            }
            for (int r = 0; r < 5; r++)
            {
                float ry = 0.5f + r * 0.4f;
                Box($"Лазалка перекладина {r}", at + new Vector3(6.3f, ry, -0.9f), new Vector3(2.8f, 0.09f, 0.09f), climb, false);
                Box($"Лазалка перекладина {r}п", at + new Vector3(6.3f, ry, 0.9f), new Vector3(2.8f, 0.09f, 0.09f), climb, false);
            }
            Box("Лазалка верх", at + new Vector3(6.3f, 2.2f, 0f), new Vector3(2.8f, 0.1f, 1.9f), climb, false);

            // горка: площадка на опорах, лесенка и скат до земли
            var metal = MatLib.Solid(new Color(0.45f, 0.46f, 0.48f), 0.55f, 0.5f);
            var deck = at + new Vector3(11.5f, 0f, 0f);
            Box("Горка площадка", deck + new Vector3(0f, 1.5f, 0f), new Vector3(1.4f, 0.12f, 1.4f), climb);
            for (int i = 0; i < 4; i++)
                Box($"Горка опора {i}", deck + new Vector3((i % 2 == 0) ? -0.6f : 0.6f, 0.75f, (i < 2) ? -0.6f : 0.6f),
                    new Vector3(0.12f, 1.5f, 0.12f), climb);
            Box("Горка перила л", deck + new Vector3(-0.65f, 2f, 0f), new Vector3(0.1f, 1f, 1.4f), climb, false);
            Box("Горка перила п", deck + new Vector3(0.65f, 2f, 0f), new Vector3(0.1f, 1f, 1.4f), climb, false);
            var slide = Box("Горка скат", deck + new Vector3(0f, 0.85f, 2.1f), new Vector3(1.1f, 0.1f, 3.4f), metal);
            slide.transform.rotation = Quaternion.Euler(-24f, 0f, 0f);
            for (int r = 0; r < 4; r++)
                Box($"Лесенка {r}", deck + new Vector3(0f, 0.35f + r * 0.38f, -0.9f - r * 0.22f),
                    new Vector3(1.2f, 0.08f, 0.22f), climb, false);
        }

        private static void Sport(Vector3 at)
        {
            var bar = MatLib.Solid(new Color(0.34f, 0.44f, 0.58f), 0.3f, 0.4f);
            Box("Турник перекладина", at + new Vector3(0f, 2.25f, 0f), new Vector3(4.6f, 0.11f, 0.11f), bar);
            Box("Турник стойка л", at + new Vector3(-2.2f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), bar);
            Box("Турник стойка п", at + new Vector3(2.2f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), bar);
            Box("Брусья л", at + new Vector3(5f, 1.3f, -0.6f), new Vector3(2.6f, 0.1f, 0.1f), bar);
            Box("Брусья п", at + new Vector3(5f, 1.3f, 0.6f), new Vector3(2.6f, 0.1f, 0.1f), bar);
            Box("Брусья стойка 1", at + new Vector3(3.8f, 0.65f, -0.6f), new Vector3(0.12f, 1.3f, 0.12f), bar);
            Box("Брусья стойка 2", at + new Vector3(6.2f, 0.65f, -0.6f), new Vector3(0.12f, 1.3f, 0.12f), bar);
            Box("Брусья стойка 3", at + new Vector3(3.8f, 0.65f, 0.6f), new Vector3(0.12f, 1.3f, 0.12f), bar);
            Box("Брусья стойка 4", at + new Vector3(6.2f, 0.65f, 0.6f), new Vector3(0.12f, 1.3f, 0.12f), bar);
            Pavement("Под турником", at, 14f, 5f, "dirt", SurfaceMap.Kind.Dirt, 4f);
            Spot("barbell", at + new Vector3(0f, 0f, -2.6f));
        }

        private static void Drying(Vector3 at)
        {
            var m = MatLib.Solid(new Color(0.42f, 0.41f, 0.39f), 0.15f);
            for (int i = 0; i < 2; i++)
            {
                Cyl($"Сушилка стойка {i}", at + new Vector3(i * 7f, 1.3f, 0f), new Vector3(0.16f, 1.3f, 0.16f), m);
                Box($"Сушилка перекладина {i}", at + new Vector3(i * 7f, 2.5f, 0f), new Vector3(0.1f, 0.1f, 1.8f), m, false);
            }
            for (int w = 0; w < 3; w++)
                Box($"Верёвка {w}", at + new Vector3(3.5f, 2.45f, -0.7f + w * 0.7f),
                    new Vector3(7f, 0.03f, 0.03f), MatLib.Solid(new Color(0.75f, 0.73f, 0.66f)), false);
        }

        private static void CarpetBeater(Vector3 at)
        {
            var m = MatLib.Solid(new Color(0.40f, 0.39f, 0.37f), 0.15f);
            Cyl("Ковровыбивалка л", at + new Vector3(-1.8f, 1.1f, 0f), new Vector3(0.14f, 1.1f, 0.14f), m);
            Cyl("Ковровыбивалка п", at + new Vector3(1.8f, 1.1f, 0f), new Vector3(0.14f, 1.1f, 0.14f), m);
            Box("Ковровыбивалка верх", at + new Vector3(0f, 2.15f, 0f), new Vector3(3.8f, 0.12f, 0.12f), m, false);
        }

        // ============================================================ ларёк

        private static void Kiosk()
        {
            // ларёк стоит на тротуаре у дороги, где ему и место
            var at = new Vector3(46f, 0f, 31f);
            Box("Ларёк", at + new Vector3(0f, 1.5f, 0f), new Vector3(6.4f, 3f, 4.4f),
                MatLib.Get("kiosk", new Vector2(3f, 2f)));
            Box("Крыша ларька", at + new Vector3(0f, 3.1f, 0f), new Vector3(7.2f, 0.25f, 5.2f),
                MatLib.Solid(new Color(0.18f, 0.30f, 0.32f)));
            Box("Окошко", at + new Vector3(0f, 1.6f, -2.3f), new Vector3(2f, 1.1f, 0.22f),
                MatLib.Get("glass", new Vector2(1f, 1f)));
            Box("Полка", at + new Vector3(0f, 1.05f, -2.5f), new Vector3(2.1f, 0.12f, 0.6f),
                MatLib.Get("wood", new Vector2(1f, 1f)));
            Box("Вывеска", at + new Vector3(0f, 3.45f, -2.2f), new Vector3(5f, 0.75f, 0.16f),
                MatLib.Solid(new Color(0.66f, 0.22f, 0.15f)));
            Box("Реклама", at + new Vector3(3.3f, 1.8f, 0f), new Vector3(0.12f, 1.6f, 3f),
                MatLib.Solid(new Color(0.70f, 0.63f, 0.20f)), false);
            Box("Ящики у ларька", at + new Vector3(-3.6f, 0.5f, 1.6f), new Vector3(1.6f, 1f, 1.2f),
                MatLib.Get("wood", new Vector2(2f, 1f)));
            Urn(at + new Vector3(-3.6f, 0f, -2.2f));
            Spot("kiosk", at + new Vector3(0f, 0f, -3.6f), 3.5f);
        }

        // ============================================================ гаражи

        private static void Garages()
        {
            // кооператив южнее дома 14, с подъездной дорогой — а не сам по себе
            Pavement("Проезд к гаражам", new Vector3(10f, 0f, -48f), 100f, 8f, "dirt", SurfaceMap.Kind.Gravel, 6f);
            SurfaceMap.Add(new Vector3(-10f, 0f, -62f), 110f, 34f, SurfaceMap.Kind.Gravel);

            var body = MatLib.Get("concrete_d", new Vector2(5f, 1.2f));
            var roof = MatLib.Solid(new Color(0.26f, 0.26f, 0.25f));

            for (int row = 0; row < 3; row++)
            {
                float z = -56f - row * 11f;
                Box($"Ряд гаражей {row}", new Vector3(-16f, 1.7f, z), new Vector3(76f, 3.4f, 6.2f), body);
                Box($"Крыша ряда {row}", new Vector3(-16f, 3.55f, z), new Vector3(77f, 0.25f, 6.8f), roof);

                for (int g = 0; g < 18; g++)
                {
                    float gx = -52f + g * 4.2f;
                    Box($"Ворота {row}-{g}", new Vector3(gx, 1.4f, z + 3.2f),
                        new Vector3(3.4f, 2.8f, 0.22f),
                        MatLib.Get((row + g) % 2 == 0 ? "garage" : "garage_2", new Vector2(1f, 1f)));
                }
            }

            // четыре открытых бокса — рабочие точки
            for (int i = 0; i < 4; i++)
            {
                float gx = -40f + i * 22f;
                Box($"Открытый бокс {i}", new Vector3(gx, 1.7f, -44f), new Vector3(5.6f, 3.4f, 6f),
                    MatLib.Get("garage", new Vector2(2f, 1f)));
                Box($"Проём {i}", new Vector3(gx, 1.3f, -41.1f), new Vector3(3.6f, 2.6f, 0.3f),
                    MatLib.Solid(new Color(0.05f, 0.05f, 0.06f)), false);
                Spot("garage", new Vector3(gx, 0f, -39f));
            }

            Box("Бочка", new Vector3(-30f, 0.5f, -41f), new Vector3(1.1f, 1f, 1.1f),
                MatLib.Get("rust", new Vector2(1f, 1f)));
            Box("Покрышки стопкой", new Vector3(6f, 0.4f, -41f), new Vector3(1.6f, 0.8f, 1.6f),
                MatLib.Solid(new Color(0.10f, 0.10f, 0.11f)));
        }

        // ============================================================ помойка

        private static void Dump()
        {
            var at = new Vector3(-54f, 0f, -30f);
            Pavement("Помойка площадка", at, 12f, 8f, "concrete_d", SurfaceMap.Kind.Concrete, 4f);
            for (int i = 0; i < 3; i++)
                Box($"Бак {i}", at + new Vector3(-3.4f + i * 3.4f, 0.75f, 0.6f), new Vector3(2.4f, 1.5f, 1.8f),
                    MatLib.Get("rust", new Vector2(1f, 1f)));
            Box("Стена приёмки", at + new Vector3(0f, 1.4f, -4.2f), new Vector3(12f, 2.8f, 0.4f),
                MatLib.Get("brick", new Vector2(6f, 1.4f)));
            Box("Старый диван", at + new Vector3(5.5f, 0.4f, 2.5f), new Vector3(2.4f, 0.8f, 1f),
                MatLib.Solid(new Color(0.34f, 0.27f, 0.22f)));
            Box("Спинка дивана", at + new Vector3(5.5f, 0.95f, 2.9f), new Vector3(2.4f, 0.7f, 0.25f),
                MatLib.Solid(new Color(0.31f, 0.25f, 0.20f)));
            Spot("recycling", at + new Vector3(0f, 0f, 5f), 3.5f);
        }

        // ============================================================ стройка

        private static void Construction()
        {
            // через дорогу от домов, обнесена забором с воротами
            var at = new Vector3(-46f, 0f, 68f);
            SurfaceMap.Add(at, 60f, 36f, SurfaceMap.Kind.Gravel);

            var c = MatLib.Get("concrete_d", new Vector2(5f, 3f));
            Box("Недострой", at + new Vector3(0f, 5f, 4f), new Vector3(28f, 10f, 16f), c);
            Box("Перекрытие 1", at + new Vector3(0f, 3.4f, 4f), new Vector3(29f, 0.4f, 17f), c, false);
            Box("Перекрытие 2", at + new Vector3(0f, 6.8f, 4f), new Vector3(29f, 0.4f, 17f), c, false);
            Box("Плиты", at + new Vector3(17f, 0.7f, -6f), new Vector3(10f, 1.4f, 5f), c);
            Box("Плиты 2", at + new Vector3(19f, 1.6f, -2f), new Vector3(6f, 0.5f, 7f), c);

            var crane = MatLib.Solid(new Color(0.62f, 0.42f, 0.14f), 0.25f, 0.3f);
            Box("Кран стойка", at + new Vector3(-20f, 9f, 4f), new Vector3(1.4f, 18f, 1.4f), crane);
            Box("Кран стрела", at + new Vector3(-8f, 17.6f, 4f), new Vector3(26f, 0.9f, 0.9f), crane, false);
            Box("Кран противовес", at + new Vector3(-25f, 17.6f, 4f), new Vector3(4f, 1.6f, 1.6f), crane, false);

            Box("Бытовка", at + new Vector3(20f, 1.4f, 10f), new Vector3(7f, 2.8f, 4f),
                MatLib.Get("rust", new Vector2(3f, 1f)));
            Box("Катушка провода", at + new Vector3(10f, 0.8f, -8f), new Vector3(1.8f, 1.6f, 1.8f),
                MatLib.Get("wood", new Vector2(1f, 1f)));

            // забор с воротами
            var fence = MatLib.Get("wood_blue", new Vector2(14f, 1f));
            Box("Забор юг л", at + new Vector3(-18f, 1.2f, -14f), new Vector3(24f, 2.4f, 0.3f), fence);
            Box("Забор юг п", at + new Vector3(18f, 1.2f, -14f), new Vector3(24f, 2.4f, 0.3f), fence);
            Box("Ворота стройки", at + new Vector3(0f, 1.2f, -14f), new Vector3(12f, 2.4f, 0.25f),
                MatLib.Get("rust", new Vector2(6f, 1f)));
            Box("Забор запад", at + new Vector3(-30f, 1.2f, 0f), new Vector3(0.3f, 2.4f, 28f), fence);
            Box("Забор восток", at + new Vector3(30f, 1.2f, 0f), new Vector3(0.3f, 2.4f, 28f), fence);

            Spot("construction", at + new Vector3(14f, 0f, -8f), 4f);
            Spot("construction", at + new Vector3(-14f, 0f, 8f), 4f);
        }

        // ============================================================ деревья

        private static void Perimeter()
        {
            var rnd = new System.Random(9081);

            void Row(float x0, float z0, float dx, float dz, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float jx = (float)rnd.NextDouble() * 6f - 3f;
                    float jz = (float)rnd.NextDouble() * 6f - 3f;
                    Tree(new Vector3(x0 + dx * i + jx, 0f, z0 + dz * i + jz),
                         0.85f + (float)rnd.NextDouble() * 0.55f);
                }
            }

            Row(-120f, 56f, 12f, 0f, 9);     // вдоль дороги на западе
            Row(60f, 56f, 12f, 0f, 8);       // и на востоке
            Row(-130f, -80f, 14f, 0f, 18);   // за гаражами
            Row(-140f, -60f, 0f, 14f, 10);   // западная кромка
            Row(112f, -60f, 0f, 14f, 10);    // восточная кромка
            Row(-120f, 92f, 16f, 0f, 14);    // за стройкой
        }

        private static void Tree(Vector3 at, float scale = 1f)
        {
            float k = scale;
            Cyl("Ствол", at + Vector3.up * 2.8f * k, new Vector3(0.65f * k, 2.8f * k, 0.65f * k),
                MatLib.Get("wood", new Vector2(1f, 3f)));
            var f = MatLib.Get("foliage", new Vector2(2f, 2f));
            Ball("Крона 1", at + new Vector3(0f, 5.6f * k, 0f), new Vector3(5f, 4.6f, 5f) * k, f);
            Ball("Крона 2", at + new Vector3(1.3f * k, 4.8f * k, 0.7f * k), new Vector3(3.2f, 3f, 3.2f) * k, f);
            Ball("Крона 3", at + new Vector3(-1.2f * k, 5f * k, -0.6f * k), new Vector3(3f, 2.8f, 3f) * k, f);
        }
    }
}
