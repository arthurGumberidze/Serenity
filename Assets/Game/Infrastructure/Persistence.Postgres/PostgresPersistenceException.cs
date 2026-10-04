using System;
using System.IO;
using Npgsql;

namespace Game.Infrastructure.Persistence.Postgres
{
    /// <summary>Safe diagnostics only; raw provider exceptions can contain credentials or payloads.</summary>
    public sealed class PostgresPersistenceException : IOException
    {
        public string Operation { get; }
        public string SqlState { get; }
        internal PostgresPersistenceException(string operation, Exception error)
            : base("PostgreSQL " + operation + " failed; SQLSTATE=" + ((error as PostgresException)?.SqlState ?? "unavailable"))
        {
            Operation = operation;
            SqlState = (error as PostgresException)?.SqlState;
        }
    }
}
