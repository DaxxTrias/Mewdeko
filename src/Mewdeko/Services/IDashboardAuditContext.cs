using System.Text.Json.Nodes;

namespace Mewdeko.Services;

/// <summary>
///     Per-request scratch space that lets controllers hand the audit filter
///     before/after snapshots for dashboard mutations.
/// </summary>
public interface IDashboardAuditContext
{
    /// <summary>
    ///     The redacted snapshot of the resource state taken before the mutation, if any.
    /// </summary>
    JsonNode? Before { get; }

    /// <summary>
    ///     The redacted snapshot of the resource state taken after the mutation, if any.
    /// </summary>
    JsonNode? After { get; }

    /// <summary>
    ///     Whether a before snapshot was recorded for this request.
    /// </summary>
    bool HasBefore { get; }

    /// <summary>
    ///     Whether an after snapshot was recorded for this request.
    /// </summary>
    bool HasAfter { get; }

    /// <summary>
    ///     Eagerly snapshots the resource state before the mutation.
    /// </summary>
    /// <param name="state">The current state of the resource being changed.</param>
    void RecordBefore(object? state);

    /// <summary>
    ///     Eagerly snapshots the resource state after the mutation.
    /// </summary>
    /// <param name="state">The new state of the resource after the change.</param>
    void RecordAfter(object? state);
}

/// <inheritdoc />
public sealed class DashboardAuditContext : IDashboardAuditContext
{
    /// <inheritdoc />
    public JsonNode? Before { get; private set; }

    /// <inheritdoc />
    public JsonNode? After { get; private set; }

    /// <inheritdoc />
    public bool HasBefore { get; private set; }

    /// <inheritdoc />
    public bool HasAfter { get; private set; }

    /// <inheritdoc />
    public void RecordBefore(object? state)
    {
        Before = AuditChangeSerializer.Snapshot(state);
        HasBefore = true;
    }

    /// <inheritdoc />
    public void RecordAfter(object? state)
    {
        After = AuditChangeSerializer.Snapshot(state);
        HasAfter = true;
    }
}
