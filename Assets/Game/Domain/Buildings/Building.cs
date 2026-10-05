using System;

namespace Game.Domain.Buildings
{
    public readonly struct BuildingState
    {
        public BuildingState(StableEntityId id, BuildingDefinitionId definitionId, GridCoordinate coordinate,
            BuildingOrientation orientation, ConstructionState constructionState)
        {
            if (!id.IsValid) throw new ArgumentException("Building identity is required.", nameof(id));
            if (!definitionId.IsValid) throw new ArgumentException("Definition identity is required.", nameof(definitionId));
            if (!Enum.IsDefined(typeof(BuildingOrientation), orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
            if (!Enum.IsDefined(typeof(ConstructionState), constructionState)) throw new ArgumentOutOfRangeException(nameof(constructionState));
            Id = id;
            DefinitionId = definitionId;
            Coordinate = coordinate;
            Orientation = orientation;
            ConstructionState = constructionState;
        }

        public StableEntityId Id { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public GridCoordinate Coordinate { get; }
        public BuildingOrientation Orientation { get; }
        public ConstructionState ConstructionState { get; }
    }

    public sealed class Building
    {
        private Building(BuildingState state)
        {
            Id = state.Id;
            DefinitionId = state.DefinitionId;
            Coordinate = state.Coordinate;
            Orientation = state.Orientation;
            ConstructionState = state.ConstructionState;
        }

        public StableEntityId Id { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public GridCoordinate Coordinate { get; }
        public BuildingOrientation Orientation { get; }
        public ConstructionState ConstructionState { get; private set; }

        public static Building CreateNew(BuildingDefinitionId definitionId, GridCoordinate coordinate,
            BuildingOrientation orientation) => Restore(new BuildingState(StableEntityId.NewId(), definitionId,
            coordinate, orientation, ConstructionState.Planned));

        public static Building Restore(BuildingState state) => new Building(state);

        public void MarkCompleted()
        {
            if (ConstructionState == ConstructionState.Completed) return;
            ConstructionState = ConstructionState.Completed;
        }

        public BuildingState CaptureState() => new BuildingState(Id, DefinitionId, Coordinate, Orientation, ConstructionState);
    }
}
