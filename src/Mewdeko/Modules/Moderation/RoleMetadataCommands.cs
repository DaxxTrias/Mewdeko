using System.Web;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Configs;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    [Group]
    public class RoleMetadataCommands : MewdekoSubmodule
    {
        private readonly BotConfig _config;
        public RoleMetadataCommands(BotConfigService configS) => _config = configS.Data;

        [Cmd, Aliases, RequireDragon]
        public async Task Authorize()
        {
            var url =
                "https://discord.com/api/oauth2/authorize?client_id=%id%&redirect_uri=%redirect%&response_type=code&scope=identify%20role_connections.write%20connections"
                    .Replace("%id%", Context.Client.CurrentUser.Id.ToString())
                    .Replace("%redirect%", HttpUtility.UrlEncode(_config.RedirectUrl));
            var components = new ComponentBuilder()
                .WithButton("Enter Code", "auth_code.enter", ButtonStyle.Success)
                .WithButton("Get Code", null, ButtonStyle.Link, url: url)
                .WithButton("Help", null, ButtonStyle.Link, url: "https://discord.gg/TBD15");
            await Context.Channel.SendMessageAsync("Please authorize bot to manage your role connections. " +
                                                   "If you already have a code, click `Enter Code`, if you don't click `Get Code`.",
                components: components.Build());
        }
    }
}