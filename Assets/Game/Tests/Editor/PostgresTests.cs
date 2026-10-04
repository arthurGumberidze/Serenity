using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Infrastructure.Persistence.Postgres;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PostgresTests
    {
        private Dictionary<string, string> values;
        [SetUp] public void SetUp() => values = new Dictionary<string, string>
        {
            ["DYNASTYGAME_PG_HOST"] = "127.0.0.1", ["DYNASTYGAME_PG_DATABASE"] = "dynasty_game_dev",
            ["DYNASTYGAME_PG_TEST_DATABASE"] = "dynasty_game_test", ["DYNASTYGAME_PG_USER"] = "test-role"
        };
        private PostgresConfiguration Config(bool test = false) => PostgresConfiguration.FromValues(k => values.TryGetValue(k, out var v) ? v : null, test);
        [Test] public void ConfigurationIsRedactedAndSupportsSeparateDatabase()
        {
            Assert.That(Config().ToString(), Is.EqualTo("PostgreSQL configuration (credentials hidden)"));
            Assert.DoesNotThrow(() => Config(true));
        }
        [TestCase("DYNASTYGAME_PG_HOST")][TestCase("DYNASTYGAME_PG_DATABASE")][TestCase("DYNASTYGAME_PG_USER")]
        public void MissingConfigurationFailsExplicitly(string key)
        { values.Remove(key); Assert.Throws<InvalidOperationException>(() => Config()); }
        [TestCase("0")][TestCase("65536")][TestCase("wrong")]
        public void InvalidPortRejected(string port)
        { values["DYNASTYGAME_PG_PORT"] = port; Assert.Throws<InvalidOperationException>(() => Config()); }
        [Test] public void RemoteHostRejected()
        { values["DYNASTYGAME_PG_HOST"] = "remote.invalid"; Assert.Throws<InvalidOperationException>(() => Config()); }
        [Test] public void IntegrationCannotTargetDevelopment()
        {
            values["DYNASTYGAME_PG_DATABASE"] = "dynasty_game_test";
            Assert.Throws<InvalidOperationException>(() => Config(true));
            values["DYNASTYGAME_PG_TEST_DATABASE"] = "arbitrary";
            Assert.Throws<InvalidOperationException>(() => Config(true));
        }
        [Test] public void UuidConversionDoesNotRegenerateIdentity()
        {
            var id = StableEntityId.Parse("0123456789abcdef0123456789abcdef");
            Assert.That(PostgresSaveStore.ToUuid(id), Is.EqualTo(new Guid("01234567-89ab-cdef-0123-456789abcdef")));
            Assert.That(PostgresSaveStore.FromUuid(PostgresSaveStore.ToUuid(id)), Is.EqualTo(id));
            Assert.Throws<ArgumentException>(() => PostgresSaveStore.ToUuid(default));
            Assert.Throws<FormatException>(() => PostgresSaveStore.FromUuid(Guid.Empty));
        }
        [Test] public void InvalidSaveFailsBeforeAnyConnection()
        {
            var store = new PostgresSaveStore(Config());
            Assert.Throws<ArgumentException>(() => store.Load(default));
            Assert.Throws<ArgumentException>(() => store.Save(default, null));
            Assert.Throws<ArgumentNullException>(() => store.Save(StableEntityId.NewId(), null));
        }
        [Test] public void MigrationOrderIsValidatedBeforeConnection()
        {
            var migrator = new PostgresMigrations(Config());
            Assert.Throws<ArgumentException>(() => migrator.Apply(Array.Empty<PostgresMigration>()));
            Assert.Throws<ArgumentException>(() => migrator.Apply(new[] { new PostgresMigration(2, "bad", "SELECT 1") }));
        }
        [Test] public void MigrationChecksumIsPortableAndDetectsChanges()
        {
            var a = new PostgresMigration(1, "a", "SELECT\r\n1;");
            Assert.That(a.Checksum, Is.EqualTo(new PostgresMigration(1, "a", "SELECT\n1;").Checksum));
            Assert.That(a.Checksum, Is.Not.EqualTo(new PostgresMigration(1, "a", "SELECT 2;").Checksum));
            Assert.That(a.Checksum.Length, Is.EqualTo(64));
        }
        [Test] public void ShippedMigrationsAreVersionedAndPresent()
        {
            var migrations = PostgresMigration.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "Serenity/Postgres"));
            Assert.That(migrations.Length, Is.EqualTo(1));
            Assert.That(migrations[0].Version, Is.EqualTo(1));
        }
        [Test] public void DatabaseProviderDoesNotLeakIntoGameplayAssemblies()
        {
            var names = new[] { "Game.Domain", "Game.Simulation", "Game.Presentation", "Game.ECS" };
            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player).Where(a => names.Contains(a.name)))
                Assert.That(assembly.allReferences.Select(Path.GetFileNameWithoutExtension), Does.Not.Contain("Npgsql"), assembly.name);
            Assert.That(typeof(PostgresSaveStore).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Any(f => f.FieldType.FullName.Contains("GameClock")), Is.False);
        }
        [Test] public void DatabaseErrorsNeverRetainUnsafeProviderMessages()
        {
            var error = new PostgresPersistenceException("save", new TimeoutException("sensitive diagnostic sentinel"));
            Assert.That(error.ToString(), Does.Not.Contain("sensitive diagnostic sentinel"));
            Assert.That(error.InnerException, Is.Null);
            Assert.That(error.Operation, Is.EqualTo("save"));
        }
    }
}
