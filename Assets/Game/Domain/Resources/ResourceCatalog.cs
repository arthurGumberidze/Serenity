using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Resources
{
    public sealed class ResourceDefinition
    {
        public ResourceDefinition(ResourceId id, string displayName, ResourceCategory category)
        {
            if (!id.IsValid) throw new ArgumentException("Valid resource ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(ResourceCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            Id = id;
            DisplayName = displayName;
            Category = category;
        }

        public ResourceId Id { get; }
        public string DisplayName { get; }
        public ResourceCategory Category { get; }
    }

    public sealed class ResourceCatalog
    {
        private readonly Dictionary<ResourceId, ResourceDefinition> definitions = new Dictionary<ResourceId, ResourceDefinition>();
        private readonly IReadOnlyList<ResourceDefinition> orderedDefinitions;

        public ResourceCatalog(IEnumerable<ResourceDefinition> definitions)
        {
            foreach (var definition in definitions ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null) throw new ArgumentException("Null resource definition.", nameof(definitions));
                if (!this.definitions.TryAdd(definition.Id, definition))
                    throw new ArgumentException("Duplicate resource ID: " + definition.Id, nameof(definitions));
            }
            orderedDefinitions = Array.AsReadOnly(this.definitions.Values.OrderBy(x => x.Id).ToArray());
        }

        public IReadOnlyList<ResourceDefinition> All => orderedDefinitions;

        public ResourceDefinition Get(ResourceId id)
        {
            if (!id.IsValid || !definitions.TryGetValue(id, out var definition))
                throw new KeyNotFoundException("Unknown resource ID: " + id);
            return definition;
        }

        public bool Contains(ResourceId id) => id.IsValid && definitions.ContainsKey(id);
    }
}
