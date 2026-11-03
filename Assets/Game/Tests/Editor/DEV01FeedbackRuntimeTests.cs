using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Simulation.Time;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Tests.Editor
{
    public sealed class DEV01FeedbackRuntimeTests
    {
        private const string Evidence = "Artifacts/DEV01Feedback/runtime";
        [Test]
        public void RunnerHeartbeatRetainsCompleteStateWhileWindowsReaderHoldsFile()
        {
            var type = Type.GetType("SerenityPersistentUnityRunner, Serenity.PersistentUnityRunner.Editor", true);
            var write = type.GetMethod("WriteState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var path = "Library/SerenityPersistentRunner/state.json";
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var before = File.ReadAllText(path);
                Assert.DoesNotThrow(() => write.Invoke(null, null));
                Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            }
            Assert.DoesNotThrow(() => write.Invoke(null, null));
            Assert.That(File.ReadAllText(path), Does.Contain("heartbeat_utc"));
        }
        [UnityTest, Timeout(1200000)]
        public IEnumerator FreshSettlementBuildsResearchHouseFromGatheredResources()
        {
            Directory.CreateDirectory(Evidence);
            yield return new EnterPlayMode();
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.OpenNewGame(); menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;
            while ((root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>()) == null || !root.IsInitialized)
                yield return null;
            root.Clock.Pause(); root.GameplayUi.SetOnboardingVisible(false);
            root.GameplayUi.SetPanel(GameplayPanel.Work);
            yield return new WaitForSecondsRealtime(1);
            Assert.That(root.GameplayUi.RootVisual.Q("work-roster"), Is.Not.Null);
            root.GameplayUi.HandleEscape();
            Assert.That(root.GameplayUi.ActivePanel, Is.EqualTo(GameplayPanel.None));
            root.GameplayUi.HandleEscape(); yield return null;
            Assert.That(root.GameplayUi.RootVisual.Q("pause-resume"), Is.Not.Null);
            Assert.That(root.Clock.IsPaused, Is.True);
            root.GameplayUi.HandleEscape();
            var residents = root.Characters.GetAll().Where(x => root.CanCommandResident(x.Id)).OrderBy(x => x.Id).ToArray();
            var professions = root.DirectWorkCommands.ProfessionGroups;
            var builders = professions.Primary(WorkGroupRole.Builders);
            professions.Reassign(residents[0].Id, builders.Id);
            professions.Reassign(residents[1].Id, builders.Id);
            root.Work.SetMemberShift(builders.Id, residents[0].Id, WorkShiftKind.Day);
            root.Work.SetMemberShift(builders.Id, residents[1].Id, WorkShiftKind.Day);
            for (var i=2; i<residents.Length; i++)
            {
                var group = professions.Primary(WorkGroupRole.General);
                professions.Reassign(residents[i].Id, group.Id);
                root.Work.SetMemberShift(group.Id, residents[i].Id, i % 3 == 0 ? WorkShiftKind.Night : WorkShiftKind.Day);
            }
            Assert.That(root.AiAgents.TryGet(residents[0].Id, out var initialAgent), Is.True);
            var anchor = initialAgent.Position;
            var placement = root.BuildingPlacementController;
            Assert.That(placement.TryStartPlacement(new BuildingDefinitionId("research_house")), Is.True);
            Building house = null;
            for (var radius=12; radius<=55 && house==null; radius+=4)
                for (var a=0; a<12 && house==null; a++)
                {
                    var point = new Vector3(anchor.X+Mathf.Cos(a*Mathf.PI/6)*radius,0,anchor.Z+Mathf.Sin(a*Mathf.PI/6)*radius);
                    point.y = root.WorldHeight.SampleHeight(point.x,point.z);
                    if (placement.TryMovePreviewToWorldPoint(point) && placement.IsPreviewValid && placement.PreviewClearanceSourceIds.Count==0)
                        house = placement.ConfirmPlacement();
                }
            Assert.That(house, Is.Not.Null, "Place an unlocked blueprint with ordinary starter stocks");
            Assert.That(house.IsOperational, Is.False);
            Assert.That(root.Production.GetCanonicalTotal(new ResourceId("wood_log")), Is.LessThan(80));
            Dump(root,"initial");
            root.Clock.SetSpeed(GameSpeed.VeryFast); root.Clock.Resume();
            var deadline = Time.realtimeSinceStartup + 600;
            var nextDump = Time.realtimeSinceStartup + 60;
            var sawTwoBuilders = false;
            while (!house.IsOperational && Time.realtimeSinceStartup < deadline)
            {
                sawTwoBuilders |= root.Work.OpenJobs.Count(x => x.Type==WorkJobType.Build && x.Target.EntityId==house.Id &&
                    x.AssigneeId.HasValue && root.AiAgents.TryGet(x.AssigneeId.Value,out var agent) && agent.Phase==AiActionPhase.Working) >= 2;
                if(Time.realtimeSinceStartup>=nextDump) { Dump(root,"progress"); nextDump+=60; }
                yield return null;
            }
            Dump(root,"house");
            Assert.That(house.IsOperational, Is.True,"Researchers must be reachable through real gathering/delivery/building, without resource grants");
            Assert.That(sawTwoBuilders,Is.True,"Both day-shift builders must physically work together");
            var scientists = professions.Primary(WorkGroupRole.Scientists);
            professions.Reassign(residents[2].Id, scientists.Id);
            professions.Reassign(residents[3].Id, scientists.Id);
            root.Work.SetMemberShift(scientists.Id,residents[2].Id,WorkShiftKind.Day);
            root.Work.SetMemberShift(scientists.Id,residents[3].Id,WorkShiftKind.Night);
            foreach(var technology in root.Technologies.All.Where(x=>root.Research.CanEnqueue(x.Id)).Take(3).ToArray())
                Assert.That(root.Research.Enqueue(technology.Id),Is.True);
            deadline=Time.realtimeSinceStartup+180;
            while ((root.Research.ActiveProject==null || root.Research.ActiveProject.AccumulatedWorkTicks==0) && Time.realtimeSinceStartup<deadline)
                yield return null;
            Dump(root,"research");
            Assert.That(root.Research.ActiveProject?.AccumulatedWorkTicks ?? 0,Is.GreaterThan(0));
            Assert.That(root.Research.Workplace.IsInside(residents[2].Id)||root.Research.Workplace.IsInside(residents[3].Id),Is.True);
            root.Clock.Pause();
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator FreshWorldSupportsUiWheelAndPhysicalResidentMovement()
        {
            Directory.CreateDirectory(Evidence);
            yield return new EnterPlayMode();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            menu.OpenNewGame(); menu.StartNewGame(1380062136);
            LocalSceneCompositionRoot root;
            while ((root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>()) == null || !root.IsInitialized)
                yield return null;
            root.Clock.Pause(); root.GameplayUi.SetOnboardingVisible(false);
            yield return new WaitForSecondsRealtime(2);
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                root.GameplayUi.SetPanel(GameplayPanel.Build);
                yield return new WaitForSecondsRealtime(1);
                var panel = root.GameplayUi.RootVisual.Q("main-panel");
                var buildBody = panel.Q("build-palette-body");
                var icons = panel.Q<ScrollView>("build-icons");
                var description = panel.Q<ScrollView>("build-details");
                Assert.That(icons.worldBound.yMin, Is.GreaterThanOrEqualTo(buildBody.worldBound.yMin - 1),
                    "The palette must not grow upward across its title");
                Assert.That(icons.worldBound.yMax, Is.LessThanOrEqualTo(buildBody.worldBound.yMax + 1));
                Assert.That(description.worldBound.yMin, Is.EqualTo(icons.worldBound.yMin).Within(1));
                Assert.That(icons.contentViewport.worldBound.height, Is.LessThanOrEqualTo(211));
                var center = panel.worldBound.center;
                // UI Toolkit worldBound uses panel units, not screen pixels at scaled resolutions.
                var panelOrigin = RuntimePanelUtils.ScreenToPanel(panel.panel, Vector2.zero);
                var panelExtent = RuntimePanelUtils.ScreenToPanel(panel.panel, new Vector2(Screen.width, Screen.height)) - panelOrigin;
                var guiPoint = new Vector2((center.x - panelOrigin.x) * Screen.width / panelExtent.x,
                    (center.y - panelOrigin.y) * Screen.height / panelExtent.y);
                var pointer = new Vector2(guiPoint.x, Screen.height - guiPoint.y);
                Assert.That(root.GameplayUi.IsGuiPointBlocked(guiPoint), Is.True, "The injected pointer must actually be inside UI at this scale");
                var zoom = root.CameraController.CurrentZoom;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0, 120) });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                yield return new WaitForSecondsRealtime(1);
                Assert.That(root.CameraController.CurrentZoom, Is.EqualTo(zoom).Within(.01f), "Wheel inside the construction panel must not zoom the map");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(root.GameplayUi.ActivePanel, Is.EqualTo(GameplayPanel.None));

                root.GameplayUi.SetPanel(GameplayPanel.Work);
                yield return new WaitForSecondsRealtime(1);
                var professionField = root.GameplayUi.RootVisual.Q<DropdownField>("resident-profession");
                var shiftField = professionField.parent.Q<DropdownField>("resident-shift");
                Assert.That(shiftField.worldBound.y, Is.EqualTo(professionField.worldBound.y).Within(2),
                    "Profession and shift must remain in the same resident row");
                Assert.That(root.GameplayUi.RootVisual.Q<ScrollView>("main-panel").scrollOffset.y, Is.EqualTo(0).Within(1),
                    "Switching workspaces must not inherit another panel's scroll");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(root.GameplayUi.RootVisual.Q("pause-resume"), Is.Not.Null, "Esc without a workspace opens pause");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;

                var id = root.MaleDemoCharacter.Id;
                Assert.That(root.AiRuntime.TryGetMovement(id, out var movement), Is.True);
                var agent = movement.Agent;
                var origin = agent.transform.position;
                var path = new NavMeshPath();
                var reachable = 0;
                for (var i = 0; i < 8; i++)
                {
                    var point = origin + new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4)) * 24;
                    point.y = root.WorldHeight.SampleHeight(point.x, point.z);
                    if (NavMesh.SamplePosition(point, out var hit, 3, NavMesh.AllAreas) &&
                        NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                        reachable++;
                }
                Assert.That(reachable, Is.GreaterThanOrEqualTo(7), "The camp must connect to its surroundings, including paths around initial buildings");
                var direction = Vector3.zero;
                for (var i = 0; i < 8 && direction == Vector3.zero; i++)
                {
                    var candidate = new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4));
                    if (!NavMesh.Raycast(origin, origin + candidate * 12, out _, NavMesh.AllAreas)) direction = candidate;
                }
                Assert.That(direction, Is.Not.EqualTo(Vector3.zero));
                agent.transform.rotation = Quaternion.LookRotation(direction);
                Assert.That(root.ThirdPersonControl.TryEnter(id).IsEligible, Is.True);
                root.Clock.SetSpeed(GameSpeed.Normal); root.Clock.Resume();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSecondsRealtime(3);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                var distance = Vector3.ProjectOnPlane(agent.transform.position - origin, Vector3.up).magnitude;
                Assert.That(distance, Is.GreaterThan(3), "Holding W must move the physical resident across ordinary terrain");
                Assert.That(root.GameplayUi.ActivePanel, Is.EqualTo(GameplayPanel.None), "W must not activate focused UI navigation");
                root.ThirdPersonControl.Exit(); root.Clock.Pause();
                File.WriteAllText(Evidence + "/movement-ui.txt", "seed=1380062136; wheel delta=" +
                    (root.CameraController.CurrentZoom - zoom) + "; reachable=" + reachable + "/8; physical W distance=" + distance);
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); }
        }

        private static void Dump(LocalSceneCompositionRoot root,string phase)
        {
            File.WriteAllLines(Evidence+"/"+phase+"-jobs.txt",root.Work.Jobs.Select(x=>x.Type+" "+x.Status+" "+x.AssigneeId+" "+x.Target.EntityId));
            File.WriteAllLines(Evidence+"/"+phase+"-residents.txt",root.Characters.GetAll().Where(x=>root.CanCommandResident(x.Id)).Select(x=>
                root.AiAgents.TryGet(x.Id,out var a)?x.Id+" "+x.Name+" "+a.CurrentAction+" "+a.Phase+" "+a.Position+" hunger="+a.Needs.Hunger+" energy="+a.Needs.Energy+
                " group="+(root.Work.Groups.TryGetForMember(x.Id,out var g)?g.Role+"/"+g.ShiftFor(x.Id):"none"):x.Id.ToString()));
            File.WriteAllLines(Evidence+"/"+phase+"-stocks.txt",root.Resources.All.Select(r=>r.Id+" "+root.Production.GetCanonicalTotal(r.Id)+" / "+root.Production.GetTarget(r.Id)));
            File.WriteAllLines(Evidence+"/"+phase+"-buildings.txt",root.Buildings.All.Select(b=>b.DefinitionId+" "+b.ConstructionPhase+" "+b.ConstructionProgress+" "+string.Join(",",b.ConstructionMaterials.Select(m=>m.ResourceId+"="+m.RemainingUnits))));
        }
        [UnityTearDown] public IEnumerator Cleanup() { if(Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
