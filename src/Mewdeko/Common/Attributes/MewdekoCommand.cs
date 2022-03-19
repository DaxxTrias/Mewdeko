﻿using Discord.Commands;
using System.Runtime.CompilerServices;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MewdekoCommandAttribute : CommandAttribute
{
    public MewdekoCommandAttribute([CallerMemberName] string memberName = "")
        : base(CommandNameLoadHelper.GetCommandNameFor(memberName)) =>
        MethodName = memberName.ToLowerInvariant();

    public string MethodName { get; }
}