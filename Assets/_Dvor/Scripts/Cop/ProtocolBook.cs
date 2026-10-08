using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Папка участкового. Протокол пишется только глядя на нарушителя, с дистанции
    /// и стоя на месте — поэтому участковый всегда опаздывает, и это задумано.
    /// </summary>
    [RequireComponent(typeof(PlayerState))]
    public class ProtocolBook : NetworkBehaviour
    {
        private PlayerState _me;

        public PlayerState Target { get; private set; }
        public float HoldProgress { get; private set; }
        public int ChecksLeft { get; private set; } = Balance.ChecksPerDay;
        public bool PatrolCalledToday { get; private set; }

        private float _hold;
        private Vector3 _holdFrom;

        private void Awake() => _me = GetComponent<PlayerState>();

        public override void OnNetworkSpawn()
        {
            if (IsServer && GameStateMachine.I != null)
                GameStateMachine.I.OnPhase += ServerOnPhase;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && GameStateMachine.I != null)
                GameStateMachine.I.OnPhase -= ServerOnPhase;
        }

        private void ServerOnPhase(GamePhase p)
        {
            if (p != GamePhase.Morning) return;
            ChecksLeft = Balance.ChecksPerDay;
            PatrolCalledToday = false;
            ResetDayRpc(RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SyncCopStateRpc(int checksLeft, bool patrolCalled, RpcParams p)
        {
            ChecksLeft = checksLeft;
            PatrolCalledToday = patrolCalled;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ResetDayRpc(RpcParams p)
        {
            ChecksLeft = Balance.ChecksPerDay;
            PatrolCalledToday = false;
        }

        private void Update()
        {
            if (!IsOwner || !_me.IsCop || PlayerMotor.CursorFree) return;
            var sm = GameStateMachine.I;
            if (sm == null || sm.Phase.Value != GamePhase.Day) { Target = null; return; }

            Target = FindInSight();

            // F — протокол
            if (Target != null && Input.GetKey(KeyCode.F))
            {
                if (_hold <= 0f) _holdFrom = transform.position;
                if (Vector3.Distance(_holdFrom, transform.position) > 0.7f) { _hold = 0f; }
                else
                {
                    _hold += Time.deltaTime;
                    HoldProgress = Mathf.Clamp01(_hold / Balance.ProtocolHoldSeconds);
                    if (_hold >= Balance.ProtocolHoldSeconds)
                    {
                        WriteProtocolRpc(Target.OwnerClientId);
                        _hold = 0f; HoldProgress = 0f;
                    }
                }
            }
            else { _hold = 0f; HoldProgress = 0f; }

            var near = NearestPlayer(Balance.CopReachDistance);
            if (near != null && Input.GetKeyDown(KeyCode.G)) CheckDocsRpc(near.OwnerClientId);
            if (near != null && Input.GetKeyDown(KeyCode.H)) TalkRpc(near.OwnerClientId);

            if (Input.GetKeyDown(KeyCode.R))
            {
                var police = DeedSpot.Nearest(transform.position, "police");
                if (police != null) CallPatrolRpc();
                else sm.ToastRpc("Звонить можно только из опорного пункта");
            }
        }

        private PlayerState NearestPlayer(float range)
        {
            PlayerState best = null; float bd = range;
            foreach (var p in GameStateMachine.I.Players)
            {
                if (p == _me || !p.IsAlive) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        private PlayerState FindInSight()
        {
            PlayerState best = null; float bestDot = 0.82f;
            foreach (var p in GameStateMachine.I.Players)
            {
                if (p == _me || !p.IsAlive) continue;
                if (p.ActingDeed.Value.IsEmpty) continue;

                Vector3 to = p.transform.position - transform.position;
                if (to.sqrMagnitude > Balance.ProtocolDistance * Balance.ProtocolDistance) continue;

                var cam = GetComponentInChildren<Camera>();
                Vector3 fwd = cam ? cam.transform.forward : transform.forward;
                float dot = Vector3.Dot(fwd, to.normalized);
                if (dot < bestDot) continue;
                if (!GameStateMachine.HasLineOfSight(transform.position, p.transform.position)) continue;

                bestDot = dot; best = p;
            }
            return best;
        }

        // ------------------------------------------------------ сервер

        [Rpc(SendTo.Server)]
        private void WriteProtocolRpc(ulong targetId)
        {
            var sm = GameStateMachine.I;
            if (!_me.IsCop || sm.Phase.Value != GamePhase.Day) return;

            var t = sm.Find(targetId);
            if (t == null || !t.IsAlive || !t.IsCommittingCrimeNow) return;
            if (Vector3.Distance(transform.position, t.transform.position) > Balance.ProtocolDistance) return;

            t.ServerAddProtocol();
            sm.ToastToRpc(OwnerClientId, $"Протокол на {t.Nick.Value} ({t.ProtocolCount}/{Balance.ProtocolsForArrest})");
            sm.ToastToRpc(targetId, "На тебя составили протокол");
        }

        [Rpc(SendTo.Server)]
        private void CheckDocsRpc(ulong targetId)
        {
            var sm = GameStateMachine.I;
            if (!_me.IsCop || ChecksLeft <= 0) return;

            var t = sm.Find(targetId);
            if (t == null || !t.IsAlive) return;
            if (Vector3.Distance(transform.position, t.transform.position) > Balance.CopReachDistance) return;

            ChecksLeft--;
            SyncCopStateRpc(ChecksLeft, PatrolCalledToday, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            StartCoroutine(HoldCheck(t));
            sm.ToastToRpc(targetId, "Участковый проверяет документы — стой");
        }

        private System.Collections.IEnumerator HoldCheck(PlayerState t)
        {
            t.IsBeingChecked = true;
            yield return new WaitForSeconds(Balance.CheckSeconds);
            t.IsBeingChecked = false;
        }

        [Rpc(SendTo.Server)]
        private void TalkRpc(ulong targetId)
        {
            var sm = GameStateMachine.I;
            if (!_me.IsCop) return;
            var t = sm.Find(targetId);
            if (t == null || !t.IsAlive) return;
            if (Vector3.Distance(transform.position, t.transform.position) > Balance.CopReachDistance) return;

            if (t.Boredom.Value > 50f) t.ServerRelieveBoredom(t.Boredom.Value - 50f);
            t.ServerRemoveProtocol();
            sm.ToastToRpc(targetId, "Профилактическая беседа. Один протокол списан");
        }

        [Rpc(SendTo.Server)]
        private void CallPatrolRpc()
        {
            var sm = GameStateMachine.I;
            if (!_me.IsCop || PatrolCalledToday) return;

            PlayerState best = null;
            foreach (var p in sm.Players)
                if (p.IsAlive && !p.IsCop && p.ProtocolCount >= Balance.ProtocolsForArrest)
                    if (best == null || p.ProtocolCount > best.ProtocolCount) best = p;

            if (best == null) { sm.ToastToRpc(OwnerClientId, "Не на кого — нужно три протокола"); return; }

            PatrolCalledToday = true;
            SyncCopStateRpc(ChecksLeft, true, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            sm.ServerSchedulePatrol(best);
            sm.ToastRpc("Участковый кому-то звонил из опорного пункта");
            sm.ToastToRpc(OwnerClientId, $"Наряд поедет за {best.Nick.Value}");
        }
    }
}
