namespace Game.Presentation.Input
{
    public enum LocalInteractionModeKind
    {
        Selection = 0,
        BuildingPlacement = 1
    }

    public sealed class LocalInteractionMode
    {
        public LocalInteractionModeKind Current { get; private set; } = LocalInteractionModeKind.Selection;
        public bool IsBuildingPlacement => Current == LocalInteractionModeKind.BuildingPlacement;

        public void EnterBuildingPlacement() => Current = LocalInteractionModeKind.BuildingPlacement;
        public void ExitBuildingPlacement() => Current = LocalInteractionModeKind.Selection;
    }
}
