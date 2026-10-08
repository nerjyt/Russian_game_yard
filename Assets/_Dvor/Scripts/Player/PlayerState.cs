using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Состояние игрока. Роль, стукачество и Скуку читает только владелец —
    /// всё остальное было бы готовым читом.
    /// </summary>
    public class PlayerState : NetworkBehaviour
    {
        public readonly NetworkVariable<RoleId> Role = new(
            RoleId.None, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsSnitch = new(
            false, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> Boredom = new(
            0f, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<PlayerStatus> Status = new(PlayerStatus.Free);
        /// <summary>Участковый публичен по дизайну — этот флаг видят все, в отличие от Role.</summary>
        public readonly NetworkVariable<bool> PublicIsCop = new(false);
        public readonly NetworkVariable<FixedString32Bytes> Nick = new();
        /// <summary>Что игрок делает прямо сейчас — видят ВСЕ. Без этого не бывает свидетелей.</summary>
        public readonly NetworkVariable<FixedString32Bytes> ActingDeed = new();
        public readonly NetworkVariable<int> MyProtocols = new(
            0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        // владелец: список дел на сегодня
        public readonly List<string> TodayDeeds = new();
        public readonly HashSet<string> DoneDeeds = new();

        // сервер
        public bool IsCommittingCrimeNow { get; private set; }
        public bool IsBeingChecked { get; set; }
        public int ProtocolCount => MyProtocols.Value;
        public bool UsedMeetingCall { get; set; }

        public bool IsAlive => Status.Value == PlayerStatus.Free;
        public bool IsCop => Role.Value == RoleId.Cop;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                GameStateMachine.I?.ServerRegister(this);
                Nick.Value = $"Игрок {OwnerClientId + 1}";
            }
            if (IsOwner) gameObject.name = "ЛокальныйИгрок";

            // шаги слышно у всех, считаются локально по движению
            var steps = gameObject.AddComponent<FootstepFeel>();
            steps.owner = this;

            var body = gameObject.AddComponent<Humanoid>();
            body.owner = this;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) GameStateMachine.I?.ServerUnregister(this);
        }

        private void Update()
        {
            if (!IsServer) return;
            var sm = GameStateMachine.I;
            if (sm == null || sm.Phase.Value != GamePhase.Day) return;
            if (!IsAlive || IsCop) return;

            float mult = IsBeingChecked ? Balance.BoredomCheckMult : 1f;
            Boredom.Value = Mathf.Min(100f, Boredom.Value + Balance.BoredomPerMinute / 60f * Time.deltaTime * mult);

            ServerTickBreakdown();
        }

        private float _breakTimer;

        /// <summary>
        /// Срыв. На максимуме Скуки персонаж делает своё дело сам, там где стоит,
        /// без спроса игрока. Проигрыш получается смешным, а не обидным.
        /// </summary>
        private void ServerTickBreakdown()
        {
            if (Boredom.Value < Balance.BoredomBreak) { _breakTimer = 0f; return; }
            if (!ActingDeed.Value.IsEmpty) return;

            _breakTimer += Time.deltaTime;
            if (_breakTimer < Balance.BreakdownEvery) return;
            _breakTimer = 0f;

            var role = RoleCatalog.Get(Role.Value);
            if (role == null || role.Deeds.Length == 0) return;

            DeedDef pick = null;
            foreach (var d in role.Deeds) if (d.Crime) { pick = d; break; }
            if (pick == null) return;

            GameStateMachine.I.ServerForceBreakdown(this, pick);
        }

        // -------------------------------------------------- сервер

        /// <summary>Позицию двигает владелец, поэтому телепорт — это просьба владельцу, а не запись здесь.</summary>
        public void ServerTeleport(Vector3 pos)
        {
            if (!IsServer) return;
            TeleportRpc(pos, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void TeleportRpc(Vector3 pos, RpcParams p)
        {
            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            transform.position = pos;
            if (cc) cc.enabled = true;
        }

        public void ServerReset()
        {
            if (!IsServer) return;
            Status.Value = PlayerStatus.Free;
            Boredom.Value = 0f;
            MyProtocols.Value = 0;
            ActingDeed.Value = default;
            IsCommittingCrimeNow = false;
            IsBeingChecked = false;
            UsedMeetingCall = false;
        }

        public void ServerNewDay()
        {
            if (!IsServer) return;
            IsBeingChecked = false;
        }

        public void ServerSetRole(RoleId role, bool snitch)
        {
            if (!IsServer) return;
            Role.Value = role;
            IsSnitch.Value = snitch;
            PublicIsCop.Value = role == RoleId.Cop;
        }

        public void ServerSetDeeds(List<string> deedIds)
        {
            if (!IsServer) return;
            SyncDeedsRpc(string.Join(",", deedIds), RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SyncDeedsRpc(string csv, RpcParams p)
        {
            TodayDeeds.Clear();
            DoneDeeds.Clear();
            foreach (var id in csv.Split(','))
                if (!string.IsNullOrEmpty(id)) TodayDeeds.Add(id);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void MarkDeedDoneRpc(string deedId, RpcParams p) => DoneDeeds.Add(deedId);

        public void ServerBeginDeed(DeedDef d)
        {
            if (!IsServer) return;
            ActingDeed.Value = d.Id;
            IsCommittingCrimeNow = d.Crime;
        }

        public void ServerEndDeed()
        {
            if (!IsServer) return;
            ActingDeed.Value = default;
            IsCommittingCrimeNow = false;
        }

        public void ServerRelieveBoredom(float amount)
        {
            if (!IsServer) return;
            Boredom.Value = Mathf.Max(0f, Boredom.Value - amount);
        }

        public bool ServerAddProtocol()
        {
            if (!IsServer) return false;
            MyProtocols.Value++;
            return MyProtocols.Value >= Balance.ProtocolsForArrest;
        }

        public void ServerRemoveProtocol()
        {
            if (!IsServer) return;
            MyProtocols.Value = Mathf.Max(0, MyProtocols.Value - 1);
        }

        public void ServerSetStatus(PlayerStatus s)
        {
            if (!IsServer) return;
            Status.Value = s;
            ServerEndDeed();
            if (s == PlayerStatus.Arrested) ServerTeleport(MapBuilder.VanPoint);
            if (s == PlayerStatus.Banished) ServerTeleport(MapBuilder.BanishPoint);
        }
    }
}
