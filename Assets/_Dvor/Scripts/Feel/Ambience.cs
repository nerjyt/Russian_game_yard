using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Фон двора: ветер петлёй и редкие далёкие звуки. Без этого тишина
    /// между делами ощущается как выключенный звук, а не как двор.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        private AudioSource _wind;
        private float _next;

        private void Start()
        {
            _wind = gameObject.AddComponent<AudioSource>();
            _wind.clip = Sfx.Get("wind");
            _wind.loop = true;
            _wind.volume = 0.055f;
            _wind.spatialBlend = 0f;
            _wind.Play();
            _next = Random.Range(6f, 14f);

            // двор-колодец между двумя панельками честно гудит — без этого звуки плоские
            var zone = gameObject.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.City;
            zone.minDistance = 30f;
            zone.maxDistance = 140f;
        }

        private void Update()
        {
            _next -= Time.deltaTime;
            if (_next > 0f) return;
            _next = Random.Range(16f, 38f);

            var cam = Camera.main;
            if (cam == null) return;

            // звук приходит откуда-то из-за домов
            float a = Random.value * Mathf.PI * 2f;
            var pos = cam.transform.position + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * Random.Range(28f, 55f);

            string[] pool = { "dog", "dog", "car", "train", "door" };
            Sfx.PlayAt(pool[Random.Range(0, pool.Length)], pos, 0.35f, 90f);
        }
    }
}
