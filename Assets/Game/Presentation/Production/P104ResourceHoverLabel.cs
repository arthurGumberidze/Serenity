using Game.Domain.Resources;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Presentation.Resources;
using Game.Simulation.Production;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Production
{
    public sealed class P104ResourceHoverLabel : MonoBehaviour
    {
        private SelectionProbe selection;
        private ProductionService production;
        private GUIStyle style;

        public void Initialize(SelectionProbe probe, ProductionService service)
        { selection = probe; production = service; }

        private void OnGUI()
        {
            var proxy = selection?.HoveredProxy;
            if (proxy == null || !proxy.IsBound || production == null) return;
            string label = null;
            var source = proxy.GetComponent<ProductionSourcePresenter>();
            if (source != null && source.IsBound)
            {
                var recipe = production.Recipes.Get(source.Site.RecipeId);
                label = string.Join(" + ", System.Linq.Enumerable.Select(recipe.Outputs,
                    x => RussianTextCatalog.Content("resource", x.ResourceId.ToString()))) +
                    $"  {source.RemainingAmount}/{source.SourceCapacity}";
            }
            else
            {
                var pile = proxy.GetComponent<WorldResourcePilePresenter>();
                if (pile != null)
                    label = RussianTextCatalog.Content("resource", pile.ResourceId.ToString()) +
                        $"  {pile.Quantity.Units}";
            }
            if (string.IsNullOrEmpty(label)) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, fontSize = 14,
                normal = { textColor = Color.white } };
            var pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            var gui = WorldPointerRaycaster.ScreenToGuiPoint(pointer);
            GUI.Box(new Rect(gui.x + 14f, gui.y + 14f, 230f, 30f), label, style);
        }
    }
}
