using System;
using Game.Domain.AI;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class WM16FollowupTests
    {
        [Test]
        public void SelectingSourceAfterAnimalModelReplacementDoesNotUseDestroyedRenderer()
        {
            var host=new UnityEngine.GameObject("Animal resource");
            try
            {
                var original=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
                original.transform.SetParent(host.transform);
                var presenter=host.AddComponent<Game.Presentation.Production.ProductionSourcePresenter>();
                var site=Game.Domain.Production.ProductionSite.CreateNatural(
                    new Game.Domain.Production.ProductionRecipeId("hunt_animal"),new WorldPosition(0,0,0),1);
                presenter.Bind(site,UnityEngine.Color.green,true);
                UnityEngine.Object.DestroyImmediate(original);
                var replacement=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Capsule);
                replacement.transform.SetParent(host.transform);
                Assert.DoesNotThrow(()=>presenter.SetSelected(true));
                Assert.That(replacement.GetComponent<UnityEngine.Renderer>().HasPropertyBlock(),Is.True);
                Assert.DoesNotThrow(()=>presenter.SetSelected(false));
                Assert.That(replacement.GetComponent<UnityEngine.Renderer>().HasPropertyBlock(),Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
        }
        [TestCase(30)] [TestCase(182.5)] [TestCase(365)]
        public void CadenceStartsAfterTwoDaysAndFailureAlsoWaits(double days)
        {
            var start=TimeSpan.FromHours(8).Ticks;
            var state=new RulerStrategyState(start);
            state.ConfigureCadence(start,TimeSpan.FromDays(days));
            var first=start+TimeSpan.FromDays(2).Ticks;
            Assert.That(state.NextAnnualTick,Is.EqualTo(first));
            Assert.That(state.TryBegin(first-1,RulerStrategyTrigger.Crisis,"test",out _),Is.False);
            Assert.That(state.TryBegin(first,RulerStrategyTrigger.Annual,"test",out _),Is.True);
            state.Complete(first,RulerStrategyProviderKind.Ollama,RulerStrategyRequestStatus.Unavailable,"offline");
            Assert.That(state.NextAnnualTick,Is.EqualTo(first+TimeSpan.FromDays(days).Ticks));
        }
    }
}
