using System.Globalization;
using Microsoft.Data.Sqlite;
using SafeVault.Core.Models;
using SafeVault.Core.Storage;
using SafeVault.Security;

namespace SafeVault.Data;

/// <summary>
/// Stockage du coffre dans un fichier SQLite local.
/// Les données sensibles n'arrivent ici que déjà chiffrées (colonne <c>payload</c>).
/// </summary>
public sealed class SqliteVaultStore : IVaultStore
{
    /// <summary>Version du schéma, stockée dans <c>PRAGMA user_version</c>.</summary>
    public const int SchemaVersion = 1;

    private readonly string _connectionString;

    public SqliteVaultStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Pas de pool : le fichier est libéré après chaque opération (utile pour la sauvegarde/restauration).
            Pooling = false,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public bool Exists()
    {
        if (!File.Exists(DatabasePath))
        {
            return false;
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'vault_meta';";
        if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
        {
            return false;
        }

        command.CommandText = "SELECT COUNT(*) FROM vault_meta;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    public void Initialize(VaultKeyEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (Exists())
        {
            throw new InvalidOperationException("Un coffre existe déjà à cet emplacement.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS vault_meta (
                id                 INTEGER PRIMARY KEY CHECK (id = 1),
                version            INTEGER NOT NULL,
                kdf_salt           BLOB    NOT NULL,
                kdf_memory         INTEGER NOT NULL,
                kdf_iterations     INTEGER NOT NULL,
                kdf_parallelism    INTEGER NOT NULL,
                wrapped_vault_key  BLOB    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS categories (
                id    INTEGER PRIMARY KEY,
                name  TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS entries (
                id           TEXT    PRIMARY KEY,
                category_id  INTEGER NOT NULL REFERENCES categories (id),
                is_favorite  INTEGER NOT NULL DEFAULT 0,
                created_at   TEXT    NOT NULL,
                updated_at   TEXT    NOT NULL,
                payload      BLOB    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                id                       INTEGER PRIMARY KEY CHECK (id = 1),
                auto_lock_minutes        INTEGER NOT NULL,
                clipboard_clear_seconds  INTEGER NOT NULL
            );
            """);

        Execute(connection, transaction, $"PRAGMA user_version = {SchemaVersion};");

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO vault_meta (id, version, kdf_salt, kdf_memory, kdf_iterations, kdf_parallelism, wrapped_vault_key)
                VALUES (1, $version, $salt, $memory, $iterations, $parallelism, $wrapped);
                """;
            AddEnvelopeParameters(command, envelope);
            command.ExecuteNonQuery();
        }

        foreach (var category in Category.Defaults)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT OR IGNORE INTO categories (id, name) VALUES ($id, $name);";
            command.Parameters.AddWithValue("$id", category.Id);
            command.Parameters.AddWithValue("$name", category.Name);
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT OR REPLACE INTO settings (id, auto_lock_minutes, clipboard_clear_seconds) VALUES (1, $lock, $clip);";
            command.Parameters.AddWithValue("$lock", VaultSettings.Default.AutoLockMinutes);
            command.Parameters.AddWithValue("$clip", VaultSettings.Default.ClipboardClearSeconds);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public VaultKeyEnvelope? LoadEnvelope()
    {
        if (!Exists())
        {
            return null;
        }

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT version, kdf_salt, kdf_memory, kdf_iterations, kdf_parallelism, wrapped_vault_key
            FROM vault_meta WHERE id = 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new VaultKeyEnvelope(
            Version: reader.GetInt32(0),
            Salt: reader.GetFieldValue<byte[]>(1),
            Kdf: new KdfParameters(reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)),
            WrappedKey: reader.GetFieldValue<byte[]>(5));
    }

    public void UpdateEnvelope(VaultKeyEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE vault_meta
            SET version = $version, kdf_salt = $salt, kdf_memory = $memory, kdf_iterations = $iterations,
                kdf_parallelism = $parallelism, wrapped_vault_key = $wrapped
            WHERE id = 1;
            """;
        AddEnvelopeParameters(command, envelope);
        EnsureOneRowAffected(command.ExecuteNonQuery(), "Aucun coffre à mettre à jour.");
    }

    public IReadOnlyList<Category> GetCategories()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM categories ORDER BY id;";

        var categories = new List<Category>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            categories.Add(new Category(reader.GetInt32(0), reader.GetString(1)));
        }

        return categories;
    }

    public IReadOnlyList<EncryptedEntry> GetEntries()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, category_id, is_favorite, created_at, updated_at, payload FROM entries;";

        var entries = new List<EncryptedEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new EncryptedEntry(
                Id: Guid.Parse(reader.GetString(0)),
                CategoryId: reader.GetInt32(1),
                IsFavorite: reader.GetInt64(2) != 0,
                CreatedAt: ParseDate(reader.GetString(3)),
                UpdatedAt: ParseDate(reader.GetString(4)),
                Payload: reader.GetFieldValue<byte[]>(5)));
        }

        return entries;
    }

    public void InsertEntry(EncryptedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO entries (id, category_id, is_favorite, created_at, updated_at, payload)
            VALUES ($id, $category, $favorite, $created, $updated, $payload);
            """;
        AddEntryParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public void UpdateEntry(EncryptedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE entries
            SET category_id = $category, is_favorite = $favorite, created_at = $created,
                updated_at = $updated, payload = $payload
            WHERE id = $id;
            """;
        AddEntryParameters(command, entry);
        EnsureOneRowAffected(command.ExecuteNonQuery(), "Entrée introuvable.");
    }

    public void DeleteEntry(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM entries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.ExecuteNonQuery();
    }

    public void ReplaceEntries(IReadOnlyList<EncryptedEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, "DELETE FROM entries;");

        foreach (var entry in entries)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO entries (id, category_id, is_favorite, created_at, updated_at, payload)
                VALUES ($id, $category, $favorite, $created, $updated, $payload);
                """;
            AddEntryParameters(command, entry);
            command.ExecuteNonQuery();
        }

        // Sans Commit (exception), la transaction est annulée à la libération : le coffre reste intact.
        transaction.Commit();
    }

    public VaultSettings GetSettings()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT auto_lock_minutes, clipboard_clear_seconds FROM settings WHERE id = 1;";

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new VaultSettings { AutoLockMinutes = reader.GetInt32(0), ClipboardClearSeconds = reader.GetInt32(1) }
            : VaultSettings.Default;
    }

    public void SaveSettings(VaultSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO settings (id, auto_lock_minutes, clipboard_clear_seconds) VALUES (1, $lock, $clip);";
        command.Parameters.AddWithValue("$lock", settings.AutoLockMinutes);
        command.Parameters.AddWithValue("$clip", settings.ClipboardClearSeconds);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Les pages libérées (entrées supprimées ou modifiées) sont écrasées par des zéros.
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA secure_delete = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AddEnvelopeParameters(SqliteCommand command, VaultKeyEnvelope envelope)
    {
        command.Parameters.AddWithValue("$version", envelope.Version);
        command.Parameters.AddWithValue("$salt", envelope.Salt);
        command.Parameters.AddWithValue("$memory", envelope.Kdf.MemoryKiB);
        command.Parameters.AddWithValue("$iterations", envelope.Kdf.Iterations);
        command.Parameters.AddWithValue("$parallelism", envelope.Kdf.Parallelism);
        command.Parameters.AddWithValue("$wrapped", envelope.WrappedKey);
    }

    private static void AddEntryParameters(SqliteCommand command, EncryptedEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
        command.Parameters.AddWithValue("$category", entry.CategoryId);
        command.Parameters.AddWithValue("$favorite", entry.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$created", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", entry.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$payload", entry.Payload);
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void EnsureOneRowAffected(int rows, string message)
    {
        if (rows != 1)
        {
            throw new InvalidOperationException(message);
        }
    }
}
