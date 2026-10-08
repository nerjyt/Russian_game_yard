using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Весь интерфейс на IMGUI: некрасиво, зато без единого ассета и всегда работает.
    /// Заменить на UGUI имеет смысл только после того, как игра окажется весёлой.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        public static GUIStyle Rich, Big, Small, Center;
        private static Texture2D _px;

        private void Update()
        {
            // предложение из УАЗика принимается одной клавишей
            if (GameStateMachine.RecruitOffer && Input.GetKeyDown(KeyCode.Y))
            {
                GameStateMachine.I?.AcceptRecruitRpc();
                GameStateMachine.RecruitOfferUntil = 0f;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (Bootstrap.I != null && Bootstrap.I.InMenu) return;

            var sm = GameStateMachine.I;
            if (sm == null) { Label(new Rect(14, 10, 400, 24), "Подключение…"); return; }

            DrawTopBar(sm);
            DrawNameplates(sm);

            var me = sm.Local();
            if (me == null) return;

            DrawSelf(sm, me);
            DrawPrompt(me);
            DrawCop(me);
            DrawToast();

            if (sm.Phase.Value == GamePhase.Meeting) DrawMeeting(sm, me);
            if (sm.Phase.Value == GamePhase.Finished) DrawEnd(sm);
            if (sm.Phase.Value == GamePhase.Lobby) DrawLobby(sm);
        }

        // ------------------------------------------------------------

        public static void EnsureStyles()
        {
            if (Rich != null) return;
            _px = new Texture2D(1, 1); _px.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.62f)); _px.Apply();

            Rich   = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            Big    = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 20, fontStyle = FontStyle.Bold };
            Small  = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 };
            Center = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        }

        private static void Panel(Rect r) => GUI.DrawTexture(r, _px);
        private static void Label(Rect r, string s) { EnsureStyles(); GUI.Label(r, s, Rich); }

        private static string PhaseName(GamePhase p) => p switch
        {
            GamePhase.Lobby => "Лобби",
            GamePhase.Morning => "Утро",
            GamePhase.Day => "День",
            GamePhase.Meeting => "СХОДКА",
            GamePhase.Evening => "Вечер",
            _ => "Конец"
        };

        // ------------------------------------------------------------

        private void DrawTopBar(GameStateMachine sm)
        {
            var r = new Rect(Screen.width / 2f - 150f, 8f, 300f, 32f);
            Panel(r);
            int left = Mathf.CeilToInt(sm.SecondsLeft());
            GUI.Label(r, $"<b>{PhaseName(sm.Phase.Value)}</b>   День {sm.Day.Value}/{Balance.DaysPerMatch}   {left / 60}:{left % 60:00}   Задержано {sm.Arrests.Value}/{Balance.ArrestsToWin}", Center);
        }

        private void DrawSelf(GameStateMachine sm, PlayerState me)
        {
            var r = new Rect(12f, 12f, 300f, 216f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));

            string role = RoleCatalog.NameOf(me.Role.Value);
            string snitch = me.IsSnitch.Value ? "  <color=#ff6b6b><b>СТУКАЧ</b></color>" : "";
            GUILayout.Label($"<b>{role}</b>{snitch}", Rich);

            var def = RoleCatalog.Get(me.Role.Value);
            if (def != null && !string.IsNullOrEmpty(def.Perk))
                GUILayout.Label($"<size=11><color=#b9b9b9>{def.Perk}</color></size>", Rich);

            if (!me.IsCop)
            {
                float b = me.Boredom.Value;
                string col = b >= Balance.BoredomBreak ? "#ff4d4d"
                           : b >= Balance.BoredomShaky ? "#ff9f43"
                           : b >= Balance.BoredomNervous ? "#feca57" : "#9adb7a";
                GUILayout.Space(4);
                GUILayout.Label($"Скука <color={col}><b>{b:0}</b></color> / 100", Rich);
                var br = GUILayoutUtility.GetRect(1, 8);
                GUI.DrawTexture(new Rect(br.x, br.y, br.width, 8), Texture2D.grayTexture);
                GUI.DrawTexture(new Rect(br.x, br.y, br.width * b / 100f, 8), Texture2D.whiteTexture);

                if (me.MyProtocols.Value > 0)
                    GUILayout.Label($"<color=#ff8a8a>Протоколов на тебе: {me.MyProtocols.Value}/{Balance.ProtocolsForArrest}</color>", Rich);

                GUILayout.Space(6);
                GUILayout.Label("<b>Дела на сегодня</b>", Rich);
                foreach (var id in me.TodayDeeds)
                {
                    var d = RoleCatalog.FindDeed(id);
                    if (d == null) continue;
                    bool done = me.DoneDeeds.Contains(id);
                    string mark = done ? "<color=#9adb7a>+</color>" : "•";
                    string crime = d.Crime ? " <color=#ff8a8a>⚠</color>" : "";
                    string snitchy = d.SnitchOnly ? " <color=#ff6b6b>(донос)</color>" : "";
                    GUILayout.Label($"<size=12>{mark} {d.Name}{crime}{snitchy}</size>", Rich);
                }
            }
            else
            {
                GUILayout.Space(6);
                GUILayout.Label("<size=12>F — протокол (держать, не двигаясь)\nG — проверить документы\nH — профбеседа\nR — вызвать наряд из опорного</size>", Rich);
            }

            if (me.Status.Value == PlayerStatus.Arrested)
            {
                GUILayout.Label("<color=#ff6b6b><b>Ты в УАЗике</b></color>", Rich);
                if (GameStateMachine.RecruitOffer)
                    GUILayout.Label("<color=#ffd479><b>[Y]</b> согласиться стучать и вернуться во двор</color>", Rich);
            }
            if (me.Status.Value == PlayerStatus.Banished)
                GUILayout.Label("<color=#ff9f43><b>15 суток — метёшь двор</b></color>", Rich);

            GUILayout.EndArea();
        }

        private void DrawPrompt(PlayerState me)
        {
            var inter = me.GetComponent<PlayerInteractor>();
            if (inter == null) return;

            var r = new Rect(Screen.width / 2f - 220f, Screen.height - 120f, 440f, 56f);

            if (inter.HoverDeed != null)
            {
                Panel(r);
                string t = inter.HoldProgress > 0f
                    ? $"<b>{inter.HoverDeed.Name}</b>  {(int)(inter.HoldProgress * 100)}%"
                    : $"[E] {inter.HoverDeed.Name}";
                GUI.Label(r, t, Center);
                if (inter.HoldProgress > 0f)
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 5, r.width * inter.HoldProgress, 5), Texture2D.whiteTexture);
            }
            else if (!string.IsNullOrEmpty(inter.CarriedLabel))
            {
                Panel(r);
                GUI.Label(r, $"В руках: <b>{inter.CarriedLabel}</b>   [Q] подбросить, у помойки — выкинуть", Center);
            }

            if (Vector3.Distance(me.transform.position, MapBuilder.GazeboPoint) < 6f && !me.UsedMeetingCall)
            {
                var g = new Rect(Screen.width / 2f - 150f, Screen.height - 160f, 300f, 26f);
                Panel(g);
                GUI.Label(g, "[M] созвать сходку", Center);
            }
        }

        private void DrawCop(PlayerState me)
        {
            if (!me.IsCop) return;
            var book = me.GetComponent<ProtocolBook>();
            if (book == null) return;

            var sm = GameStateMachine.I;
            var board = GameStateMachine.CopBoard;
            var r = new Rect(Screen.width - 260f, 12f, 248f, 44f + board.Count * 18f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12));
            GUILayout.Label($"<b>Папка</b>   проверок: {book.ChecksLeft}", Rich);
            foreach (var row in board)
            {
                string st = string.IsNullOrEmpty(row.Note) ? "" : $" <color=#888>({row.Note})</color>";
                string col = row.Protocols >= Balance.ProtocolsForArrest ? "#ff8a8a" : "#dddddd";
                GUILayout.Label($"<size=12>{row.Nick} — <color={col}>{row.Protocols}</color>{st}</size>", Rich);
            }
            GUILayout.EndArea();

            if (book.Target != null)
            {
                var t = new Rect(Screen.width / 2f - 200f, Screen.height - 190f, 400f, 44f);
                Panel(t);
                GUI.Label(t, $"[F] протокол на <b>{book.Target.Nick.Value}</b>  {(int)(book.HoldProgress * 100)}%", Center);
                if (book.HoldProgress > 0f)
                    GUI.DrawTexture(new Rect(t.x, t.yMax - 5, t.width * book.HoldProgress, 5), Texture2D.whiteTexture);
            }
        }

        private void DrawNameplates(GameStateMachine sm)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var me = sm.Local();
            bool copView = me != null && me.IsCop;

            foreach (var p in sm.Players)
            {
                if (me != null && p == me) continue;
                if (p.Status.Value == PlayerStatus.Arrested) continue;

                Vector3 world = p.transform.position + Vector3.up * 2.1f;
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;

                float dist = Vector3.Distance(cam.transform.position, p.transform.position);
                bool isCop = p.PublicIsCop.Value;
                bool showName = copView || isCop || dist < 14f;
                if (!showName && dist > 26f) continue;

                string name = showName ? p.Nick.Value.ToString() : "?";
                if (isCop) name = $"<color=#7fb3ff>УЧАСТКОВЫЙ {name}</color>";

                var voice = p.GetComponent<ProximityVoice>();
                if (voice != null && voice.IsSpeaking) name = $"<color=#9adb7a>◉</color> {name}";

                if (!p.ActingDeed.Value.IsEmpty)
                {
                    var d = RoleCatalog.FindDeed(p.ActingDeed.Value.ToString());
                    if (d != null) name += $"\n<size=11><color=#ffd479>{d.Name}…</color></size>";
                }

                var r = new Rect(sp.x - 90f, Screen.height - sp.y - 26f, 180f, 40f);
                GUI.Label(r, name, Center);
            }
        }

        private void DrawToast()
        {
            if (!GameStateMachine.ToastVisible || string.IsNullOrEmpty(GameStateMachine.LastToast)) return;
            var r = new Rect(Screen.width / 2f - 260f, 52f, 520f, 30f);
            Panel(r);
            GUI.Label(r, GameStateMachine.LastToast, Center);
        }

        private Vector2 _scroll;

        private void DrawMeeting(GameStateMachine sm, PlayerState me)
        {
            var mm = MeetingManager.I;
            if (mm == null) return;

            var r = new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 170f, 400f, 340f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 12, r.width - 32, r.height - 24));

            GUILayout.Label("<size=20><b>СХОДКА</b></size>", Rich);
            GUILayout.Label($"Кто стучит? Проголосовало {mm.VotesCast.Value}/{mm.VotesNeeded.Value}", Rich);
            GUILayout.Space(6);

            if (me.IsCop) { GUILayout.Label("У тебя нет голоса. Говори.", Rich); GUILayout.EndArea(); return; }
            if (!me.IsAlive) { GUILayout.Label("Ты не голосуешь.", Rich); GUILayout.EndArea(); return; }
            if (mm.LocalVoted) { GUILayout.Label("<color=#9adb7a>Голос отдан</color>", Rich); GUILayout.EndArea(); return; }

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var p in sm.Players)
            {
                if (!p.IsAlive || p == me) continue;
                if (GUILayout.Button(p.Nick.Value.ToString(), GUILayout.Height(26)))
                {
                    mm.CastVoteRpc(p.OwnerClientId);
                    mm.MarkLocalVoted();
                }
            }
            if (GUILayout.Button("Воздержаться", GUILayout.Height(26)))
            {
                mm.CastVoteRpc(ulong.MaxValue);
                mm.MarkLocalVoted();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawEnd(GameStateMachine sm)
        {
            var r = new Rect(Screen.width / 2f - 230f, Screen.height / 2f - 190f, 460f, 380f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, r.width - 36, r.height - 28));

            string title = sm.Winner.Value == Side.Dvor
                ? "<color=#9adb7a>ДВОР ВЫСТОЯЛ</color>"
                : "<color=#ff6b6b>УЧАСТКОВЫЙ И СТУКАЧИ</color>";
            GUILayout.Label($"<size=22><b>{title}</b></size>", Rich);
            GUILayout.Space(10);

            foreach (var row in sm.Reveal)
            {
                string tag = row.Role == RoleId.Cop ? "<color=#7fb3ff>участковый</color>"
                           : row.Snitch ? "<color=#ff6b6b>СТУКАЧ</color>" : "дворовой";
                GUILayout.Label($"{row.Nick} — {RoleCatalog.NameOf(row.Role)} · {tag}", Rich);
            }

            GUILayout.Space(12);
            if (NetworkManager.Singleton.IsServer && GUILayout.Button("Ещё партию", GUILayout.Height(32)))
                sm.StartMatchRpc();

            GUILayout.EndArea();
        }

        private void DrawLobby(GameStateMachine sm)
        {
            var r = new Rect(Screen.width / 2f - 200f, Screen.height / 2f - 90f, 400f, 180f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, r.width - 36, r.height - 28));

            GUILayout.Label($"<size=18><b>Во дворе: {sm.Players.Count}</b></size>", Rich);
            GUILayout.Label($"Нужно минимум {Balance.MinPlayers}. Для реальной партии — 8.", Rich);
            GUILayout.Space(8);
            GUILayout.Label("<size=12>Хост запускает партию кнопкой ниже.\nESC — освободить курсор.</size>", Rich);
            GUILayout.Space(8);

            if (NetworkManager.Singleton.IsServer)
            {
                if (GUILayout.Button("Начать партию", GUILayout.Height(34))) sm.StartMatchRpc();
            }
            else GUILayout.Label("Ждём хоста…", Rich);

            GUILayout.EndArea();
        }
    }
}
