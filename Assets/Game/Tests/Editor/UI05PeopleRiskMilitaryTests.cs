using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI05")]
    public sealed class UI05PeopleRiskMilitaryTests
    {
        private static string Source => File.ReadAllText(Path.Combine(Application.dataPath,
            "Game/Presentation/UI/GameplayUiController.cs"));

        [Test]
        public void PeopleWorkspaceUsesCanonicalDomesticHealthAndBoundedRoster()
        {
            StringAssert.Contains("new ListView(residents", Source);
            StringAssert.Contains("context.Domestic.Homes", Source);
            StringAssert.Contains("context.Domestic.EvaluateSleep", Source);
            StringAssert.Contains("context.Health.Conditions", Source);
            StringAssert.Contains("context.Family.GetChildren", Source);
            StringAssert.Contains("people-home-card", Source);
            StringAssert.DoesNotContain("new Update per resident", Source);
        }

        [Test]
        public void DynastyAndAttentionProjectCanonicalStateAndCommands()
        {
            StringAssert.Contains("GetOrderedSuccessionCandidates", Source);
            StringAssert.Contains("context.Dynasty.MemberIds", Source);
            StringAssert.Contains("context.Events.PendingPlayerEvents", Source);
            StringAssert.Contains("context.Events.ResolvePlayerChoice", Source);
            StringAssert.Contains("EventChoiceFailureText", Source);
            StringAssert.Contains("dynasty-family-tree", Source);
            StringAssert.Contains("attention-decision-card", Source);
        }

        [Test]
        public void MilitaryWorkspaceUsesVirtualizedCanonicalRosterSupplyTrainingAndCombat()
        {
            StringAssert.Contains("new ListView(units", Source);
            StringAssert.Contains("new ListView(activeMembers", Source);
            StringAssert.Contains("context.Military.LoadInitialSupply", Source);
            StringAssert.Contains("context.MilitaryTraining.CheckStart", Source);
            StringAssert.Contains("context.MilitaryCommands.Hold", Source);
            StringAssert.Contains("context.Battles.Battles", Source);
            StringAssert.DoesNotContain("SetOrder(selected.Id, MilitaryUnitOrderKind.Engage)", Source);
        }

        [Test]
        public void Ui05WorkspacesAreFocusedWithoutOwningMutableAuthority()
        {
            StringAssert.Contains("IsPeopleRiskWorkspace", Source);
            StringAssert.Contains("is-people-risk-workspace", Source);
            StringAssert.DoesNotContain("List<MilitaryUnitState> canonical", Source);
            StringAssert.DoesNotContain("Dictionary<StableEntityId, Character> roster", Source);
            StringAssert.DoesNotContain("UnityEngine.Random", Source);
        }
    }
}
