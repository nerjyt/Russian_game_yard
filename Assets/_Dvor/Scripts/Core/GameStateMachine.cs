using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Сердце игры. Фазы, роли, улики, итоги. Вся логика — на сервере;
    /// клиент получает только то, что ему положено видеть.
    /// </summary>
    public class GameStateMachine : NetworkBehaviour
    {
        public static GameStateMachine I { get; private set; }

        public readonly NetworkVariable<GamePhase> Phase = new(GamePhase.Lobby);
        public readonly NetworkVariable<int> Day = new(0);
        public readonly NetworkVariable<double> PhaseEndsAt = new(0d);
        public readonly NetworkVariable<Side> Winner = new(Side.None);
        public readonly NetworkVariable<int> Arrests = new(0);

        public NetworkList<EvidenceRec> Evidence;

        public event System.Action<GamePhase> OnPhase;

        private readonly List<PlayerState> _players = new();
        public IReadOnlyList<PlayerState> Players => _players;

        // --- только сервер ---
        private int _wrongVotesInARow;
        private int _evidenceCounter;
        private PlayerState _pendingArrest;
        private readonly Dictionary<ulong, List<WitnessEntry>> _witness = new();

        private struct WitnessEntry { public ulong Actor; public string Deed; public double Time; }

        // --- финал, приходит всем ---
        public struct RevealRow { public ulong Client; public RoleId Role; public bool Snitch; public string Nick; }

        /// <summary>Строки папки участкового. Приходят только ему — счётчики протоколов чужая тайна.</summary>
        public struct BoardRow { public string Nick; public int Protocols; public string Note; }
        public static List<BoardRow> CopBoard = new();
        private float _boardTimer;
        public List<RevealRow> Reveal { get; private set; } = new();

        private void Awake() => Evidence = new NetworkList<EvidenceRec>();

        public override void OnNetworkSpawn()
        {
            I = this;
            Phase.OnValueChanged += (_, n) => OnPhase?.Invoke(n);
        }

        public override void OnNetworkDespawn() { if (I == this) I = null; }

        // ---------------------------------------------------------- реестр

        public void ServerRegister(PlayerState p)
        {
            if (!IsServer || _players.Contains(p)) return;
            _players.Add(p);
            _witness[p.OwnerClientId] = new List<WitnessEntry>();
        }

        public void ServerUnregister(PlayerState p)
        {
            if (!IsServer) return;
            _players.Remove(p);
            _witness.Remove(p.OwnerClientId);
        }

        public PlayerState Find(ulong clientId)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].OwnerClientId == clientId) return _players[i];
            return null;
        }

        public PlayerState Local()
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].IsOwner) return _players[i];
            return null;
        }

        public PlayerState ServerCop() => _players.FirstOrDefault(p => p.Role.Value == RoleId.Cop);

        public float SecondsLeft() =>
            Mathf.Max(0f, (float)(PhaseEndsAt.Value - NetworkManager.ServerTime.Time));

        // ---------------------------------------------------------- старт

        [Rpc(SendTo.Server)]
        public void StartMatchRpc()
        {
            if (Phase.Value != GamePhase.Lobby && Phase.Value != GamePhase.Finished) return;
            if (_players.Count < Balance.MinPlayers)
            {
                ToastRpc($"Нужно минимум {Balance.MinPlayers} игроков");
                return;
            }

            Day.Value = 0;
            Arrests.Value = 0;
            Winner.Value = Side.None;
            _wrongVotesInARow = 0;
            _pendingArrest = null;
            Evidence.Clear();
            foreach (var l in _witness.Values) l.Clear();

            foreach (var p in _players)
            {
                p.ServerReset();
                p.ServerTeleport(MapBuilder.SpawnPoint(_players.IndexOf(p)));
            }

            ServerAssignRoles();
            ServerEnter(GamePhase.Morning);
        }

        private void ServerAssignRoles()
        {
            var pool = _players.OrderBy(_ => Random.value).ToList();

            pool[0].ServerSetRole(RoleId.Cop, false);

            var roles = RoleCatalog.DvorRoles.OrderBy(_ => Random.value).ToList();
            for (int i = 1; i < pool.Count; i++)
                pool[i].ServerSetRole(roles[(i - 1) % roles.Count], false);

            int snitches = Balance.SnitchCountFor(pool.Count);
            foreach (var s in pool.Skip(1).OrderBy(_ => Random.value).Take(snitches))
                s.ServerSetRole(s.Role.Value, true);

            Debug.Log($"[Двор] Роли розданы. Игроков {pool.Count}, стукачей {snitches}");
        }

        private void ServerDealDeeds()
        {
            foreach (var p in _players)
            {
                if (!p.IsAlive || p.Role.Value == RoleId.Cop) continue;
                var role = RoleCatalog.Get(p.Role.Value);
                if (role == null) continue;

                var picked = role.Deeds.OrderBy(_ => Random.value)
                                       .Take(Mathf.Min(Balance.DeedsPerMorning, role.Deeds.Length))
                                       .Select(d => d.Id).ToList();

                if (p.IsSnitch.Value)
                    picked.Add(RoleCatalog.SnitchDeeds[Random.Range(0, RoleCatalog.SnitchDeeds.Length)].Id);

                p.ServerSetDeeds(picked);
            }
        }

        // ---------------------------------------------------------- фазы

        private void ServerEnter(GamePhase phase)
        {
            Phase.Value = phase;
            PhaseEndsAt.Value = NetworkManager.ServerTime.Time + Balance.SecondsFor(phase);
            OnPhase?.Invoke(phase);

            if (phase == GamePhase.Morning) ServerDealDeeds();
            if (phase == GamePhase.Evening) ServerResolveEvening();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Phase.Value is GamePhase.Lobby or GamePhase.Finished) return;
            ServerTickCopBoard();
            if (NetworkManager.ServerTime.Time < PhaseEndsAt.Value) return;
            ServerAdvance();
        }

        private void ServerTickCopBoard()
        {
            _boardTimer -= Time.deltaTime;
            if (_boardTimer > 0f) return;
            _boardTimer = 1f;

            var cop = ServerCop();
            if (cop == null) return;

            var sb = new System.Text.StringBuilder();
            foreach (var p in _players)
            {
                if (p.PublicIsCop.Value) continue;
                string note = p.Status.Value switch
                {
                    PlayerStatus.Arrested => "увезён",
                    PlayerStatus.Banished => "15 суток",
                    _ => ""
                };
                sb.Append(p.Nick.Value).Append('\u001f').Append(p.ProtocolCount).Append('\u001f').Append(note).Append('\u001e');
            }
            CopBoardRpc(sb.ToString(), RpcTarget.Single(cop.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void CopBoardRpc(string packed, RpcParams p)
        {
            CopBoard = new List<BoardRow>();
            foreach (var row in packed.Split('\u001e'))
            {
                if (string.IsNullOrEmpty(row)) continue;
                var f = row.Split('\u001f');
                if (f.Length < 3) continue;
                int.TryParse(f[1], out int n);
                CopBoard.Add(new BoardRow { Nick = f[0], Protocols = n, Note = f[2] });
            }
        }

        private void ServerAdvance()
        {
            switch (Phase.Value)
            {
                case GamePhase.Morning:
                    Day.Value++;
                    ServerEnter(GamePhase.Day);
                    break;
                case GamePhase.Day:
                    ServerEnter(GamePhase.Evening);
                    break;
                case GamePhase.Meeting:
                    MeetingManager.I?.ServerCloseMeeting();
                    if (Phase.Value == GamePhase.Meeting) ServerEnter(GamePhase.Day);
                    break;
                case GamePhase.Evening:
                    if (!ServerCheckWin())
                    {
                        if (Day.Value >= Balance.DaysPerMatch) ServerFinish(Side.Dvor);
                        else ServerEnter(GamePhase.Morning);
                    }
                    break;
            }
        }

        public void ServerStartMeeting()
        {
            if (!IsServer || Phase.Value != GamePhase.Day) return;
            ServerEnter(GamePhase.Meeting);
        }

        public void ServerBackToDay()
        {
            if (!IsServer || Phase.Value != GamePhase.Meeting) return;
            ServerEnter(GamePhase.Day);
        }

        private void ServerResolveEvening()
        {
            if (_pendingArrest != null && _pendingArrest.IsAlive)
            {
                Arrests.Value++;
                _pendingArrest.ServerSetStatus(PlayerStatus.Arrested);
                NoiseRpc(MapBuilder.VanPoint, "siren", 120f);
                ToastRpc($"Наряд забрал: {_pendingArrest.Nick.Value}");
                ServerOfferRecruit(_pendingArrest);
                _pendingArrest = null;
            }
            else ToastRpc("Вечер тихий, наряд не приезжал");

            foreach (var p in _players) p.ServerNewDay();
        }

        // ---------------------------------------------------------- дела и улики

        /// <summary>Срыв: персонаж делает дело сам, там где стоит.</summary>
        public void ServerForceBreakdown(PlayerState actor, DeedDef deed)
        {
            if (!IsServer) return;
            StartCoroutine(RunBreakdown(actor, deed));
        }

        private System.Collections.IEnumerator RunBreakdown(PlayerState actor, DeedDef deed)
        {
            actor.ServerBeginDeed(deed);
            ToastToRpc(actor.OwnerClientId, $"Не выдержал: {deed.Name}");
            if (!string.IsNullOrEmpty(deed.SoundId))
                NoiseRpc(actor.transform.position, deed.SoundId, deed.NoiseRadius);

            yield return new WaitForSeconds(2f);

            ServerReportDeed(actor, deed);
            actor.ServerRelieveBoredom(18f);
            actor.ServerEndDeed();
        }

        /// <summary>Звук дела слышат все в радиусе — петарду через весь двор, распитие нет.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        public void NoiseRpc(Vector3 pos, string soundId, float radius)
        {
            if (string.IsNullOrEmpty(soundId)) return;
            Sfx.PlayAt(soundId, pos, 1f, Mathf.Max(6f, radius));
        }

        /// <summary>Вызывает PlayerInteractor, когда дело реально завершилось на сервере.</summary>
        public void ServerReportDeed(PlayerState actor, DeedDef deed)
        {
            if (!IsServer) return;

            if (!string.IsNullOrEmpty(deed.Evidence))
            {
                Evidence.Add(new EvidenceRec
                {
                    Id = ++_evidenceCounter,
                    DeedId = deed.Id,
                    Label = deed.Evidence,
                    Pos = actor.transform.position + Vector3.up * 0.3f,
                    Holder = ulong.MaxValue
                });
            }

            if (!deed.Crime) return;

            // кто это видел — тот может стукнуть
            foreach (var w in _players)
            {
                if (w == actor || !w.IsAlive) continue;
                if (Vector3.Distance(w.transform.position, actor.transform.position) > Balance.WitnessRange) continue;
                if (!HasLineOfSight(w.transform.position, actor.transform.position)) continue;

                var log = _witness[w.OwnerClientId];
                log.Add(new WitnessEntry { Actor = actor.OwnerClientId, Deed = deed.Name, Time = NetworkManager.ServerTime.Time });
                if (log.Count > 8) log.RemoveAt(0);
            }
        }

        public static bool HasLineOfSight(Vector3 a, Vector3 b)
        {
            a += Vector3.up * 1.2f; b += Vector3.up * 1.2f;
            return !Physics.Linecast(a, b, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Стукач отправляет донос: самое свежее, что он видел.</summary>
        public void ServerSnitchReport(PlayerState snitch)
        {
            if (!IsServer) return;
            var log = _witness[snitch.OwnerClientId];
            if (log.Count == 0)
            {
                ToastToRpc(snitch.OwnerClientId, "Тебе нечего рассказать — ты ничего не видел");
                return;
            }

            var e = log[^1];
            log.RemoveAt(log.Count - 1);

            var target = Find(e.Actor);
            if (target == null || !target.IsAlive) return;

            target.ServerAddProtocol();
            var cop = ServerCop();
            if (cop != null)
                ToastToRpc(cop.OwnerClientId, $"Сигнал: {target.Nick.Value} — {e.Deed}");
            ToastToRpc(snitch.OwnerClientId, $"Сдал: {target.Nick.Value}");
        }

        [Rpc(SendTo.Server)]
        public void PickEvidenceRpc(int id, RpcParams p = default)
        {
            ulong who = p.Receive.SenderClientId;
            var me = Find(who);
            if (me == null || !me.IsAlive) return;

            for (int i = 0; i < Evidence.Count; i++)
            {
                var e = Evidence[i];
                if (e.Id != id || !e.OnGround) continue;
                if (Vector3.Distance(me.transform.position, e.Pos) > 3f) return;
                e.Holder = who;
                Evidence[i] = e;
                return;
            }
        }

        [Rpc(SendTo.Server)]
        public void DropEvidenceRpc(int id, bool destroy, RpcParams p = default)
        {
            ulong who = p.Receive.SenderClientId;
            var me = Find(who);
            if (me == null) return;

            for (int i = 0; i < Evidence.Count; i++)
            {
                var e = Evidence[i];
                if (e.Id != id || e.Holder != who) continue;

                if (destroy) { Evidence.RemoveAt(i); ToastToRpc(who, "Улика выкинута"); return; }

                e.Holder = ulong.MaxValue;
                e.Pos = me.transform.position + me.transform.forward * 1.2f + Vector3.up * 0.3f;
                Evidence[i] = e;
                ToastToRpc(who, "Улика подброшена");
                return;
            }
        }

        // ---------------------------------------------------------- итоги

        // ---------------------------------------------------------- вербовка

        /// <summary>Клиент: до какого момента показывать предложение.</summary>
        public static float RecruitOfferUntil;
        public static bool RecruitOffer => Time.time < RecruitOfferUntil;

        private PlayerState _recruit;
        private double _recruitUntil;

        /// <summary>
        /// В УАЗике участковый предлагает вернуться во двор в обмен на сотрудничество.
        /// Размен честный: задержание перестаёт считаться, зато у него новый стукач.
        /// </summary>
        private void ServerOfferRecruit(PlayerState p)
        {
            if (!IsServer || p == null || p.IsSnitch.Value) return;
            _recruit = p;
            _recruitUntil = NetworkManager.ServerTime.Time + 20d;
            RecruitOfferRpc(RpcTarget.Single(p.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void RecruitOfferRpc(RpcParams p) => RecruitOfferUntil = Time.time + 20f;

        [Rpc(SendTo.Server)]
        public void AcceptRecruitRpc(RpcParams p = default)
        {
            ulong who = p.Receive.SenderClientId;
            if (_recruit == null || _recruit.OwnerClientId != who) return;
            if (NetworkManager.ServerTime.Time > _recruitUntil) return;

            var me = _recruit;
            _recruit = null;

            Arrests.Value = Mathf.Max(0, Arrests.Value - 1);
            me.ServerSetStatus(PlayerStatus.Free);
            me.ServerSetRole(me.Role.Value, true);
            me.ServerTeleport(MapBuilder.SpawnPoint((int)who));

            ToastRpc("Кого-то выпустили из УАЗика");
            ToastToRpc(who, "Ты согласился. Теперь ты стучишь — и объясняй двору, как вышел");

            var cop = ServerCop();
            if (cop != null) ToastToRpc(cop.OwnerClientId, $"{me.Nick.Value} согласился сотрудничать");
        }

        public void ServerSchedulePatrol(PlayerState target)
        {
            if (!IsServer) return;
            _pendingArrest = target;
        }

        public void ServerReportBanish(PlayerState victim)
        {
            if (!IsServer) return;
            _wrongVotesInARow = victim.IsSnitch.Value ? 0 : _wrongVotesInARow + 1;
            victim.ServerSetStatus(PlayerStatus.Banished);
            ServerCheckWin();
        }

        private bool ServerCheckWin()
        {
            bool snitchesLeft = _players.Any(p => p.IsSnitch.Value && p.IsAlive);
            if (!snitchesLeft)                                { ServerFinish(Side.Dvor); return true; }
            if (Arrests.Value >= Balance.ArrestsToWin)        { ServerFinish(Side.Cop);  return true; }
            if (_wrongVotesInARow >= Balance.WrongVotesToLose){ ServerFinish(Side.Cop);  return true; }
            return false;
        }

        private void ServerFinish(Side side)
        {
            Winner.Value = side;
            Phase.Value = GamePhase.Finished;
            PhaseEndsAt.Value = NetworkManager.ServerTime.Time;

            var ids    = _players.Select(p => p.OwnerClientId).ToArray();
            var roles  = _players.Select(p => (byte)p.Role.Value).ToArray();
            var snitch = _players.Select(p => p.IsSnitch.Value).ToArray();
            var nicks  = string.Join("\u001f", _players.Select(p => p.Nick.Value.ToString()));
            RevealRpc(ids, roles, snitch, nicks);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RevealRpc(ulong[] ids, byte[] roles, bool[] snitch, string nicksPacked)
        {
            var nicks = nicksPacked.Split('\u001f');
            Reveal = new List<RevealRow>();
            for (int i = 0; i < ids.Length; i++)
                Reveal.Add(new RevealRow
                {
                    Client = ids[i], Role = (RoleId)roles[i],
                    Snitch = snitch[i], Nick = i < nicks.Length ? nicks[i] : "?"
                });
        }

        // ---------------------------------------------------------- тосты

        public static string LastToast = "";
        private static float _toastAt;
        public static bool ToastVisible => Time.time - _toastAt < 5f;

        [Rpc(SendTo.ClientsAndHost)]
        public void ToastRpc(string text) { LastToast = text; _toastAt = Time.time; }

        public void ToastToRpc(ulong client, string text)
        {
            if (!IsServer) return;
            ToastOneRpc(text, RpcTarget.Single(client, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ToastOneRpc(string text, RpcParams p) { LastToast = text; _toastAt = Time.time; }
    }
}
