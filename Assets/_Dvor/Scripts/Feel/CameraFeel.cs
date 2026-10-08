using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Живость камеры: покачивание при ходьбе, увод поля зрения на беге
    /// и дрожь от Скуки. Дрожь — это честное предупреждение игроку:
    /// «ты на грани, иди делай дело».
    /// </summary>
    public class CameraFeel : MonoBehaviour
    {
        public PlayerState owner;

        private Camera _cam;
        private Vector3 _base;
        private float _bobPhase;
        private float _fovBase = 70f;
        private float _shake;

        public float Speed01;      // ставит PlayerMotor
        public bool Sprinting;
        public bool Crouched;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _base = transform.localPosition;
            if (_cam) _fovBase = _cam.fieldOfView;
        }

        private void LateUpdate()
        {
            float boredom = owner != null ? owner.Boredom.Value : 0f;

            // покачивание
            _bobPhase += Time.deltaTime * (Sprinting ? 13f : 9f) * Speed01;
            float bobY = Mathf.Sin(_bobPhase) * 0.045f * Speed01;
            float bobX = Mathf.Cos(_bobPhase * 0.5f) * 0.035f * Speed01;

            // дрожь от Скуки
            float target = 0f;
            if (boredom >= Balance.BoredomBreak)   target = 0.055f;
            else if (boredom >= Balance.BoredomShaky)  target = 0.028f;
            else if (boredom >= Balance.BoredomNervous) target = 0.010f;
            _shake = Mathf.Lerp(_shake, target, Time.deltaTime * 3f);

            float sx = (Mathf.PerlinNoise(Time.time * 17f, 0f) - 0.5f) * _shake;
            float sy = (Mathf.PerlinNoise(0f, Time.time * 19f) - 0.5f) * _shake;

            float crouchDrop = Crouched ? -0.45f : 0f;
            transform.localPosition = _base + new Vector3(bobX + sx, bobY + sy + crouchDrop, 0f);

            float roll = Mathf.Sin(_bobPhase * 0.5f) * 0.7f * Speed01 + sx * 60f;
            transform.localRotation = Quaternion.Euler(transform.localEulerAngles.x, 0f, roll);

            if (_cam)
            {
                float fov = _fovBase + (Sprinting ? 7f : 0f) + (boredom >= Balance.BoredomBreak ? 4f : 0f);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, Time.deltaTime * 6f);
            }
        }
    }
}
