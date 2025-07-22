namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Request model for mapping Patreon tiers to Discord roles
/// </summary>
public class PatreonTierMappingRequest
{
    /// <summary>
    ///     Patreon tier ID
    /// </summary>
    public string TierId { get; set; } = null!;

    /// <summary>
    ///     Discord role ID
    /// </summary>
    public ulong RoleId { get; set; }
}