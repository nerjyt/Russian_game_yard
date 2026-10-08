using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Точка, где можно сделать дело. Не сетевой объект: геометрия карты
    /// одинакова у всех, а занятость держит сервер.
    /// Маркер загорается ТОЛЬКО если у тебя есть сюда невыполненное дело —
    /// иначе двор был бы усыпан подсказками и палился бы сам.
    /// </summary>
    public class DeedSpot : MonoBehaviour
    {
        public string Tag = "bench";
        public int Index;
        public float UseRadius = 3f;
        public Renderer Marker;

        public static readonly System.Collections.Generic.List<DeedSpot> All = new();

        private float _check;
        private float _pulse;

        private void OnEnable()  { Index = All.Count; All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public bool InRange(Vector3 p) =>
            (p - transform.position).sqrMagnitude <= UseRadius * UseRadius;

        private void Update()
        {
            if (Marker == null) return;

            _check -= Time.deltaTime;
            if (_check <= 0f)
            {
                _check = 0.3f;
                Marker.enabled = LocalNeedsThis();
            }

            if (!Marker.enabled) return;
            _pulse += Time.deltaTime * 2.2f;
            float s = UseRadius * (0.52f + Mathf.Sin(_pulse) * 0.05f);
            Marker.transform.localScale = new Vector3(s, 0.015f, s);
        }

        private bool LocalNeedsThis()
        {
            var sm = GameStateMachine.I;
            if (sm == null || sm.Phase.Value != GamePhase.Day) return false;

            var me = sm.Local();
            if (me == null || !me.IsAlive) return false;

            foreach (var id in me.TodayDeeds)
            {
                if (me.DoneDeeds.Contains(id)) continue;
                var d = RoleCatalog.FindDeed(id);
                if (d != null && d.SpotTag == Tag) return true;
            }
            return false;
        }

        public static DeedSpot Nearest(Vector3 pos, string tag)
        {
            DeedSpot best = null; float bestD = float.MaxValue;
            foreach (var s in All)
            {
                if (s.Tag != tag) continue;
                float d = (s.transform.position - pos).sqrMagnitude;
                if (d < bestD && s.InRange(pos)) { bestD = d; best = s; }
            }
            return best;
        }

        public static DeedSpot ByIndex(int i) => (i >= 0 && i < All.Count) ? All[i] : null;
    }
}
