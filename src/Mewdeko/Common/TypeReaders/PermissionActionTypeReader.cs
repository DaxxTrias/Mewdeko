﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.TypeReaders.Models;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Used instead of bool for more flexible keywords for true/false only in the permission module
/// </summary>
public class PermissionActionTypeReader : MewdekoTypeReader<PermissionAction>
{
    public PermissionActionTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
    {
        input = input.ToUpperInvariant();
        switch (input)
        {
            case "1":
            case "T":
            case "TRUE":
            case "ENABLE":
            case "ENABLED":
            case "ALLOW":
            case "PERMIT":
            case "UNBAN":
                return Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Enable));
            case "0":
            case "F":
            case "FALSE":
            case "DENY":
            case "DISABLE":
            case "DISABLED":
            case "DISALLOW":
            case "BAN":
                return Task.FromResult(TypeReaderResult.FromSuccess(PermissionAction.Disable));
            default:
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                    "Must be either deny or allow."));
        }
    }
}