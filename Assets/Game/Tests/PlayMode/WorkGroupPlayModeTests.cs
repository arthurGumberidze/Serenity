using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U11")]
    public sealed class WorkGroupPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftMultiSelectionCreatesGroupAndSurvivesPresenterRespawn()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            SelectFromAbove(root, root.DemoPresenters[0], false);
            SelectFromAbove(root, root.DemoPresenters[1], true);
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));
            Assert.That(root.WorkDebug.SelectionCount, Is.EqualTo(2));
            var group = root.WorkDebug.CreateGroupFromSelection("Founders");
            Assert.That(group.MemberCount, Is.EqualTo(2));
            root.WorkDebug.AssignHotkey(1, group.Id);
            SelectFromAbove(root, root.DemoPilePresenters[0], false);
            Assert.That(root.WorkDebug.RecallHotkey(1), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));

            var character = root.MaleDemoCharacter;
            var oldPresenter = root.DemoPresenters.Single(x => x.CharacterId == character.Id);
            root.CharacterSpawner.Despawn(oldPresenter);
            yield return null;
            var replacement = root.CharacterSpawner.Spawn(character, new Vector3(-3f, 0f, 0f), Quaternion.identity);
            root.AiRuntime.Register(character, replacement);
            root.WorkDebug.SelectCurrentGroup();

            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.EqualTo(2));
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Does.Contain(character.Id));
            Assert.That(root.Work.Groups.Get(group.Id).Contains(character.Id), Is.True);
            root.AiRuntime.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator GroupMoveCommandDrivesPhysicalNavMeshMovement()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            var group = root.WorkDebug.CreateGroupFromSelection("Scouts");
            var start = root.DemoPresenters.ToDictionary(x => x.CharacterId, x => x.transform.position);
            var jobs = root.WorkDebug.AssignCurrentGroupMove(new WorldPosition(0f, 0f, 9f), WorkPriority.Urgent);
            Assert.That(jobs.Count, Is.EqualTo(2));
            root.AiRuntime.SetPaused(false);

            var timeout = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < timeout && jobs.Any(x => !x.IsTerminal)) yield return null;
            Assert.That(jobs.All(x => x.Status == WorkJobStatus.Completed), Is.True);
            Assert.That(root.DemoPresenters.Any(x => Vector3.Distance(start[x.CharacterId], x.transform.position) > 2f), Is.True);
            Assert.That(root.Work.Groups.Get(group.Id).MemberCount, Is.EqualTo(2));
            Assert.That(root.AiRuntime.PerAgentAiUpdateCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator GroupHaulUsesCanonicalInventoryThenAgentsReturnToAutonomousAi()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            root.WorkDebug.CreateGroupFromSelection("Haulers");
            var pile = root.DemoPilePresenters.OrderBy(x => x.PileId.ToString()).First();
            var source = new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId);
            var destination = new InventoryOwner(InventoryOwnerKind.BuildingStorage, root.DemoStorageBuilding.Id);
            var initialTotal = root.Inventories.All.Sum(x => x.GetAmount(pile.ResourceId).Units);
            var initialSource = root.Inventories.Get(source).GetAmount(pile.ResourceId).Units;
            var initialStorage = root.Inventories.Get(destination).GetAmount(pile.ResourceId).Units;
            var jobs = root.WorkDebug.AssignCurrentGroupHaul(source, destination, pile.ResourceId,
                new ResourceQuantity(1), WorkPriority.Urgent);
            Assert.That(jobs.Count, Is.EqualTo(2));
            root.AiRuntime.SetPaused(false);

            var timeout = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < timeout && jobs.Any(x => !x.IsTerminal)) yield return null;
            Assert.That(jobs.All(x => x.Status == WorkJobStatus.Completed), Is.True);
            Assert.That(root.Inventories.Get(source).GetAmount(pile.ResourceId).Units, Is.EqualTo(initialSource - 2));
            Assert.That(root.Inventories.Get(destination).GetAmount(pile.ResourceId).Units, Is.EqualTo(initialStorage + 2));
            Assert.That(root.Inventories.All.Sum(x => x.GetAmount(pile.ResourceId).Units), Is.EqualTo(initialTotal));
            Assert.That(jobs.All(x => x.ClaimState == WorkClaimState.Released), Is.True);

            timeout = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < timeout && root.AiAgents.All.Any(x =>
                       x.CurrentAction == UtilityActionKind.ManualHaul || x.CurrentAction == UtilityActionKind.ManualMove))
                yield return null;
            Assert.That(root.AiAgents.All.All(x => x.CurrentAction != UtilityActionKind.ManualHaul &&
                x.CurrentAction != UtilityActionKind.ManualMove), Is.True);
            Assert.That(root.AiAgents.All.Any(x => x.CurrentAction == UtilityActionKind.Haul ||
                x.CurrentAction == UtilityActionKind.Rest || x.CurrentAction == UtilityActionKind.Idle), Is.True);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(root.PointerRaycaster.WorldCamera);
        }

        private static void SelectFromAbove(LocalSceneCompositionRoot root, Component target, bool additive)
        {
            var collider = target.GetComponentInChildren<Collider>(true);
            Assert.That(collider, Is.Not.Null);
            var camera = root.PointerRaycaster.WorldCamera;
            var center = collider.bounds.center;
            camera.transform.SetPositionAndRotation(center + Vector3.up * 12f,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            Physics.SyncTransforms();
            var point = camera.WorldToScreenPoint(center);
            Assert.That(root.SelectionProbe.TrySelectAt(point, additive), Is.True);
        }

        private static void Capture(Camera camera)
        {
            camera.transform.position = new Vector3(-2f, 14f, -18f);
            camera.transform.LookAt(new Vector3(-1f, 0.5f, 1f));
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/U11-work-groups.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(image);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
