using Discord.Commands;
using DataModel;
using Microsoft.Extensions.DependencyInjection;
using LinqToDB;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to require a user to be in dragon mode to execute a command or method. Used rarely, but mostly in beta commands.
/// </summary>
public class RequireDragonAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks the permissions of the command or method before execution.
    /// Verifies if the user associated with the command context has the 'IsDragon' flag set in the database.
    /// </summary>
    /// <param name="context">The command context, containing user and guild information.</param>
    /// <param name="command">The command being executed.</param>
    /// <param name="services">The service provider used to resolve dependencies like the database factory and settings service.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result (<see cref="PreconditionResult.FromSuccess"/> if the user is a dragon, <see cref="PreconditionResult.FromError"/> otherwise).</returns>
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var dbFactory = services.GetRequiredService<IDataConnectionFactory>();
        var guildConfigService = services.GetRequiredService<GuildSettingsService>();

        await using var db = await dbFactory.CreateConnectionAsync();

        var userId = context.User.Id;
        var user = await db.DiscordUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user != null)
            return user.IsDragon
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(
                    "Your meek human arms could never push the 10,000 pound rock blocking the " +
                    "path out of the cave of stable features. You must call upon the dragon in " +
                    "your soul to open a passage into the abyss of new features. (enable beta " +
                    $"mode by running `{await guildConfigService.GetPrefix(context.Guild)}dragon` to use this command)");

        user = new DiscordUser
        {
            UserId = userId,
            Username = context.User.Username,
            Discriminator = context.User.Discriminator,
            AvatarId = context.User.AvatarId,
            DateAdded = DateTime.UtcNow,
            IsDragon = false
        };
        try
        {
            await db.InsertAsync(user);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to insert new DiscordUser for UserId {UserId} in RequireDragonAttribute", userId);
            return PreconditionResult.FromError("Database error checking permissions.");
        }

        return user.IsDragon
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("Your meek human arms could never push the 10,000 pound rock blocking the " +
                                           "path out of the cave of stable features. You must call upon the dragon in " +
                                           "your soul to open a passage into the abyss of new features. (enable beta " +
                                           $"mode by running `{await guildConfigService.GetPrefix(context.Guild)}dragon` to use this command)");
    }
}