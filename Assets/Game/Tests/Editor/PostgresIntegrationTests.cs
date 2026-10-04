using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Domain;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Infrastructure.Persistence.Postgres;
using Game.Simulation;
using Game.Simulation.Time;
using Npgsql;
using NpgsqlTypes;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    [Category("PostgresIntegration")]
    public sealed class PostgresIntegrationTests
    {
        private PostgresConfiguration configuration;
        private PostgresSaveStore store;
        private PostgresMigration[] migrations;
        private StableEntityId slot;
        private static SaveSnapshot Snapshot(int speed = 5, bool paused = true)
            => new SaveSnapshot(1, StableEntityId.NewId(), new GameTimeState(9007199254740993, long.MaxValue - 1, speed, paused), 321);

        [OneTimeSetUp] public void Configure()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DYNASTYGAME_PG_TEST_DATABASE")))
                Assert.Ignore("PostgreSQL integration not configured: DYNASTYGAME_PG_TEST_DATABASE is absent.");
            configuration = PostgresConfiguration.FromEnvironment(true);
            if (Environment.GetEnvironmentVariable("SERENITY_PG_EXPECT_FRESH") == "1")
                Assert.That(Scalar("SELECT to_regnamespace('serenity') IS NULL"), Is.EqualTo(true), "Managed test database must start without a Serenity schema.");
            migrations = PostgresMigration.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "Serenity/Postgres"));
            new PostgresMigrations(configuration).Apply(migrations); // Connection failures are failures, never skipped.
            store = new PostgresSaveStore(configuration);
        }
        [SetUp] public void SetUp() => slot = StableEntityId.NewId();
        [TearDown] public void CleanOwnSlot()
        {
            if (configuration != null)
                Execute("DELETE FROM serenity.save_sessions WHERE slot_id = @slot", slot);
        }
        private void Execute(string sql, StableEntityId id, byte[] payload = null)
        {
            using (var connection = configuration.Open())
            using (var command = new NpgsqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("slot", NpgsqlDbType.Uuid, PostgresSaveStore.ToUuid(id));
                if (payload != null) command.Parameters.AddWithValue("payload", NpgsqlDbType.Bytea, payload);
                command.ExecuteNonQuery();
            }
        }
        private object Scalar(string sql)
        {
            using (var connection = configuration.Open())
            using (var command = new NpgsqlCommand(sql, connection)) return command.ExecuteScalar();
        }
        private static void Equal(SaveSnapshot a, SaveSnapshot b)
        {
            Assert.That(b.FormatVersion, Is.EqualTo(a.FormatVersion));
            Assert.That(b.SessionId, Is.EqualTo(a.SessionId));
            Assert.That(b.CalendarTicks, Is.EqualTo(a.CalendarTicks));
            Assert.That(b.BiologicalTicks, Is.EqualTo(a.BiologicalTicks));
            Assert.That(b.SpeedMultiplier, Is.EqualTo(a.SpeedMultiplier));
            Assert.That(b.IsPaused, Is.EqualTo(a.IsPaused));
            Assert.That(b.BiologicalMultiplier, Is.EqualTo(a.BiologicalMultiplier));
        }
        [Test] public void ConnectionAndMigrationHistoryAreAvailable()
        {
            Assert.That(Scalar("SELECT current_database()"), Is.EqualTo(Environment.GetEnvironmentVariable("DYNASTYGAME_PG_TEST_DATABASE")));
            Assert.That(Scalar("SELECT count(*) FROM serenity.schema_migrations"), Is.EqualTo(1L));
            TestContext.WriteLine("PostgreSQL server version: " + Scalar("SHOW server_version"));
        }
        [Test] public void MigrationsAreIdempotentAndConcurrentSafe()
        {
            var migrator = new PostgresMigrations(configuration);
            Assert.That(migrator.Apply(migrations), Is.EqualTo(1));
            Task.WaitAll(Task.Run(() => migrator.Apply(migrations)), Task.Run(() => migrator.Apply(migrations)));
            Assert.That(Scalar("SELECT count(*) FROM serenity.schema_migrations"), Is.EqualTo(1L));
        }
        [Test] public void AlteredOrNewerMigrationHistoryIsRejected()
        {
            Assert.Throws<InvalidDataException>(() => new PostgresMigrations(configuration).Apply(new[] {
                new PostgresMigration(1, migrations[0].Name, migrations[0].Sql + "\n-- changed") }));
        }
        [Test] public void FailedMigrationRollsBackDdlAndHistory()
        {
            var bad = new PostgresMigration(2, "002_test_failure.sql",
                "CREATE TABLE serenity.u04a_rollback_probe (id integer); SELECT 1 / 0;");
            var error = Assert.Throws<PostgresPersistenceException>(() => new PostgresMigrations(configuration).Apply(migrations.Concat(new[] { bad }).ToArray()));
            Assert.That(error.SqlState, Is.EqualTo("22012"));
            Assert.That(Scalar("SELECT to_regclass('serenity.u04a_rollback_probe') IS NULL"), Is.EqualTo(true));
            Assert.That(Scalar("SELECT count(*) FROM serenity.schema_migrations"), Is.EqualTo(1L));
        }
        [TestCase(1, false)][TestCase(2, true)][TestCase(3, false)][TestCase(5, true)][TestCase(10, false)]
        public void SaveLoadSurvivesNewStoreAndConnection(int speed, bool paused)
        {
            var snapshot = Snapshot(speed, paused);
            new PostgresSaveStore(configuration).Save(slot, snapshot);
            Equal(snapshot, new PostgresSaveStore(configuration).Load(slot));
        }
        [TestCase("Character")][TestCase("Dynasty")][TestCase("City")][TestCase("HistoricalEvent")]
        public void FutureEntityIdentityUsesSameUuidMapping(string futureOwner)
        {
            // Mapping contract only: intentionally no speculative gameplay tables/models.
            var id = StableEntityId.NewId();
            using (var connection = configuration.Open())
            using (var command = new NpgsqlCommand("SELECT CAST(@id AS uuid)", connection))
            {
                command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, PostgresSaveStore.ToUuid(id));
                Assert.That(PostgresSaveStore.FromUuid((Guid)command.ExecuteScalar()), Is.EqualTo(id), futureOwner);
            }
        }
        [Test] public void OverwriteIsAtomicAndKeepsCreationTimestamp()
        {
            store.Save(slot, Snapshot());
            DateTime created;
            using (var connection = configuration.Open())
            using (var command = new NpgsqlCommand("SELECT created_at FROM serenity.save_sessions WHERE slot_id=@slot", connection))
            {
                command.Parameters.AddWithValue("slot", PostgresSaveStore.ToUuid(slot));
                created = (DateTime)command.ExecuteScalar();
            }
            var replacement = Snapshot(10, false); store.Save(slot, replacement); Equal(replacement, store.Load(slot));
            using (var connection = configuration.Open())
            using (var command = new NpgsqlCommand("SELECT created_at, updated_at FROM serenity.save_sessions WHERE slot_id=@slot", connection))
            {
                command.Parameters.AddWithValue("slot", PostgresSaveStore.ToUuid(slot));
                using (var reader = command.ExecuteReader())
                { Assert.That(reader.Read(), Is.True); Assert.That(reader.GetDateTime(0), Is.EqualTo(created)); Assert.That(reader.GetDateTime(1), Is.GreaterThanOrEqualTo(created)); }
            }
        }
        [Test] public void MissingSaveIsExplicit() => Assert.Throws<FileNotFoundException>(() => store.Load(slot));
        [Test] public void ConnectionFailureIsControlledAndPreservesRuntime()
        {
            var unavailable = PostgresConfiguration.FromValues(key => key == "DYNASTYGAME_PG_PORT" ? "1" : Environment.GetEnvironmentVariable(key), true);
            var coordinator = new SaveCoordinator(StableEntityId.NewId(), new GameClock(new GameTimeState(), new GameTimeSettings()), new PostgresSaveStore(unavailable));
            var before = coordinator.Capture(); var clock = coordinator.Clock;
            Assert.Throws<PostgresPersistenceException>(() => coordinator.Save(slot));
            var error = Assert.Throws<PostgresPersistenceException>(() => coordinator.Load(slot));
            Assert.That(error.InnerException, Is.Null);
            var credential = Environment.GetEnvironmentVariable("DYNASTYGAME_PG_PASSWORD");
            Assert.That(string.IsNullOrEmpty(credential) || !error.ToString().Contains(credential), Is.True, "Diagnostics must be redacted.");
            Assert.That(coordinator.Clock, Is.SameAs(clock)); Equal(before, coordinator.Capture());
        }
        [TestCase("xml")][TestCase("id")][TestCase("ticks")][TestCase("version")][TestCase("metadataVersion")][TestCase("metadataId")]
        public void CorruptionCannotPartiallyRestoreRuntime(string kind)
        {
            var snapshot = Snapshot(); store.Save(slot, snapshot);
            var xml = Encoding.UTF8.GetString(new SaveXmlCodec().Serialize(snapshot));
            if (kind == "metadataVersion") Execute("UPDATE serenity.save_sessions SET save_format_version=999 WHERE slot_id=@slot", slot);
            else if (kind == "metadataId") Execute("UPDATE serenity.save_sessions SET session_id=slot_id WHERE slot_id=@slot", slot);
            else
            {
                if (kind == "xml") xml = "<broken";
                if (kind == "id") xml = xml.Replace(snapshot.SessionId.ToString(), "invalid");
                if (kind == "ticks") xml = xml.Replace("9007199254740993", "-1");
                if (kind == "version") xml = xml.Replace("<version>1</version>", "<version>999</version>");
                Execute("UPDATE serenity.save_sessions SET payload=@payload WHERE slot_id=@slot", slot, Encoding.UTF8.GetBytes(xml));
            }
            var coordinator = new SaveCoordinator(StableEntityId.NewId(), new GameClock(new GameTimeState(), new GameTimeSettings()), store);
            var clock = coordinator.Clock; var session = coordinator.SessionId; var before = coordinator.Capture();
            if (kind == "version" || kind == "metadataVersion") Assert.Throws<NotSupportedException>(() => coordinator.Load(slot));
            else Assert.Throws<InvalidDataException>(() => coordinator.Load(slot));
            Assert.That(coordinator.Clock, Is.SameAs(clock)); Assert.That(coordinator.SessionId, Is.EqualTo(session)); Equal(before, coordinator.Capture());
        }
        [Test] public void ConcurrentUpsertsProduceOneCompleteSnapshot()
        {
            var snapshots = Enumerable.Range(0, 8).Select(_ => Snapshot()).ToArray();
            Task.WaitAll(snapshots.Select(s => Task.Run(() => new PostgresSaveStore(configuration).Save(slot, s))).ToArray());
            var result = store.Load(slot); Equal(snapshots.Single(s => s.SessionId == result.SessionId), result);
        }
        [Test] public void DatabaseFailureRollsBackPreviouslyWrittenPayload()
        {
            var snapshot = Snapshot(); store.Save(slot, snapshot);
            using (var connection = configuration.Open())
            using (var transaction = connection.BeginTransaction())
            {
                using (var command = new NpgsqlCommand("UPDATE serenity.save_sessions SET payload=@payload WHERE slot_id=@slot", connection, transaction))
                {
                    command.Parameters.AddWithValue("payload", Encoding.UTF8.GetBytes("broken"));
                    command.Parameters.AddWithValue("slot", PostgresSaveStore.ToUuid(slot)); command.ExecuteNonQuery();
                }
                using (var command = new NpgsqlCommand("SELECT 1 / 0", connection, transaction))
                    Assert.Throws<PostgresException>(() => command.ExecuteNonQuery());
                transaction.Rollback();
            }
            Equal(snapshot, store.Load(slot));
        }
        [Test] public void CoordinatorRebindsOnlyAfterSuccessfulDatabaseLoad()
        {
            var snapshot = Snapshot(); store.Save(slot, snapshot);
            var coordinator = new SaveCoordinator(StableEntityId.NewId(), new GameClock(new GameTimeState(), new GameTimeSettings()), store);
            var old = coordinator.Clock; coordinator.Load(slot);
            Assert.That(coordinator.Clock, Is.Not.SameAs(old)); Equal(snapshot, coordinator.Capture());
        }
    }
}
