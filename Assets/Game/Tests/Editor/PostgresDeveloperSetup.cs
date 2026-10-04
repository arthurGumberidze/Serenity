using System.IO;
using Game.Infrastructure.Persistence.Postgres;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Batchmode developer setup; compiled for Editor only.</summary>
    public static class PostgresDeveloperSetup
    {
        public static void Migrate()
        {
            var version = new PostgresMigrations(PostgresConfiguration.FromEnvironment()).Apply(
                PostgresMigration.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "Serenity/Postgres")));
            Debug.Log("Serenity PostgreSQL schema version: " + version);
        }
    }
}
