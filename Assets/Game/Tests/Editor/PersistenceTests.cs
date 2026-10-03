using System;
using System.IO;
using System.Text;
using Game.Domain;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Time;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class PersistenceTests
    {
        private string directory;
        private StableEntityId slot;
        private FileSaveStore store;
        private SaveXmlCodec codec;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "Serenity-U04-" + Guid.NewGuid().ToString("N"));
            slot = StableEntityId.NewId();
            store = new FileSaveStore(directory);
            codec = new SaveXmlCodec();
        }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private SaveCoordinator Coordinator() => new SaveCoordinator(StableEntityId.NewId(),
            new GameClock(new GameTimeState(9007199254740993, long.MaxValue - 1, 5, true), new GameTimeSettings(321)), store);
        private static void Equal(SaveSnapshot expected, SaveSnapshot actual)
        {
            Assert.That(actual.FormatVersion, Is.EqualTo(expected.FormatVersion));
            Assert.That(actual.SessionId, Is.EqualTo(expected.SessionId));
            Assert.That(actual.CalendarTicks, Is.EqualTo(expected.CalendarTicks));
            Assert.That(actual.BiologicalTicks, Is.EqualTo(expected.BiologicalTicks));
            Assert.That(actual.SpeedMultiplier, Is.EqualTo(expected.SpeedMultiplier));
            Assert.That(actual.IsPaused, Is.EqualTo(expected.IsPaused));
            Assert.That(actual.BiologicalMultiplier, Is.EqualTo(expected.BiologicalMultiplier));
        }
        [Test] public void NewIdsAreValidAndDistinct()
        {
            var id = StableEntityId.NewId(); Assert.That(id.IsValid, Is.True);
            Assert.That(id, Is.Not.EqualTo(StableEntityId.NewId()));
            Assert.That(default(StableEntityId).IsValid, Is.False);
        }
        [Test] public void RestoredIdHasValueEqualityAndCanonicalRepresentation()
        {
            const string value = "0123456789abcdef0123456789abcdef";
            var id = StableEntityId.Parse(value.ToUpperInvariant());
            Assert.That(id.ToString(), Is.EqualTo(value));
            var restored = StableEntityId.Parse(id.ToString());
            Assert.That(id == restored, Is.True); Assert.That(id != restored, Is.False);
            Assert.That(id.Equals((object)restored), Is.True); Assert.That(id.GetHashCode(), Is.EqualTo(restored.GetHashCode()));
        }
        [TestCase(null)] [TestCase("")] [TestCase("bad")]
        [TestCase("00000000000000000000000000000000")]
        [TestCase("01234567-89ab-cdef-0123-456789abcdef")]
        public void InvalidIdsAreRejected(string value)
        {
            Assert.That(StableEntityId.TryParse(value, out _), Is.False);
            Assert.Throws<FormatException>(() => StableEntityId.Parse(value));
        }
        [Test] public void CaptureIsDetachedFromMutableRuntime()
        {
            var coordinator = Coordinator(); var snapshot = coordinator.Capture();
            coordinator.Clock.State.CalendarTicks = 0;
            Assert.That(snapshot.CalendarTicks, Is.EqualTo(9007199254740993));
            snapshot.CreateTimeState().CalendarTicks = 1;
            Assert.That(snapshot.CalendarTicks, Is.EqualTo(9007199254740993));
        }
        [Test] public void XmlRoundTripPreservesIdAndAllTimeFieldsExactly()
        {
            var snapshot = Coordinator().Capture(); Equal(snapshot, codec.Deserialize(codec.Serialize(snapshot)));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(5)] [TestCase(10)]
        public void EverySpeedAndRunningStateRoundTrip(int speed)
        {
            var snapshot = new SaveSnapshot(1, slot, new GameTimeState(0, 0, speed, false), 400);
            Equal(snapshot, codec.Deserialize(codec.Serialize(snapshot)));
        }
        [Test] public void SaveChangeLoadRestoresSessionAndClock()
        {
            var coordinator = Coordinator(); var expected = coordinator.Capture(); coordinator.Save(slot);
            var previousClock = coordinator.Clock; previousClock.State.CalendarTicks = 9; previousClock.Resume();
            coordinator.Load(slot); Equal(expected, coordinator.Capture());
            Assert.That(coordinator.Clock, Is.Not.SameAs(previousClock));
            var freshSession = Coordinator(); freshSession.Load(slot); Equal(expected, freshSession.Capture());
        }
        [Test] public void LoadedCustomSettingsContinueDeterministically()
        {
            var original = new SaveCoordinator(slot, new GameClock(new GameTimeState(), new GameTimeSettings(123)), store);
            original.Clock.Advance(TimeSpan.FromSeconds(3)); original.Save(slot);
            var restored = Coordinator(); restored.Load(slot);
            original.Clock.Advance(TimeSpan.FromSeconds(7)); restored.Clock.Advance(TimeSpan.FromSeconds(7));
            Equal(original.Capture(), restored.Capture());
        }
        [Test] public void StoreOverwritesExistingSlot()
        {
            var coordinator = Coordinator(); coordinator.Save(slot); coordinator.Clock.State.CalendarTicks = 12;
            coordinator.Save(slot); Equal(coordinator.Capture(), store.Load(slot));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void MissingSaveDoesNotMutateRuntime()
        {
            Directory.CreateDirectory(directory);
            var coordinator = Coordinator(); var oldClock = coordinator.Clock; var before = coordinator.Capture();
            Assert.Throws<FileNotFoundException>(() => coordinator.Load(slot));
            Assert.That(coordinator.Clock, Is.SameAs(oldClock)); Equal(before, coordinator.Capture());
        }
        [TestCase("<speed>5</speed>", "<speed>4</speed>")]
        [TestCase("<paused>true</paused>", "<paused>maybe</paused>")]
        [TestCase("<calendarTicks>9007199254740993</calendarTicks>", "<calendarTicks>-1</calendarTicks>")]
        [TestCase("<biologicalTicks>9223372036854775806</biologicalTicks>", "<biologicalTicks>9223372036854775808</biologicalTicks>")]
        [TestCase("<speed>5</speed>", "")]
        [TestCase("<speed>5</speed>", "<speed>5</speed><speed>1</speed>")]
        [TestCase("<biologicalMultiplier>321</biologicalMultiplier>", "<biologicalMultiplier>0</biologicalMultiplier>")]
        public void InvalidSaveCannotPartiallyRestore(string from, string to)
        {
            var coordinator = Coordinator(); var before = coordinator.Capture(); var oldClock = coordinator.Clock;
            coordinator.Save(slot);
            var path = Path.Combine(directory, slot + ".xml");
            var xml = File.ReadAllText(path); Assert.That(xml, Does.Contain(from));
            File.WriteAllText(path, xml.Replace(from, to));
            Assert.Throws<InvalidDataException>(() => coordinator.Load(slot));
            Assert.That(coordinator.Clock, Is.SameAs(oldClock)); Equal(before, coordinator.Capture());
        }
        [TestCase(0)] [TestCase(2)] [TestCase(2147483647)]
        public void UnknownVersionsAreExplicitlyRejected(int version)
        {
            var coordinator = Coordinator(); var before = coordinator.Capture(); coordinator.Save(slot);
            var path = Path.Combine(directory, slot + ".xml");
            File.WriteAllText(path, File.ReadAllText(path).Replace("<version>1</version>", "<version>" + version + "</version>"));
            Assert.Throws<NotSupportedException>(() => coordinator.Load(slot)); Equal(before, coordinator.Capture());
        }
        [TestCase("garbage")] [TestCase("<serenitySave>")] [TestCase("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///invalid'>]><serenitySave>&e;</serenitySave>")]
        public void MalformedXmlIsControlled(string xml) => Assert.Throws<InvalidDataException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(xml)));
        [Test] public void InvalidRuntimeCannotOverwriteLastGoodSave()
        {
            var coordinator = Coordinator(); var expected = coordinator.Capture(); coordinator.Save(slot);
            coordinator.Clock.State.SpeedMultiplier = 4;
            Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.Save(slot)); Equal(expected, store.Load(slot));
        }
        [Test] public void FailedAtomicReplacePreservesLastGoodBytes()
        {
            var coordinator = Coordinator(); coordinator.Save(slot); var path = Path.Combine(directory, slot + ".xml");
            var bytes = File.ReadAllBytes(path); coordinator.Clock.State.CalendarTicks = 17;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => coordinator.Save(slot));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes)); Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void InvalidSlotAndSessionAreRejected()
        {
            Assert.Throws<ArgumentException>(() => store.Save(default, Coordinator().Capture()));
            Assert.Throws<ArgumentException>(() => store.Load(default));
            Assert.Throws<ArgumentException>(() => new SaveCoordinator(default, new GameClock(new GameTimeState()), store));
        }
        [Test] public void OversizedSaveIsRejected()
        {
            Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, slot + ".xml"), new byte[65537]);
            Assert.Throws<InvalidDataException>(() => store.Load(slot));
        }
    }
}
