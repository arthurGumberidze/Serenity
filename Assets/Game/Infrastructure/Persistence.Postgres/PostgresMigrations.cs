using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace Game.Infrastructure.Persistence.Postgres
{
    public sealed class PostgresMigration
    {
        public int Version { get; }
        public string Name { get; }
        public string Sql { get; }
        public string Checksum { get; }
        public PostgresMigration(int version, string name, string sql)
        {
            if (version < 1 || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Migration requires a positive version, name and SQL.");
            Version = version; Name = name; Sql = sql.Replace("\r\n", "\n");
            using (var hash = SHA256.Create())
                Checksum = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Sql))).Replace("-", "").ToLowerInvariant();
        }
        public static PostgresMigration[] LoadDirectory(string directory)
            => Directory.GetFiles(directory, "*.sql").OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(path => new PostgresMigration(int.Parse(Path.GetFileName(path).Split('_')[0]),
                    Path.GetFileName(path), File.ReadAllText(path))).ToArray();
    }

    /// <summary>Explicit setup step; migrations never run on a simulation tick or implicitly during Save.</summary>
    public sealed class PostgresMigrations
    {
        private readonly PostgresConfiguration configuration;
        public PostgresMigrations(PostgresConfiguration configuration)
        { this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); }

        public int Apply(IReadOnlyList<PostgresMigration> migrations)
        {
            if (migrations == null || migrations.Count == 0) throw new ArgumentException("Migrations required.");
            for (var i = 0; i < migrations.Count; i++)
                if (migrations[i] == null || migrations[i].Version != i + 1)
                    throw new ArgumentException("Migrations must be contiguous and ordered from version 1.");
            try
            {
                using (var connection = configuration.Open())
                using (var transaction = connection.BeginTransaction())
                {
                    // Serialize concurrent migrators, including first schema creation.
                    Execute(connection, transaction, "SELECT pg_advisory_xact_lock(739184042)");
                    Execute(connection, transaction, @"CREATE SCHEMA IF NOT EXISTS serenity;
CREATE TABLE IF NOT EXISTS serenity.schema_migrations (
version integer PRIMARY KEY, name text NOT NULL, checksum text NOT NULL,
applied_at timestamptz NOT NULL DEFAULT clock_timestamp())");
                    var applied = 0;
                    using (var command = new NpgsqlCommand("SELECT version, name, checksum FROM serenity.schema_migrations ORDER BY version", connection, transaction))
                    using (var reader = command.ExecuteReader())
                        while (reader.Read())
                        {
                            if (applied >= migrations.Count || reader.GetInt32(0) != applied + 1
                                || reader.GetString(1) != migrations[applied].Name || reader.GetString(2) != migrations[applied].Checksum)
                                throw new InvalidDataException("Database migration history is newer, altered or non-contiguous.");
                            applied++;
                        }
                    for (var i = applied; i < migrations.Count; i++)
                    {
                        var migration = migrations[i];
                        Execute(connection, transaction, migration.Sql); // Repository-owned SQL only, never user values.
                        using (var command = new NpgsqlCommand("INSERT INTO serenity.schema_migrations(version, name, checksum) VALUES (@version, @name, @checksum)", connection, transaction))
                        {
                            command.Parameters.AddWithValue("version", NpgsqlDbType.Integer, migration.Version);
                            command.Parameters.AddWithValue("name", NpgsqlDbType.Text, migration.Name);
                            command.Parameters.AddWithValue("checksum", NpgsqlDbType.Text, migration.Checksum);
                            command.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                    return migrations.Count;
                }
            }
            catch (Exception ex) when (ex is NpgsqlException || ex is TimeoutException)
            { throw new PostgresPersistenceException("migrate", ex); }
        }
        private static void Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
        { using (var command = new NpgsqlCommand(sql, connection, transaction)) command.ExecuteNonQuery(); }
    }
}
