using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Голос по дистанции без внешних SDK: микрофон → сервер → только тем,
    /// кто реально рядом. Отсечка по дальности делается НА СЕРВЕРЕ — иначе
    /// чужой пакет можно было бы подслушать через полкарты.
    ///
    /// 8 кГц, 8 бит — примерно 8 КБ/с на говорящего. Для комнаты на 12 человек нормально.
    /// Для релиза лучше заменить на Vivox: там эхоподавление и модерация из коробки.
    /// </summary>
    [RequireComponent(typeof(PlayerState))]
    public class ProximityVoice : NetworkBehaviour
    {
        private const int Rate = Balance.VoiceSampleRate;
        private const int FrameSamples = 800;      // 100 мс

        private PlayerState _me;
        private AudioClip _mic;
        private int _lastMicPos;
        private readonly float[] _buf = new float[FrameSamples * 4];

        private AudioSource _out;
        private readonly Queue<float> _incoming = new();
        private readonly object _lock = new();

        public static bool MicEnabled = true;
        public bool IsSpeaking { get; private set; }
        private float _speakDecay;

        private void Awake() => _me = GetComponent<PlayerState>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner) StartMic();
            else SetupOutput();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && Microphone.devices.Length > 0) Microphone.End(null);
        }

        private void StartMic()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Двор] Микрофон не найден — голос выключен");
                return;
            }
            _mic = Microphone.Start(null, true, 2, Rate);
            _lastMicPos = 0;
        }

        private void SetupOutput()
        {
            _out = gameObject.AddComponent<AudioSource>();
            _out.spatialBlend = 1f;
            _out.rolloffMode = AudioRolloffMode.Logarithmic;
            _out.minDistance = Balance.VoiceFullRange;
            _out.maxDistance = Balance.VoiceMaxRange;
            _out.loop = true;
            _out.clip = AudioClip.Create("voice", Rate, 1, Rate, true, OnAudioRead);
            _out.Play();
        }

        private void OnAudioRead(float[] data)
        {
            lock (_lock)
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = _incoming.Count > 0 ? _incoming.Dequeue() : 0f;
            }
        }

        private void Update()
        {
            if (_speakDecay > 0f) { _speakDecay -= Time.deltaTime; if (_speakDecay <= 0f) IsSpeaking = false; }
            if (!IsOwner || _mic == null || !MicEnabled) return;

            int pos = Microphone.GetPosition(null);
            int avail = pos - _lastMicPos;
            if (avail < 0) avail += _mic.samples;
            if (avail < FrameSamples) return;

            int take = Mathf.Min(avail, FrameSamples);   // 800 байт — влезает в один UDP-пакет
            _mic.GetData(_buf, _lastMicPos);
            _lastMicPos = (_lastMicPos + take) % _mic.samples;

            float peak = 0f;
            var bytes = new byte[take];
            for (int i = 0; i < take; i++)
            {
                float s = Mathf.Clamp(_buf[i], -1f, 1f);
                if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s);
                bytes[i] = (byte)Mathf.RoundToInt((s + 1f) * 127.5f);
            }

            if (peak < 0.02f) return;   // тишину не шлём
            IsSpeaking = true; _speakDecay = 0.25f;
            SendVoiceRpc(bytes);
        }

        // ------------------------------------------------------ сервер

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendVoiceRpc(byte[] data)
        {
            var sm = GameStateMachine.I;
            if (sm == null) return;

            var targets = new List<ulong>();
            bool meeting = sm.Phase.Value == GamePhase.Meeting;
            bool arrested = _me.Status.Value == PlayerStatus.Arrested;

            foreach (var p in sm.Players)
            {
                if (p == _me) continue;

                if (arrested)
                {
                    if (p.Status.Value == PlayerStatus.Arrested) targets.Add(p.OwnerClientId);
                    continue;
                }
                if (p.Status.Value == PlayerStatus.Arrested) continue;

                if (meeting && p.IsAlive) { targets.Add(p.OwnerClientId); continue; }

                float d = Vector3.Distance(p.transform.position, _me.transform.position);
                if (d <= Balance.VoiceMaxRange) targets.Add(p.OwnerClientId);
            }

            if (targets.Count == 0) return;
            RelayVoiceRpc(data, RpcTarget.Group(targets, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void RelayVoiceRpc(byte[] data, RpcParams p)
        {
            IsSpeaking = true; _speakDecay = 0.25f;
            if (_out == null) SetupOutput();

            lock (_lock)
            {
                if (_incoming.Count > Rate) _incoming.Clear();   // отстали — рвём, а не копим
                for (int i = 0; i < data.Length; i++)
                    _incoming.Enqueue(data[i] / 127.5f - 1f);
            }
        }
    }
}
