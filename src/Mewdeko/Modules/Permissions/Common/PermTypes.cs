namespace Mewdeko.Modules.Permissions.Common;

/// <summary>
///     Specifies the primary permission type.
/// </summary>
public enum PrimaryPermissionType
{
    /// <summary>
    ///     User-specific permission.
    /// </summary>
    User,

    /// <summary>
    ///     Channel-specific permission.
    /// </summary>
    Channel,

    /// <summary>
    ///     Role-specific permission.
    /// </summary>
    Role,

    /// <summary>
    ///     Server-wide permission.
    /// </summary>
    Server,

    /// <summary>
    ///     Category-specific permission.
    /// </summary>
    Category
}

/// <summary>
///     Specifies the secondary permission type.
/// </summary>
public enum SecondaryPermissionType
{
    /// <summary>
    ///     Module-specific permission.
    /// </summary>
    Module,

    /// <summary>
    ///     Command-specific permission.
    /// </summary>
    Command,

    /// <summary>
    ///     Permission for all modules.
    /// </summary>
    AllModules
}