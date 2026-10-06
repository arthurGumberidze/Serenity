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
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>Strict versioned UTF-8 XML checkpoint codec. Version 2 adds U15 production state.</summary>
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
            if (snapshot.FormatVersion >= SaveSnapshot.CurrentVersion)
                root.Add(SerializeProduction(snapshot.Production));
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
                    if (version != SaveSnapshot.LegacyVersion && version != SaveSnapshot.CurrentVersion)
                        throw new NotSupportedException("Unsupported save format version: " + version);
                    RequireFields(root, version == SaveSnapshot.LegacyVersion
                        ? TimeFields : TimeFields.Concat(new[] { "production" }).ToArray());
                    var production = version == SaveSnapshot.CurrentVersion
                        ? DeserializeProduction(root.Element("production")) : null;
                    var snapshot = new SaveSnapshot(version, StableEntityId.Parse(Value(root, "sessionId")),
                        new GameTimeState(Long(root, "calendarTicks"), Long(root, "biologicalTicks"),
                            Int(root, "speed"), XmlConvert.ToBoolean(Value(root, "paused"))),
                        Int(root, "biologicalMultiplier"), production);
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

        private static long Long(XElement parent, XName name) => XmlConvert.ToInt64(Value(parent, name));
        private static int Int(XElement parent, XName name) => XmlConvert.ToInt32(Value(parent, name));
    }
}
