using Game.Domain.Resources;
using System.Linq;
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
            DrawWorkProgress();
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

        private void DrawWorkProgress()
        {
            if (production == null || Camera.main == null) return;
            foreach (var task in production.ActiveTasks)
            {
                if (!production.TryGetSite(task.SiteId, out var site)) continue;
                var point = Camera.main.WorldToScreenPoint(new Vector3(site.Position.X, site.Position.Y + 4f, site.Position.Z));
                if (point.z <= 0f || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                var progress = Mathf.Clamp01((float)task.AccumulatedWorkTicks / task.RequiredWorkTicks);
                var rect = new Rect(point.x - 85f, Screen.height - point.y - 18f, 170f, 25f);
                var recipe = production.Recipes.Get(site.RecipeId);
                var output = recipe.OutputsFor(site).First();
                GUI.Box(rect, RussianTextCatalog.Content("recipe", recipe.Id.ToString()) + "  " +
                    Mathf.FloorToInt(progress * 100f) + "%  → " + output.Quantity.Units);
                var old = GUI.color;
                GUI.color = new Color(0.3f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax, rect.width * progress, 4f), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }
    }
}
