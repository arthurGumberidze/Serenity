using System;
using System.Collections;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Characters;
using Game.Domain.Military;
using Game.Infrastructure;
using Game.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U21")]
    public sealed class MilitaryUnitPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            Screen.SetResolution(1600, 900, false);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionCreatesUnitAndDetailedAggregateRoundTripPreservesIdentity()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            var created = root.MilitaryDebug.CreateFromSelection(new MilitarySideId("player"));
            var memberIds = created.ActiveMemberIds.ToArray();

            root.MilitaryRepresentations.Transition(created.Id, MilitaryUnitRepresentationMode.Aggregate);
            Assert.That(memberIds.All(id => root.Tiers.GetTier(id) == CharacterSimulationTier.Tier3), Is.True);
            Assert.That(memberIds.All(id => root.Tiers.CountActiveRepresentations(id) == 1), Is.True);
            root.MilitaryRepresentations.Transition(created.Id, MilitaryUnitRepresentationMode.Detailed);
            yield return null;

            var restored = root.MilitaryUnits.Get(created.Id);
            Assert.That(restored.Id, Is.EqualTo(created.Id));
            Assert.That(restored.CaptainId, Is.EqualTo(created.CaptainId));
            Assert.That(restored.ActiveMemberIds, Is.EqualTo(memberIds));
            Assert.That(memberIds.All(id => root.CharacterPresentations.TryGet(id, out _)), Is.True);
            root.GameplayUi.SetDeveloperUiVisible(true);
            Assert.That(root.MilitaryDebug.TryGetUiBlockingRect(out var rect), Is.True);
            Assert.That(root.PointerRaycaster.IsPointerBlockedByUi(
                WorldPointerRaycaster.GuiToScreenPoint(rect.center)), Is.True);
        }

        [UnityTest]
        public IEnumerator ActivePhysicalCombatBlocksUnitAggregationAndCancellationReleasesIt()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.SelectionProbe.SelectCharacters(root.DemoPresenters);
            var unit = root.MilitaryDebug.CreateFromSelection(new MilitarySideId("player"));
            var encounter = root.StartDemoCombat();
            Assert.Throws<InvalidOperationException>(() => root.MilitaryRepresentations.Transition(unit.Id,
                MilitaryUnitRepresentationMode.Aggregate));
            Assert.That(root.Combat.CancelEncounter(encounter.EncounterId), Is.True);
            root.MilitaryCombat.ReconcileAll();
            Assert.DoesNotThrow(() => root.MilitaryRepresentations.Transition(unit.Id,
                MilitaryUnitRepresentationMode.Aggregate));
            yield return null;
            Assert.That(root.MilitaryUnits.Get(unit.Id).Representation,
                Is.EqualTo(MilitaryUnitRepresentationMode.Aggregate));
            Assert.That(root.Combat.Encounters.Single().Status, Is.EqualTo(CombatEncounterStatus.Cancelled));
        }
    }
}
