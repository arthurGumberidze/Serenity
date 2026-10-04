using System;
using Npgsql;

namespace Game.Infrastructure.Persistence.Postgres
{
    /// <summary>Process-local configuration. Never serialize this object or log credentials.</summary>
    public sealed class PostgresConfiguration
    {
        private readonly string connectionString;
        private PostgresConfiguration(string value) { connectionString = value; }

        public static PostgresConfiguration FromEnvironment(bool test = false)
            => FromValues(Environment.GetEnvironmentVariable, test);

        public static PostgresConfiguration FromValues(Func<string, string> read, bool test = false)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            var host = read("DYNASTYGAME_PG_HOST");
            var database = read(test ? "DYNASTYGAME_PG_TEST_DATABASE" : "DYNASTYGAME_PG_DATABASE");
            var user = read("DYNASTYGAME_PG_USER");
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("PostgreSQL not configured: set DYNASTYGAME_PG_HOST, DATABASE (or TEST_DATABASE) and USER.");
            if (host != "localhost" && host != "127.0.0.1" && host != "::1")
                throw new InvalidOperationException("U04A permits only a local PostgreSQL host.");
            if (test && (database == read("DYNASTYGAME_PG_DATABASE") || !database.EndsWith("_test", StringComparison.Ordinal)))
                throw new InvalidOperationException("Integration database must be separate from development and end in _test.");
            var portText = read("DYNASTYGAME_PG_PORT");
            var port = 5432;
            if (!string.IsNullOrEmpty(portText) && (!int.TryParse(portText, out port) || port < 1 || port > 65535))
                throw new InvalidOperationException("Invalid PostgreSQL port.");
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host, Port = port, Database = database, Username = user,
                Password = read("DYNASTYGAME_PG_PASSWORD") ?? "", Timeout = 5, CommandTimeout = 15,
                Pooling = false, IncludeErrorDetail = false, LogParameters = false,
                ApplicationName = "Serenity.Persistence", SslMode = SslMode.Disable
            };
            return new PostgresConfiguration(builder.ConnectionString);
        }

        internal NpgsqlConnection Open()
        {
            var connection = new NpgsqlConnection(connectionString);
            try { connection.Open(); return connection; }
            catch { connection.Dispose(); throw; }
        }
        public override string ToString() => "PostgreSQL configuration (credentials hidden)";
    }
}
