CREATE TABLE serenity.save_sessions (
    slot_id uuid PRIMARY KEY CHECK (slot_id <> '00000000-0000-0000-0000-000000000000'),
    session_id uuid NOT NULL CHECK (session_id <> '00000000-0000-0000-0000-000000000000'),
    save_format_version integer NOT NULL CHECK (save_format_version > 0),
    payload bytea NOT NULL CHECK (octet_length(payload) BETWEEN 1 AND 65536),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
