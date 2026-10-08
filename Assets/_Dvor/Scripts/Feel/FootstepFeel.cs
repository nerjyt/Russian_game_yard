using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Шаги считаются локально по движению объекта — чужие слышно без единого
    /// сетевого пакета. Звук берётся по поверхности под ногами, ноги чередуются,
    /// на бегу шаг чаще и громче.
    /// </summary>
    public class FootstepFeel : MonoBehaviour
    {
        public PlayerState owner;

        private Vector3 _last;
        private float _accum;
        private float _stride = 2.0f;
        private bool _rightFoot;

        private void Start() => _last = transform.position;

        private void Update()
        {
            Vector3 p = transform.position;
            Vector3 d = p - _last;
            d.y = 0f;
            _last = p;

            float dist = d.magnitude;
            if (dist < 0.0005f) return;

            float speed = dist / Mathf.Max(0.0001f, Time.deltaTime);
            bool running = speed > 5f;
            bool sneaking = speed < 2.4f;

            _accum += dist;
            if (_accum < _stride) return;
            _accum = 0f;

            // на бегу шире и чаще, крадучись — мельче
            _stride = running ? 2.3f : sneaking ? 1.5f : 1.9f;
            _stride += Random.value * 0.2f;

            var kind = SurfaceMap.At(p);
            string sound = SurfaceMap.StepSound(kind);

            float volume = running ? 0.34f : sneaking ? 0.10f : 0.20f;
            if (owner != null && owner.Boredom.Value >= Balance.BoredomNervous) volume += 0.05f;

            float range = running ? 24f : sneaking ? 9f : 16f;

            _rightFoot = !_rightFoot;
            Sfx.PlayAt(sound, p, volume, range, _rightFoot ? 1.04f : 0.96f);
        }
    }
}
