using Npgsql;

namespace Saas.Api.Tests.Drivers;

/// <summary>
/// Queries Postgres directly, as the runtime role.
/// </summary>
/// <remarks>
/// The point of these queries is to check the isolation layer the application
/// cannot vouch for itself. Connecting as <c>saas_app</c> -- the same non-owner
/// login the API uses -- and setting the tenant by hand tests the row-level
/// security policies with no EF query filter anywhere in the picture, so a pass
/// means the database is enforcing it rather than the ORM.
/// </remarks>
public class DatabaseDriver(TestSettings settings)
{
    /// <summary>Counts rows in a table as a given tenant would see them.</summary>
    public async Task<int> CountAsTenantAsync(string table, Guid? organizationId, Guid? userId = null)
    {
        await using var connection = new NpgsqlConnection(settings.Postgres);
        await connection.OpenAsync();

        await SetTenantAsync(connection, organizationId, userId);

        await using var command = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\"", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>True when the role may read the table at all.</summary>
    public async Task<bool> CanReadAsync(string connectionString, string qualifiedTable)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                $"SELECT 1 FROM {qualifiedTable} LIMIT 1", connection);
            await command.ExecuteScalarAsync();

            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == "42501")
        {
            // insufficient_privilege
            return false;
        }
    }

    /// <summary>Whether row-level security is both enabled and forced on a table.</summary>
    public async Task<(bool Enabled, bool Forced)> GetRowLevelSecurityAsync(string table)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname = @table",
            connection);
        command.Parameters.AddWithValue("table", table);

        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync()
            ? (reader.GetBoolean(0), reader.GetBoolean(1))
            : (false, false);
    }

    /// <summary>
    /// Runs a statement as the table owner with the tenant set. Row-level
    /// security is forced on Memberships even for the owner, so every write
    /// has to go through here for its WITH CHECK to pass.
    /// </summary>
    public async Task<int> ExecuteAsTenantAsync(
        Guid organizationId,
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        Guid? userId = null)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await SetTenantAsync(connection, organizationId, userId);

        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>Adds a membership with a raw integer role, which may be undefined.</summary>
    public Task<int> AddMembershipAsync(Guid organizationId, Guid userId, int role) =>
        ExecuteAsTenantAsync(
            organizationId,
            "INSERT INTO \"Memberships\" (\"Id\", \"OrganizationId\", \"UserId\", \"Role\", \"CreatedDate\") " +
            "VALUES (@id, @organization, @user, @role, now())",
            new Dictionary<string, object>
            {
                ["id"] = Guid.CreateVersion7(),
                ["organization"] = organizationId,
                ["user"] = userId,
                ["role"] = role,
            });

    /// <summary>Sets a membership's raw integer role.</summary>
    public Task<int> SetMembershipRoleAsync(Guid organizationId, Guid userId, int role) =>
        ExecuteAsTenantAsync(
            organizationId,
            "UPDATE \"Memberships\" SET \"Role\" = @role, \"ModifiedDate\" = now() " +
            "WHERE \"OrganizationId\" = @organization AND \"UserId\" = @user",
            new Dictionary<string, object>
            {
                ["organization"] = organizationId,
                ["user"] = userId,
                ["role"] = role,
            });

    /// <summary>Removes a membership row.</summary>
    public Task<int> RemoveMembershipAsync(Guid organizationId, Guid userId) =>
        ExecuteAsTenantAsync(
            organizationId,
            "DELETE FROM \"Memberships\" WHERE \"OrganizationId\" = @organization AND \"UserId\" = @user",
            new Dictionary<string, object>
            {
                ["organization"] = organizationId,
                ["user"] = userId,
            });

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid? organizationId,
        Guid? userId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @organization, false), " +
            "       set_config('app.user_id', @user, false)",
            connection);

        command.Parameters.AddWithValue("organization", organizationId?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("user", userId?.ToString() ?? string.Empty);

        await command.ExecuteNonQueryAsync();
    }
}
