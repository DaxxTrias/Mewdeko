using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OwnerOnlyAttribute : PreconditionAttribute
{
    public bool IsOwnerOnly { get; set; } = true;

    public OwnerOnlyAttribute() { }
    public OwnerOnlyAttribute(bool isOwnerOnly) => IsOwnerOnly = isOwnerOnly;

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
        CommandInfo executingCommand, IServiceProvider services)
    {
        var creds = services.GetService<IBotCredentials>();

        return Task.FromResult(
            creds != null && (creds.IsOwner(context.User) || context.Client.CurrentUser.Id == context.User.Id)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(
                    "Not owner\n"));
    }
}