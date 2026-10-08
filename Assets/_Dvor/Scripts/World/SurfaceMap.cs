using System.Collections.Generic;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Какая поверхность под ногами. Не лучами по коллайдерам, а прямоугольниками
    /// на плане — так дешевле, детерминированно и одинаково у всех.
    /// Зоны проверяются с конца: положенная позже перекрывает лежащую под ней.
    /// </summary>
    public static class SurfaceMap
    {
        public enum Kind { Grass, Asphalt, Concrete, Dirt, Wood, Metal, Gravel }

        private static readonly List<(Rect area, Kind kind)> Zones = new();

        public static void Clear() => Zones.Clear();

        public static void Add(Vector3 center, float sizeX, float sizeZ, Kind kind)
            => Zones.Add((new Rect(center.x - sizeX * 0.5f, center.z - sizeZ * 0.5f, sizeX, sizeZ), kind));

        public static Kind At(Vector3 p)
        {
            var v = new Vector2(p.x, p.z);
            for (int i = Zones.Count - 1; i >= 0; i--)
                if (Zones[i].area.Contains(v)) return Zones[i].kind;
            return Kind.Grass;
        }

        public static string StepSound(Kind k) => k switch
        {
            Kind.Asphalt  => "step_asphalt",
            Kind.Concrete => "step_concrete",
            Kind.Dirt     => "step_dirt",
            Kind.Wood     => "step_wood",
            Kind.Metal    => "step_metal",
            Kind.Gravel   => "step_gravel",
            _             => "step_grass"
        };
    }
}
