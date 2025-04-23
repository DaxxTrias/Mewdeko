using System.Diagnostics;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Permissions.Common;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a permission.
/// </summary>
[DebuggerDisplay("{global::Mewdeko.Modules.Permissions.PermissionExtensions.GetCommand(this)}",
    Target = typeof(Permission))]
public class Permission : DbEntity
{
    /// <summary>
    ///     Gets or sets the previous permission.
    /// </summary>
    public Permission Previous { get; set; } = null;

    /// <summary>
    ///     Gets or sets the next permission.
    /// </summary>
    public Permission Next { get; set; } = null;

    /// <summary>
    ///     Gets or sets the primary target of the permission.
    /// </summary>
    public PrimaryPermissionType PrimaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the primary target ID.
    /// </summary>
    public ulong PrimaryTargetId { get; set; }

    /// <summary>
    ///     Gets or sets the secondary target of the permission.
    /// </summary>
    public SecondaryPermissionType SecondaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the name of the secondary target.
    /// </summary>
    public string? SecondaryTargetName { get; set; }

    /// <summary>
    ///     Gets or sets the state of the permission.
    /// </summary>
    public bool State { get; set; }
}

/// <summary>
///     Represents an indexed permission.
/// </summary>
[DebuggerDisplay("{PrimaryTarget}{SecondaryTarget} {SecondaryTargetName} {State} {PrimaryTargetId}")]
public class Permissionv2 : DbEntity, IIndexed
{
    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the primary target of the permission.
    /// </summary>
    public PrimaryPermissionType PrimaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the primary target ID.
    /// </summary>
    public ulong PrimaryTargetId { get; set; }

    /// <summary>
    ///     Gets or sets the secondary target of the permission.
    /// </summary>
    public SecondaryPermissionType SecondaryTarget { get; set; }

    /// <summary>
    ///     Gets or sets the name of the secondary target.
    /// </summary>
    public string? SecondaryTargetName { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this is a custom command.
    /// </summary>
    public bool IsCustomCommand { get; set; } = false;

    /// <summary>
    ///     Gets or sets the state of the permission.
    /// </summary>
    public bool State { get; set; }

    /// <summary>
    ///     Gets or sets the index of the permission.
    /// </summary>
    public int Index { get; set; }
}

/// <summary>
///     Represents an indexed entity.
/// </summary>
public interface IIndexed
{
    /// <summary>
    ///     Gets or sets the index of the entity.
    /// </summary>
    int Index { get; set; }
}