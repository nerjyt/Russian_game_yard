using System.Collections.Generic;
using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Рисует улики, лежащие в сетевом списке. Раньше они существовали
    /// только в данных — то есть подбросить бутылку было нечем.
    /// </summary>
    public class EvidenceView : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _views = new();
        private float _tick;

        private void Update()
        {
            var sm = GameStateMachine.I;
            if (sm == null || sm.Evidence == null) return;

            _tick -= Time.deltaTime;
            if (_tick <= 0f) { _tick = 0.25f; Sync(sm); }

            foreach (var go in _views.Values)
            {
                if (go == null) continue;
                go.transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
            }
        }

        private void Sync(GameStateMachine sm)
        {
            var alive = new HashSet<int>();

            for (int i = 0; i < sm.Evidence.Count; i++)
            {
                var e = sm.Evidence[i];
                if (!e.OnGround) continue;          // в руках — не видно на земле
                alive.Add(e.Id);

                if (!_views.TryGetValue(e.Id, out var go) || go == null)
                {
                    go = Make(e.Label.ToString());
                    _views[e.Id] = go;
                }
                go.transform.position = e.Pos + Vector3.up * 0.15f;
            }

            var dead = new List<int>();
            foreach (var kv in _views)
                if (!alive.Contains(kv.Key)) dead.Add(kv.Key);

            foreach (var id in dead)
            {
                if (_views[id] != null) Destroy(_views[id]);
                _views.Remove(id);
            }
        }

        private static GameObject Make(string label)
        {
            var root = new GameObject($"Улика: {label}");

            PrimitiveType shape = PrimitiveType.Cube;
            Color c = new(0.75f, 0.72f, 0.60f);
            Vector3 size = new(0.28f, 0.28f, 0.28f);

            string l = label.ToLowerInvariant();
            if (l.Contains("тара") || l.Contains("стакан") || l.Contains("банк"))
            { shape = PrimitiveType.Cylinder; c = new Color(0.30f, 0.52f, 0.28f); size = new Vector3(0.16f, 0.22f, 0.16f); }
            else if (l.Contains("баллон") || l.Contains("краск"))
            { shape = PrimitiveType.Cylinder; c = new Color(0.85f, 0.25f, 0.30f); size = new Vector3(0.14f, 0.2f, 0.14f); }
            else if (l.Contains("провод") || l.Contains("номер") || l.Contains("магнит") || l.Contains("тележк"))
            { c = new Color(0.55f, 0.55f, 0.60f); size = new Vector3(0.34f, 0.18f, 0.24f); }
            else if (l.Contains("сумка") || l.Contains("пакет") || l.Contains("спортсумк"))
            { c = new Color(0.72f, 0.35f, 0.25f); size = new Vector3(0.42f, 0.26f, 0.26f); }
            else if (l.Contains("мелоч") || l.Contains("купюр") || l.Contains("квитанц"))
            { c = new Color(0.85f, 0.75f, 0.35f); size = new Vector3(0.26f, 0.08f, 0.2f); }

            var body = GameObject.CreatePrimitive(shape);
            body.name = "предмет";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = size;
            body.GetComponent<Renderer>().sharedMaterial = MatLib.Solid(c, 0.45f);
            Destroy(body.GetComponent<Collider>());

            // подсветка снизу, чтобы улику было видно в траве
            var halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            halo.name = "след";
            halo.transform.SetParent(root.transform, false);
            halo.transform.localPosition = new Vector3(0f, -0.13f, 0f);
            halo.transform.localScale = new Vector3(0.55f, 0.01f, 0.55f);
            halo.GetComponent<Renderer>().sharedMaterial = MatLib.Solid(new Color(0.95f, 0.85f, 0.45f), 0.6f);
            Destroy(halo.GetComponent<Collider>());

            return root;
        }
    }
}
