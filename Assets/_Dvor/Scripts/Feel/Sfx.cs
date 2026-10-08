using System.Collections.Generic;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Звуки синтезируются кодом при запуске. Главное правило: всё низко и мягко.
    /// Синтез легко скатывается в звон — поэтому каждый звук проходит
    /// фильтр низких частот и мягкие края, а высокие частоты вырезаны.
    /// </summary>
    public static class Sfx
    {
        private const int Rate = 22050;

        /// <summary>Общая громкость всех звуков. Крутить здесь, если громко.</summary>
        public static float Master = 0.55f;

        private static readonly Dictionary<string, AudioClip> Cache = new();

        public static AudioClip Get(string name)
        {
            if (Cache.TryGetValue(name, out var c) && c != null) return c;
            c = Build(name);
            Cache[name] = c;
            return c;
        }

        public static void PlayAt(string name, Vector3 pos, float volume = 1f, float maxDist = 25f, float pitch = 1f)
        {
            var clip = Get(name);
            if (clip == null) return;

            var go = new GameObject($"sfx_{name}");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * Master;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 3f;
            src.maxDistance = maxDist;
            src.pitch = pitch * Random.Range(0.95f, 1.05f);
            src.dopplerLevel = 0f;        // от допплера синтез только гнусавит
            src.spread = 35f;             // чуть шире точки — звук перестаёт быть «из дырки»
            src.reverbZoneMix = 1.15f;    // двор гудит
            src.Play();
            Object.Destroy(go, clip.length / src.pitch + 0.25f);
        }

        // ================================================================= сборка

        private static AudioClip Build(string name)
        {
            switch (name)
            {
                // Шаг = удар пятки (низ, очень коротко) + шорох подошвы (шум).
                // Разница между поверхностями в том, чего в этой паре больше.
                case "step":
                case "step_asphalt":
                {
                    var d = New(0.13f);
                    Heel(d, 105f, 1f, 34f);
                    Scuff(d, 0.55f, 0.16f, 38f);
                    return Finish(name, d, 0.5f);
                }
                case "step_concrete":
                {
                    var d = New(0.16f);
                    Heel(d, 128f, 1f, 30f);
                    Scuff(d, 0.5f, 0.26f, 30f);       // звонче асфальта
                    Tone(d, 640f, 0.12f, 45f);
                    return Finish(name, d, 0.55f);
                }
                case "step_grass":
                {
                    var d = New(0.22f);
                    Heel(d, 78f, 0.35f, 40f);         // почти нет удара
                    Scuff(d, 1f, 0.30f, 13f);         // длинный мягкий шорох
                    return Finish(name, d, 0.36f);
                }
                case "step_dirt":
                {
                    var d = New(0.18f);
                    Heel(d, 85f, 0.6f, 34f);
                    Scuff(d, 0.8f, 0.12f, 20f);       // глухо, без верхов
                    return Finish(name, d, 0.42f);
                }
                case "step_gravel":
                {
                    var d = New(0.24f);
                    Heel(d, 95f, 0.5f, 36f);
                    Scuff(d, 1f, 0.42f, 16f);         // хруст щебня
                    Crunch(d, 5);
                    return Finish(name, d, 0.46f);
                }
                case "step_wood":
                {
                    var d = New(0.26f);
                    Heel(d, 150f, 1f, 22f);
                    Tone(d, 232f, 0.55f, 16f);        // доска гудит
                    Tone(d, 348f, 0.25f, 20f);
                    Scuff(d, 0.35f, 0.2f, 30f);
                    return Finish(name, d, 0.5f);
                }
                case "step_metal":
                {
                    var d = New(0.35f);
                    Heel(d, 170f, 0.8f, 26f);
                    Tone(d, 420f, 0.5f, 11f);         // лязг
                    Tone(d, 690f, 0.25f, 14f);
                    Scuff(d, 0.3f, 0.35f, 26f);
                    return Finish(name, d, 0.5f);
                }
                case "bottle":
                {
                    var d = New(0.5f);
                    Tone(d, 470f, 0.6f, 9f); Tone(d, 705f, 0.3f, 12f); Tone(d, 940f, 0.12f, 16f);
                    LowPass(d, 0.35f, 1);
                    return Finish(name, d, 0.45f);
                }
                case "glug":
                {
                    var d = New(0.6f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2f * Mathf.PI * (150f + 70f * Mathf.Sin(t * 21f)) * t);
                    }
                    Decay(d, 4f); LowPass(d, 0.2f, 2);
                    return Finish(name, d, 0.5f);
                }
                case "firework":
                {
                    var d = New(0.7f);
                    NoiseInto(d, 1f); Decay(d, 9f); LowPass(d, 0.22f, 2);
                    Tone(d, 70f, 0.8f, 11f);
                    return Finish(name, d, 0.85f);
                }
                case "spray":
                {
                    var d = New(0.9f);
                    NoiseInto(d, 1f); LowPass(d, 0.30f, 2);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] *= Mathf.Min(1f, t * 14f) * Mathf.Min(1f, (0.9f - t) * 5f);
                    }
                    return Finish(name, d, 0.3f);
                }
                case "metal":
                {
                    var d = New(0.55f);
                    Tone(d, 220f, 0.5f, 8f); Tone(d, 330f, 0.3f, 11f);
                    NoiseInto(d, 0.3f, true); LowPass(d, 0.22f, 2);
                    Decay(d, 6f);
                    return Finish(name, d, 0.45f);
                }
                case "punch":
                {
                    var d = New(0.22f);
                    Tone(d, 85f, 1f, 28f); NoiseInto(d, 0.45f, true);
                    LowPass(d, 0.14f, 3); Decay(d, 24f);
                    return Finish(name, d, 0.6f);
                }
                case "door":
                {
                    var d = New(0.35f);
                    Tone(d, 105f, 0.8f, 16f); NoiseInto(d, 0.3f, true);
                    LowPass(d, 0.12f, 3); Decay(d, 14f);
                    return Finish(name, d, 0.45f);
                }
                case "coin":
                {
                    var d = New(0.33f);
                    Tone(d, 780f, 0.5f, 14f); Tone(d, 1050f, 0.22f, 19f);
                    LowPass(d, 0.4f, 1);
                    return Finish(name, d, 0.3f);
                }
                case "pipe":
                {
                    var d = New(1.3f);
                    Tone(d, 196f, 0.6f, 3.0f); Tone(d, 294f, 0.3f, 3.6f); Tone(d, 392f, 0.15f, 4.4f);
                    LowPass(d, 0.3f, 1);
                    return Finish(name, d, 0.6f);
                }
                case "whistle":
                {
                    var d = New(0.6f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2f * Mathf.PI * (1150f + 90f * Mathf.Sin(t * 26f)) * t);
                    }
                    Decay(d, 5f); LowPass(d, 0.45f, 1);
                    return Finish(name, d, 0.28f);
                }
                case "siren":
                {
                    var d = New(2.4f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2f * Mathf.PI * (430f + 150f * Mathf.Sin(t * 4.6f)) * t);
                    }
                    LowPass(d, 0.3f, 1);
                    return Finish(name, d, 0.4f);
                }
                case "dog":
                {
                    var d = New(0.4f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2f * Mathf.PI * (170f + 110f * Mathf.Exp(-t * 16f)) * t);
                    }
                    NoiseInto(d, 0.25f, true);
                    Decay(d, 8f); LowPass(d, 0.2f, 2);
                    return Finish(name, d, 0.5f);
                }
                case "car":
                {
                    var d = New(1.8f);
                    Tone(d, 68f, 0.6f, 0f); Tone(d, 102f, 0.3f, 0f);
                    NoiseInto(d, 0.25f, true);
                    Hump(d); LowPass(d, 0.09f, 3);
                    return Finish(name, d, 0.5f);
                }
                case "train":
                {
                    var d = New(2.6f);
                    Tone(d, 124f, 0.55f, 0f); Tone(d, 186f, 0.3f, 0f);
                    Hump(d); LowPass(d, 0.13f, 2);
                    return Finish(name, d, 0.45f);
                }
                case "wind":
                {
                    // коричневый шум: интеграл белого, почти одни низы — не звенит
                    var d = New(5f);
                    float acc = 0f;
                    var rng = new System.Random(4242);
                    for (int i = 0; i < d.Length; i++)
                    {
                        acc = acc * 0.996f + ((float)rng.NextDouble() * 2f - 1f) * 0.05f;
                        d[i] = acc;
                    }
                    LowPass(d, 0.03f, 4);
                    Normalize(d, 0.7f);
                    for (int i = 0; i < d.Length; i++)
                        d[i] *= 0.65f + 0.35f * Mathf.Sin(i / (float)Rate * 0.55f);
                    Seamless(d, 0.6f);          // стык петли без щелчка
                    return Raw(name, d);
                }
                default: return null;
            }
        }

        // ================================================================= кирпичи

        private static float[] New(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];

        private static void Tone(float[] d, float hz, float amp, float decay)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = decay > 0f ? Mathf.Exp(-t * decay) : 1f;
                d[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * amp * env;
            }
        }

        private static void NoiseInto(float[] d, float amp, bool add = false)
        {
            var rng = new System.Random(1337);
            for (int i = 0; i < d.Length; i++)
            {
                float w = (float)rng.NextDouble() * 2f - 1f;
                if (add) d[i] += w * amp;
                else     d[i]  = w * amp;
            }
        }

        /// <summary>Удар пятки: короткий низкий импульс.</summary>
        private static void Heel(float[] d, float hz, float amp, float decay)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * amp * Mathf.Exp(-t * decay);
            }
        }

        /// <summary>Шорох подошвы: отфильтрованный шум поверх удара.</summary>
        private static void Scuff(float[] d, float amp, float bright, float decay)
        {
            var tmp = new float[d.Length];
            NoiseInto(tmp, 1f);
            LowPass(tmp, bright, 2);
            for (int i = 0; i < d.Length; i++)
                d[i] += tmp[i] * amp * Mathf.Exp(-(i / (float)Rate) * decay);
        }

        /// <summary>Отдельные камешки — от этого щебень слышно щебнем.</summary>
        private static void Crunch(float[] d, int grains)
        {
            var rng = new System.Random(77);
            for (int g = 0; g < grains; g++)
            {
                int at = rng.Next(d.Length / 6, d.Length - 1);
                float amp = 0.3f + (float)rng.NextDouble() * 0.4f;
                for (int i = at; i < Mathf.Min(d.Length, at + 260); i++)
                    d[i] += ((float)rng.NextDouble() * 2f - 1f) * amp * Mathf.Exp(-(i - at) / 90f);
            }
        }

        private static void Decay(float[] d, float k)
        {
            for (int i = 0; i < d.Length; i++)
                d[i] *= Mathf.Exp(-(i / (float)Rate) * k);
        }

        /// <summary>Проезд мимо: тише — громче — тише.</summary>
        private static void Hump(float[] d)
        {
            for (int i = 0; i < d.Length; i++)
                d[i] *= Mathf.Sin(i / (float)d.Length * Mathf.PI);
        }

        /// <summary>Однополюсный фильтр низких частот. a меньше — глуше.</summary>
        private static void LowPass(float[] d, float a, int passes)
        {
            for (int p = 0; p < passes; p++)
            {
                float y = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    y += (d[i] - y) * a;
                    d[i] = y;
                }
            }
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Края в ноль — иначе на старте и в конце слышен щелчок.</summary>
        private static void FadeEdges(float[] d, float inSec = 0.004f, float outSec = 0.03f)
        {
            int fi = Mathf.Min(d.Length / 2, Mathf.RoundToInt(inSec * Rate));
            int fo = Mathf.Min(d.Length / 2, Mathf.RoundToInt(outSec * Rate));
            for (int i = 0; i < fi; i++) d[i] *= i / (float)fi;
            for (int i = 0; i < fo; i++) d[d.Length - 1 - i] *= i / (float)fo;
        }

        /// <summary>Сшивает конец с началом, чтобы петля не щёлкала.</summary>
        private static void Seamless(float[] d, float seconds)
        {
            int n = Mathf.Min(d.Length / 3, Mathf.RoundToInt(seconds * Rate));
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                int tail = d.Length - n + i;
                float mix = d[tail] * (1f - k) + d[i] * k;
                d[tail] = mix;
            }
        }

        private static AudioClip Finish(string name, float[] d, float peak)
        {
            Normalize(d, peak);
            FadeEdges(d);
            return Raw(name, d);
        }

        private static AudioClip Raw(string name, float[] d)
        {
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
