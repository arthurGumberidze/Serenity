using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Research;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Weather;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Control;
using Game.Domain.Buildings;
using Game.Domain.World;
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>Strict versioned UTF-8 XML checkpoint codec. V13 adds strategic AI state.</summary>
    public sealed class SaveXmlCodec
    {
        private static readonly string[] TimeFields =
            { "version", "sessionId", "calendarTicks", "biologicalTicks", "speed", "paused", "biologicalMultiplier" };

        public byte[] Serialize(SaveSnapshot snapshot)
        {
            SaveCoordinator.CreateClock(snapshot);
            var root = new XElement("serenitySave",
                new XElement("version", snapshot.FormatVersion),
                new XElement("sessionId", snapshot.SessionId.ToString()),
                new XElement("calendarTicks", snapshot.CalendarTicks),
                new XElement("biologicalTicks", snapshot.BiologicalTicks),
                new XElement("speed", snapshot.SpeedMultiplier),
                new XElement("paused", snapshot.IsPaused),
                new XElement("biologicalMultiplier", snapshot.BiologicalMultiplier));
            if (snapshot.FormatVersion >= SaveSnapshot.ProductionVersion)
                root.Add(SerializeProduction(snapshot.Production));
            if (snapshot.FormatVersion >= SaveSnapshot.ResearchVersion)
                root.Add(SerializeResearch(snapshot.Research));
            if (snapshot.FormatVersion >= SaveSnapshot.DynastyVersion)
                root.Add(SerializeDynastyWorld(snapshot.DynastyWorld));
            if (snapshot.FormatVersion >= SaveSnapshot.HealthVersion)
                root.Add(SerializeHealthFamily(snapshot.HealthFamily));
            if (snapshot.FormatVersion >= SaveSnapshot.WeatherVersion)
                root.Add(SerializeWeather(snapshot.Weather));
            if (snapshot.FormatVersion >= SaveSnapshot.CombatVersion)
                root.Add(SerializeCombat(snapshot.Combat, snapshot.FormatVersion >= SaveSnapshot.BattleVersion));
            if (snapshot.FormatVersion >= SaveSnapshot.MilitaryVersion)
                root.Add(SerializeMilitary(snapshot.Military));
            if (snapshot.FormatVersion >= SaveSnapshot.PlayerControlVersion)
                root.Add(SerializePlayerControl(snapshot.PlayerControl));
            if (snapshot.FormatVersion >= SaveSnapshot.BuildingVersion)
                root.Add(SerializeBuildingWorld(snapshot.BuildingWorld));
            if (snapshot.FormatVersion >= SaveSnapshot.GlobalMapVersion)
                root.Add(SerializeGlobalMap(snapshot.GlobalMap));
            if (snapshot.FormatVersion >= SaveSnapshot.StrategicAiVersion)
                root.Add(SerializeStrategicAi(snapshot.StrategicAi));
            using (var stream = new MemoryStream())
            {
                new XDocument(root).Save(stream);
                return stream.ToArray();
            }
        }

        public SaveSnapshot Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 4 * 1024 * 1024
                }))
                {
                    var root = XDocument.Load(reader).Root;
                    if (root == null || root.Name != "serenitySave") throw new FormatException("Invalid save root.");
                    var versionElement = root.Elements("version").SingleOrDefault();
                    if (versionElement == null) throw new FormatException("Missing save version.");
                    var version = XmlConvert.ToInt32(versionElement.Value);
                    if (version != SaveSnapshot.LegacyVersion && version != SaveSnapshot.ProductionVersion &&
                        version != SaveSnapshot.ResearchVersion && version != SaveSnapshot.DynastyVersion &&
                        version != SaveSnapshot.HealthVersion && version != SaveSnapshot.WeatherVersion &&
                        version != SaveSnapshot.CombatVersion && version != SaveSnapshot.MilitaryVersion &&
                        version != SaveSnapshot.PlayerControlVersion &&
                        version != SaveSnapshot.BattleVersion && version != SaveSnapshot.BuildingVersion &&
                        version != SaveSnapshot.GlobalMapVersion && version != SaveSnapshot.CurrentVersion)
                        throw new NotSupportedException("Unsupported save format version: " + version);
                    RequireFields(root, FieldsForVersion(version));
                    var production = version >= SaveSnapshot.ProductionVersion
                        ? DeserializeProduction(root.Element("production")) : null;
                    var research = version >= SaveSnapshot.ResearchVersion
                        ? DeserializeResearch(root.Element("research")) : null;
                    var dynastyWorld = version >= SaveSnapshot.DynastyVersion
                        ? DeserializeDynastyWorld(root.Element("dynastyWorld")) : null;
                    var healthFamily = version >= SaveSnapshot.HealthVersion
                        ? DeserializeHealthFamily(root.Element("healthFamily")) : null;
                    var weather = version >= SaveSnapshot.WeatherVersion
                        ? DeserializeWeather(root.Element("weather")) : null;
                    var combat = version >= SaveSnapshot.CombatVersion
                        ? DeserializeCombat(root.Element("combat"), version >= SaveSnapshot.BattleVersion) : null;
                    var military = version >= SaveSnapshot.MilitaryVersion
                        ? DeserializeMilitary(root.Element("military")) : null;
                    var playerControl = version >= SaveSnapshot.PlayerControlVersion
                        ? DeserializePlayerControl(root.Element("playerControl")) : null;
                    var buildingWorld = version >= SaveSnapshot.BuildingVersion
                        ? DeserializeBuildingWorld(root.Element("buildingWorld")) : null;
                    var globalMap = version >= SaveSnapshot.GlobalMapVersion
                        ? DeserializeGlobalMap(root.Element("globalMap")) : null;
                    var strategicAi = version >= SaveSnapshot.StrategicAiVersion
                        ? DeserializeStrategicAi(root.Element("strategicAi")) : null;
                    var snapshot = new SaveSnapshot(version, StableEntityId.Parse(Value(root, "sessionId")),
                        new GameTimeState(Long(root, "calendarTicks"), Long(root, "biologicalTicks"),
                            Int(root, "speed"), XmlConvert.ToBoolean(Value(root, "paused"))),
                        Int(root, "biologicalMultiplier"), production, research, dynastyWorld, healthFamily, weather,
                        combat, military, playerControl, buildingWorld, globalMap, strategicAi);
                    SaveCoordinator.CreateClock(snapshot);
                    return snapshot;
                }
            }
            catch (Exception ex) when (ex is XmlException || ex is FormatException || ex is ArgumentException ||
                                       ex is OverflowException || ex is InvalidOperationException)
            {
                throw new InvalidDataException("Corrupted save data.", ex);
            }
        }

        private static XElement SerializeProduction(ProductionSaveState state)
        {
            var element = new XElement("production");
            if (state == null) return element;
            element.Add(new XElement("lastCalendarTick", state.LastCalendarTick),
                new XElement("foodRemainderTicks", state.FoodRemainderTicks),
                new XElement("foodConsumed", state.FoodConsumed),
                new XElement("foodShortage", state.FoodShortage),
                new XElement("targets", state.Targets.Select(target => new XElement("target",
                    new XElement("resourceId", target.ResourceId.ToString()), new XElement("value", target.Target)))),
                new XElement("sites", state.Sites.Select(site => new XElement("site",
                    new XElement("id", site.Id.ToString()), new XElement("recipeId", site.RecipeId.ToString()),
                    new XElement("kind", (int)site.Kind), new XElement("x", XmlConvert.ToString(site.Position.X)),
                    new XElement("y", XmlConvert.ToString(site.Position.Y)), new XElement("z", XmlConvert.ToString(site.Position.Z)),
                    new XElement("remaining", site.RemainingAmount), new XElement("capacity", site.SourceCapacity),
                    new XElement("cropPhase", (int)site.CropPhase), new XElement("growthProgress", site.GrowthProgressTicks),
                    new XElement("growthRequired", site.GrowthRequiredTicks)))),
                new XElement("tasks", state.Tasks.Select(task => new XElement("task",
                    new XElement("id", task.Id.ToString()), new XElement("workerId", task.WorkerId.ToString()),
                    new XElement("siteId", task.SiteId.ToString()), new XElement("recipeId", task.RecipeId.ToString()),
                    new XElement("kind", (int)task.Kind), new XElement("work", task.AccumulatedWorkTicks),
                    new XElement("required", task.RequiredWorkTicks), new XElement("status", (int)task.Status)))),
                new XElement("producedTotals", state.ProducedTotals.Select(SerializeAmount)),
                new XElement("inventories", state.Inventories.Select(SerializeInventory)));
            return element;
        }

        private static XElement SerializeInventory(ResourceInventoryState state) => new XElement("inventory",
            new XElement("ownerKind", (int)state.Owner.Kind), new XElement("ownerId", state.Owner.Id.ToString()),
            new XElement("capacity", state.Capacity), new XElement("acceptsAll", state.AcceptsAllCategories),
            new XElement("categories", state.AcceptedCategories.Select(category => new XElement("category", (int)category))),
            new XElement("amounts", state.Amounts.Select(SerializeAmount)));

        private static XElement SerializeAmount(ResourceAmount amount) => new XElement("amount",
            new XElement("resourceId", amount.ResourceId.ToString()), new XElement("quantity", amount.Quantity.Units));

        private static ProductionSaveState DeserializeProduction(XElement element)
        {
            if (element == null) throw new FormatException("Missing production state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty production state.");
                return null;
            }
            RequireFields(element, new[] { "lastCalendarTick", "foodRemainderTicks", "foodConsumed", "foodShortage",
                "targets", "sites", "tasks", "producedTotals", "inventories" });
            var targets = Items(element.Element("targets"), "target", item =>
            {
                RequireFields(item, new[] { "resourceId", "value" });
                return new ResourceStockTarget(new ResourceId(Value(item, "resourceId")), Long(item, "value"));
            });
            var sites = Items(element.Element("sites"), "site", item =>
            {
                RequireFields(item, new[] { "id", "recipeId", "kind", "x", "y", "z", "remaining", "capacity",
                    "cropPhase", "growthProgress", "growthRequired" });
                return new ProductionSiteState(StableEntityId.Parse(Value(item, "id")),
                    new ProductionRecipeId(Value(item, "recipeId")), (ProductionSiteKind)Int(item, "kind"),
                    new WorldPosition(XmlConvert.ToSingle(Value(item, "x")), XmlConvert.ToSingle(Value(item, "y")),
                        XmlConvert.ToSingle(Value(item, "z"))), Long(item, "remaining"), Long(item, "capacity"),
                    (CropPlotPhase)Int(item, "cropPhase"), Long(item, "growthProgress"), Long(item, "growthRequired"));
            });
            var tasks = Items(element.Element("tasks"), "task", item =>
            {
                RequireFields(item, new[] { "id", "workerId", "siteId", "recipeId", "kind", "work", "required", "status" });
                return new ProductionTaskState(StableEntityId.Parse(Value(item, "id")),
                    StableEntityId.Parse(Value(item, "workerId")), StableEntityId.Parse(Value(item, "siteId")),
                    new ProductionRecipeId(Value(item, "recipeId")), (ProductionTaskKind)Int(item, "kind"),
                    Long(item, "work"), Long(item, "required"), (ProductionTaskStatus)Int(item, "status"));
            });
            var produced = Items(element.Element("producedTotals"), "amount", DeserializeAmount);
            var inventories = Items(element.Element("inventories"), "inventory", item =>
            {
                RequireFields(item, new[] { "ownerKind", "ownerId", "capacity", "acceptsAll", "categories", "amounts" });
                var acceptsAll = XmlConvert.ToBoolean(Value(item, "acceptsAll"));
                var categories = Items(item.Element("categories"), "category",
                    category => (ResourceCategory)XmlConvert.ToInt32(category.Value));
                var amounts = Items(item.Element("amounts"), "amount", DeserializeAmount);
                return new ResourceInventoryState(new InventoryOwner((InventoryOwnerKind)Int(item, "ownerKind"),
                    StableEntityId.Parse(Value(item, "ownerId"))), Long(item, "capacity"),
                    acceptsAll ? null : categories, amounts);
            });
            return new ProductionSaveState(Long(element, "lastCalendarTick"), Long(element, "foodRemainderTicks"),
                Long(element, "foodConsumed"), Long(element, "foodShortage"), targets, sites, tasks, produced, inventories);
        }

        private static ResourceAmount DeserializeAmount(XElement item)
        {
            RequireFields(item, new[] { "resourceId", "quantity" });
            return new ResourceAmount(new ResourceId(Value(item, "resourceId")), new ResourceQuantity(Long(item, "quantity")));
        }

        private static XElement SerializeResearch(ResearchSaveState state)
        {
            var element = new XElement("research");
            if (state == null) return element;
            element.Add(new XElement("lastProcessedTick", state.LastProcessedTick),
                new XElement("completed", state.Completed.Select(id => new XElement("technology", id.ToString()))),
                new XElement("activeProject", state.ActiveProject.HasValue
                    ? new[]
                    {
                        new XElement("technologyId", state.ActiveProject.Value.TechnologyId.ToString()),
                        new XElement("work", state.ActiveProject.Value.AccumulatedWorkTicks),
                        new XElement("required", state.ActiveProject.Value.RequiredWorkTicks),
                        new XElement("startTick", state.ActiveProject.Value.StartTick)
                    }
                    : Array.Empty<XElement>()),
                new XElement("workers", state.Workers.Select(worker => new XElement("worker",
                    new XElement("id", worker.WorkerId.ToString()),
                    new XElement("lastProcessedTick", worker.LastProcessedTick),
                    new XElement("remainder", worker.FractionalRemainder)))));
            return element;
        }

        private static ResearchSaveState DeserializeResearch(XElement element)
        {
            if (element == null) throw new FormatException("Missing research state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty research state.");
                return null;
            }
            RequireFields(element, new[] { "lastProcessedTick", "completed", "activeProject", "workers" });
            var completed = Items(element.Element("completed"), "technology",
                item => new TechnologyId(Scalar(item)));
            var activeElement = element.Element("activeProject");
            ResearchProjectState? active = null;
            if (activeElement == null || activeElement.HasAttributes ||
                activeElement.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
                throw new FormatException("Invalid active research project.");
            if (activeElement.HasElements)
            {
                RequireFields(activeElement, new[] { "technologyId", "work", "required", "startTick" });
                active = new ResearchProjectState(new TechnologyId(Value(activeElement, "technologyId")),
                    Long(activeElement, "work"), Long(activeElement, "required"), Long(activeElement, "startTick"));
            }
            var workers = Items(element.Element("workers"), "worker", item =>
            {
                RequireFields(item, new[] { "id", "lastProcessedTick", "remainder" });
                return new ResearchWorkerState(StableEntityId.Parse(Value(item, "id")),
                    Long(item, "lastProcessedTick"), Int(item, "remainder"));
            });
            return new ResearchSaveState(Long(element, "lastProcessedTick"), completed, active, workers);
        }

        private static XElement SerializeDynastyWorld(DynastyWorldSaveState state)
        {
            var element = new XElement("dynastyWorld");
            if (state == null) return element;
            var dynasty = state.Dynasty;
            element.Add(new XElement("characters", state.Characters.Select(SerializeCharacter)),
                new XElement("dynasty",
                    new XElement("id", dynasty.Id.ToString()),
                    new XElement("name", dynasty.Name),
                    new XElement("founderId", dynasty.FounderId.ToString()),
                    OptionalId("currentMonarchId", dynasty.CurrentMonarchId),
                    new XElement("successionRuleId", dynasty.SuccessionRuleId),
                    new XElement("status", (int)dynasty.Status),
                    new XElement("members", dynasty.MemberIds.Select(id => new XElement("member", id.ToString()))),
                    new XElement("processedDeaths", dynasty.ProcessedMonarchDeathIds.Select(id => new XElement("character", id.ToString()))),
                    new XElement("history", dynasty.History.Select(entry => new XElement("entry",
                        new XElement("sequence", entry.SequenceNumber),
                        new XElement("previousMonarchId", entry.PreviousMonarchId.ToString()),
                        OptionalId("newMonarchId", entry.NewMonarchId),
                        new XElement("calendarTick", entry.CalendarTick),
                        new XElement("reason", (int)entry.Reason))))),
                OptionalId("currentRulerId", state.CurrentRulerId),
                new XElement("runStatus", (int)state.RunStatus),
                new XElement("gameOverReason", (int)state.GameOverReason));
            return element;
        }

        private static XElement SerializeCharacter(CharacterState state) => new XElement("character",
            new XElement("id", state.Id.ToString()),
            new XElement("givenName", state.Name.GivenName),
            new XElement("familyName", state.Name.FamilyName),
            new XElement("sex", (int)state.Sex),
            new XElement("birthTick", state.BiologicalBirthTick),
            new XElement("lifeState", (int)state.LifeState),
            new XElement("deathTick", state.BiologicalDeathTick.HasValue ? state.BiologicalDeathTick.Value.ToString() : string.Empty),
            new XElement("parents", state.Parents.Select(parent => new XElement("parent",
                new XElement("id", parent.ParentId.ToString()), new XElement("role", (int)parent.Role),
                new XElement("kind", (int)parent.Kind), new XElement("known", parent.IsKnownToCharacter)))),
            OptionalId("spouseId", state.SpouseId), OptionalId("familyId", state.FamilyId),
            OptionalId("dynastyId", state.DynastyId),
            new XElement("professionId", state.ProfessionId.HasValue ? state.ProfessionId.Value.ToString() : string.Empty),
            new XElement("traits", state.Traits.Select(trait => new XElement("trait", trait.ToString()))),
            new XElement("skills", state.Skills.Select(skill => new XElement("skill",
                new XElement("id", skill.Id.ToString()), new XElement("level", skill.Level.Value)))),
            new XElement("attributes", new XElement("intelligence", state.Attributes.Intelligence.Value),
                new XElement("physicalStrength", state.Attributes.PhysicalStrength.Value)),
            new XElement("health", new XElement("head", state.Health.Head.Value),
                new XElement("torso", state.Health.Torso.Value), new XElement("leftArm", state.Health.LeftArm.Value),
                new XElement("rightArm", state.Health.RightArm.Value), new XElement("leftLeg", state.Health.LeftLeg.Value),
                new XElement("rightLeg", state.Health.RightLeg.Value)),
            new XElement("wealth", state.Wealth.Value), new XElement("influence", state.Influence.Value),
            new XElement("relationships", state.Relationships.Select(relationship => new XElement("relationship",
                new XElement("id", relationship.OtherCharacterId.ToString()),
                new XElement("affinity", relationship.Affinity.Value)))));

        private static DynastyWorldSaveState DeserializeDynastyWorld(XElement element)
        {
            if (element == null) throw new FormatException("Missing dynasty world state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty dynasty world state.");
                return null;
            }
            RequireFields(element, new[] { "characters", "dynasty", "currentRulerId", "runStatus", "gameOverReason" });
            var characters = Items(element.Element("characters"), "character", DeserializeCharacter);
            var dynastyElement = element.Element("dynasty");
            RequireFields(dynastyElement, new[] { "id", "name", "founderId", "currentMonarchId", "successionRuleId",
                "status", "members", "processedDeaths", "history" });
            var members = Items(dynastyElement.Element("members"), "member", item => StableEntityId.Parse(Scalar(item)));
            var processed = Items(dynastyElement.Element("processedDeaths"), "character", item => StableEntityId.Parse(Scalar(item)));
            var history = Items(dynastyElement.Element("history"), "entry", item =>
            {
                RequireFields(item, new[] { "sequence", "previousMonarchId", "newMonarchId", "calendarTick", "reason" });
                return new SuccessionHistoryEntry(Long(item, "sequence"), StableEntityId.Parse(Value(item, "previousMonarchId")),
                    OptionalStableId(item, "newMonarchId"), Long(item, "calendarTick"), (SuccessionReason)Int(item, "reason"));
            });
            var dynasty = new DynastyStateSnapshot(StableEntityId.Parse(Value(dynastyElement, "id")),
                Value(dynastyElement, "name"), StableEntityId.Parse(Value(dynastyElement, "founderId")),
                OptionalStableId(dynastyElement, "currentMonarchId"), Value(dynastyElement, "successionRuleId"),
                (DynastyStatus)Int(dynastyElement, "status"), members, processed, history);
            return new DynastyWorldSaveState(characters, dynasty, OptionalStableId(element, "currentRulerId"),
                (GameRunStatus)Int(element, "runStatus"), (GameOverReason)Int(element, "gameOverReason"));
        }

        private static CharacterState DeserializeCharacter(XElement item)
        {
            RequireFields(item, new[] { "id", "givenName", "familyName", "sex", "birthTick", "lifeState", "deathTick",
                "parents", "spouseId", "familyId", "dynastyId", "professionId", "traits", "skills", "attributes",
                "health", "wealth", "influence", "relationships" });
            var parents = Items(item.Element("parents"), "parent", parent =>
            {
                RequireFields(parent, new[] { "id", "role", "kind", "known" });
                return new ParentLink(StableEntityId.Parse(Value(parent, "id")), (ParentRole)Int(parent, "role"),
                    (ParentageKind)Int(parent, "kind"), XmlConvert.ToBoolean(Value(parent, "known")));
            });
            var traits = Items(item.Element("traits"), "trait", trait => new TraitId(Scalar(trait)));
            var skills = Items(item.Element("skills"), "skill", skill =>
            {
                RequireFields(skill, new[] { "id", "level" });
                return new SkillState(new SkillId(Value(skill, "id")), new SkillLevel(Int(skill, "level")));
            });
            var attributes = item.Element("attributes");
            RequireFields(attributes, new[] { "intelligence", "physicalStrength" });
            var health = item.Element("health");
            RequireFields(health, new[] { "head", "torso", "leftArm", "rightArm", "leftLeg", "rightLeg" });
            var relationships = Items(item.Element("relationships"), "relationship", relation =>
            {
                RequireFields(relation, new[] { "id", "affinity" });
                return new RelationshipState(StableEntityId.Parse(Value(relation, "id")),
                    new RelationshipScore(Int(relation, "affinity")));
            });
            var deathText = Value(item, "deathTick");
            var professionText = Value(item, "professionId");
            return new CharacterState(StableEntityId.Parse(Value(item, "id")),
                new CharacterName(Value(item, "givenName"), Value(item, "familyName")),
                (CharacterSex)Int(item, "sex"), Long(item, "birthTick"), (CharacterLifeState)Int(item, "lifeState"),
                string.IsNullOrEmpty(deathText) ? (long?)null : XmlConvert.ToInt64(deathText), parents,
                OptionalStableId(item, "spouseId"), OptionalStableId(item, "familyId"), OptionalStableId(item, "dynastyId"),
                string.IsNullOrEmpty(professionText) ? (ProfessionId?)null : new ProfessionId(professionText),
                traits, skills,
                new CharacterAttributes(new CharacterAttributeValue(Int(attributes, "intelligence")),
                    new CharacterAttributeValue(Int(attributes, "physicalStrength"))),
                new BodyHealth(new HealthValue(Int(health, "head")), new HealthValue(Int(health, "torso")),
                    new HealthValue(Int(health, "leftArm")), new HealthValue(Int(health, "rightArm")),
                    new HealthValue(Int(health, "leftLeg")), new HealthValue(Int(health, "rightLeg"))),
                new Wealth(Int(item, "wealth")), new Influence(Int(item, "influence")), relationships);
        }

        private static XElement SerializeHealthFamily(HealthFamilySaveState state)
        {
            var element = new XElement("healthFamily");
            if (state == null) return element;
            element.Add(new XElement("worldSeed", state.WorldSeed),
                new XElement("lastCalendarTick", state.LastCalendarTick),
                new XElement("lastBiologicalTick", state.LastBiologicalTick),
                new XElement("conditions", state.Conditions.Select(value => new XElement("condition",
                    new XElement("id", value.InstanceId.ToString()),
                    new XElement("definitionId", value.DefinitionId.ToString()),
                    new XElement("targetId", value.TargetId.ToString()),
                    new XElement("startTick", value.StartCalendarTick),
                    new XElement("nextProgressTick", value.NextProgressCalendarTick),
                    new XElement("resolveTick", value.ResolveCalendarTick),
                    new XElement("severity", (int)value.Severity),
                    new XElement("state", (int)value.State),
                    new XElement("progressionSteps", value.ProgressionSteps),
                    new XElement("deathProcessed", value.DeathProcessed)))),
                new XElement("events", state.Events.Select(value => new XElement("event",
                    new XElement("id", value.InstanceId.ToString()),
                    new XElement("definitionId", value.DefinitionId.ToString()),
                    new XElement("primaryId", value.PrimaryId.ToString()),
                    new XElement("secondaryId", value.SecondaryId.ToString()),
                    new XElement("calendarTick", value.CalendarTick),
                    new XElement("appliedDelta", value.AppliedDelta),
                    new XElement("applied", value.Applied)))),
                new XElement("pregnancies", state.Pregnancies.Select(value => new XElement("pregnancy",
                    new XElement("id", value.PregnancyId.ToString()),
                    new XElement("motherId", value.MotherId.ToString()),
                    new XElement("fatherId", value.FatherId.ToString()),
                    new XElement("conceptionCalendarTick", value.ConceptionCalendarTick),
                    new XElement("conceptionBiologicalTick", value.ConceptionBiologicalTick),
                    new XElement("dueCalendarTick", value.DueCalendarTick),
                    new XElement("dueBiologicalTick", value.DueBiologicalTick),
                    new XElement("status", (int)value.Status),
                    OptionalId("childId", value.ChildId)))));
            return element;
        }

        private static HealthFamilySaveState DeserializeHealthFamily(XElement element)
        {
            if (element == null) throw new FormatException("Missing health/family state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty health/family state.");
                return null;
            }
            RequireFields(element, new[] { "worldSeed", "lastCalendarTick", "lastBiologicalTick",
                "conditions", "events", "pregnancies" });
            var conditions = Items(element.Element("conditions"), "condition", value =>
            {
                RequireFields(value, new[] { "id", "definitionId", "targetId", "startTick", "nextProgressTick",
                    "resolveTick", "severity", "state", "progressionSteps", "deathProcessed" });
                return new HealthConditionInstanceState(StableEntityId.Parse(Value(value, "id")),
                    new HealthConditionId(Value(value, "definitionId")), StableEntityId.Parse(Value(value, "targetId")),
                    Long(value, "startTick"), Long(value, "nextProgressTick"), Long(value, "resolveTick"),
                    (HealthSeverity)Int(value, "severity"), (HealthConditionState)Int(value, "state"),
                    Int(value, "progressionSteps"), XmlConvert.ToBoolean(Value(value, "deathProcessed")));
            });
            var events = Items(element.Element("events"), "event", value =>
            {
                RequireFields(value, new[] { "id", "definitionId", "primaryId", "secondaryId", "calendarTick",
                    "appliedDelta", "applied" });
                return new CharacterEventInstanceState(StableEntityId.Parse(Value(value, "id")),
                    new CharacterEventId(Value(value, "definitionId")), StableEntityId.Parse(Value(value, "primaryId")),
                    StableEntityId.Parse(Value(value, "secondaryId")), Long(value, "calendarTick"),
                    Int(value, "appliedDelta"), XmlConvert.ToBoolean(Value(value, "applied")));
            });
            var pregnancies = Items(element.Element("pregnancies"), "pregnancy", value =>
            {
                RequireFields(value, new[] { "id", "motherId", "fatherId", "conceptionCalendarTick",
                    "conceptionBiologicalTick", "dueCalendarTick", "dueBiologicalTick", "status", "childId" });
                return new PregnancyState(StableEntityId.Parse(Value(value, "id")),
                    StableEntityId.Parse(Value(value, "motherId")), StableEntityId.Parse(Value(value, "fatherId")),
                    Long(value, "conceptionCalendarTick"), Long(value, "conceptionBiologicalTick"),
                    Long(value, "dueCalendarTick"), Long(value, "dueBiologicalTick"),
                    (PregnancyStatus)Int(value, "status"), OptionalStableId(value, "childId"));
            });
            return new HealthFamilySaveState(Long(element, "worldSeed"), Long(element, "lastCalendarTick"),
                Long(element, "lastBiologicalTick"), conditions, events, pregnancies);
        }

        private static XElement SerializeWeather(WeatherSaveState state)
        {
            var element = new XElement("weather");
            if (state == null) return element;
            element.Add(new XElement("worldSeed", state.WorldSeed),
                new XElement("climateProfileId", state.ClimateProfileId.ToString()),
                new XElement("exposureCheckpoints", state.ExposureCheckpoints.Select(value =>
                    new XElement("checkpoint", new XElement("characterId", value.CharacterId.ToString()),
                        new XElement("lastProcessedDay", value.LastProcessedDay)))));
            return element;
        }

        private static WeatherSaveState DeserializeWeather(XElement element)
        {
            if (element == null) throw new FormatException("Missing weather state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty weather state.");
                return null;
            }
            RequireFields(element, new[] { "worldSeed", "climateProfileId", "exposureCheckpoints" });
            var checkpoints = Items(element.Element("exposureCheckpoints"), "checkpoint", value =>
            {
                RequireFields(value, new[] { "characterId", "lastProcessedDay" });
                return new WeatherExposureCheckpoint(StableEntityId.Parse(Value(value, "characterId")),
                    Long(value, "lastProcessedDay"));
            });
            return new WeatherSaveState(Long(element, "worldSeed"),
                new ClimateProfileId(Value(element, "climateProfileId")), checkpoints);
        }

        private static XElement SerializeCombat(CombatSaveState state, bool includeBattleState)
        {
            var element = new XElement("combat");
            if (state == null) return element;
            element.Add(new XElement("worldSeed", state.WorldSeed),
                new XElement("lastCalendarTick", state.LastCalendarTick),
                new XElement("encounters", state.Encounters.Select(encounter => new XElement("encounter",
                    new XElement("id", encounter.EncounterId.ToString()),
                    new XElement("sideA", encounter.SideA.ToString()),
                    new XElement("sideB", encounter.SideB.ToString()),
                    new XElement("startTick", encounter.StartCalendarTick),
                    new XElement("lastTick", encounter.LastProcessedCalendarTick),
                    new XElement("status", (int)encounter.Status),
                    new XElement("outcome", (int)encounter.Outcome),
                    includeBattleState ? new XElement("mode", (int)encounter.Mode) : null,
                    includeBattleState ? new XElement("sideAUnitId",
                        encounter.SideAUnitId.HasValue ? encounter.SideAUnitId.Value.ToString() : string.Empty) : null,
                    includeBattleState ? new XElement("sideBUnitId",
                        encounter.SideBUnitId.HasValue ? encounter.SideBUnitId.Value.ToString() : string.Empty) : null,
                    new XElement("combatants", encounter.Combatants.Select(value => new XElement("combatant",
                        new XElement("characterId", value.CharacterId.ToString()),
                        new XElement("sideId", value.SideId.ToString()),
                        new XElement("weaponId", value.WeaponId.ToString()),
                        new XElement("state", (int)value.State),
                        OptionalId("targetId", value.TargetId),
                        OptionalId("assignedTargetId", value.AssignedTargetId),
                        new XElement("nextAttackTick", value.NextAttackTick),
                        new XElement("attackSequence", value.AttackSequence),
                        new XElement("morale", value.Morale),
                        new XElement("retreatX", XmlConvert.ToString(value.RetreatPoint.X)),
                        new XElement("retreatY", XmlConvert.ToString(value.RetreatPoint.Y)),
                        new XElement("retreatZ", XmlConvert.ToString(value.RetreatPoint.Z)),
                        new XElement("recentInjury", value.RecentInjury),
                        new XElement("casualtyPenaltyApplied", value.CasualtyPenaltyApplied)))),
                    new XElement("attacks", encounter.AttackHistory.Select(value => new XElement("attack",
                        new XElement("attackerId", value.AttackerId.ToString()),
                        new XElement("defenderId", value.DefenderId.ToString()),
                        new XElement("sequence", value.AttackSequence),
                        new XElement("calendarTick", value.CalendarTick),
                        new XElement("injuryId", value.InjuryId.ToString()),
                        new XElement("applied", value.Applied),
                        new XElement("fatal", value.Fatal)))),
                    includeBattleState ? new XElement("battleHistory", encounter.BattleHistory.Select(value =>
                        new XElement("event",
                            new XElement("sequence", value.Sequence),
                            new XElement("calendarTick", value.CalendarTick),
                            new XElement("kind", (int)value.Kind),
                            OptionalId("characterId", value.CharacterId),
                            new XElement("sideId", value.SideId.HasValue ? value.SideId.Value.ToString() : string.Empty),
                            new XElement("mode", value.Mode.HasValue ? ((int)value.Mode.Value).ToString() : string.Empty)))) : null))));
            return element;
        }

        private static CombatSaveState DeserializeCombat(XElement element, bool includeBattleState)
        {
            if (element == null) throw new FormatException("Missing combat state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty combat state.");
                return null;
            }
            RequireFields(element, new[] { "worldSeed", "lastCalendarTick", "encounters" });
            var encounters = Items(element.Element("encounters"), "encounter", encounter =>
            {
                var encounterFields = new[] { "id", "sideA", "sideB", "startTick", "lastTick", "status",
                    "outcome", "combatants", "attacks" }.AsEnumerable();
                if (includeBattleState) encounterFields = encounterFields.Concat(new[]
                    { "mode", "sideAUnitId", "sideBUnitId", "battleHistory" });
                RequireFields(encounter, encounterFields.ToArray());
                var combatants = Items(encounter.Element("combatants"), "combatant", value =>
                {
                    RequireFields(value, new[] { "characterId", "sideId", "weaponId", "state", "targetId",
                        "assignedTargetId", "nextAttackTick", "attackSequence", "morale", "retreatX",
                        "retreatY", "retreatZ", "recentInjury", "casualtyPenaltyApplied" });
                    return new CombatantCombatState(StableEntityId.Parse(Value(value, "characterId")),
                        new CombatSideId(Value(value, "sideId")), new CombatWeaponId(Value(value, "weaponId")),
                        (CombatantState)Int(value, "state"), OptionalStableId(value, "targetId"),
                        OptionalStableId(value, "assignedTargetId"), Long(value, "nextAttackTick"),
                        ULong(value, "attackSequence"), Int(value, "morale"),
                        new WorldPosition(XmlConvert.ToSingle(Value(value, "retreatX")),
                            XmlConvert.ToSingle(Value(value, "retreatY")), XmlConvert.ToSingle(Value(value, "retreatZ"))),
                        Value(value, "recentInjury"), XmlConvert.ToBoolean(Value(value, "casualtyPenaltyApplied")));
                });
                var attacks = Items(encounter.Element("attacks"), "attack", value =>
                {
                    RequireFields(value, new[] { "attackerId", "defenderId", "sequence", "calendarTick",
                        "injuryId", "applied", "fatal" });
                    return new CombatAttackRecordState(StableEntityId.Parse(Value(value, "attackerId")),
                        StableEntityId.Parse(Value(value, "defenderId")), ULong(value, "sequence"),
                        Long(value, "calendarTick"), new HealthConditionId(Value(value, "injuryId")),
                        XmlConvert.ToBoolean(Value(value, "applied")), XmlConvert.ToBoolean(Value(value, "fatal")));
                });
                var battleHistory = includeBattleState
                    ? Items(encounter.Element("battleHistory"), "event", value =>
                    {
                        RequireFields(value, new[] { "sequence", "calendarTick", "kind", "characterId", "sideId", "mode" });
                        var sideText = Value(value, "sideId");
                        var modeText = Value(value, "mode");
                        return new BattleEventState(ULong(value, "sequence"), Long(value, "calendarTick"),
                            (BattleEventKind)Int(value, "kind"), OptionalStableId(value, "characterId"),
                            string.IsNullOrEmpty(sideText) ? (CombatSideId?)null : new CombatSideId(sideText),
                            string.IsNullOrEmpty(modeText) ? (BattleMode?)null :
                                (BattleMode)XmlConvert.ToInt32(modeText));
                    })
                    : Array.Empty<BattleEventState>();
                return new CombatEncounterState(StableEntityId.Parse(Value(encounter, "id")),
                    new CombatSideId(Value(encounter, "sideA")), new CombatSideId(Value(encounter, "sideB")),
                    Long(encounter, "startTick"), Long(encounter, "lastTick"),
                    (CombatEncounterStatus)Int(encounter, "status"), (CombatOutcome)Int(encounter, "outcome"),
                    combatants, attacks,
                    includeBattleState ? (BattleMode)Int(encounter, "mode") : BattleMode.Physical,
                    includeBattleState ? OptionalMilitaryUnitId(encounter, "sideAUnitId") : null,
                    includeBattleState ? OptionalMilitaryUnitId(encounter, "sideBUnitId") : null,
                    battleHistory);
            });
            return new CombatSaveState(Long(element, "worldSeed"), Long(element, "lastCalendarTick"), encounters);
        }

        private static XElement SerializeMilitary(MilitaryUnitSaveState state)
        {
            var element = new XElement("military");
            if (state == null) return element;
            element.Add(new XElement("units", state.Units.Select(unit => new XElement("unit",
                new XElement("id", unit.Id.ToString()),
                new XElement("ownerSideId", unit.OwnerSideId.ToString()),
                OptionalId("captainId", unit.CaptainId),
                new XElement("status", (int)unit.Status),
                new XElement("representation", (int)unit.Representation),
                new XElement("abstractLocationId", unit.AbstractLocationId),
                new XElement("morale", unit.MoraleSummary),
                new XElement("cohesion", unit.Cohesion),
                new XElement("revision", unit.Revision),
                OptionalId("activeEncounterId", unit.ActiveEncounterId),
                new XElement("order",
                    new XElement("kind", (int)unit.Order.Kind),
                    new XElement("hasTarget", unit.Order.HasTarget),
                    new XElement("targetX", XmlConvert.ToString(unit.Order.Target.X)),
                    new XElement("targetY", XmlConvert.ToString(unit.Order.Target.Y)),
                    new XElement("targetZ", XmlConvert.ToString(unit.Order.Target.Z)),
                    new XElement("targetUnitId", unit.Order.TargetUnitId.HasValue
                        ? unit.Order.TargetUnitId.Value.ToString() : string.Empty),
                    new XElement("sequence", unit.Order.Sequence)),
                new XElement("roster", unit.Roster.Select(member => new XElement("member",
                    new XElement("characterId", member.CharacterId.ToString()),
                    new XElement("role", (int)member.Role),
                    new XElement("status", (int)member.Status),
                    new XElement("joinedSequence", member.JoinedSequence),
                    new XElement("removedSequence", member.RemovedSequence.HasValue
                        ? XmlConvert.ToString(member.RemovedSequence.Value) : string.Empty))))))));
            return element;
        }

        private static MilitaryUnitSaveState DeserializeMilitary(XElement element)
        {
            if (element == null) throw new FormatException("Missing military state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty military state.");
                return null;
            }
            RequireFields(element, new[] { "units" });
            var units = Items(element.Element("units"), "unit", unit =>
            {
                RequireFields(unit, new[] { "id", "ownerSideId", "captainId", "status", "representation",
                    "abstractLocationId", "morale", "cohesion", "revision", "activeEncounterId", "order", "roster" });
                var order = unit.Element("order");
                RequireFields(order, new[] { "kind", "hasTarget", "targetX", "targetY", "targetZ",
                    "targetUnitId", "sequence" });
                var targetUnitText = Value(order, "targetUnitId");
                var orderState = new MilitaryUnitOrderState((MilitaryUnitOrderKind)Int(order, "kind"),
                    new WorldPosition(XmlConvert.ToSingle(Value(order, "targetX")),
                        XmlConvert.ToSingle(Value(order, "targetY")), XmlConvert.ToSingle(Value(order, "targetZ"))),
                    XmlConvert.ToBoolean(Value(order, "hasTarget")),
                    string.IsNullOrEmpty(targetUnitText) ? (MilitaryUnitId?)null : MilitaryUnitId.Parse(targetUnitText),
                    ULong(order, "sequence"));
                var roster = Items(unit.Element("roster"), "member", member =>
                {
                    RequireFields(member, new[] { "characterId", "role", "status", "joinedSequence", "removedSequence" });
                    var removedText = Value(member, "removedSequence");
                    return new MilitaryUnitMemberState(StableEntityId.Parse(Value(member, "characterId")),
                        (MilitaryUnitMemberRole)Int(member, "role"), (MilitaryUnitMemberStatus)Int(member, "status"),
                        ULong(member, "joinedSequence"), string.IsNullOrEmpty(removedText)
                            ? (ulong?)null : XmlConvert.ToUInt64(removedText));
                });
                return new MilitaryUnitState(MilitaryUnitId.Parse(Value(unit, "id")),
                    new MilitarySideId(Value(unit, "ownerSideId")), OptionalStableId(unit, "captainId"), roster,
                    (MilitaryUnitStatus)Int(unit, "status"),
                    (MilitaryUnitRepresentationMode)Int(unit, "representation"),
                    Value(unit, "abstractLocationId"), Int(unit, "morale"), Int(unit, "cohesion"), orderState,
                    OptionalStableId(unit, "activeEncounterId"), ULong(unit, "revision"));
            });
            return new MilitaryUnitSaveState(units);
        }

        private static XElement SerializePlayerControl(PlayerControlSaveState state)
        {
            var element = new XElement("playerControl");
            if (state == null) return element;
            element.Add(new XElement("mode", (int)state.Mode),
                OptionalId("controlledCharacterId", state.ControlledCharacterId));
            return element;
        }

        private static PlayerControlSaveState DeserializePlayerControl(XElement element)
        {
            if (element == null) throw new FormatException("Missing player-control state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value))
                    throw new FormatException("Invalid empty player-control state.");
                return null;
            }
            RequireFields(element, new[] { "mode", "controlledCharacterId" });
            return new PlayerControlSaveState((PlayerControlMode)Int(element, "mode"),
                OptionalStableId(element, "controlledCharacterId"));
        }

        private static XElement SerializeBuildingWorld(BuildingWorldSaveState state)
        {
            var element = new XElement("buildingWorld");
            if (state == null) return element;
            element.Add(new XElement("lastFireTick", state.LastFireTick),
                new XElement("buildings", state.Buildings.Select(value => new XElement("building",
                    new XElement("id", value.Id.ToString()), new XElement("definitionId", value.DefinitionId.ToString()),
                    new XElement("x", value.Coordinate.X), new XElement("z", value.Coordinate.Z),
                    new XElement("level", value.Coordinate.Level), new XElement("orientation", (int)value.Orientation),
                    new XElement("constructionState", (int)value.ConstructionState),
                    new XElement("maxIntegrity", value.MaxIntegrity), new XElement("currentIntegrity", value.CurrentIntegrity),
                    new XElement("structuralStatus", (int)value.StructuralStatus),
                    new XElement("ownerSideId", value.OwnerSideId.ToString()),
                    new XElement("fireStatus", (int)value.FireStatus), new XElement("fireIntensity", value.FireIntensity),
                    new XElement("fireStartTick", value.FireStartTick),
                    new XElement("lastFireProcessedTick", value.LastFireProcessedTick),
                    new XElement("accumulatedFireDamage", value.AccumulatedFireDamage),
                    new XElement("destructionProcessed", value.DestructionProcessed),
                    new XElement("storageSpilled", value.StorageSpilled),
                    new XElement("processedDamageEvents", value.ProcessedDamageEvents.Select(id =>
                        new XElement("eventId", id.ToString()))),
                    new XElement("history", value.History.Select(history => new XElement("event",
                        new XElement("sequence", history.Sequence), new XElement("calendarTick", history.CalendarTick),
                        new XElement("kind", (int)history.Kind), OptionalId("damageEventId", history.DamageEventId),
                        new XElement("damageType", history.DamageType.HasValue ? (int)history.DamageType.Value : 0),
                        new XElement("amount", history.Amount),
                        new XElement("ownerSideId", history.OwnerSideId.HasValue ? history.OwnerSideId.Value.ToString() : string.Empty))))))),
                new XElement("worldPiles", state.WorldPiles.Select(value => new XElement("pile",
                    new XElement("id", value.Id.ToString()), new XElement("resourceId", value.ResourceId.ToString()),
                    new XElement("x", value.Coordinate.X), new XElement("z", value.Coordinate.Z),
                    new XElement("level", value.Coordinate.Level)))));
            return element;
        }

        private static BuildingWorldSaveState DeserializeBuildingWorld(XElement element)
        {
            if (element == null) throw new FormatException("Missing building-world state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty building-world state.");
                return null;
            }
            RequireFields(element, new[] { "lastFireTick", "buildings", "worldPiles" });
            var buildings = Items(element.Element("buildings"), "building", item =>
            {
                RequireFields(item, new[] { "id", "definitionId", "x", "z", "level", "orientation",
                    "constructionState", "maxIntegrity", "currentIntegrity", "structuralStatus", "ownerSideId",
                    "fireStatus", "fireIntensity", "fireStartTick", "lastFireProcessedTick",
                    "accumulatedFireDamage", "destructionProcessed", "storageSpilled",
                    "processedDamageEvents", "history" });
                var processed = Items(item.Element("processedDamageEvents"), "eventId", value =>
                    StableEntityId.Parse(Scalar(value)));
                var history = Items(item.Element("history"), "event", value =>
                {
                    RequireFields(value, new[] { "sequence", "calendarTick", "kind", "damageEventId",
                        "damageType", "amount", "ownerSideId" });
                    var damageType = Int(value, "damageType");
                    var owner = Value(value, "ownerSideId");
                    return new BuildingEventState(ULong(value, "sequence"), Long(value, "calendarTick"),
                        (BuildingEventKind)Int(value, "kind"), OptionalStableId(value, "damageEventId"),
                        damageType == 0 ? (BuildingDamageType?)null : (BuildingDamageType)damageType,
                        Int(value, "amount"), string.IsNullOrEmpty(owner) ? (CombatSideId?)null : new CombatSideId(owner));
                });
                return new BuildingState(StableEntityId.Parse(Value(item, "id")),
                    new BuildingDefinitionId(Value(item, "definitionId")),
                    new GridCoordinate(Int(item, "x"), Int(item, "z"), Int(item, "level")),
                    (BuildingOrientation)Int(item, "orientation"), (ConstructionState)Int(item, "constructionState"),
                    Int(item, "maxIntegrity"), Int(item, "currentIntegrity"),
                    (BuildingStructuralStatus)Int(item, "structuralStatus"),
                    new CombatSideId(Value(item, "ownerSideId")), (BuildingFireStatus)Int(item, "fireStatus"),
                    Int(item, "fireIntensity"), Long(item, "fireStartTick"), Long(item, "lastFireProcessedTick"),
                    Long(item, "accumulatedFireDamage"), XmlConvert.ToBoolean(Value(item, "destructionProcessed")),
                    XmlConvert.ToBoolean(Value(item, "storageSpilled")), processed, history);
            });
            var piles = Items(element.Element("worldPiles"), "pile", item =>
            {
                RequireFields(item, new[] { "id", "resourceId", "x", "z", "level" });
                return new WorldResourcePileState(StableEntityId.Parse(Value(item, "id")),
                    new ResourceId(Value(item, "resourceId")),
                    new GridCoordinate(Int(item, "x"), Int(item, "z"), Int(item, "level")));
            });
            return new BuildingWorldSaveState(buildings, piles, Long(element, "lastFireTick"));
        }

        private static XElement SerializeGlobalMap(GlobalMapSaveState state)
        {
            var element = new XElement("globalMap");
            if (state == null) return element;
            element.Add(new XElement("graphId", state.GraphId), new XElement("mode", (int)state.Mode),
                new XElement("activeLocationId", state.ActiveLocationId.ToString()),
                new XElement("lastProcessedTick", state.LastProcessedTick),
                new XElement("locations", state.Locations.Select(value => new XElement("location",
                    new XElement("id", value.Id.ToString()), new XElement("name", value.Name),
                    new XElement("type", (int)value.Type), new XElement("mapX", value.MapX),
                    new XElement("mapY", value.MapY)))),
                new XElement("routes", state.Routes.Select(value => new XElement("route",
                    new XElement("from", value.From.ToString()), new XElement("to", value.To.ToString()),
                    new XElement("durationTicks", value.TravelDurationTicks)))),
                new XElement("settlements", state.Settlements.Select(value => new XElement("settlement",
                    new XElement("id", value.Id.ToString()), new XElement("locationId", value.LocationId.ToString()),
                    new XElement("ownerSideId", value.OwnerSideId.ToString()),
                    new XElement("buildingIds", value.BuildingIds.Select(id => new XElement("id", id.ToString()))),
                    new XElement("residentIds", value.ResidentCharacterIds.Select(id => new XElement("id", id.ToString())))))),
                new XElement("travels", state.Travels.Select(value => new XElement("travel",
                    new XElement("unitId", value.UnitId.ToString()), new XElement("origin", value.Origin.ToString()),
                    new XElement("destination", value.Destination.ToString()),
                    new XElement("departureTick", value.DepartureTick),
                    new XElement("arrivalTick", value.ArrivalTick)))));
            return element;
        }

        private static GlobalMapSaveState DeserializeGlobalMap(XElement element)
        {
            if (element == null) throw new FormatException("Missing global-map state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty global-map state.");
                return null;
            }
            RequireFields(element, new[] { "graphId", "mode", "activeLocationId", "lastProcessedTick",
                "locations", "routes", "settlements", "travels" });
            var locations = Items(element.Element("locations"), "location", item =>
            {
                RequireFields(item, new[] { "id", "name", "type", "mapX", "mapY" });
                return new WorldLocationState(new WorldLocationId(Value(item, "id")), Value(item, "name"),
                    (WorldLocationType)Int(item, "type"), Int(item, "mapX"), Int(item, "mapY"));
            });
            var routes = Items(element.Element("routes"), "route", item =>
            {
                RequireFields(item, new[] { "from", "to", "durationTicks" });
                return new WorldRouteState(new WorldLocationId(Value(item, "from")),
                    new WorldLocationId(Value(item, "to")), Long(item, "durationTicks"));
            });
            var settlements = Items(element.Element("settlements"), "settlement", item =>
            {
                RequireFields(item, new[] { "id", "locationId", "ownerSideId", "buildingIds", "residentIds" });
                var buildings = Items(item.Element("buildingIds"), "id", id => StableEntityId.Parse(Scalar(id)));
                var residents = Items(item.Element("residentIds"), "id", id => StableEntityId.Parse(Scalar(id)));
                return new SettlementState(new SettlementId(StableEntityId.Parse(Value(item, "id"))),
                    new WorldLocationId(Value(item, "locationId")), new CombatSideId(Value(item, "ownerSideId")),
                    buildings, residents);
            });
            var travels = Items(element.Element("travels"), "travel", item =>
            {
                RequireFields(item, new[] { "unitId", "origin", "destination", "departureTick", "arrivalTick" });
                return new WorldTravelState(MilitaryUnitId.Parse(Value(item, "unitId")),
                    new WorldLocationId(Value(item, "origin")), new WorldLocationId(Value(item, "destination")),
                    Long(item, "departureTick"), Long(item, "arrivalTick"));
            });
            return new GlobalMapSaveState(Value(element, "graphId"), (GameWorldMode)Int(element, "mode"),
                new WorldLocationId(Value(element, "activeLocationId")), locations, routes, settlements, travels,
                Long(element, "lastProcessedTick"));
        }

        private static XElement SerializeStrategicAi(AiStrategicSaveState state)
        {
            var element = new XElement("strategicAi");
            if (state == null) return element;
            element.Add(new XElement("worldSeed", state.WorldSeed),
                new XElement("lastProcessedTick", state.LastProcessedTick),
                new XElement("factions", state.Factions.Select(faction =>
                {
                    var strategic = faction.Strategic;
                    return new XElement("faction",
                        new XElement("strategic",
                            new XElement("id", strategic.Id.ToString()),
                            new XElement("ownerId", strategic.OwnerId),
                            new XElement("sideId", strategic.SideId.Value),
                            new XElement("homeSettlementId", strategic.HomeSettlementId.ToString()),
                            new XElement("rulerCharacterId", strategic.RulerCharacterId.ToString()),
                            new XElement("controlledSettlements", strategic.ControlledSettlements.Select(x => new XElement("settlement", x.ToString()))),
                            new XElement("knownMilitaryUnits", strategic.KnownMilitaryUnits.Select(x => new XElement("unit", x.ToString()))),
                            new XElement("goal", (int)strategic.Goal),
                            new XElement("targetSettlementId", strategic.TargetSettlementId.HasValue ? strategic.TargetSettlementId.Value.ToString() : string.Empty),
                            new XElement("targetLocationId", strategic.TargetLocationId.HasValue ? strategic.TargetLocationId.Value.ToString() : string.Empty),
                            new XElement("lastDecisionTick", strategic.LastDecisionTick),
                            new XElement("nextDecisionTick", strategic.NextDecisionTick),
                            new XElement("decisionSequence", strategic.DecisionSequence),
                            new XElement("defeated", strategic.IsDefeated),
                            new XElement("defeatReason", (int)strategic.DefeatReason),
                            new XElement("orders", strategic.PendingOrders.Select(x => new XElement("order",
                                new XElement("sequence", x.Sequence), new XElement("kind", (int)x.Kind),
                                new XElement("issuedTick", x.IssuedTick),
                                new XElement("unitId", x.UnitId.HasValue ? x.UnitId.Value.ToString() : string.Empty),
                                new XElement("settlementId", x.TargetSettlementId.HasValue ? x.TargetSettlementId.Value.ToString() : string.Empty),
                                new XElement("locationId", x.TargetLocationId.HasValue ? x.TargetLocationId.Value.ToString() : string.Empty)))),
                            new XElement("decisions", strategic.RecentDecisions.Select(x => new XElement("decision",
                                new XElement("sequence", x.Sequence), new XElement("tick", x.Tick),
                                new XElement("kind", (int)x.Kind), new XElement("goal", (int)x.Goal),
                                new XElement("score", x.Score), new XElement("reason", x.Reason),
                                new XElement("settlementId", x.TargetSettlementId.HasValue ? x.TargetSettlementId.Value.ToString() : string.Empty),
                                new XElement("locationId", x.TargetLocationId.HasValue ? x.TargetLocationId.Value.ToString() : string.Empty),
                                new XElement("unitId", x.UnitId.HasValue ? x.UnitId.Value.ToString() : string.Empty))))),
                        SerializeAiDynasty(faction.Dynasty), SerializeResearch(faction.Research),
                        SerializeProduction(faction.Production),
                        new XElement("worldPiles", faction.WorldPiles.Select(value => new XElement("pile",
                            new XElement("id", value.Id.ToString()),
                            new XElement("resourceId", value.ResourceId.ToString()),
                            new XElement("x", value.Coordinate.X), new XElement("z", value.Coordinate.Z),
                            new XElement("level", value.Coordinate.Level)))));
                })));
            return element;
        }

        private static AiStrategicSaveState DeserializeStrategicAi(XElement element)
        {
            if (element == null) throw new FormatException("Missing strategic AI state.");
            if (!element.HasElements)
            {
                if (!string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("Invalid empty strategic AI state.");
                return null;
            }
            RequireFields(element, new[] { "worldSeed", "lastProcessedTick", "factions" });
            var factions = Items(element.Element("factions"), "faction", item =>
            {
                RequireFields(item, new[] { "strategic", "dynasty", "research", "production", "worldPiles" });
                var value = item.Element("strategic");
                RequireFields(value, new[] { "id", "ownerId", "sideId", "homeSettlementId", "rulerCharacterId",
                    "controlledSettlements", "knownMilitaryUnits", "goal", "targetSettlementId", "targetLocationId",
                    "lastDecisionTick", "nextDecisionTick", "decisionSequence", "defeated", "defeatReason", "orders", "decisions" });
                var settlements = Items(value.Element("controlledSettlements"), "settlement",
                    x => new SettlementId(StableEntityId.Parse(Scalar(x))));
                var units = Items(value.Element("knownMilitaryUnits"), "unit", x => MilitaryUnitId.Parse(Scalar(x)));
                var orders = Items(value.Element("orders"), "order", x =>
                {
                    RequireFields(x, new[] { "sequence", "kind", "issuedTick", "unitId", "settlementId", "locationId" });
                    return new AiPendingOrder(Long(x, "sequence"), (AiDecisionKind)Int(x, "kind"), Long(x, "issuedTick"),
                        OptionalUnit(x, "unitId"), OptionalSettlement(x, "settlementId"), OptionalLocation(x, "locationId"));
                });
                var decisions = Items(value.Element("decisions"), "decision", x =>
                {
                    RequireFields(x, new[] { "sequence", "tick", "kind", "goal", "score", "reason", "settlementId", "locationId", "unitId" });
                    return new AiDecision(Long(x, "sequence"), Long(x, "tick"), (AiDecisionKind)Int(x, "kind"),
                        (AiStrategicGoal)Int(x, "goal"), Int(x, "score"), Value(x, "reason"),
                        OptionalSettlement(x, "settlementId"), OptionalLocation(x, "locationId"), OptionalUnit(x, "unitId"));
                });
                var strategic = new AiStrategicStateSnapshot(new AiStateId(StableEntityId.Parse(Value(value, "id"))),
                    Value(value, "ownerId"), new CombatSideId(Value(value, "sideId")),
                    new SettlementId(StableEntityId.Parse(Value(value, "homeSettlementId"))),
                    StableEntityId.Parse(Value(value, "rulerCharacterId")), settlements, units,
                    (AiStrategicGoal)Int(value, "goal"), OptionalSettlement(value, "targetSettlementId"),
                    OptionalLocation(value, "targetLocationId"), Long(value, "lastDecisionTick"),
                    Long(value, "nextDecisionTick"), Long(value, "decisionSequence"), orders, decisions,
                    XmlConvert.ToBoolean(Value(value, "defeated")), (AiDefeatReason)Int(value, "defeatReason"));
                var piles = Items(item.Element("worldPiles"), "pile", pile =>
                {
                    RequireFields(pile, new[] { "id", "resourceId", "x", "z", "level" });
                    return new WorldResourcePileState(StableEntityId.Parse(Value(pile, "id")),
                        new ResourceId(Value(pile, "resourceId")),
                        new GridCoordinate(Int(pile, "x"), Int(pile, "z"), Int(pile, "level")));
                });
                return new AiFactionSaveState(strategic, DeserializeAiDynasty(item.Element("dynasty")),
                    DeserializeResearch(item.Element("research")), DeserializeProduction(item.Element("production")), piles);
            });
            return new AiStrategicSaveState(Long(element, "worldSeed"), Long(element, "lastProcessedTick"), factions);
        }

        private static XElement SerializeAiDynasty(DynastyStateSnapshot dynasty) => new XElement("dynasty",
            new XElement("id", dynasty.Id.ToString()), new XElement("name", dynasty.Name),
            new XElement("founderId", dynasty.FounderId.ToString()), OptionalId("currentMonarchId", dynasty.CurrentMonarchId),
            new XElement("successionRuleId", dynasty.SuccessionRuleId), new XElement("status", (int)dynasty.Status),
            new XElement("members", dynasty.MemberIds.Select(id => new XElement("member", id.ToString()))),
            new XElement("processedDeaths", dynasty.ProcessedMonarchDeathIds.Select(id => new XElement("character", id.ToString()))),
            new XElement("history", dynasty.History.Select(entry => new XElement("entry",
                new XElement("sequence", entry.SequenceNumber), new XElement("previousMonarchId", entry.PreviousMonarchId.ToString()),
                OptionalId("newMonarchId", entry.NewMonarchId), new XElement("calendarTick", entry.CalendarTick),
                new XElement("reason", (int)entry.Reason)))));

        private static DynastyStateSnapshot DeserializeAiDynasty(XElement element)
        {
            RequireFields(element, new[] { "id", "name", "founderId", "currentMonarchId", "successionRuleId",
                "status", "members", "processedDeaths", "history" });
            var members = Items(element.Element("members"), "member", x => StableEntityId.Parse(Scalar(x)));
            var processed = Items(element.Element("processedDeaths"), "character", x => StableEntityId.Parse(Scalar(x)));
            var history = Items(element.Element("history"), "entry", x =>
            {
                RequireFields(x, new[] { "sequence", "previousMonarchId", "newMonarchId", "calendarTick", "reason" });
                return new SuccessionHistoryEntry(Long(x, "sequence"), StableEntityId.Parse(Value(x, "previousMonarchId")),
                    OptionalStableId(x, "newMonarchId"), Long(x, "calendarTick"), (SuccessionReason)Int(x, "reason"));
            });
            return new DynastyStateSnapshot(StableEntityId.Parse(Value(element, "id")), Value(element, "name"),
                StableEntityId.Parse(Value(element, "founderId")), OptionalStableId(element, "currentMonarchId"),
                Value(element, "successionRuleId"), (DynastyStatus)Int(element, "status"), members, processed, history);
        }

        private static MilitaryUnitId? OptionalUnit(XElement parent, string name)
        { var value = Value(parent, name); return string.IsNullOrEmpty(value) ? (MilitaryUnitId?)null : MilitaryUnitId.Parse(value); }
        private static SettlementId? OptionalSettlement(XElement parent, string name)
        { var value = Value(parent, name); return string.IsNullOrEmpty(value) ? (SettlementId?)null : new SettlementId(StableEntityId.Parse(value)); }
        private static WorldLocationId? OptionalLocation(XElement parent, string name)
        { var value = Value(parent, name); return string.IsNullOrEmpty(value) ? (WorldLocationId?)null : new WorldLocationId(value); }

        private static string[] FieldsForVersion(int version)
        {
            var fields = TimeFields.AsEnumerable();
            if (version >= SaveSnapshot.ProductionVersion) fields = fields.Concat(new[] { "production" });
            if (version >= SaveSnapshot.ResearchVersion) fields = fields.Concat(new[] { "research" });
            if (version >= SaveSnapshot.DynastyVersion) fields = fields.Concat(new[] { "dynastyWorld" });
            if (version >= SaveSnapshot.HealthVersion) fields = fields.Concat(new[] { "healthFamily" });
            if (version >= SaveSnapshot.WeatherVersion) fields = fields.Concat(new[] { "weather" });
            if (version >= SaveSnapshot.CombatVersion) fields = fields.Concat(new[] { "combat" });
            if (version >= SaveSnapshot.MilitaryVersion) fields = fields.Concat(new[] { "military" });
            if (version >= SaveSnapshot.PlayerControlVersion) fields = fields.Concat(new[] { "playerControl" });
            if (version >= SaveSnapshot.BuildingVersion) fields = fields.Concat(new[] { "buildingWorld" });
            if (version >= SaveSnapshot.GlobalMapVersion) fields = fields.Concat(new[] { "globalMap" });
            if (version >= SaveSnapshot.StrategicAiVersion) fields = fields.Concat(new[] { "strategicAi" });
            return fields.ToArray();
        }

        private static XElement OptionalId(string name, StableEntityId? value) =>
            new XElement(name, value.HasValue ? value.Value.ToString() : string.Empty);

        private static StableEntityId? OptionalStableId(XElement parent, XName name)
        {
            var text = Value(parent, name);
            return string.IsNullOrEmpty(text) ? (StableEntityId?)null : StableEntityId.Parse(text);
        }

        private static MilitaryUnitId? OptionalMilitaryUnitId(XElement parent, XName name)
        {
            var text = Value(parent, name);
            return string.IsNullOrEmpty(text) ? (MilitaryUnitId?)null : MilitaryUnitId.Parse(text);
        }

        private static T[] Items<T>(XElement container, XName itemName, Func<XElement, T> parser)
        {
            if (container == null || container.HasAttributes ||
                container.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)) ||
                container.Elements().Any(child => child.Name != itemName))
                throw new FormatException("Invalid save collection.");
            return container.Elements(itemName).Select(parser).ToArray();
        }

        private static void RequireFields(XElement element, string[] fields)
        {
            if (element == null || element.HasAttributes ||
                element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
                throw new FormatException("Invalid save element.");
            var children = element.Elements().ToArray();
            if (children.Length != fields.Length || fields.Any(field => children.Count(child => child.Name == field) != 1) ||
                children.Any(child => child.HasAttributes))
                throw new FormatException("Invalid save fields.");
        }

        private static string Value(XElement parent, XName name)
        {
            var element = parent.Element(name);
            if (element == null || element.HasElements || element.HasAttributes) throw new FormatException("Invalid scalar field.");
            return element.Value;
        }

        private static string Scalar(XElement element)
        {
            if (element == null || element.HasElements || element.HasAttributes)
                throw new FormatException("Invalid scalar field.");
            return element.Value;
        }

        private static long Long(XElement parent, XName name) => XmlConvert.ToInt64(Value(parent, name));
        private static int Int(XElement parent, XName name) => XmlConvert.ToInt32(Value(parent, name));
        private static ulong ULong(XElement parent, XName name) => XmlConvert.ToUInt64(Value(parent, name));
    }
}
