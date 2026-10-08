using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Сходка у беседки: созыв, спор, тайное голосование, 15 суток.
    /// Участковый говорит, но не голосует. Роль выгнанного не раскрывается —
    /// в этом главное отличие от мафии и причина, почему спор идёт дальше.
    /// </summary>
    public class MeetingManager : NetworkBehaviour
    {
        public static MeetingManager I { get; private set; }

        public readonly NetworkVariable<int> VotesCast = new(0);
        public readonly NetworkVariable<int> VotesNeeded = new(0);

        private readonly Dictionary<ulong, ulong> _votes = new();
        public bool LocalVoted { get; private set; }

        public override void OnNetworkSpawn()
        {
            I = this;
            if (IsServer && GameStateMachine.I != null)
                GameStateMachine.I.OnPhase += ServerOnPhase;
        }

        public override void OnNetworkDespawn()
        {
            if (I == this) I = null;
            if (IsServer && GameStateMachine.I != null)
                GameStateMachine.I.OnPhase -= ServerOnPhase;
        }

        private void ServerOnPhase(GamePhase p)
        {
            if (p == GamePhase.Meeting) ServerOpenMeeting();
        }

        [Rpc(SendTo.Server)]
        public void CallMeetingRpc(RpcParams p = default)
        {
            var sm = GameStateMachine.I;
            if (sm.Phase.Value != GamePhase.Day) return;

            var caller = sm.Find(p.Receive.SenderClientId);
            if (caller == null || !caller.IsAlive) return;
            if (caller.UsedMeetingCall) { sm.ToastToRpc(caller.OwnerClientId, "Ты уже созывал сходку"); return; }
            if (Vector3.Distance(caller.transform.position, MapBuilder.GazeboPoint) > 6f) return;

            caller.UsedMeetingCall = true;
            sm.NoiseRpc(MapBuilder.GazeboPoint, "pipe", 200f);
            sm.ToastRpc($"{caller.Nick.Value} бьёт по трубе — все к беседке");
            sm.ServerStartMeeting();
        }

        private void ServerOpenMeeting()
        {
            _votes.Clear();
            VotesCast.Value = 0;

            var alive = GameStateMachine.I.Players.Where(x => x.IsAlive).ToList();
            VotesNeeded.Value = alive.Count(x => !x.PublicIsCop.Value);

            for (int i = 0; i < alive.Count; i++)
            {
                float a = i * Mathf.PI * 2f / Mathf.Max(1, alive.Count);
                var pos = MapBuilder.GazeboPoint + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 7f + Vector3.up * 1.2f;
                alive[i].ServerTeleport(pos);
            }
            ResetVoteRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ResetVoteRpc() => LocalVoted = false;

        [Rpc(SendTo.Server)]
        public void CastVoteRpc(ulong targetId, RpcParams p = default)
        {
            var sm = GameStateMachine.I;
            if (sm.Phase.Value != GamePhase.Meeting) return;

            ulong voter = p.Receive.SenderClientId;
            var me = sm.Find(voter);
            if (me == null || !me.IsAlive || me.IsCop) return;
            if (_votes.ContainsKey(voter)) return;

            _votes[voter] = targetId;
            VotesCast.Value = _votes.Count;

            if (VotesCast.Value >= VotesNeeded.Value) ServerCloseMeeting();
        }

        public void ServerCloseMeeting()
        {
            if (!IsServer) return;
            var sm = GameStateMachine.I;
            if (sm.Phase.Value != GamePhase.Meeting) return;

            var tally = new Dictionary<ulong, int>();
            foreach (var t in _votes.Values)
            {
                if (t == ulong.MaxValue) continue;
                tally.TryGetValue(t, out int c);
                tally[t] = c + 1;
            }
            _votes.Clear();
            VotesCast.Value = 0;

            if (tally.Count == 0) { sm.ToastRpc("Все воздержались — никого не выгнали"); sm.ServerBackToDay(); return; }

            int top = tally.Values.Max();
            var leaders = tally.Where(kv => kv.Value == top).Select(kv => kv.Key).ToList();
            if (leaders.Count != 1) { sm.ToastRpc("Ничья — никого не выгнали"); sm.ServerBackToDay(); return; }

            var victim = sm.Find(leaders[0]);
            if (victim == null) { sm.ServerBackToDay(); return; }

            sm.ToastRpc($"{victim.Nick.Value} получает 15 суток. Кем он был — двор не узнает");
            sm.ServerReportBanish(victim);
            if (sm.Phase.Value == GamePhase.Meeting) sm.ServerBackToDay();
        }

        public void MarkLocalVoted() => LocalVoted = true;
    }
}
