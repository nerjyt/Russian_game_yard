using System.Collections.Generic;

namespace Dvor
{
    public class DeedDef
    {
        public string Id;
        public string Name;
        public string SpotTag;
        public float  Hold;
        public float  Relief;
        public bool   Crime;
        public bool   NeedsPartner;
        public string Evidence;     // null — следов не остаётся
        public bool   SnitchOnly;
        public string SoundId;      // звук из Sfx
        public float  NoiseRadius;  // с какого расстояния слышно

        public DeedDef(string id, string name, string spot, float hold, float relief,
                       bool crime = true, bool partner = false, string evidence = null,
                       string sound = null, float noise = 14f, bool snitch = false)
        {
            Id = id; Name = name; SpotTag = spot; Hold = hold; Relief = relief;
            Crime = crime; NeedsPartner = partner; Evidence = evidence;
            SoundId = sound; NoiseRadius = noise; SnitchOnly = snitch;
        }
    }

    public class RoleDef
    {
        public RoleId Id;
        public string Name;
        public string Flavor;
        public string Perk;
        public DeedDef[] Deeds;
    }

    /// <summary>Одиннадцать ролей двора и участковый. Раздаются без повторов.</summary>
    public static class RoleCatalog
    {
        public static readonly Dictionary<RoleId, RoleDef> All = new();

        /// <summary>Секретные дела стукача — поверх дел роли-прикрытия.</summary>
        public static readonly DeedDef[] SnitchDeeds =
        {
            new("snitch_phone", "Позвонить с таксофона", "phone", 6f, 0f, false, false, null, "coin", 10f, true),
            new("snitch_post",  "Оставить записку в ящике", "post", 4f, 0f, false, false, null, "door", 8f, true),
        };

