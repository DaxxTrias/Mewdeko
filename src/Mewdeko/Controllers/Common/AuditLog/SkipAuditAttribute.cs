namespace Mewdeko.Controllers.Common.AuditLog;

/// <summary>
///     Marks a controller or action that the dashboard audit filter must ignore.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class SkipAuditAttribute : Attribute;
