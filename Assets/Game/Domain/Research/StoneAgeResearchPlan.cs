using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Production;

namespace Game.Domain.Research
{
    /// <summary>Balance inputs shared by the simulation and its UI. No mutable campaign state.</summary>
    public sealed class ResearchBalanceSettings
    {
        public int BaseWorkerLimit { get; }
        public int EpochDiscoveries { get; }
        public int EpochDirections { get; }
        public int DiscoveriesPerDirection { get; }
        public IReadOnlyDictionary<string, long> PracticalTargets { get; }

        public ResearchBalanceSettings(int baseWorkerLimit = 4, int epochDiscoveries = 14,
            int epochDirections = 3, int discoveriesPerDirection = 3,
            IReadOnlyDictionary<string, long> practicalTargets = null)
        {
            if (baseWorkerLimit < 1 || epochDiscoveries < 1 || epochDirections < 1 ||
                epochDirections > 5 || discoveriesPerDirection < 1)
                throw new ArgumentOutOfRangeException(nameof(baseWorkerLimit));
            BaseWorkerLimit = baseWorkerLimit;
            EpochDiscoveries = epochDiscoveries;
            EpochDirections = epochDirections;
            DiscoveriesPerDirection = discoveriesPerDirection;
            var targets = new Dictionary<string, long> {
                ["food"] = 40, ["population"] = 10, ["herbs"] = 5,
                ["hides"] = 2, ["soldiers"] = 2, ["shelter"] = 1
            };
            if (practicalTargets != null)
                foreach (var pair in practicalTargets)
                {
                    if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(practicalTargets));
                    targets[pair.Key] = pair.Value;
                }
            PracticalTargets = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(targets);
        }
    }

    /// <summary>Approved R4 content and layout. Definitions contain no mutable research/save state.</summary>
    public sealed class ResearchTechnologyProfile
    {
        public ResearchTechnologyProfile(string id, string name, string branch, string parent, double days,
            int offset, int depth, bool hub, bool key, string icon, string effect, string integration,
            string future, string[] practice, int wood, int stone, string[] unlocks)
        {
            Id=id; Name=name; Branch=branch; Parent=parent; WorkDays=days; Offset=offset; Depth=depth;
            IsHub=hub; IsKey=key; Icon=icon; Effect=effect; Integration=integration; Future=future;
            PracticalRequirements=Array.AsReadOnly(practice); WoodCost=wood; StoneCost=stone;
            UnlockIds=Array.AsReadOnly(unlocks);
        }
        public string Id { get; } public string Name { get; } public string Branch { get; }
        public string Parent { get; } public double WorkDays { get; }
        public int Offset { get; } public int Depth { get; } public bool IsHub { get; }
        public bool IsKey { get; } public string Icon { get; } public string Effect { get; }
        public string Integration { get; } public string Future { get; }
        public bool IsFuture => Integration == "PLANNED" || Integration == "PROPOSED";
        public IReadOnlyList<string> PracticalRequirements { get; }
        public int WoodCost { get; } public int StoneCost { get; }
        public IReadOnlyList<string> UnlockIds { get; }
    }

    public static class StoneAgeResearchPlan
    {
        public static readonly IReadOnlyList<string> Branches = Array.AsReadOnly(new[]
            { "survival", "production", "military", "knowledge", "society" });
        public static readonly IReadOnlyList<ResearchTechnologyProfile> All = Array.AsReadOnly(new[]
        {
            new ResearchTechnologyProfile("basic_survival", "Основы выживания", "root", "", 1d, 0, 0, false, false, "book", "Общие знания о пище, ночлеге и совместной жизни.", "IMPLEMENTED", "", Array.Empty<string>(), 0, 0, new[] { "cap:basic_survival" }),
            new ResearchTechnologyProfile("gathering", "Собирательство", "survival", "basic_survival", 1d, 0, 0, true, false, "leaf", "Сбор ягод, грибов, веток и растительных волокон.", "IMPLEMENTED", "", Array.Empty<string>(), 0, 0, new[] { "recipe:gather_berries", "recipe:gather_mushrooms", "recipe:gather_branches", "recipe:gather_fiber", "building:gathering_area" }),
            new ResearchTechnologyProfile("shelter_construction", "Простые укрытия", "survival", "gathering", 1.5d, 60, 1, false, false, "house", "Строительство примитивного укрытия. Первый безопасный ночлег.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:primitive_shelter" }),
            new ResearchTechnologyProfile("controlled_fire", "Управляемый огонь", "survival", "gathering", 1.5d, -60, 1, false, false, "fire", "Костёр и наземный факел. Огонь становится доступным инструментом поселения.", "PARTIAL", "", Array.Empty<string>(), 3, 0, new[] { "building:campfire", "building:ground_torch" }),
            new ResearchTechnologyProfile("hunting", "Организованная охота", "survival", "controlled_fire", 2.5d, -30, 2, false, true, "bow", "Охотничий лагерь и разделка добычи. Мясо, шкуры и кости вместо зависимости от ягод.", "IMPLEMENTED", "", Array.Empty<string>(), 0, 0, new[] { "building:hunting_camp", "building:butcher_station", "recipe:hunt_animal", "recipe:dress_carcass", "recipe:butcher_carcass", "recipe:butcher_field_carcass" }),
            new ResearchTechnologyProfile("fishing", "Рыболовство", "survival", "shelter_construction", 2.5d, 90, 2, false, true, "fish", "Рыба как самостоятельный путь снабжения; рыбацкий лагерь и причал.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:fishing_camp", "building:fishing_pier", "recipe:fish_water", "recipe:fish_camp", "recipe:fish_breed" }),
            new ResearchTechnologyProfile("primitive_agriculture", "Первое земледелие", "survival", "reliable_housing", 4d, 30, 3, false, true, "grain", "Грядки, сад и выращивание культур. Альтернатива охоте и рыболовству.", "IMPLEMENTED", "", Array.Empty<string>(), 0, 0, new[] { "building:primitive_crop_plot", "building:primitive_garden" }),
            new ResearchTechnologyProfile("cooking", "Приготовление пищи", "survival", "controlled_fire", 2.5d, -90, 2, false, false, "pot", "Жареная рыба, приготовленное мясо и ягоды; кухня для постоянного производства.", "PARTIAL", "", Array.Empty<string>(), 4, 0, new[] { "building:stone_age_kitchen", "recipe:cook_meat", "recipe:cook_fish", "recipe:cook_berries" }),
            new ResearchTechnologyProfile("reliable_housing", "Надёжные жилища", "survival", "shelter_construction", 4d, 30, 2, false, true, "house", "Хижины каменного века вместо временных укрытий.", "PARTIAL", "", new[] { "shelter" }, 0, 0, new[] { "building:stone_age_hut" }),
            new ResearchTechnologyProfile("food_preservation", "Сохранение пищи", "survival", "cooking", 4d, -90, 3, false, true, "storage", "Сушильная стойка и пищевой склад; существующая сушка мяса.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:food_storage", "building:drying_rack", "recipe:dry_meat" }),
            new ResearchTechnologyProfile("seasonal_preparation", "Сезонные запасы", "survival", "food_preservation", 5d, -90, 4, false, true, "sun", "Организация общего продовольственного склада перед сменой сезона.", "PARTIAL", "", new[] { "food" }, 0, 0, new[] { "building:central_storehouse" }),
            new ResearchTechnologyProfile("stone_tools", "Каменные орудия", "production", "basic_survival", 2.5d, 0, 0, true, false, "tools", "Добыча камня, рубка дерева, мастерская каменных орудий.", "IMPLEMENTED", "", Array.Empty<string>(), 0, 0, new[] { "recipe:gather_stone", "recipe:chop_wood", "building:stone_tool_workshop" }),
            new ResearchTechnologyProfile("fiber_working", "Растительные волокна", "production", "stone_tools", 1.5d, 0, 1, false, false, "fiber", "Подготовка волокон для верёвок и ткани.", "PROPOSED", "Нет отдельного рецепта обработки волокна или верёвки. Не закрывает доступ к существующей добыче волокон и оружию.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("woodworking", "Обработка древесины", "production", "stone_tools", 2.5d, -90, 1, false, false, "wood", "Корзины для хранения и начало строительного ремесла.", "IMPLEMENTED", "", Array.Empty<string>(), 5, 0, new[] { "building:storage_basket" }),
            new ResearchTechnologyProfile("clay_working", "Работа с глиной", "production", "stone_tools", 2.5d, 90, 1, false, false, "pot", "Добыча глины как основа будущего гончарства.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "recipe:gather_clay" }),
            new ResearchTechnologyProfile("organized_labor", "Организация труда", "production", "woodworking", 4d, -90, 2, false, true, "people", "Ремесленный зал и общинный дом. Работники распределяются через профессиональные группы.", "PARTIAL", "", new[] { "shelter" }, 0, 0, new[] { "building:crafting_hall", "building:communal_house" }),
            new ResearchTechnologyProfile("pottery", "Гончарное дело", "production", "clay_working", 4d, 90, 2, false, false, "pot", "Сосуды для хранения и приготовления пищи.", "PROPOSED", "Нужны рецепты керамики, ресурс-сосуд и рабочее место. Их нет в подтверждённом каталоге; технология недоступна.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("advanced_stonecraft", "Точная обработка камня", "production", "woodworking", 4d, -30, 2, false, true, "tools", "Добыча кремня, колодец и подготовка к специализированной добыче недр.", "PARTIAL", "", Array.Empty<string>(), 0, 8, new[] { "recipe:gather_flint", "building:well" }),
            new ResearchTechnologyProfile("hide_working", "Выделка шкур", "production", "organized_labor", 3d, -90, 3, false, true, "cloth", "Кожевенная мастерская, выделанная кожа и кожаная одежда.", "PARTIAL", "", new[] { "hides" }, 0, 0, new[] { "building:hide_workshop", "recipe:cure_hide", "recipe:make_leather_clothing" }),
            new ResearchTechnologyProfile("industrial_extraction", "Разработка недр", "production", "advanced_stonecraft", 5d, -30, 3, false, true, "ore", "Добыча уже существующих железной и золотой руд. Выплавку металла это не открывает.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "recipe:gather_iron", "recipe:gather_gold" }),
            new ResearchTechnologyProfile("weaving", "Плетение и ткачество", "production", "fiber_working", 4d, 30, 2, false, false, "cloth", "Ткань, плетёные изделия и тёплая одежда из волокон.", "PROPOSED", "Нужны рецепт ткани, ткацкое рабочее место и тканевая одежда. Существующая кожаная одежда открывается через выделку шкур.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("restorative_mining", "Восстановление выработок", "production", "industrial_extraction", 5.5d, -30, 4, false, false, "ore", "Установка восстановительных блоков в подходящих шахтах; пополнение запасов по существующим правилам добычи.", "IMPLEMENTED", "", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("standardization", "Ремесленные стандарты", "production", "advanced_stonecraft", 5.5d, 30, 3, false, false, "measure", "Единые размеры и качество ремесленных изделий.", "PROPOSED", "Нет подтверждённой системы качества и стандартизованных деталей. Не выдаём выдуманный бонус производства.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("primitive_weapons", "Примитивное оружие", "military", "basic_survival", 2.5d, 0, 0, true, false, "shield", "Изготовление каменных копий и деревянных щитов.", "PARTIAL", "", Array.Empty<string>(), 3, 3, new[] { "recipe:craft_stone_spear", "recipe:craft_wooden_shield" }),
            new ResearchTechnologyProfile("militia", "Ополчение поселения", "military", "primitive_weapons", 3d, -90, 1, false, true, "people", "Казарма для подготовки военных. Гражданские не могут входить в отряд.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:military_barracks" }),
            new ResearchTechnologyProfile("territory_guard", "Охрана территории", "military", "primitive_weapons", 2.5d, 90, 1, false, false, "tower", "Сторожевая башня для организации защиты поселения.", "PARTIAL", "Автоматические маршруты патрулирования не входят в эту технологию.", Array.Empty<string>(), 0, 0, new[] { "building:watchtower" }),
            new ResearchTechnologyProfile("formation_training", "Строевая подготовка", "military", "militia", 4d, -90, 2, false, true, "shield", "Тренировочная площадка. Подготовка постоянного войска.", "PARTIAL", "", new[] { "soldiers" }, 0, 0, new[] { "building:training_ground" }),
            new ResearchTechnologyProfile("throwing_weapons", "Лук и стрелы", "military", "primitive_weapons", 3d, 0, 1, false, true, "bow", "Изготовление боевых луков и стрел; отдельная специализация вооружения.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "recipe:craft_combat_bow", "recipe:craft_arrows" }),
            new ResearchTechnologyProfile("military_organization", "Военная организация", "military", "formation_training", 5.5d, -90, 3, false, true, "crown", "Увеличение максимального числа отрядов с двух до четырёх.", "IMPLEMENTED", "", new[] { "soldiers" }, 0, 0, new[] { "cap:military_organization" }),
            new ResearchTechnologyProfile("fortification", "Укрепление поселения", "military", "territory_guard", 5d, 90, 2, false, true, "tower", "Частокол и ворота. Защитный строительный путь без обязательного расширения армии.", "PARTIAL", "", Array.Empty<string>(), 12, 0, new[] { "building:palisade", "building:gate" }),
            new ResearchTechnologyProfile("military_logistics", "Военное снабжение", "military", "military_organization", 5.5d, -90, 4, false, false, "storage", "Организация походных запасов для дальних операций.", "PROPOSED", "Система снабжения существует, но отдельного эффекта capability military_logistics нет. Конкретный модификатор и потребитель требуют отдельного решения.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("observation", "Наблюдение природы", "knowledge", "basic_survival", 1.5d, 0, 0, true, false, "eye", "Систематизация наблюдений: +5% к эффективности исследовательской группы.", "PARTIAL", "", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("plant_lore", "Знание растений", "knowledge", "observation", 1.5d, -60, 1, false, false, "leaf", "Различать лекарственные растения и собирать их для будущего лечения.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "recipe:gather_medicinal_plants" }),
            new ResearchTechnologyProfile("animal_lore", "Знание животных", "knowledge", "observation", 2.5d, 60, 1, false, true, "animal", "Пастбище и приготовление корма. Прикладное изучение животных.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:animal_pasture", "recipe:make_animal_feed" }),
            new ResearchTechnologyProfile("herbalism", "Травничество", "knowledge", "plant_lore", 2.5d, -60, 2, false, true, "herb", "Приготовление лекарственного средства из собранных растений.", "PARTIAL", "", new[] { "herbs" }, 0, 0, new[] { "recipe:prepare_medicine" }),
            new ResearchTechnologyProfile("season_calendar", "Календарь сезонов", "knowledge", "animal_lore", 3d, 60, 2, false, true, "sun", "Сопоставление повторяющихся сезонов: ещё +5% к исследованиям.", "PARTIAL", "", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("wound_care", "Уход за ранеными", "knowledge", "herbalism", 3d, -60, 3, false, true, "cross", "Хижина лекаря как место медицинской работы.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:healer_hut" }),
            new ResearchTechnologyProfile("record_keeping", "Учёт знаний", "knowledge", "season_calendar", 4d, 60, 3, false, true, "book", "Передача результатов внутри группы: +1 эффективное место исследователя.", "PARTIAL", "", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("practical_anatomy", "Практическая анатомия", "knowledge", "wound_care", 4d, -60, 4, false, false, "cross", "Понимание травм и устройства тела.", "PROPOSED", "Нет подтверждённого потребителя исследовательского эффекта в лечении. Снижение осложнений пока не работает и не обещается.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("knowledge_transfer", "Передача опыта", "knowledge", "record_keeping", 5d, 60, 4, false, true, "people", "Обучение внутри группы: ещё +1 эффективное место исследователя.", "PARTIAL", "", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("natural_philosophy", "Систематизация знаний", "knowledge", "knowledge_transfer", 5.5d, 60, 5, false, true, "eye", "Сравнение наблюдений и причин: ещё +10% к исследованиям.", "PARTIAL", "", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("kinship", "Родовая община", "society", "basic_survival", 1.5d, 0, 0, true, false, "people", "Родовой дом. Семья и наследование продолжают работать с начала игры.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:clan_house" }),
            new ResearchTechnologyProfile("shared_storage", "Общинные пути", "society", "kinship", 1.5d, 0, 1, false, false, "road", "Прокладка примитивных путей между жилищами, складами и местами работы.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:primitive_path" }),
            new ResearchTechnologyProfile("settlement_council", "Совет поселения", "society", "kinship", 3d, -90, 1, false, true, "crown", "Место собраний и хижина вождя.", "PARTIAL", "Министры и ведомства — план U33; не часть действующего эффекта.", new[] { "shelter" }, 0, 0, new[] { "building:meeting_area", "building:chief_hut" }),
            new ResearchTechnologyProfile("spiritual_tradition", "Общие традиции", "society", "kinship", 2.5d, 90, 1, false, true, "sun", "Святилище как общественное здание.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "building:shrine" }),
            new ResearchTechnologyProfile("land_administration", "Управление землями", "society", "settlement_council", 4d, -90, 2, false, false, "house", "Назначение управляющих и распределение полномочий.", "PLANNED", "U33: министры, губернаторы и ведомства. Нет работающей власти, которую можно открыть одним флагом.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("exchange_and_measures", "Обмен и меры", "society", "shared_storage", 3d, 0, 2, false, false, "measure", "Договорённости об обмене и оценке товаров.", "PROPOSED", "Торговый рынок и обмен с ценами не подтверждены. Нужны отдельные правила торговли и интеграция.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("customary_law", "Обычное право", "society", "land_administration", 5d, -90, 3, false, false, "book", "Обязательные для общины правила и разрешение споров.", "PLANNED", "U35: законы и протесты; зависит от будущего U33, без влияния на условия эпохи.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("trade_contacts", "Торговые связи", "society", "exchange_and_measures", 4d, 0, 3, false, false, "road", "Устойчивый обмен с соседними поселениями.", "PROPOSED", "Нет подтверждённого API торговых договоров. Не создаём фиктивные дипломатические отношения.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("early_diplomacy", "Посольства общин", "society", "trade_contacts", 5.5d, 0, 4, false, false, "people", "Переговоры, обязательства и соглашения между общинами.", "PLANNED", "U36: дипломатия. Технология остаётся превью до реализации договоров и AI-оценки условий.", Array.Empty<string>(), 0, 0, Array.Empty<string>()),
            new ResearchTechnologyProfile("stone_age_mastery", "Освоение каменного века", "epoch", "", 6.5d, 0, 0, false, false, "book", "Завершение каменного века. Изучение оставшихся технологий остаётся доступным.", "PARTIAL", "", Array.Empty<string>(), 0, 0, new[] { "cap:stone_age_completed" }),
        });

        public static TechnologyCatalog Create(ProductionCatalog production, IEnumerable<BuildingDefinitionId> buildings,
            bool startsWithStoneTools)
        {
            if (production == null) throw new ArgumentNullException(nameof(production));
            var buildingSet = new HashSet<BuildingDefinitionId>(buildings ?? throw new ArgumentNullException(nameof(buildings)));
            var recipes = new HashSet<ProductionRecipeId>(production.All.Select(x=>x.Id));
            var caps = new[] { new CapabilityId("basic_survival"), new CapabilityId("military_organization"),
                new CapabilityId("stone_age_completed") };
            var definitions = new List<TechnologyDefinition>();
            foreach (var p in All)
            {
                var unlocks = new List<TechnologyUnlock>();
                if (!p.IsFuture)
                foreach (var key in p.UnlockIds)
                {
                    var parts=key.Split(':');
                    if (parts[0]=="building" && buildingSet.Contains(new BuildingDefinitionId(parts[1])))
                        unlocks.Add(TechnologyUnlock.Building(new BuildingDefinitionId(parts[1])));
                    else if (parts[0]=="recipe" && recipes.Contains(new ProductionRecipeId(parts[1])))
                        unlocks.Add(TechnologyUnlock.Recipe(new ProductionRecipeId(parts[1])));
                    else if (parts[0]=="cap") unlocks.Add(TechnologyUnlock.Capability(new CapabilityId(parts[1])));
                }
                if (p.Id=="primitive_agriculture")
                    unlocks.AddRange(production.All.Where(x=>x.SiteKind==ProductionSiteKind.PrimitiveCropPlot)
                        .Select(x=>TechnologyUnlock.Recipe(x.Id)));
                definitions.Add(new TechnologyDefinition(new TechnologyId(p.Id), p.Name,
                    string.IsNullOrEmpty(p.Parent) ? Array.Empty<TechnologyId>() : new[]{new TechnologyId(p.Parent)},
                    TimeSpan.FromDays(p.WorkDays), unlocks.Distinct(),
                    description:p.Effect, startingKnowledge:p.Id=="basic_survival" || p.Id=="gathering" ||
                        startsWithStoneTools && p.Id=="stone_tools", profile:p));
            }
            return new TechnologyCatalog(definitions, new TechnologyUnlockTargets(buildingSet, recipes, caps));
        }
    }
}
