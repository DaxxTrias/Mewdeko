namespace Mewdeko.Common.Attributes.DB;

/// <summary>
///     Used for specifying when a db column is new and to not copy it
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class NewPropertyAttribute : Attribute;