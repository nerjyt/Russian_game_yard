using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Делание дел. Клиент удерживает кнопку и ПРОСИТ — эффект применяет сервер,
    /// перепроверив фазу, дистанцию, роль и напарника.
    /// </summary>
    [RequireComponent(typeof(PlayerState))]
    public class PlayerInteractor : NetworkBehaviour
    {
        private PlayerState _me;

        public DeedDef HoverDeed { get; private set; }
        public DeedSpot HoverSpot { get; private set; }
        public float HoldProgress { get; private set; }
        public int CarriedEvidenceId { get; private set; } = -1;
        public string CarriedLabel { get; private set; } = "";

        private float _hold;

        private void Awake() => _me = GetComponent<PlayerState>();

        private void Update()
        {
            if (!IsOwner) return;
            var sm = GameStateMachine.I;
            if (sm == null) return;

            RefreshCarried();

            if (sm.Phase.Value != GamePhase.Day || !_me.IsAlive)
            {
                HoverDeed = null; HoverSpot = null; _hold = 0f; HoldProgress = 0f;
                return;
            }

            FindHover();
            HandleHold();
            HandleEvidence();
            HandleMeetingCall();
        }

        private void RefreshCarried()
        {
            var sm = GameStateMachine.I;
            CarriedEvidenceId = -1; CarriedLabel = "";
            if (sm.Evidence == null) return;
            for (int i = 0; i < sm.Evidence.Count; i++)
            {
                var e = sm.Evidence[i];
                if (e.Holder == NetworkManager.LocalClientId)
                {
                    CarriedEvidenceId = e.Id;
                    CarriedLabel = e.Label.ToString();
                    return;
                }
            }
        }

        private void FindHover()
        {
            HoverDeed = null; HoverSpot = null;

            foreach (var deedId in _me.TodayDeeds)
            {
                if (_me.DoneDeeds.Contains(deedId)) continue;
                var deed = RoleCatalog.FindDeed(deedId);
                if (deed == null) continue;

                var spot = DeedSpot.Nearest(transform.position, deed.SpotTag);
                if (spot == null) continue;

                HoverDeed = deed; HoverSpot = spot;
                return;
            }
        }

        private void HandleHold()
        {
            if (HoverDeed == null) { _hold = 0f; HoldProgress = 0f; return; }

            if (Input.GetKey(KeyCode.E) && !PlayerMotor.CursorFree)
            {
                _hold += Time.deltaTime;
                HoldProgress = Mathf.Clamp01(_hold / HoverDeed.Hold);
                if (_hold >= HoverDeed.Hold)
                {
                    RequestDeedRpc(HoverDeed.Id, HoverSpot.Index);
                    _hold = 0f; HoldProgress = 0f;
                }
            }
            else { _hold = 0f; HoldProgress = 0f; }
        }

        private void HandleEvidence()
        {
            if (!Input.GetKeyDown(KeyCode.Q) || PlayerMotor.CursorFree) return;
            var sm = GameStateMachine.I;

            if (sm.Evidence == null) return;

            if (CarriedEvidenceId >= 0)
            {
                // у помойки — уничтожаем, иначе подбрасываем
                var dump = DeedSpot.Nearest(transform.position, "recycling");
                sm.DropEvidenceRpc(CarriedEvidenceId, dump != null);
                return;
            }

            int bestId = -1; float bestD = 3f;
            for (int i = 0; i < sm.Evidence.Count; i++)
            {
                var e = sm.Evidence[i];
                if (!e.OnGround) continue;
                float d = Vector3.Distance(transform.position, e.Pos);
                if (d < bestD) { bestD = d; bestId = e.Id; }
            }
            if (bestId >= 0) sm.PickEvidenceRpc(bestId);
        }

        private void HandleMeetingCall()
        {
            if (!Input.GetKeyDown(KeyCode.M) || PlayerMotor.CursorFree) return;
            if (Vector3.Distance(transform.position, MapBuilder.GazeboPoint) > 6f) return;
            MeetingManager.I?.CallMeetingRpc();
        }

        // ------------------------------------------------------ сервер

        [Rpc(SendTo.Server)]
        private void RequestDeedRpc(string deedId, int spotIndex)
        {
            var sm = GameStateMachine.I;
            if (sm.Phase.Value != GamePhase.Day || !_me.IsAlive) return;

            var deed = RoleCatalog.FindDeed(deedId);
            if (deed == null) return;

            // дело должно быть своё: либо из роли, либо стукаческое
            var role = RoleCatalog.Get(_me.Role.Value);
            bool mine = role != null && role.Deeds.Any(d => d.Id == deedId);
            bool snitchDeed = deed.SnitchOnly && _me.IsSnitch.Value;
            if (!mine && !snitchDeed) return;

            var spot = DeedSpot.ByIndex(spotIndex);
            if (spot == null || spot.Tag != deed.SpotTag) return;
            if (!spot.InRange(transform.position)) return;

            if (deed.NeedsPartner && !PartnerNear(spot.transform.position))
            {
                sm.ToastToRpc(OwnerClientId, "Нужен второй человек рядом");
                return;
            }

            StartCoroutine(RunDeed(deed));
        }

        private System.Collections.IEnumerator RunDeed(DeedDef deed)
        {
            _me.ServerBeginDeed(deed);
            if (!string.IsNullOrEmpty(deed.SoundId))
                GameStateMachine.I.NoiseRpc(transform.position, deed.SoundId, deed.NoiseRadius);
            yield return new WaitForSeconds(0.6f);

            _me.ServerRelieveBoredom(deed.Relief);
            GameStateMachine.I.ServerReportDeed(_me, deed);

            if (deed.SnitchOnly) GameStateMachine.I.ServerSnitchReport(_me);

            _me.MarkDeedDoneRpc(deed.Id, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            _me.ServerEndDeed();
        }

        private bool PartnerNear(Vector3 pos)
        {
            var sm = GameStateMachine.I;
            foreach (var o in sm.Players)
            {
                if (o == _me || !o.IsAlive) continue;
                if (Vector3.Distance(o.transform.position, pos) <= Balance.PartnerRange) return true;
            }
            return false;
        }
    }
}
