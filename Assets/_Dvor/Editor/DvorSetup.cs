#if UNITY_EDITOR
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dvor.EditorTools
{
    /// <summary>
    /// Собирает префаб игрока и сцену. Делает это сам Unity, поэтому GUID
    /// и GlobalObjectIdHash получаются правильные — руками их не написать.
    /// </summary>
    public static class DvorSetup
    {
        private const string PrefabDir = "Assets/_Dvor/Prefabs";
        private const string SceneDir  = "Assets/_Dvor/Scenes";
        private const string PlayerPath = PrefabDir + "/Игрок.prefab";
        private const string ScenePath  = SceneDir + "/Двор.unity";

        [MenuItem("Двор/Собрать проект", priority = 0)]
        public static void BuildAll()
        {
            EnsureFolders();
            var player = BuildPlayerPrefab();
            BuildScene(player);
            CheckInput();

            EditorUtility.DisplayDialog("Двор",
                "Готово.\n\nСцена «Двор» открыта и добавлена в Build Settings.\n" +
                "Нажми Play → «Создать двор (хост)».\n\n" +
                "Для игры вдвоём собери билд (Ctrl+B) и запусти его рядом с редактором.",
                "Поехали");
        }

        [MenuItem("Двор/Пересобрать только префаб игрока", priority = 20)]
        public static void RebuildPlayer()
        {
            EnsureFolders();
            BuildPlayerPrefab();
            var nm = Object.FindFirstObjectByType<NetworkManager>();
            if (nm != null)
            {
                nm.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
                EditorUtility.SetDirty(nm);
                EditorSceneManager.MarkSceneDirty(nm.gameObject.scene);
            }
        }

        // -------------------------------------------------------------

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();
        }

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Игрок");

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            root.AddComponent<NetworkObject>();
            root.AddComponent<ClientNetworkTransform>();
            root.AddComponent<PlayerState>();
            root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerInteractor>();
            root.AddComponent<ProtocolBook>();
            root.AddComponent<ProximityVoice>();

            // тело — видно другим, себе не мешает
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Тело";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Взгляд";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.5f, 0.42f);
            nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.25f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Двор] Префаб игрока собран: " + PlayerPath);
            return prefab;
        }

        private static void BuildScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();

            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.EnableSceneManagement = false;   // карта строится кодом, сцена одна
            nm.NetworkConfig.ConnectionApproval = false;
            nm.NetworkConfig.TickRate = 30;
            utp.SetConnectionData("127.0.0.1", 7777);

            var director = new GameObject("GameDirector");
            director.AddComponent<NetworkObject>();
            director.AddComponent<GameStateMachine>();
            director.AddComponent<MeetingManager>();

            // камера меню: без неё до спавна игрока в Game view нечего рисовать
            var menuCam = new GameObject("Камера меню");
            menuCam.tag = "MainCamera";
            var cam = menuCam.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            menuCam.AddComponent<AudioListener>();
            menuCam.transform.position = new Vector3(-6f, 18f, -46f);
            menuCam.transform.rotation = Quaternion.Euler(24f, 22f, 0f);

            var boot = new GameObject("Bootstrap");
            boot.AddComponent<Bootstrap>();
            boot.AddComponent<HudUI>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new(ScenePath, true)
            };
            EditorBuildSettings.scenes = list.ToArray();

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[Двор] Сцена собрана: " + ScenePath);
        }

        private static void CheckInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null) return;

            if (prop.intValue == 1)   // только новая Input System — Input.GetKey упадёт
            {
                prop.intValue = 2;    // обе
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.LogWarning("[Двор] Переключил Active Input Handling на «Both». Перезапусти Unity.");
            }
        }
    }
}
#endif
