using UnityEngine;

namespace Dvor
{
    /// <summary>
    /// Блочный человечек из примитивов с процедурной походкой.
    /// Внешность НЕ зависит от роли — иначе роль читалась бы глазами
    /// и вся дедукция ломалась бы. Цвета выбираются по номеру игрока,
    /// одинаково у всех. Публично выглядит только участковый и тот,
    /// кто получил 15 суток.
    /// </summary>
    public class Humanoid : MonoBehaviour
    {
        public PlayerState owner;

        private Transform _pelvis, _torso, _head, _armL, _armR, _legL, _legR, _vest, _cap;
        private Vector3 _lastPos;
        private float _phase, _speed, _actLean;
        private bool _built;

        private static readonly Color[] Tracksuits =
        {
            new(0.16f, 0.17f, 0.22f), new(0.22f, 0.26f, 0.20f), new(0.35f, 0.18f, 0.18f),
            new(0.18f, 0.24f, 0.34f), new(0.30f, 0.28f, 0.22f), new(0.24f, 0.20f, 0.28f),
            new(0.40f, 0.36f, 0.30f), new(0.14f, 0.28f, 0.26f)
        };
        private static readonly Color[] Skins =
        {
            new(0.86f, 0.70f, 0.58f), new(0.78f, 0.60f, 0.47f), new(0.65f, 0.48f, 0.36f), new(0.92f, 0.78f, 0.66f)
        };

        private void Start() { Build(); _lastPos = transform.position; }

        private void Build()
        {
            if (_built) return;
            _built = true;

            // капсула из префаба больше не нужна
            foreach (var n in new[] { "Тело", "Взгляд" })
            {
                var t = transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }

            ulong id = owner != null ? owner.OwnerClientId : 0;
            bool isCop = owner != null && owner.PublicIsCop.Value;
            var seed = new System.Random((int)(id * 2654435761u % int.MaxValue));

            Color suit = isCop ? new Color(0.17f, 0.20f, 0.30f) : Tracksuits[seed.Next(Tracksuits.Length)];
            Color pants = isCop ? new Color(0.13f, 0.15f, 0.23f) : Shade(suit, 0.72f);
            Color skin = Skins[seed.Next(Skins.Length)];
            Color shoe = new(0.10f, 0.10f, 0.11f);

            var root = new GameObject("Аватар").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.zero;

            _pelvis = Node(root, "Таз", new Vector3(0f, 0.92f, 0f));

            _torso = Node(_pelvis, "Торс", new Vector3(0f, 0.30f, 0f));
            Part(_torso, "тело", new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.62f, 0.26f), suit);
            // лампасы — узнаваемый силуэт, но ничего не выдаёт
            if (!isCop && seed.Next(2) == 0)
            {
                Part(_torso, "полоса л", new Vector3(-0.235f, 0f, 0f), new Vector3(0.02f, 0.62f, 0.27f), Color.white);
                Part(_torso, "полоса п", new Vector3( 0.235f, 0f, 0f), new Vector3(0.02f, 0.62f, 0.27f), Color.white);
            }

            _head = Node(_torso, "Шея", new Vector3(0f, 0.42f, 0f));
            Part(_head, "голова", new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.26f, 0.23f), skin);
            Part(_head, "нос", new Vector3(0f, 0.08f, 0.13f), new Vector3(0.05f, 0.05f, 0.05f), Shade(skin, 0.9f));

            if (isCop)
            {
                _cap = Node(_head, "Фуражка", new Vector3(0f, 0.24f, 0f));
                Part(_cap, "тулья", Vector3.zero, new Vector3(0.27f, 0.10f, 0.26f), new Color(0.13f, 0.16f, 0.26f));
                Part(_cap, "козырёк", new Vector3(0f, -0.03f, 0.16f), new Vector3(0.26f, 0.03f, 0.10f), new Color(0.08f, 0.08f, 0.10f));
            }
            else if (seed.Next(3) == 0)
            {
                _cap = Node(_head, "Кепка", new Vector3(0f, 0.23f, 0f));
                Part(_cap, "кепка", Vector3.zero, new Vector3(0.26f, 0.09f, 0.25f), Shade(suit, 1.25f));
                Part(_cap, "козырёк", new Vector3(0f, -0.03f, 0.15f), new Vector3(0.24f, 0.03f, 0.09f), Shade(suit, 1.1f));
            }

