using System;
using System.IO;
using Game.Domain;
using Game.Simulation;
using Npgsql;
using NpgsqlTypes;

namespace Game.Infrastructure.Persistence.Postgres
{
    /// <summary>Checkpoint-only storage. No runtime references; each call closes its own connection.</summary>
    public sealed class PostgresSaveStore : ISaveStore
    {
        private readonly PostgresConfiguration configuration;
        private readonly SaveXmlCodec codec = new SaveXmlCodec();
        public PostgresSaveStore(PostgresConfiguration configuration)
        { this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); }

        public static Guid ToUuid(StableEntityId id)
        {
            if (!id.IsValid) throw new ArgumentException("A non-empty stable ID is required.", nameof(id));
            return Guid.ParseExact(id.ToString(), "N");
        }
        public static StableEntityId FromUuid(Guid value) => StableEntityId.Parse(value.ToString("N"));

        public void Save(StableEntityId slotId, SaveSnapshot snapshot)
        {
            var slot = ToUuid(slotId);
            var payload = codec.Serialize(snapshot); // Validate before opening a connection.
            try
            {
                using (var connection = configuration.Open())
                using (var command = new NpgsqlCommand(@"
INSERT INTO serenity.save_sessions (slot_id, session_id, save_format_version, payload)
VALUES (@slot, @session, @version, @payload)
ON CONFLICT (slot_id) DO UPDATE SET session_id = EXCLUDED.session_id,
save_format_version = EXCLUDED.save_format_version, payload = EXCLUDED.payload, updated_at = clock_timestamp()", connection))
                {
                    command.Parameters.AddWithValue("slot", NpgsqlDbType.Uuid, slot);
                    command.Parameters.AddWithValue("session", NpgsqlDbType.Uuid, ToUuid(snapshot.SessionId));
                    command.Parameters.AddWithValue("version", NpgsqlDbType.Integer, snapshot.FormatVersion);
                    command.Parameters.AddWithValue("payload", NpgsqlDbType.Bytea, payload);
                    command.ExecuteNonQuery(); // A single atomic statement/implicit PostgreSQL transaction.
                }
            }
            catch (Exception ex) when (ex is NpgsqlException || ex is TimeoutException)
            { throw new PostgresPersistenceException("save", ex); }
        }

        public SaveSnapshot Load(StableEntityId slotId)
        {
            var slot = ToUuid(slotId);
            try
            {
                using (var connection = configuration.Open())
                using (var command = new NpgsqlCommand(@"
SELECT session_id, save_format_version, payload FROM serenity.save_sessions WHERE slot_id = @slot", connection))
                {
                    command.Parameters.AddWithValue("slot", NpgsqlDbType.Uuid, slot);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new FileNotFoundException("Save slot does not exist.");
                        if (reader.GetInt32(1) != SaveSnapshot.CurrentVersion)
                            throw new NotSupportedException("Unsupported save format version.");
                        var snapshot = codec.Deserialize((byte[])reader.GetValue(2));
                        if (reader.GetGuid(0) == Guid.Empty || FromUuid(reader.GetGuid(0)) != snapshot.SessionId)
                            throw new InvalidDataException("Stored session identity does not match the snapshot.");
                        return snapshot;
                    }
                }
            }
            catch (Exception ex) when (ex is NpgsqlException || ex is TimeoutException)
            { throw new PostgresPersistenceException("load", ex); }
        }
    }
}
