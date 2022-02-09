﻿using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group]
    public class BlacklistCommands : MewdekoSubmodule<BlacklistService>
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;

        public BlacklistCommands(DbService db, IBotCredentials creds)
        {
            _db = db;
            _creds = creds;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public Task UserBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.User);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public Task UserBlacklist(AddRemove action, IUser usr) => Blacklist(action, usr.Id, BlacklistType.User);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public Task ChannelBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.Channel);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public Task ServerBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.Server);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public Task ServerBlacklist(AddRemove action, IGuild guild) => Blacklist(action, guild.Id, BlacklistType.Server);

        private async Task Blacklist(AddRemove action, ulong id, BlacklistType type)
        {
            if (action == AddRemove.Add && _creds.OwnerIds.Contains(id))
                return;

            if (action == AddRemove.Add)
                Service.Blacklist(type, id);
            else
                Service.UnBlacklist(type, id);

            if (action == AddRemove.Add)
                await ReplyConfirmLocalizedAsync("blacklisted", Format.Code(type.ToString()),
                    Format.Code(id.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("unblacklisted", Format.Code(type.ToString()),
                    Format.Code(id.ToString())).ConfigureAwait(false);
        }
    }
}