namespace Mewdeko.Controllers.Common.Administration;

/// <summary>
///     Request model for adding self-assignable roles
/// </summary>
public class AddSelfAssignableRoleRequest
{
    /// <summary>
    ///     The group number for the self-assignable role
    /// </summary>
    public int Group { get; set; }
}