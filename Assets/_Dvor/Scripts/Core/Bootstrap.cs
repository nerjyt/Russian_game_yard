using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Точка входа. Строит карту, показывает меню, поднимает хост или клиент.
    /// Всё остальное игра делает сама.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        public static Bootstrap I;

        private string _ip = "127.0.0.1";
        private string _nick = "";
        public bool InMenu = true;

        private void Awake()
        {
            I = this;
            MapBuilder.Build();
            gameObject.AddComponent<Ambience>();
            gameObject.AddComponent<EvidenceView>();
            Application.runInBackground = true;   // иначе второе окно замолкает
            if (string.IsNullOrEmpty(_nick)) _nick = "Дворовой";
        }

        private void Start() => PlayerMotor.LockCursor(false);

        private UnityTransport Transport =>
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        public void Host()
        {
            Transport.SetConnectionData("0.0.0.0", 7777);
            NetworkManager.Singleton.StartHost();
            InMenu = false;
            PlayerMotor.LockCursor(true);
        }

        public void Join()
        {
            Transport.SetConnectionData(_ip.Trim(), 7777);
            NetworkManager.Singleton.StartClient();
            InMenu = false;
            PlayerMotor.LockCursor(true);
        }

        private void OnGUI()
        {
            if (!InMenu) return;
            HudUI.EnsureStyles();

            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsServer || nm.IsClient)) { InMenu = false; return; }

            const int w = 360, h = 250;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 20, r.y + 16, w - 40, h - 32));

            GUILayout.Label("<size=26><b>ДВОР</b></size>", HudUI.Rich);
            GUILayout.Label("Социальная дедукция во дворе 2000-х", HudUI.Rich);
            GUILayout.Space(14);

            GUILayout.Label("Ник:");
            _nick = GUILayout.TextField(_nick, 16);
            GUILayout.Space(8);

            if (GUILayout.Button("Создать двор (хост)", GUILayout.Height(34))) Host();

            GUILayout.Space(8);
            GUILayout.Label("IP хоста:");
            GUILayout.BeginHorizontal();
            _ip = GUILayout.TextField(_ip, 24);
            if (GUILayout.Button("Зайти", GUILayout.Width(80), GUILayout.Height(22))) Join();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