        static RoleCatalog()
        {
            Add(RoleId.Gopnik, "Гопник", "Есть чё? А если найду?", "Бегает на 10% быстрее", new[]
            {
                new DeedDef("gop_smoke", "Стрельнуть сигарету", "yard",     4f, 25f, true, true,  "Чужая пачка",  null,   10f),
                new DeedDef("gop_korty", "Присесть на корты",   "entrance", 5f, 20f, true, false, null,           null,   8f),
                new DeedDef("gop_otzhat","Отжать мелочь",       "kiosk",    6f, 30f, true, false, "Чужая мелочь", "coin", 16f),
            });

            Add(RoleId.Alkash, "Алкаш", "Я не пьяный, я уставший", "Видит чужую тару издалека", new[]
            {
                new DeedDef("alk_buy",   "Взять в ларьке",   "kiosk",     4f, 10f, false, false, null,          "coin",   12f),
                new DeedDef("alk_drink", "Распить на лавке", "bench",     6f, 30f, true,  false, "Пустая тара", "bottle", 12f),
                new DeedDef("alk_tara",  "Сдать тару",       "recycling", 5f, 15f, false, false, null,          "bottle", 14f),
            });

            Add(RoleId.Sportik, "Спортик", "Турник, брусья, дома не сидим", "Бежит быстрее на спринте", new[]
            {
                new DeedDef("spo_turnik","Подтянуться",         "barbell", 6f, 15f, false, false, null,              "metal", 10f),
                new DeedDef("spo_spar",  "Спарринг в гараже",   "garage",  6f, 30f, true,  true,  "Разбитая губа",   "punch", 18f),
                new DeedDef("spo_banka", "Продать банку с рук", "yard",    5f, 25f, true,  true,  "Спортсумка",      null,    8f),
            });

            Add(RoleId.Baryga, "Барыга", "Бери две, третья со скидкой", "Знает, где чужие тайники", new[]
            {
                new DeedDef("bar_sell", "Продать кассету с рук", "yard", 5f, 30f, true, true, "Пачка купюр",   "coin", 12f),
                new DeedDef("bar_take", "Забрать товар",   "garage",   6f, 25f, true, false, "Клетчатая сумка", "door", 14f),
                new DeedDef("bar_hide", "Спрятать сумку",  "basement", 6f, 20f, true, false, null,              null,   8f),
            });

            Add(RoleId.Babka, "Бабка с первого", "Я всё вижу, я всё запишу", "Слышит шаги через стену", new[]
            {
                new DeedDef("bab_apparat","Залить аппарат",   "flat",   7f, 30f, true,  false, "Банки",  "glug", 12f),
                new DeedDef("bab_okno",   "Продать в окно",   "window", 5f, 25f, true,  true,  "Стакан", "glug", 12f),
                new DeedDef("bab_lavka",  "Посидеть на лавке","bench",  8f, 12f, false, false, null,     null,   6f),
            });

            Add(RoleId.Fortochnik, "Форточник", "Открытое окно — это приглашение", "Ходит тихо", new[]
            {
                new DeedDef("for_magnit","Вытащить магнитолу","car",      6f, 30f, true, false, "Сигнализация", "metal", 22f),
                new DeedDef("for_fortka","Залезть в форточку","entrance", 7f, 28f, true, false, "Чужой пакет",  "door",  14f),
                new DeedDef("for_balkon","Обнести балкон",    "window",   6f, 26f, true, false, "Чужой пакет",  null,    10f),
            });

            Add(RoleId.Garazhnik, "Гаражник", "Машина не угнана, она разобрана", "Открывает чужие гаражи", new[]
            {
                new DeedDef("gar_kolesa","Снять колёса",     "garage",       6f, 30f, true, false, "След масла", "metal", 18f),
                new DeedDef("gar_nomera","Сменить номера",   "car",          6f, 26f, true, false, "Старый номер","metal", 14f),
                new DeedDef("gar_zhech", "Сжечь обивку",     "construction", 7f, 28f, true, false, "Запах гари", null,    20f),
            });

            Add(RoleId.Petardschik, "Петардщик", "А если сюда кинуть?", "Не боится собственных взрывов", new[]
            {
                new DeedDef("pet_podezd","Взорвать в подъезде","entrance",    5f, 32f, true, false, "Запах серы", "firework", 90f),
                new DeedDef("pet_puh",   "Поджечь пух",       "yard",         5f, 26f, true, false, "Гарь",       "firework", 60f),
                new DeedDef("pet_stroy", "Бахнуть на стройке","construction", 5f, 28f, true, false, null,         "firework", 90f),
            });

            Add(RoleId.Katala, "Катала", "Следи за шариком, не за руками", "Видит, у кого сколько денег", new[]
            {
                new DeedDef("kat_naperstki","Развести в напёрстки","yard",     6f, 32f, true, true,  "Столик",  "coin", 14f),
                new DeedDef("kat_karty",    "Катнуть в беседке",   "yard",     6f, 26f, true, true,  "Колода",  null,   8f),
                new DeedDef("kat_pryatat",  "Спрятать выигрыш",    "basement", 5f, 18f, true, false, null,      "coin", 8f),
            });

            Add(RoleId.Raspisnoy, "Расписной", "Стена была пустая, я исправил", "Видит, кто где наследил", new[]
            {
                new DeedDef("ras_garazh", "Набить тэг на гараже","garage",       6f, 28f, true, false, "Краска на пальцах","spray", 12f),
                new DeedDef("ras_lift",   "Расписать лифт",      "entrance",     6f, 26f, true, false, "Баллончик",        "spray", 12f),
                new DeedDef("ras_zakrasit","Закрасить чужой тэг","construction", 6f, 24f, true, false, "Краска на пальцах","spray", 12f),
            });

            Add(RoleId.Tsvetmetchik, "Цветметчик", "Лежит без дела — значит ничьё", "Знает, где лежат чужие улики", new[]
            {
                new DeedDef("cvet_provod",  "Снять провод",      "construction", 7f, 30f, true, false, "Моток провода", "metal", 22f),
                new DeedDef("cvet_batareya","Вынести батарею",   "basement",     7f, 28f, true, false, "Тележка",       "metal", 18f),
                new DeedDef("cvet_sdat",    "Сдать в приёмку",   "recycling",    5f, 16f, false, false, "Квитанция",    "metal", 16f),
            });

            Add(RoleId.Cop, "Участковый", "Гражданин, пройдёмте", "Видит всех по именам", System.Array.Empty<DeedDef>());
        }

        private static void Add(RoleId id, string name, string flavor, string perk, DeedDef[] deeds)
            => All[id] = new RoleDef { Id = id, Name = name, Flavor = flavor, Perk = perk, Deeds = deeds };

        public static RoleDef Get(RoleId id) => All.TryGetValue(id, out var r) ? r : null;

        public static string NameOf(RoleId id) => Get(id)?.Name ?? "—";

        public static DeedDef FindDeed(string deedId)
        {
            foreach (var r in All.Values)
                foreach (var d in r.Deeds)
                    if (d.Id == deedId) return d;
            foreach (var d in SnitchDeeds)
                if (d.Id == deedId) return d;
            return null;
        }

        public static readonly RoleId[] DvorRoles =
        {
            RoleId.Gopnik, RoleId.Alkash, RoleId.Sportik, RoleId.Baryga,
            RoleId.Babka,  RoleId.Fortochnik, RoleId.Garazhnik,
            RoleId.Petardschik, RoleId.Katala, RoleId.Raspisnoy, RoleId.Tsvetmetchik
        };
    }
}
