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
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>Strict versioned UTF-8 XML checkpoint codec. V5 adds health, family events and pregnancy.</summary>
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
            if (snapshot.FormatVersion >= SaveSnapshot.CurrentVersion)
                root.Add(SerializeHealthFamily(snapshot.HealthFamily));
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
                        version != SaveSnapshot.CurrentVersion)
                        throw new NotSupportedException("Unsupported save format version: " + version);
                    RequireFields(root, version == SaveSnapshot.LegacyVersion ? TimeFields :
                        version == SaveSnapshot.ProductionVersion ? TimeFields.Concat(new[] { "production" }).ToArray() :
                        version == SaveSnapshot.ResearchVersion ? TimeFields.Concat(new[] { "production", "research" }).ToArray() :
                        version == SaveSnapshot.DynastyVersion ? TimeFields.Concat(new[] { "production", "research", "dynastyWorld" }).ToArray() :
                        TimeFields.Concat(new[] { "production", "research", "dynastyWorld", "healthFamily" }).ToArray());
                    var production = version >= SaveSnapshot.ProductionVersion
                        ? DeserializeProduction(root.Element("production")) : null;
                    var research = version >= SaveSnapshot.ResearchVersion
                        ? DeserializeResearch(root.Element("research")) : null;
                    var dynastyWorld = version >= SaveSnapshot.DynastyVersion
                        ? DeserializeDynastyWorld(root.Element("dynastyWorld")) : null;
                    var healthFamily = version >= SaveSnapshot.CurrentVersion
                        ? DeserializeHealthFamily(root.Element("healthFamily")) : null;
                    var snapshot = new SaveSnapshot(version, StableEntityId.Parse(Value(root, "sessionId")),
                        new GameTimeState(Long(root, "calendarTicks"), Long(root, "biologicalTicks"),
                            Int(root, "speed"), XmlConvert.ToBoolean(Value(root, "paused"))),
                        Int(root, "biologicalMultiplier"), production, research, dynastyWorld, healthFamily);
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

        private static XElement OptionalId(string name, StableEntityId? value) =>
            new XElement(name, value.HasValue ? value.Value.ToString() : string.Empty);

        private static StableEntityId? OptionalStableId(XElement parent, XName name)
        {
            var text = Value(parent, name);
            return string.IsNullOrEmpty(text) ? (StableEntityId?)null : StableEntityId.Parse(text);
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
    }
}