            _armL = Node(_torso, "Плечо Л", new Vector3(-0.30f, 0.22f, 0f));
            Part(_armL, "рука", new Vector3(0f, -0.26f, 0f), new Vector3(0.13f, 0.54f, 0.14f), suit);
            Part(_armL, "кисть", new Vector3(0f, -0.56f, 0f), new Vector3(0.12f, 0.12f, 0.13f), skin);

            _armR = Node(_torso, "Плечо П", new Vector3(0.30f, 0.22f, 0f));
            Part(_armR, "рука", new Vector3(0f, -0.26f, 0f), new Vector3(0.13f, 0.54f, 0.14f), suit);
            Part(_armR, "кисть", new Vector3(0f, -0.56f, 0f), new Vector3(0.12f, 0.12f, 0.13f), skin);

            if (isCop)   // папка подмышкой — его ни с кем не спутать
                Part(_armR, "папка", new Vector3(0.10f, -0.34f, 0.02f), new Vector3(0.05f, 0.30f, 0.22f), new Color(0.55f, 0.36f, 0.16f));

            _legL = Node(_pelvis, "Бедро Л", new Vector3(-0.13f, 0f, 0f));
            Part(_legL, "нога", new Vector3(0f, -0.28f, 0f), new Vector3(0.17f, 0.60f, 0.18f), pants);
            Part(_legL, "ботинок", new Vector3(0f, -0.60f, 0.03f), new Vector3(0.18f, 0.10f, 0.26f), shoe);

            _legR = Node(_pelvis, "Бедро П", new Vector3(0.13f, 0f, 0f));
            Part(_legR, "нога", new Vector3(0f, -0.28f, 0f), new Vector3(0.17f, 0.60f, 0.18f), pants);
            Part(_legR, "ботинок", new Vector3(0f, -0.60f, 0.03f), new Vector3(0.18f, 0.10f, 0.26f), shoe);

            // оранжевый жилет — публичный признак пятнадцати суток
            _vest = Part(_torso, "жилет", Vector3.zero, new Vector3(0.50f, 0.44f, 0.30f), new Color(0.95f, 0.45f, 0.05f)).transform;
            _vest.gameObject.SetActive(false);

            // себя от первого лица видеть не надо
            if (owner != null && owner.IsOwner)
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        private static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, 1f);

        private static Transform Node(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        private static GameObject Part(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MatLib.Solid(color);
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void Update()
        {
            if (!_built || _pelvis == null) return;

            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            _lastPos = transform.position;
            _speed = Mathf.Lerp(_speed, d.magnitude / Mathf.Max(0.0001f, Time.deltaTime), Time.deltaTime * 9f);

            float walk01 = Mathf.Clamp01(_speed / 6f);
            _phase += Time.deltaTime * (3.2f + walk01 * 7f) * (walk01 > 0.02f ? 1f : 0.25f);

            bool acting = owner != null && !owner.ActingDeed.Value.IsEmpty;
            _actLean = Mathf.Lerp(_actLean, acting ? 1f : 0f, Time.deltaTime * 5f);

            float swing = Mathf.Sin(_phase) * 38f * walk01;
            float armSwing = Mathf.Sin(_phase) * 30f * walk01;

            _legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            // при деле руки уходят вперёд, корпус наклоняется
            float armBase = _actLean * -70f;
            _armL.localRotation = Quaternion.Euler(armBase - armSwing, 0f, _actLean * 6f);
            _armR.localRotation = Quaternion.Euler(armBase + armSwing, 0f, -_actLean * 6f);

            _torso.localRotation = Quaternion.Euler(_actLean * 22f + Mathf.Abs(Mathf.Sin(_phase)) * 3f * walk01, 0f, 0f);
            _head.localRotation = Quaternion.Euler(-_actLean * 10f, 0f, 0f);

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * walk01 - _actLean * 0.18f;
            _pelvis.localPosition = new Vector3(0f, 0.92f + bob, 0f);

            if (_vest != null)
            {
                bool banished = owner != null && owner.Status.Value == PlayerStatus.Banished;
                if (_vest.gameObject.activeSelf != banished) _vest.gameObject.SetActive(banished);
            }
        }
    }
}
