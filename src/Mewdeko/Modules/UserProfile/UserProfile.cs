﻿using DataModel;
using Discord.Commands;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Modules.UserProfile.Services;
using SkiaSharp;

namespace Mewdeko.Modules.UserProfile;

/// <summary>
///     Handles text commands for user profiles, providing functionalities to view and manage user profile details.
/// </summary>
public class UserProfile(IDataConnectionFactory dbFactory) : MewdekoModuleBase<UserProfileService>
{
    /// <summary>
    ///     Shows the user's profile or another user's profile if specified.
    /// </summary>
    /// <param name="user">The user whose profile is to be shown. If null, shows the caller's profile.</param>
    [Cmd]
    [Aliases]
    public async Task Profile(IUser? user = null)
    {
        user ??= ctx.User;
        var embed = await Service.GetProfileEmbed(user, ctx.User);
        if (embed is null)
            await ctx.Channel.SendErrorAsync(Strings.ProfilePrivate(ctx.Guild.Id), Config);
        else
            await ctx.Channel.SendMessageAsync(embed: embed);
    }

    /// <summary>
    ///     Allows a user to toggle opting out of greet dms. Only works if the server they are joining uses mewdeko for dm
    ///     greets.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task GreetDmOptOut()
    {
        var optOut = await Service.ToggleDmGreetOptOutAsync(ctx.User);

        if (optOut)
            await ReplyConfirmAsync(Strings.GreetdmOptOut(ctx.Guild.Id));
        else
            await ReplyConfirmAsync(Strings.GreetdmOptIn(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets or updates the biography in the user's profile.
    /// </summary>
    /// <param name="bio">The biography text. Must be under 2048 characters.</param>
    [Cmd]
    [Aliases]
    public async Task SetBio([Remainder] string bio)
    {
        if (bio.Length > 2048)
        {
            await ctx.Channel.SendErrorAsync(Strings.BioLengthLimit(ctx.Guild.Id), Config);
            return;
        }

        await Service.SetBio(ctx.User, bio);
        await ctx.Channel.SendConfirmAsync(Strings.BioSet(ctx.Guild.Id, bio));
    }

    /// <summary>
    ///     Sets the zodiac sign in the user's profile.
    /// </summary>
    /// <param name="zodiac">The zodiac sign to set.</param>
    [Cmd]
    [Aliases]
    public async Task SetZodiac(string zodiac)
    {
        var result = await Service.SetZodiac(ctx.User, zodiac);
        if (!result)
            await ctx.Channel.SendErrorAsync(Strings.ZodiacInvalid(ctx.Guild.Id), Config);
        else
            await ctx.Channel.SendConfirmAsync(Strings.ZodiacSet(ctx.Guild.Id, zodiac));
    }

    /// <summary>
    ///     Sets the profile color based on an SKColor input.
    /// </summary>
    /// <param name="input">The SKColor representing the desired profile color.</param>
    [Cmd]
    [Aliases]
    public async Task SetProfileColor(SKColor input)
    {
        var discordColor = new Color(input.Red, input.Green, input.Blue);
        await Service.SetProfileColor(ctx.User, discordColor);
        await ctx.Channel.SendConfirmAsync(Strings.ProfileColorSet(ctx.Guild.Id, input));
    }

    /// <summary>
    ///     Sets the birthday in the user's profile.
    /// </summary>
    /// <param name="dateTime">The birthday date.</param>
    [Cmd]
    [Aliases]
    public async Task SetBirthday([Remainder] DateTime dateTime)
    {
        await Service.SetBirthday(ctx.User, dateTime);
        await ctx.Channel.SendConfirmAsync(Strings.BirthdaySet(ctx.Guild.Id, dateTime.ToString("d")));
    }

    /// <summary>
    ///     Toggles the user's opt-out status for command statistics collection.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task UserStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.User);
        if (!optout)
            await ctx.Channel.SendConfirmAsync(
                "Succesfully enabled command stats collection! (This does ***not*** collect message contents!)");
        else
            await ctx.Channel.SendConfirmAsync(Strings.StatsCollectionDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Deletes the user's command statistics data.
    /// </summary>
    [Cmd]
    [Aliases]
    [Ratelimit(3600)]
    public async Task DeleteUserStatsData()
    {
        if (await PromptUserConfirmAsync(
                "Are you sure you want to delete your command stats? This action is irreversible!", ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.User))
                await ctx.Channel.SendErrorAsync(Strings.StatsDeleted(ctx.Guild.Id), Config);
            else
                await ctx.Channel.SendErrorAsync(Strings.NoDataDelete(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Sets the birthday privacy mode in the user's profile.
    /// </summary>
    /// <param name="birthdayDisplayModeEnum">The birthday display mode to set.</param>
    [Cmd]
    [Aliases]
    public async Task SetBirthdayPrivacy(BirthdayDisplayModeEnum birthdayDisplayModeEnum)
    {
        await Service.SetBirthdayDisplayMode(ctx.User, birthdayDisplayModeEnum);
        await ctx.Channel.SendConfirmAsync(
            $"Your birthday display mode has been set to {birthdayDisplayModeEnum.ToString()}");
    }

    /// <summary>
    ///     Sets the profile image URL in the user's profile.
    /// </summary>
    /// <param name="url">The URL of the image to set as the profile image.</param>
    [Cmd]
    [Aliases]
    public async Task SetProfileImage(string url)
    {
        if (!url.IsImage())
        {
            await ctx.Channel.SendErrorAsync(
                Strings.InvalidFormat(ctx.Guild.Id),
                Config);
            return;
        }

        await Service.SetProfileImage(ctx.User, url);
        var eb = new EmbedBuilder().WithOkColor().WithDescription(Strings.ProfileImageSet(ctx.Guild.Id))
            .WithImageUrl(url);
        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Sets the privacy level of the user's profile.
    /// </summary>
    /// <param name="privacyEnum">The privacy setting to apply.</param>
    [Cmd]
    [Aliases]
    public async Task SetPrivacy(ProfilePrivacyEnum privacyEnum)
    {
        await Service.SetPrivacy(ctx.User, privacyEnum);
        await ctx.Channel.SendConfirmAsync($"Privacy succesfully set to `{privacyEnum.ToString()}`");
    }

    /// <summary>
    ///     Sets or clears the Nintendo Switch friend code in the user's profile.
    /// </summary>
    /// <param name="switchFc">The Nintendo Switch friend code. If blank, clears the existing code.</param>
    [Cmd]
    [Aliases]
    public async Task SetSwitchFc(string switchFc = "")
    {
        if (!await Service.SetSwitchFc(ctx.User, switchFc))
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidSwitchFriendCode(ctx.Guild.Id), Config);
            return;
        }


        if (switchFc.Length == 0)
            await ctx.Channel.SendConfirmAsync(Strings.SwitchCodeRemoved(ctx.Guild.Id));
        else
            await ctx.Channel.SendConfirmAsync(Strings.SwitchFriendCodeSet(ctx.Guild.Id, switchFc));
    }

    /// <summary>
    ///     Displays the pronouns of the specified user or the command caller if no user is specified.
    /// </summary>
    /// <param name="user">Optional. The user whose pronouns are to be displayed.</param>
    [Cmd]
    [Aliases]
    public async Task Pronouns(IUser? user = null)
    {
        user ??= ctx.User;
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var dbUser = await dbContext.GetOrCreateUser(user).ConfigureAwait(false);
        if (await PronounsDisabled(dbUser).ConfigureAwait(false)) return;
        var pronouns = await Service.GetPronounsOrUnspecifiedAsync(user.Id).ConfigureAwait(false);
        var cb = new ComponentBuilder();
        if (!pronouns.PronounDb)
            cb.WithButton(Strings.PronounsReportButton(ctx.Guild.Id), $"pronouns_report.{user.Id};",
                ButtonStyle.Danger);
        await ctx.Channel.SendConfirmAsync(
            pronouns.PronounDb
                ? pronouns.Pronouns.Contains(' ')
                    ? Strings.PronounsPndbSpecial(ctx.Guild.Id, user.ToString(), pronouns.Pronouns)
                    : Strings.PronounsPndbGet(ctx.Guild.Id, user.ToString(), pronouns.Pronouns)
                : Strings.PronounsInternalGet(ctx.Guild.Id, user.ToString(), pronouns.Pronouns),
            cb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets or clears the pronouns for the user.
    /// </summary>
    /// <param name="pronouns">The pronouns to set. If null or empty, clears any existing pronouns.</param>
    [Cmd]
    [Aliases]
    public async Task SetPronouns([Remainder] string? pronouns = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var user = await dbContext.GetOrCreateUser(ctx.User).ConfigureAwait(false);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        if (string.IsNullOrWhiteSpace(pronouns))
        {
            var cb = new ComponentBuilder().WithButton(Strings.PronounsOverwriteButton(ctx.Guild.Id),
                "pronouns_overwrite");
            if (string.IsNullOrWhiteSpace(user.Pronouns))
            {
                await ctx.Channel.SendConfirmAsync(Strings.PronounsInternalNoOverride(ctx.Guild.Id), cb)
                    .ConfigureAwait(false);
                return;
            }

            cb.WithButton(Strings.PronounsOverwriteClearButton(ctx.Guild.Id), "pronouns_overwrite_clear",
                ButtonStyle.Danger);
            await ctx.Channel.SendConfirmAsync(Strings.PronounsInternalSelf(ctx.Guild.Id, user.Pronouns), cb)
                .ConfigureAwait(false);
            return;
        }

        user.Pronouns = pronouns;
        await dbContext.UpdateAsync(user);
        await ConfirmAsync(Strings.PronounsInternalUpdate(ctx.Guild.Id, user.Pronouns)).ConfigureAwait(false);
    }

    private async Task<bool> PronounsDisabled(DiscordUser user)
    {
        if (!user.PronounsDisabled) return false;
        await ReplyErrorAsync(Strings.PronounsDisabledUser(ctx.Guild.Id, user.PronounsClearedReason))
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Force-clears the pronouns for a user, optionally marking them as disabled due to abuse.
    /// </summary>
    /// <param name="user">The user whose pronouns are to be cleared.</param>
    /// <param name="pronounsDisabledAbuse">Whether the pronouns are being disabled due to abuse.</param>
    /// <param name="reason">The reason for the action.</param>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task PronounsForceClear(IUser? user, bool pronounsDisabledAbuse, [Remainder] string reason)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var dbUser = await dbContext.GetOrCreateUser(user).ConfigureAwait(false);
        dbUser.PronounsDisabled = pronounsDisabledAbuse;
        dbUser.PronounsClearedReason = reason;
        await dbContext.UpdateAsync(dbUser);
        await ctx.Channel.SendConfirmAsync(
            pronounsDisabledAbuse
                ? Strings.PronounsDisabledUser(ctx.Guild.Id, reason)
                : Strings.PronounsCleared(ctx.Guild.Id)
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Force-clears the pronouns for a user by ID, optionally marking them as disabled due to abuse.
    /// </summary>
    /// <param name="user">The user ID of the user whose pronouns are to be cleared.</param>
    /// <param name="pronounsDisabledAbuse">Whether the pronouns are being disabled due to abuse.</param>
    /// <param name="reason">The reason for the action.</param>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task PronounsForceClear(ulong user, bool pronounsDisabledAbuse, [Remainder] string reason)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var dbUser = await dbContext.DiscordUsers.AsQueryable().FirstAsync(x => x.UserId == user).ConfigureAwait(false);
        dbUser.PronounsDisabled = pronounsDisabledAbuse;
        dbUser.PronounsClearedReason = reason;
        await dbContext.UpdateAsync(dbUser);
        await ctx.Channel.SendConfirmAsync(
            pronounsDisabledAbuse
                ? Strings.PronounsDisabledUser(ctx.Guild.Id, reason)
                : Strings.PronounsCleared(ctx.Guild.Id)
        ).ConfigureAwait(false);
    }
}