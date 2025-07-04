namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Request model for Patreon operations
/// </summary>
public class PatreonOperationRequest
{
    /// <summary>
    ///     Operation to perform (sync, refresh_token, manual_announcement, sync_roles)
    /// </summary>
    public string Operation { get; set; } = null!;
}