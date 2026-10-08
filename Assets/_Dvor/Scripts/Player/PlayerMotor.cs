using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>Ходьба, бег, приседание и обзор от первого лица. Работает только у владельца.</summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerState))]
    public class PlayerMotor : NetworkBehaviour
    {
        public float walkSpeed   = 4.2f;
        public float runSpeed    = 6.4f;
        public float crouchSpeed = 1.9f;
        public float mouseSens   = 2.2f;

        private CharacterController _cc;
        private PlayerState _state;
        private Camera _cam;
        private CameraFeel _feel;
        private float _pitch;
        private float _vy;
        private bool _crouched;

        public static bool CursorFree;

        private const float StandHeight  = 1.8f;
        private const float CrouchHeight = 1.15f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _state = GetComponent<PlayerState>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            // своя камера появилась — камера меню больше не нужна
            var menuCam = GameObject.Find("Камера меню");
            if (menuCam != null) menuCam.SetActive(false);

            var camGo = new GameObject("Камера");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.tag = "MainCamera";
            _cam = camGo.AddComponent<Camera>();
            _cam.nearClipPlane = 0.05f;
            _cam.fieldOfView = 70f;
            MapBuilder.ApplySky(_cam);
            camGo.AddComponent<AudioListener>();

            _feel = camGo.AddComponent<CameraFeel>();
            _feel.owner = _state;

            // своё тело в кадре только мешает
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;

            LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            CursorFree = !locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(CursorFree);

            bool frozen = _state.Status.Value == PlayerStatus.Arrested;
            var sm = GameStateMachine.I;
            if (sm != null && sm.Phase.Value == GamePhase.Meeting) frozen = true;

            if (!CursorFree)
            {
                float mx = Input.GetAxisRaw("Mouse X") * mouseSens;
                float my = Input.GetAxisRaw("Mouse Y") * mouseSens;
                transform.Rotate(0f, mx, 0f);
                _pitch = Mathf.Clamp(_pitch - my, -80f, 80f);
                if (_cam) _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            // приседание: медленнее, тише, ниже — и можно спрятаться за машиной
            bool wantCrouch = !frozen && !CursorFree && Input.GetKey(KeyCode.LeftControl);
            if (wantCrouch != _crouched)
            {
                _crouched = wantCrouch;
                _cc.height = _crouched ? CrouchHeight : StandHeight;
                _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            }

            Vector3 move = Vector3.zero;
            bool sprinting = false;

            if (!frozen && !CursorFree)
            {
                float h = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                float v = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

                sprinting = Input.GetKey(KeyCode.LeftShift) && !_crouched && v > 0f;
                float speed = _crouched ? crouchSpeed : (sprinting ? runSpeed : walkSpeed);
                if (_state.Role.Value == RoleId.Gopnik) speed *= 1.1f;
                if (_state.Role.Value == RoleId.Sportik && sprinting) speed *= 1.08f;

                move = (transform.right * h + transform.forward * v).normalized * speed;
            }

            _vy = _cc.isGrounded ? -1f : _vy + Physics.gravity.y * Time.deltaTime;
            Vector3 step = move;
            step.y = _vy;
            _cc.Move(step * Time.deltaTime);

            if (_feel != null)
            {
                _feel.Speed01 = Mathf.Clamp01(move.magnitude / runSpeed);
                _feel.Sprinting = sprinting;
                _feel.Crouched = _crouched;
            }
        }
    }
}
