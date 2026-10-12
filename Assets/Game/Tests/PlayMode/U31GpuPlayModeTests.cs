using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain.Combat;
using Game.Domain.Control;
using Game.Domain.Military;
using Game.Infrastructure;
using Game.Presentation.UI;
using Game.Simulation.Combat;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U31")]
    public sealed class U31GpuPlayModeTests
    {
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-u31-gpu-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", saveDirectory);
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) Object.Destroy(root.gameObject);
            yield return null;
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", null);
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator RealInputAttackVictoryBannerAndPostVictoryUiRemainOperational()
        {
            Assert.That(Environment.GetCommandLineArgs(), Does.Not.Contain("-nographics"),
                "U31 GPU validation must be launched without Unity's no-graphics switch.");
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.GameplayUi.RootVisual.Q<Button>("pause-button"), Is.Not.Null);
            var members = root.PlayerCharacterIds.ToArray();
            var preparedUnit = root.MilitaryUnits.Create(MilitaryUnitId.NewId(), new MilitarySideId("player"),
                members, members[0], MilitaryUnitRepresentationMode.Detailed, "local");
            Assert.That(root.MilitaryUnits.LoadInitialSupply(preparedUnit.Id, root.Inventories,
                root.Inventories.All.Select(x => x.Owner)), Is.GreaterThan(0));
            var battle = root.StartDemoUnitBattle(BattleMode.Physical);
            var attacker = root.MilitaryUnits.Get(battle.SideAUnitId.Value).CaptainId.Value;
            var target = battle.Combatants.First(x => x.SideId == battle.SideB).CharacterId;
            Assert.That(root.CharacterPresentations.TryGet(attacker, out var attackerPresenter), Is.True);
            Assert.That(root.CharacterPresentations.TryGet(target, out var targetPresenter), Is.True);
            targetPresenter.transform.position = attackerPresenter.transform.position + attackerPresenter.transform.forward;
            Physics.SyncTransforms();
            Assert.That(root.ThirdPersonControl.TryEnter(attacker).IsEligible, Is.True);
            Assert.That(root.PlayerControl.State.Mode, Is.EqualTo(PlayerControlMode.ThirdPerson));
            var playerUnitId = battle.SideAUnitId.Value;
            var battleId = battle.BattleId;
            yield return null;

            var camera = root.PointerRaycaster.WorldCamera;
            Assert.That(camera, Is.Not.Null);
            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            var aimPoint = targetPresenter.transform.position + Vector3.up;
            camera.transform.position = attackerPresenter.transform.position + Vector3.up +
                attackerPresenter.transform.forward * 0.4f;
            camera.transform.rotation = Quaternion.LookRotation(aimPoint - camera.transform.position, Vector3.up);
            Physics.SyncTransforms();

            Submit(root.GameplayUi.RootVisual.Q<Button>("pause-button"));
            Assert.That(root.Clock.IsPaused, Is.True);
            yield return AttackWithMouse(root);
            Assert.That(root.ThirdPersonControl.LastAttackStatus, Is.EqualTo(ManualCombatAttackStatus.Invalid));
            Submit(root.GameplayUi.RootVisual.Q<Button>("pause-button"));

            var before = root.Combat.Encounters.Single(x => x.BattleId == battleId).AttackHistory
                .Count(x => x.AttackerId == attacker);
            yield return AttackWithMouse(root);
            Assert.That(root.ThirdPersonControl.LastAttackStatus, Is.EqualTo(ManualCombatAttackStatus.Hit));
            var after = root.Combat.Encounters.Single(x => x.BattleId == battleId).AttackHistory
                .Count(x => x.AttackerId == attacker);
            Assert.That(after, Is.EqualTo(before + 1));
            yield return AttackWithMouse(root);
            Assert.That(root.ThirdPersonControl.LastAttackStatus, Is.EqualTo(ManualCombatAttackStatus.Cooldown));
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battleId).AttackHistory
                .Count(x => x.AttackerId == attacker), Is.EqualTo(after));
            Assert.That(root.MilitaryUnits.Get(playerUnitId).CaptainId.Value, Is.EqualTo(attacker));

            root.ThirdPersonControl.Exit();
            root.MilitaryUnits.SetAbstractLocation(battle.SideAUnitId.Value, "enemy_settlement");
            root.MilitaryUnits.SetAbstractLocation(battle.SideBUnitId.Value, "enemy_settlement");
            var transition = root.Battles.Transition(battleId, BattleMode.Abstract,
                root.Clock.State.CalendarTicks);
            Assert.That(transition.Status, Is.EqualTo(BattleTransitionStatus.Changed));
            for (var i = 0; i < 40 && root.Combat.Encounters.Single(x => x.BattleId == battleId).Status ==
                 CombatEncounterStatus.Active; i++)
            {
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(1));
                yield return null;
            }
            Assert.That(root.Combat.Encounters.Single(x => x.BattleId == battleId).Outcome,
                Is.EqualTo(CombatOutcome.SideAVictory));
            root.GameplayUi.RefreshNow();
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(string.Join("\n", root.GameplayUi.RootVisual.Query<Label>().ToList().Select(x => x.text)),
                Does.Contain("Military victory"));

            var beforeContinuation = root.Clock.State.CalendarTicks;
            Submit(root.GameplayUi.RootVisual.Q<Button>("speed-10"));
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(1));
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(beforeContinuation));
            root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
            root.GameplayUi.SaveSlot(1);
            root.GameplayUi.RequestLoadSlot(1);
            root.GameplayUi.ConfirmLoadSlot(1);
            Assert.That(root.CampaignProgress.State.MilitaryVictory, Is.True);
            Assert.That(root.GameplayUi.SubscriptionCount, Is.EqualTo(4));
        }

        private static IEnumerator AttackWithMouse(LocalSceneCompositionRoot root)
        {
            var action = root.Input.Actions.FindActionMap("ThirdPerson", true).FindAction("Attack", true);
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.EnableDevice(mouse);
            root.Input.Actions.devices = new InputDevice[] { mouse };
            try
            {
                Assert.That(action.controls.Select(x => x.device).OfType<Mouse>().Any(), Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(
                    UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
            }
            finally
            {
                root.Input.Actions.devices = null;
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            }
        }

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
