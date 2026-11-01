using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Research;
using Game.Simulation.Research;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    public sealed partial class GameplayUiController
    {
        private const float ResearchWidth=1340, ResearchHeight=810;
        private VisualElement researchViewport, researchWorld;
        private ScrollView researchDetails;
        private Label researchStatus, researchZoomLabel;
        private ResearchLinks researchLinks;
        private readonly Dictionary<TechnologyId,Button> researchNodes=new Dictionary<TechnologyId,Button>();
        private string researchFilter="", researchSearch="", researchDetailKey="";
        private float researchZoom=1;
        private Vector2 researchPan;
        private bool researchFit=true, researchDragging;
        private Vector2 researchDragStart, researchPanStart;

        private static string BranchName(string branch) => branch switch {
            "survival"=>"Выживание", "production"=>"Производство", "military"=>"Военное дело",
            "knowledge"=>"Знания и медицина", "society"=>"Общество", _=>"Общие знания" };
        private static Vector2 ResearchPosition(TechnologyDefinition d)
        {
            var p=d.Profile;
            if(p==null||p.Branch=="root")return new Vector2(649,6);
            var index=StoneAgeResearchPlan.Branches.ToList().IndexOf(p.Branch);
            var center=132+index*260;
            return p.IsHub?new Vector2(center-44,94):new Vector2(center+p.Offset-21,232+(p.Depth-1)*100);
        }
        private static float ResearchDiameter(TechnologyDefinition d)=>d.Profile?.IsHub==true?88:42;
        private static string ResearchIcon(TechnologyDefinition d)=>d.Profile?.Icon switch {
            "fire" or "pot"=>"food", "leaf" or "grain" or "herb"=>"gather", "tools" or "ore"=>"stone",
            "fish"=>"fish", "wood"=>"wood_log", "house"=>"housing", "storage"=>"storage",
            "shield" or "bow"=>"military", "tower"=>"defense", "crown"=>"dynasty",
            "people"=>"population", "road"=>"world", "cloth" or "fiber"=>"production", _=>"research" };
        private void DrawResearchPanel()
        {
            researchNodes.Clear();researchDetailKey="";
            AddTitle(mainPanel,"Дерево знаний · Каменный век");
            var toolbar=Row();
            toolbar.Add(ActionButton("Все направления",()=>{researchFilter="";UpdateResearchView();},"research-category-all"));
            foreach(var branch in StoneAgeResearchPlan.Branches)
            { var captured=branch; toolbar.Add(ActionButton(BranchName(branch),()=>{researchFilter=captured;UpdateResearchView();},"research-category-"+branch)); }
            var search=new TextField {name="research-search",value=researchSearch};
            StyleTextField(search);search.textEdition.placeholder="Поиск";
            search.tooltip="Поиск по названию";search.style.width=160;
            search.RegisterValueChangedCallback(e=>{researchSearch=e.newValue;UpdateResearchView();});toolbar.Add(search);
            toolbar.Add(ActionButton("Всё дерево",()=>{researchFit=true;FitResearch();},"research-fit"));
            toolbar.Add(ActionButton("К выбранному",CenterResearch,"research-center"));
            toolbar.Add(ActionButton("−",()=>ZoomResearch(researchZoom/1.2f,researchViewport.contentRect.center),"research-zoom-out"));
            researchZoomLabel=new Label();toolbar.Add(researchZoomLabel);
            toolbar.Add(ActionButton("+",()=>ZoomResearch(researchZoom*1.2f,researchViewport.contentRect.center),"research-zoom-in"));
            mainPanel.Add(toolbar);
            researchStatus=new Label {name="research-worker-status"};mainPanel.Add(researchStatus);
            var workspace=Row();workspace.name="research-tree-workspace";
            workspace.style.height=Mathf.Max(300,root.resolvedStyle.height-390);workspace.style.flexShrink=0;
            workspace.style.width=new Length(100,LengthUnit.Percent);workspace.style.flexWrap=Wrap.NoWrap;
            researchViewport=new VisualElement {name="research-viewport"};
            researchViewport.style.flexGrow=1;researchViewport.style.minWidth=0;researchViewport.style.height=new Length(100,LengthUnit.Percent);
            researchViewport.style.overflow=Overflow.Hidden;researchViewport.style.backgroundColor=new Color(.07f,.105f,.15f);
            researchWorld=new VisualElement {name="research-dependency-tree"};
            researchWorld.style.width=ResearchWidth;researchWorld.style.height=ResearchHeight;researchWorld.style.position=Position.Absolute;
            researchWorld.style.transformOrigin=new TransformOrigin(0,0,0);
            researchWorld.pickingMode=PickingMode.Ignore;
            researchViewport.Add(researchWorld);workspace.Add(researchViewport);
            researchDetails=new ScrollView {name="research-detail-host"};researchDetails.style.width=310;
            researchDetails.style.flexShrink=0;researchDetails.style.height=new Length(100,LengthUnit.Percent);
            researchDetails.style.backgroundColor=new Color(.095f,.13f,.17f);researchDetails.style.paddingLeft=18;researchDetails.style.paddingRight=18;
            workspace.Add(researchDetails);mainPanel.Add(workspace);
            researchLinks=new ResearchLinks();researchWorld.Add(researchLinks);
            foreach(var d in context.Research.Catalog.All.Where(x=>x.Profile?.Branch!="epoch"))
            {
                var technology=d;var p=ResearchPosition(d);var size=ResearchDiameter(d);
                var button=new Button(()=>{selectedResearchTechnology=technology.Id;researchDetailKey="";UpdateResearchView();})
                    {name="research-node-"+d.Id,tooltip=d.DisplayName,focusable=true};
                button.AddToClassList("serenity-research-tree-node");
                button.style.position=Position.Absolute;button.style.left=p.x;button.style.top=p.y;
                button.style.width=size;button.style.height=size;button.style.minHeight=0;
                button.style.marginLeft=0;button.style.marginRight=0;button.style.marginTop=0;button.style.marginBottom=0;
                button.style.paddingLeft=6;button.style.paddingRight=6;button.style.paddingTop=6;button.style.paddingBottom=6;
                button.style.borderTopLeftRadius=size;button.style.borderTopRightRadius=size;button.style.borderBottomLeftRadius=size;button.style.borderBottomRightRadius=size;
                button.style.borderTopWidth=2;button.style.borderBottomWidth=2;button.style.borderLeftWidth=2;button.style.borderRightWidth=2;
                button.style.alignItems=Align.Center;button.style.justifyContent=Justify.Center;
                var icon=SerenityIconLibrary.Image(ResearchIcon(d),d.DisplayName);icon.style.width=d.Profile?.IsHub==true?50:26;icon.style.height=icon.style.width;
                button.Add(icon);
                var label=new Label(d.Profile?.IsHub==true?BranchName(d.Profile.Branch):d.DisplayName){name="research-node-caption"};
                label.pickingMode=PickingMode.Ignore;label.style.position=Position.Absolute;label.style.top=size+8;
                label.style.left=d.Profile?.IsHub==true?-80:-65;label.style.width=d.Profile?.IsHub==true?248:172;
                label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleCenter;
                label.style.fontSize=d.Profile?.IsHub==true?16:12;label.style.color=new Color(.9f,.85f,.72f);
                label.style.display=d.Profile?.IsHub==true?DisplayStyle.Flex:DisplayStyle.None;
                button.Add(label);
                void ShowCaption(bool show){button.EnableInClassList("research-node-hover",show);if(d.Profile?.IsHub==true)return;label.style.display=show||selectedResearchTechnology==technology.Id?DisplayStyle.Flex:DisplayStyle.None;if(show)button.BringToFront();}
                button.RegisterCallback<PointerEnterEvent>(_=>ShowCaption(true));button.RegisterCallback<PointerLeaveEvent>(_=>ShowCaption(false));
                button.RegisterCallback<FocusInEvent>(_=>ShowCaption(true));button.RegisterCallback<FocusOutEvent>(_=>ShowCaption(false));
                if(d.Profile?.IsKey==true){var star=new Label("✦");star.style.position=Position.Absolute;star.style.right=-8;star.style.top=-10;star.style.color=AccentColor;star.pickingMode=PickingMode.Ignore;button.Add(star);}
                researchWorld.Add(button);researchNodes.Add(d.Id,button);
            }
            var epoch=ActionButton("Освоение каменного века · условия завершения →",()=>{
                selectedResearchTechnology=new TechnologyId("stone_age_mastery");researchDetailKey="";UpdateResearchView();},"research-node-stone_age_mastery");
            epoch.style.position=Position.Absolute;epoch.style.left=170;epoch.style.top=741;epoch.style.width=1000;epoch.style.height=59;
            researchWorld.Add(epoch);
            var legend=Row();legend.name="research-state-legend";
            foreach(var state in new[]{"Изучено","Доступно","Исследуется","Закрыто","Нужна практика","Будущая механика"})
            {
                var marker=new Label("● "+state);marker.style.fontSize=12;marker.style.marginRight=16;
                marker.style.color=state=="Изучено"?new Color(.58f,.7f,.44f):state=="Доступно"?AccentColor:
                    state=="Исследуется"?new Color(.48f,.77f,.81f):state=="Нужна практика"?new Color(.73f,.5f,.28f):
                    state=="Будущая механика"?new Color(.6f,.52f,.72f):new Color(.5f,.55f,.6f);
                legend.Add(marker);
            }
            mainPanel.Add(legend);
            AddMuted(mainPanel,"Наведите — название · нажмите — подробности · тяните фон — перемещение · колесо — масштаб");
            researchViewport.RegisterCallback<GeometryChangedEvent>(_=>{if(researchFit)FitResearch();else ApplyResearchTransform();});
            researchViewport.RegisterCallback<WheelEvent>(e=>{
                ZoomResearch(researchZoom*Mathf.Exp(-e.delta.y*.08f),researchViewport.WorldToLocal(e.mousePosition));e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            researchViewport.RegisterCallback<PointerDownEvent>(e=>{
                if(e.button!=0 || (e.target as VisualElement)?.GetFirstAncestorOfType<Button>()!=null || e.target is Button)return;
                researchDragging=true;researchDragStart=e.position;researchPanStart=researchPan;researchViewport.CapturePointer(e.pointerId);e.StopPropagation();});
            researchViewport.RegisterCallback<PointerMoveEvent>(e=>{if(!researchDragging)return;researchFit=false;researchPan=researchPanStart+(Vector2)e.position-researchDragStart;ApplyResearchTransform();e.StopPropagation();});
            researchViewport.RegisterCallback<PointerUpEvent>(e=>{researchDragging=false;if(researchViewport.HasPointerCapture(e.pointerId))researchViewport.ReleasePointer(e.pointerId);});
            researchViewport.RegisterCallback<PointerCaptureOutEvent>(_=>researchDragging=false);
            if(!selectedResearchTechnology.HasValue)selectedResearchTechnology=new TechnologyId("basic_survival");
            UpdateResearchView();researchViewport.schedule.Execute(()=>{if(researchFit)FitResearch();else ApplyResearchTransform();});
        }

        private void FitResearch()
        {
            var r=researchViewport.contentRect;if(r.width<=0||r.height<=0)return;
            researchZoom=Mathf.Min((r.width-16)/ResearchWidth,(r.height-12)/ResearchHeight);
            researchPan=new Vector2((r.width-ResearchWidth*researchZoom)/2,(r.height-ResearchHeight*researchZoom)/2);ApplyResearchTransform();
        }
        private void ZoomResearch(float value,Vector2 anchor)
        {researchFit=false;value=Mathf.Clamp(value,.3f,2.5f);researchPan=anchor-(anchor-researchPan)*value/researchZoom;researchZoom=value;ApplyResearchTransform();}
        private void ApplyResearchTransform()
        {researchWorld.style.scale=new Scale(new Vector3(researchZoom,researchZoom,1));researchWorld.style.translate=new Translate(researchPan.x,researchPan.y,0);researchZoomLabel.text=Mathf.RoundToInt(researchZoom*100)+"%";}
        private void CenterResearch()
        {
            if(!selectedResearchTechnology.HasValue)return;researchFit=false;researchZoom=Mathf.Max(1,researchZoom);
            var d=context.Research.Catalog.Get(selectedResearchTechnology.Value);
            var center=d.Profile?.Branch=="epoch"?new Vector2(670,770):ResearchPosition(d)+Vector2.one*ResearchDiameter(d)/2;
            researchPan=researchViewport.contentRect.center-center*researchZoom;ApplyResearchTransform();
        }
        private string ResearchNodeState(TechnologyDefinition d)
        {
            if(context.Research.IsCompleted(d.Id))return "completed";
            if(context.Research.ActiveProject?.TechnologyId==d.Id)return "active";
            if(d.Profile?.IsFuture==true)return "future";
            if(!context.Research.ArePrerequisitesMet(d))return "locked";
            return context.Research.IsAvailable(d.Id)?"available":"practice";
        }
        private static string ResearchStateName(string state)=>state switch {"completed"=>"Изучено","active"=>"Исследуется","future"=>"Будущая механика","available"=>"Доступно","practice"=>"Нужна практика или материалы",_=>"Закрыто"};
        private void ResizeResearchWorkspace()
        {
            if(researchViewport?.parent==null)return;
            var workspaceHeight=Mathf.Max(300,root.resolvedStyle.height-390);
            if(Mathf.Abs(researchViewport.parent.resolvedStyle.height-workspaceHeight)>.5f)
                researchViewport.parent.style.height=workspaceHeight;
        }
        private void UpdateResearchView()
        {
            ResizeResearchWorkspace();
            var active=context.Research.ActiveProject;
            var status=ResearchWorkerStatusQuery.Capture(context.Research,context.AiAgents);
            researchStatus.text=(active==null?"Нет текущего исследования":context.Research.Catalog.Get(active.TechnologyId).DisplayName+" · "+Mathf.RoundToInt((float)active.Progress*100)+"%")+
                " · Исследователи: "+status.ActiveCount+" / "+status.AssignedCount+" · вклад группы до "+context.Research.EffectiveWorkerLimit+" работников";
            foreach(var entry in researchNodes)
            {
                var d=context.Research.Catalog.Get(entry.Key);var state=ResearchNodeState(d);var button=entry.Value;
                foreach(var value in new[]{"completed","active","future","available","practice","locked"})button.EnableInClassList("research-state-"+value,state==value);
                var color=state=="completed"?new Color(.58f,.7f,.44f):state=="active"?new Color(.48f,.77f,.81f):state=="available"?AccentColor:
                    state=="practice"?new Color(.73f,.5f,.28f):state=="future"?new Color(.6f,.52f,.72f):new Color(.31f,.36f,.41f);
                button.style.backgroundColor=state=="available"?new Color(.45f,.4f,.25f):state=="completed"?new Color(.2f,.28f,.17f):new Color(.12f,.17f,.23f);
                if(selectedResearchTechnology==d.Id)color=new Color(.98f,.87f,.6f);
                button.style.borderTopColor=color;button.style.borderBottomColor=color;button.style.borderLeftColor=color;button.style.borderRightColor=color;
                var matches=(string.IsNullOrEmpty(researchFilter)||d.Profile?.Branch==researchFilter||d.Profile?.Branch=="root")&&
                    (string.IsNullOrEmpty(researchSearch)||d.DisplayName.IndexOf(researchSearch,StringComparison.OrdinalIgnoreCase)>=0);
                button.style.opacity=matches?1:.15f;button.tooltip=d.DisplayName+" · "+ResearchStateName(state);
                var label=button.Q<Label>("research-node-caption");label.style.display=d.Profile?.IsHub==true||selectedResearchTechnology==d.Id||button.ClassListContains("research-node-hover")?DisplayStyle.Flex:DisplayStyle.None;
            }
            researchLinks.Refresh(context.Research,selectedResearchTechnology,researchFilter);
            var selected=context.Research.Catalog.Get(selectedResearchTechnology.Value);
            var blockers=context.Research.GetBlockers(selected.Id);
            var key=selected.Id+"|"+ResearchNodeState(selected)+"|"+string.Join(";",blockers)+"|"+(active==null?"":active.TechnologyId+":"+(int)(active.Progress*100));
            if(key!=researchDetailKey){researchDetailKey=key;DrawResearchDetail(selected,blockers);}
        }
        private void DrawResearchDetail(TechnologyDefinition d,IReadOnlyList<string> blockers)
        {
            var scroll=researchDetails.scrollOffset;researchDetails.Clear();var p=d.Profile;
            var card=new VisualElement {name="research-selected-detail"};card.style.paddingTop=18;researchDetails.Add(card);
            var icon=SerenityIconLibrary.Image(ResearchIcon(d));icon.style.width=54;icon.style.height=54;card.Add(icon);
            AddTitle(card,d.DisplayName);AddMuted(card,ResearchStateName(ResearchNodeState(d)));AddLine(card,d.Description);
            if(p?.IsKey==true)AddMuted(card,"✦ Ключевое достижение направления");
            if(d.Prerequisites.Count>0){AddSection(card,"Сначала изучите");foreach(var id in d.Prerequisites){var captured=id;card.Add(ActionButton((context.Research.IsCompleted(id)?"✓ ":"○ ")+context.Research.Catalog.Get(id).DisplayName,()=>{selectedResearchTechnology=captured;researchDetailKey="";UpdateResearchView();CenterResearch();}));}}
            if(d.Id==new TechnologyId("stone_age_mastery")){
                var rules=context.Research.Settings;AddSection(card,"Условия эпохи");
                AddLine(card,rules.EpochDiscoveries+" открытий; по "+rules.DiscoveriesPerDirection+" открытия в любых "+rules.EpochDirections+" направлениях; ключевые достижения в "+rules.EpochDirections+" направлениях.");
                AddMuted(card,"Огонь, орудия, жильё; "+context.Research.PracticalTarget("population")+" жителей и "+context.Research.PracticalTarget("food")+" пищи. Военная ветка необязательна. После рубежа остальные исследования остаются доступны.");}
            if(blockers.Count>0&&!context.Research.IsCompleted(d.Id)&&context.Research.ActiveProject?.TechnologyId!=d.Id)
                {AddSection(card,"Пока не выполнено");foreach(var reason in blockers)AddMuted(card,reason);}
            if(p?.IsFuture==true)AddLine(card,p.Integration=="PLANNED"?
                "Появится вместе с развитием этой игровой механики. Сейчас изучение недоступно.":
                "Возможное будущее направление развития поселения. Сейчас изучение недоступно.");
            else if(d.Unlocks.Count>0){AddSection(card,"Открывает");foreach(var unlock in d.Unlocks)if(unlock.Kind!=TechnologyUnlockKind.Capability)AddMuted(card,UnlockName(unlock));}
            if(!d.StartingKnowledge){AddSection(card,"Стоимость");AddLine(card,TimeSpan.FromTicks(d.ResearchWorkRequiredTicks).TotalDays.ToString("0.#")+" человеко-дней");
                if(p!=null&&(p.WoodCost>0||p.StoneCost>0))AddMuted(card,"Древесина: "+p.WoodCost+" · камень: "+p.StoneCost+" (списываются при старте)");}
            var active=context.Research.ActiveProject;
            if(active?.TechnologyId==d.Id){AddInspectorBar(card,"Прогресс",(float)active.Progress*100,Mathf.RoundToInt((float)active.Progress*100)+"%");
                var workers=ResearchWorkerStatusQuery.Capture(context.Research,context.AiAgents);
                var rate=workers.ActiveWorkerIds.Sum(id=>(long)Math.Max(0,context.Research.GetRatePerThousand(id)))*
                    Math.Min(context.Research.EffectiveWorkerLimit,workers.AssignedCount)/Math.Max(1d,workers.AssignedCount)*
                    (100+context.Research.ScienceBonusPercent)/100;
                AddMuted(card,rate>0?"При текущей работе группы: ещё ≈ "+
                    ((active.RequiredWorkTicks-active.AccumulatedWorkTicks)/(double)TimeSpan.TicksPerDay*1000/rate).ToString("0.0")+" игровых дней":"Ожидает работающих исследователей.");
                card.Add(ActionButton("Отменить исследование",()=>TryCommand(()=>context.Research.Cancel()),"research-cancel"));AddMuted(card,"Потраченные материалы и работа при отмене не возвращаются.");}
            else if(!context.Research.IsCompleted(d.Id)&&p?.IsFuture!=true){var start=ActionButton("Начать исследование",()=>TryCommand(()=>{if(!context.Research.Start(d.Id))throw new InvalidOperationException("Условия исследования изменились.");}),"research-start-selected");start.SetEnabled(blockers.Count==0&&active==null);card.Add(start);if(active!=null)AddMuted(card,"Сначала завершите или отмените текущее исследование.");}
            AddMuted(card,"Состав и смены задаются в группе «Исследователи». Работники распределяются автоматически.");
            researchDetails.scrollOffset=scroll;
        }
        private sealed class ResearchLinks : VisualElement
        {
            private ResearchService research;private TechnologyId? selected;private string filter;
            public ResearchLinks(){name="research-prerequisite-connectors";pickingMode=PickingMode.Ignore;style.position=Position.Absolute;style.width=ResearchWidth;style.height=ResearchHeight;generateVisualContent+=Draw;}
            public void Refresh(ResearchService service,TechnologyId? selection,string branch){research=service;selected=selection;filter=branch;userData=service.Catalog.All.Sum(x=>x.Prerequisites.Count);MarkDirtyRepaint();}
            private void Draw(MeshGenerationContext ctx)
            {
                if(research==null)return;var pen=ctx.painter2D;
                foreach(var child in research.Catalog.All.Where(x=>x.Profile?.Branch!="epoch"))foreach(var id in child.Prerequisites)
                {
                    var parent=research.Catalog.Get(id);var a=ResearchPosition(parent)+Vector2.one*ResearchDiameter(parent)/2;
                    var b=ResearchPosition(child)+Vector2.one*ResearchDiameter(child)/2;var delta=(b-a).normalized;
                    var bright=selected==id||selected==child.Id;var color=bright?new Color(.95f,.84f,.58f):research.IsCompleted(id)?new Color(.72f,.67f,.49f):new Color(.4f,.46f,.5f,.35f);
                    if(!string.IsNullOrEmpty(filter)&&child.Profile?.Branch!=filter&&!bright)color.a=.1f;
                    pen.strokeColor=color;pen.lineWidth=bright?3:2;pen.BeginPath();pen.MoveTo(a+delta*(ResearchDiameter(parent)/2+5));pen.LineTo(b-delta*(ResearchDiameter(child)/2+5));pen.Stroke();
                }
            }
        }
    }
}
