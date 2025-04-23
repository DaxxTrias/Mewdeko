﻿using Mewdeko.Database.EF.EFCore.Filters;
using Mewdeko.Database.EF.EFCore.GuildConfigs;
using Mewdeko.Database.EF.EFCore.Protections;
using Mewdeko.Database.EF.EFCore.Xp;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents the database context for Mewdeko.
/// </summary>
public class MewdekoContext : DbContext
{
    private readonly BotCredentials creds;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MewdekoContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    public MewdekoContext(DbContextOptions options) : base(options)
    {
        creds = new BotCredentials();
    }

    /// <summary>
    ///     Gets or sets the global user balances.
    /// </summary>
    public DbSet<GlobalUserBalance> GlobalUserBalances { get; set; }

    /// <summary>
    ///     Gets or sets invite counts
    /// </summary>
    public DbSet<InviteCount> InviteCounts { get; set; }
    /// <summary>
    ///
    /// </summary>
    public DbSet<Ticket> Tickets { get; set; }

    /// <summary>
    /// Gets or sets the custom voice channels.
    /// </summary>
    public DbSet<CustomVoiceChannel> CustomVoiceChannels { get; set; }

    /// <summary>
    /// Gets or sets the custom voice configs.
    /// </summary>
    public DbSet<CustomVoiceConfig> CustomVoiceConfigs { get; set; }

    /// <summary>
    /// Gets or sets the User Voice Preferences.
    /// </summary>
    public DbSet<UserVoicePreference> UserVoicePreferences { get; set; }

    /// <summary>
    /// Gets or sets case notes
    /// </summary>
    public DbSet<CaseNote> CaseNotes { get; set; }

    /// <summary>
    /// Gets or sets the chat logs.
    /// </summary>
    public DbSet<ChatLog> ChatLogs { get; set; }

    /// <summary>
    ///
    /// </summary>
    public DbSet<TicketCase> TicketCases { get; set; }

    /// <summary>
    ///     Gets or sets the guild user XP data.
    /// </summary>
    public DbSet<GuildUserXp> GuildUserXps { get; set; }

    /// <summary>
    ///     Gets or sets the guild XP settings.
    /// </summary>
    public DbSet<GuildXpSettings> GuildXpSettings { get; set; }

    /// <summary>
    ///     Gets or sets the XP boost events.
    /// </summary>
    public DbSet<XpBoostEvent> XpBoostEvents { get; set; }

    /// <summary>
    ///     Gets or sets the XP channel multipliers.
    /// </summary>
    public DbSet<XpChannelMultiplier> XpChannelMultipliers { get; set; }

    /// <summary>
    ///     Gets or sets the XP competition entries.
    /// </summary>
    public DbSet<XpCompetitionEntry> XpCompetitionEntries { get; set; }

    /// <summary>
    ///     Gets or sets the XP competition rewards.
    /// </summary>
    public DbSet<XpCompetitionReward> XpCompetitionRewards { get; set; }

    /// <summary>
    ///     Gets or sets the XP competitions.
    /// </summary>
    public DbSet<XpCompetition> XpCompetitions { get; set; }

    /// <summary>
    ///     Gets or sets the XP currency rewards.
    /// </summary>
    public DbSet<XpCurrencyReward> XpCurrencyRewards { get; set; }

    /// <summary>
    ///     Gets or sets the XP excluded items.
    /// </summary>
    public DbSet<XpExcludedItem> XpExcludedItems { get; set; }

    /// <summary>
    ///     Gets or sets the XP role multipliers.
    /// </summary>
    public DbSet<XpRoleMultiplier> XpRoleMultipliers { get; set; }

    /// <summary>
    ///     Gets or sets the XP role rewards.
    /// </summary>
    public DbSet<XpRoleReward> XpRoleRewards { get; set; }

    /// <summary>
    ///     Gets or sets the XP role tracking data.
    /// </summary>
    public DbSet<XpRoleTracking> XpRoleTracking { get; set; }

    /// <summary>
    ///     Gets or sets the XP user snapshots.
    /// </summary>
    public DbSet<XpUserSnapshot> XpUserSnapshots { get; set; }


    /// <summary>
    ///     Gets or sets invited by
    /// </summary>
    public DbSet<InvitedBy> InvitedBy { get; set; }

    /// <summary>
    ///     Gets or sets the lockdown channel permissions.
    /// </summary>
    public DbSet<LockdownChannelPermissions> LockdownChannelPermissions { get; set; }

    /// <summary>
    ///     Gets or sets the giveaway users.
    /// </summary>
    public DbSet<GiveawayUsers> GiveawayUsers { get; set; }

    /// <summary>
    ///     Role Monitor
    /// </summary>
    public DbSet<RoleMonitoringSettings> RoleMonitoringSettings { get; set; }

    /// <summary>
    ///     Role Monitor BLR
    /// </summary>
    public DbSet<BlacklistedRole> BlacklistedRoles { get; set; }

    /// <summary>
    ///     Role Monitor BLP
    /// </summary>
    public DbSet<BlacklistedPermission> BlacklistedPermissions { get; set; }

    /// <summary>
    ///     Gets or sets starboard posts
    /// </summary>
    public DbSet<StarboardPost> StarboardPosts { get; set; }

    /// <summary>
    ///     Gets or sets starboard configs
    /// </summary>
    public DbSet<StarboardConfig> Starboards { get; set; }

    /// <summary>
    ///     Role Monitor WR
    /// </summary>
    public DbSet<WhitelistedRole> WhitelistedRoles { get; set; }

    /// <summary>
    ///     Role Monitor WU
    /// </summary>
    public DbSet<WhitelistedUser> WhitelistedUsers { get; set; }

    /// <summary>
    ///     The bots reviews, can be added via dashboard or the bot itself
    /// </summary>
    public DbSet<BotReviews> BotReviews { get; set; }

    /// <summary>
    ///     Message Counts
    /// </summary>
    public DbSet<MessageCount> MessageCounts { get; set; }

    /// <summary>
    /// Gets or sets message timestamps
    /// </summary>
    public DbSet<MessageTimestamp> MessageTimestamps { get; set; }

    /// <summary>
    ///     Gets or sets the anti-alt settings.
    /// </summary>
    public DbSet<AntiAltSetting> AntiAltSettings { get; set; }

    /// <summary>
    ///     Gets or sets the per guild AI Config
    /// </summary>
    public DbSet<GuildAiConfig> GuildAiConfig { get; set; }

    /// <summary>
    ///     AI Messages
    /// </summary>
    public DbSet<AiMessage> AiMessages { get; set; }

    /// <summary>
    ///     AI Conversations
    /// </summary>
    public DbSet<AiConversation> AiConversations { get; set; }

    /// <summary>
    ///     Gets or sets the anti-spam settings.
    /// </summary>
    public DbSet<AntiSpamSetting> AntiSpamSettings { get; set; }

    /// <summary>
    ///     Gets or sets the anti-raid settings.
    /// </summary>
    public DbSet<AntiRaidSetting> AntiRaidSettings { get; set; }

    /// <summary>
    ///     Gets or sets ticket panels
    /// </summary>
    public DbSet<TicketPanel> TicketPanels { get; set; }


    /// <summary>
    ///     Gets or sets the anti-spam ignore settings.
    /// </summary>
    public DbSet<AntiSpamIgnore> AntiSpamIgnore { get; set; }


    /// <summary>
    ///     Gets or sets the anti mass mention settings.
    /// </summary>
    public DbSet<AntiMassMentionSetting> AntiMassMentionSettings { get; set; }

    /// <summary>
    ///     Gets or sets the guild user balances.
    /// </summary>
    public DbSet<GuildUserBalance> GuildUserBalances { get; set; }

    /// <summary>
    /// Gets or sets the ticket panel buttons
    /// </summary>
    public DbSet<PanelButton> PanelButtons { get; set; }

    /// <summary>
    /// Gets or sets reaction role messages
    /// </summary>
    public DbSet<ReactionRoleMessage> ReactionRoleMessages { get; set; }

    /// <summary>
    /// Gets or sets the select menu options for tickets
    /// </summary>
    public DbSet<SelectMenuOption> SelectMenuOptions { get; set; }

    /// <summary>
    /// Gets or sets the select menus used for tickets
    /// </summary>
    public DbSet<PanelSelectMenu> PanelSelectMenus { get; set; }

    /// <summary>
    /// Gets or sets guild ticket settings
    /// </summary>
    public DbSet<GuildTicketSettings> GuildTicketSettings { get; set; }

    /// <summary>
    ///     Gets or sets the transaction histories.
    /// </summary>
    public DbSet<TransactionHistory> TransactionHistories { get; set; }

    /// <summary>
    ///     Gets or sets the auto publish settings.
    /// </summary>
    public DbSet<AutoPublish> AutoPublish { get; set; }

    /// <summary>
    ///     Gets or sets the auto ban roles.
    /// </summary>
    public DbSet<AutoBanRoles> AutoBanRoles { get; set; }

    /// <summary>
    ///     Logging settings for guilds
    /// </summary>
    public DbSet<LoggingV2> LoggingV2 { get; set; }

    /// <summary>
    ///     Gets or sets the publish word blacklists.
    /// </summary>
    public DbSet<PublishWordBlacklist> PublishWordBlacklists { get; set; }

    /// <summary>
    ///     Gets or sets the publish user blacklists.
    /// </summary>
    public DbSet<PublishUserBlacklist> PublishUserBlacklists { get; set; }

    /// <summary>
    ///     Gets or sets the join and leave logs.
    /// </summary>
    public DbSet<JoinLeaveLogs> JoinLeaveLogs { get; set; }

    /// <summary>
    ///     Gets or sets the guild configurations.
    /// </summary>
    public DbSet<GuildConfig> GuildConfigs { get; set; }

    /// <summary>
    ///     Gets or sets the suggestions.
    /// </summary>
    public DbSet<SuggestionsModel> Suggestions { get; set; }

    /// <summary>
    ///     Gets or sets the filtered words.
    /// </summary>
    public DbSet<FilteredWord> FilteredWords { get; set; }

    /// <summary>
    ///     Gets or sets the owner only settings.
    /// </summary>
    public DbSet<OwnerOnly> OwnerOnly { get; set; }

    /// <summary>
    ///     Gets or sets the warnings (second version).
    /// </summary>
    public DbSet<Warning2> Warnings2 { get; set; }

    /// <summary>
    ///     Gets or sets the templates.
    /// </summary>
    public DbSet<Template> Templates { get; set; }

    /// <summary>
    ///     Gets or sets the server recovery store.
    /// </summary>
    public DbSet<ServerRecoveryStore> ServerRecoveryStore { get; set; }

    /// <summary>
    ///     Gets or sets the AFK settings.
    /// </summary>
    public DbSet<Afk.Afk> Afk { get; set; }

    /// <summary>
    ///     Gets or sets the multi greet settings.
    /// </summary>
    public DbSet<MultiGreet> MultiGreets { get; set; }

    /// <summary>
    ///     Gets or sets the user role states.
    /// </summary>
    public DbSet<UserRoleStates> UserRoleStates { get; set; }

    /// <summary>
    ///     Gets or sets the role state settings.
    /// </summary>
    public DbSet<RoleStateSettings> RoleStateSettings { get; set; }

    /// <summary>
    ///     Gets or sets the giveaways.
    /// </summary>
    public DbSet<Giveaways> Giveaways { get; set; }

    /// <summary>
    ///     Gets or sets the quotes.
    /// </summary>
    public DbSet<Quote> Quotes { get; set; }

    /// <summary>
    /// Gets or sets the music playlist tracks in the database.
    /// </summary>
    public DbSet<MusicPlaylistTrack> MusicPlaylistTracks { get; set; }

    /// <summary>
    /// Gets or sets blacklisted nsfw tags.
    /// </summary>
    public DbSet<NsfwBlacklitedTag> NsfwBlacklistedTags { get; set; }

    /// <summary>
    /// Gets or sets command cooldowns.
    /// </summary>
    public DbSet<CommandCooldown> CommandCooldowns { get; set; }

    /// <summary>
    /// Gets or sets stream role settings.
    /// </summary>
    public DbSet<StreamRoleSettings> StreamRoleSettings { get; set; }

    /// <summary>
    /// Gets or sets permissions.
    /// </summary>
    public DbSet<Permissionv2> Permissions { get; set; }

    /// <summary>
    /// Gets or sets followed streams.
    /// </summary>
    public DbSet<FollowedStream> FollowedStreams { get; set; }

    /// <summary>
    /// Gets or sets filter word channel ids.
    /// </summary>
    public DbSet<FilterWordsChannelIds> FilterWordsChannelIds { get; set; }

    /// <summary>
    /// Gets or sets filter invite channel ids.
    /// </summary>
    public DbSet<FilterInvitesChannelIds> FilterInvitesChannelIds { get; set; }

    /// <summary>
    /// Gets or sets filter links channel ids.
    /// </summary>
    public DbSet<FilterLinksChannelId> FilterLinksChannelIds { get; set; }


    /// <summary>
    ///     Gets or sets the reminders.
    /// </summary>
    public DbSet<Reminder> Reminders { get; set; }

    /// <summary>
    ///     Gets or sets the confessions.
    /// </summary>
    public DbSet<Confessions> Confessions { get; set; }

    /// <summary>
    ///     Gets or sets the self-assigned roles.
    /// </summary>
    public DbSet<SelfAssignedRole> SelfAssignableRoles { get; set; }

    /// <summary>
    ///     Gets or sets the role greets.
    /// </summary>
    public DbSet<RoleGreet> RoleGreets { get; set; }

    /// <summary>
    ///     Gets or sets the highlights.
    /// </summary>
    public DbSet<Highlights> Highlights { get; set; }

    /// <summary>
    ///     Gets or sets the command statistics.
    /// </summary>
    public DbSet<CommandStats> CommandStats { get; set; }

    /// <summary>
    ///     Gets or sets the highlight settings.
    /// </summary>
    public DbSet<HighlightSettings> HighlightSettings { get; set; }

    /// <summary>
    ///     Gets or sets the music playlists.
    /// </summary>
    public DbSet<MusicPlaylist> MusicPlaylists { get; set; }

    /// <summary>
    ///     Gets or sets the chat triggers.
    /// </summary>
    public DbSet<ChatTriggers> ChatTriggers { get; set; }

    /// <summary>
    ///     Gets or sets the music player settings.
    /// </summary>
    public DbSet<MusicPlayerSettings> MusicPlayerSettings { get; set; }

    /// <summary>
    ///     Gets or sets the warnings.
    /// </summary>
    public DbSet<Warning> Warnings { get; set; }

    /// <summary>
    ///     Gets or sets the user XP stats.
    /// </summary>
    public DbSet<UserXpStats> UserXpStats { get; set; }

    /// <summary>
    ///     Gets or sets the vote roles.
    /// </summary>
    public DbSet<VoteRoles> VoteRoles { get; set; }

    /// <summary>
    ///     Gets or sets the polls.
    /// </summary>
    public DbSet<Polls> Poll { get; set; }

    /// <summary>
    ///     Gets or sets the command cooldowns.
    /// </summary>
    public DbSet<CommandCooldown> CommandCooldown { get; set; }

    /// <summary>
    ///     Gets or sets the suggest votes.
    /// </summary>
    public DbSet<SuggestVotes> SuggestVotes { get; set; }

    /// <summary>
    ///     Gets or sets the suggest threads.
    /// </summary>
    public DbSet<SuggestThreads> SuggestThreads { get; set; }

    /// <summary>
    ///     Gets or sets the votes.
    /// </summary>
    public DbSet<Votes> Votes { get; set; }

    /// <summary>
    ///     Gets or sets the command aliases.
    /// </summary>
    public DbSet<CommandAlias> CommandAliases { get; set; }

    /// <summary>
    ///     Gets or sets the ignored log channels.
    /// </summary>
    public DbSet<IgnoredLogChannel> IgnoredLogChannels { get; set; }

    /// <summary>
    ///     Gets or sets the rotating playing statuses.
    /// </summary>
    public DbSet<RotatingPlayingStatus> RotatingStatus { get; set; }

    /// <summary>
    ///     Gets or sets the blacklist entries.
    /// </summary>
    public DbSet<BlacklistEntry> Blacklist { get; set; }

    /// <summary>
    ///     Gets or sets the auto commands.
    /// </summary>
    public DbSet<AutoCommand> AutoCommands { get; set; }

    /// <summary>
    ///     Gets or sets vc roles
    /// </summary>
    public DbSet<VcRole> VcRoles { get; set; }

    /// <summary>
    ///     Gets or sets ticket notes.
    /// </summary>
    public DbSet<TicketNote> TicketNotes { get; set; }

    /// <summary>
    ///     Gets or sets ticket priorities.
    /// </summary>
    public DbSet<TicketPriority> TicketPriorities { get; set; }

    /// <summary>
    ///     Gets or sets ticket tags
    /// </summary>
    public DbSet<TicketTag> TicketTags { get; set; }

    /// <summary>
    ///     Gets or sets the auto ban words.
    /// </summary>
    public DbSet<AutoBanEntry> AutoBanWords { get; set; }

    /// <summary>
    ///     Gets or sets the status roles.
    /// </summary>
    public DbSet<StatusRolesTable> StatusRoles { get; set; }

    /// <summary>
    ///     Gets or sets the ban templates.
    /// </summary>
    public DbSet<BanTemplate> BanTemplates { get; set; }

    /// <summary>
    ///     Gets or sets the discord permission overrides.
    /// </summary>
    public DbSet<DiscordPermOverride> DiscordPermOverrides { get; set; }

    /// <summary>
    ///     Gets or sets the discord users.
    /// </summary>
    public DbSet<DiscordUser> DiscordUser { get; set; }

    /// <summary>
    ///     Gets or sets the embeds saved by users
    /// </summary>
    public DbSet<Embeds> Embeds { get; set; }

    /// <summary>
    ///     Gets or sets the feed subscriptions.
    /// </summary>
    public DbSet<FeedSub> FeedSubs { get; set; }

    /// <summary>
    ///     Gets or sets the muted user IDs.
    /// </summary>
    public DbSet<MutedUserId> MutedUserIds { get; set; }

    /// <summary>
    ///     Gets or sets the playlist songs.
    /// </summary>
    public DbSet<PlaylistSong> PlaylistSongs { get; set; }

    /// <summary>
    ///     Gets or sets the poll votes.
    /// </summary>
    public DbSet<PollVote> PollVotes { get; set; }

    /// <summary>
    ///     Gets or sets the role connection authentication storage.
    /// </summary>
    public DbSet<RoleConnectionAuthStorage> AuthCodes { get; set; }

    /// <summary>
    ///     Gets or sets the repeaters.
    /// </summary>
    public DbSet<Repeater> Repeaters { get; set; }

    /// <summary>
    ///     Gets or sets the unban timers.
    /// </summary>
    public DbSet<UnbanTimer> UnbanTimers { get; set; }

    /// <summary>
    ///     Gets or sets the unmute timers.
    /// </summary>
    public DbSet<UnmuteTimer> UnmuteTimers { get; set; }

    /// <summary>
    ///     Gets or sets the unrole timers.
    /// </summary>
    public DbSet<UnroleTimer> UnroleTimers { get; set; }

    /// <summary>
    ///     Gets or sets the warning punishments.
    /// </summary>
    public DbSet<WarningPunishment> WarningPunishments { get; set; }

    /// <summary>
    ///     Gets or sets the second version of warning punishments.
    /// </summary>
    public DbSet<WarningPunishment2> WarningPunishments2 { get; set; }

    /// <summary>
    ///     gets or sets the local running instances, for dashboard management.
    /// </summary>
    public DbSet<BotInstance> BotInstances { get; set; }

    /// <summary>
    ///     Settings for invite counting
    /// </summary>
    public DbSet<InviteCountSettings> InviteCountSettings { get; set; }

    /// <summary>
    /// Gets or sets channels where command results are deleted.
    /// </summary>
    public DbSet<DelMsgOnCmdChannel> DelMsgOnCmdChannels { get; set; }

    /// <summary>
    ///     Configures the model that was discovered by convention from the entity types
    ///     exposed in <see cref="DbSet{TEntity}" /> properties on your derived context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
     protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region Afk

        var afkEntity = modelBuilder.Entity<Afk.Afk>();
        afkEntity.Property(x => x.WasTimed)
            .HasDefaultValue(false);

        #endregion

        #region AntiProtection

        modelBuilder.Entity<AntiSpamIgnore>()
            .Property(x => x.AntiSpamSettingId)
            .IsRequired(false);

        #endregion

        #region ChatTriggers

        var chatTriggerEntity = modelBuilder.Entity<ChatTriggers>();
        chatTriggerEntity.Property(x => x.IsRegex)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.OwnerOnly)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.AutoDeleteTrigger)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.ReactToTrigger)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.NoRespond)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.DmResponse)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.ContainsAnywhere)
            .HasDefaultValue(false);

        chatTriggerEntity.Property(x => x.AllowTarget)
            .HasDefaultValue(false);

        #endregion

        #region CommandStats

        var commandStatsEntity = modelBuilder.Entity<CommandStats>();

        commandStatsEntity.Property(x => x.IsSlash)
            .HasDefaultValue(false);

        commandStatsEntity.Property(x => x.Trigger)
            .HasDefaultValue(false);

        commandStatsEntity.Property(x => x.Module)
            .IsRequired(false);

        commandStatsEntity.Property(x => x.Module)
            .HasDefaultValue("");

        #endregion


        #region DelMsgOnCmdChannel

        var delMsgOnCmdChannelEntity = modelBuilder.Entity<DelMsgOnCmdChannel>();

        delMsgOnCmdChannelEntity.Property(x => x.State)
            .HasDefaultValue(true);

        #endregion

        #region QUOTES

        var quoteEntity = modelBuilder.Entity<Quote>();
        quoteEntity.HasIndex(x => x.GuildId);
        quoteEntity.HasIndex(x => x.Keyword);

        #endregion

        #region GuildConfig

        var configEntity = modelBuilder.Entity<GuildConfig>();
        configEntity
            .HasIndex(c => c.GuildId)
            .IsUnique();

        configEntity.HasIndex(x => x.WarnExpireHours)
            .IsUnique(false);

        configEntity.Property(x => x.DeleteMessageOnCommand)
            .HasDefaultValue(false);

        configEntity.Property(x => x.StatsOptOut)
            .HasDefaultValue(false);

        configEntity.Property(x => x.DmOnGiveawayWin)
            .HasDefaultValue(true);

        configEntity.Property(x => x.SendDmGreetMessage)
            .HasDefaultValue(false);

        configEntity.Property(x => x.SendChannelByeMessage)
            .HasDefaultValue(false);

        configEntity.Property(x => x.StarboardAllowBots)
            .HasDefaultValue(false);

        configEntity.Property(x => x.StarboardRemoveOnDelete)
            .HasDefaultValue(false);

        configEntity.Property(x => x.StarboardRemoveOnReactionsClear)
            .HasDefaultValue(false);

        configEntity.Property(x => x.UseStarboardBlacklist)
            .HasDefaultValue(true);

        configEntity.Property(x => x.StarboardRemoveOnBelowThreshold)
            .HasDefaultValue(true);

        configEntity.Property(x => x.ArchiveOnDeny)
            .HasDefaultValue(false);

        configEntity.Property(x => x.ArchiveOnAccept)
            .HasDefaultValue(false);

        configEntity.Property(x => x.ArchiveOnImplement)
            .HasDefaultValue(false);

        configEntity.Property(x => x.ArchiveOnConsider)
            .HasDefaultValue(false);

        configEntity.Property(x => x.GBAction)
            .HasDefaultValue(false);

        #endregion

        #region HighlightSettings

        var highlightSettingsEntity = modelBuilder.Entity<HighlightSettings>();

        highlightSettingsEntity.Property(x => x.HighlightsOn)
            .HasDefaultValue(false);

        #endregion

        #region MultiGreets

        var multiGreetsEntity = modelBuilder.Entity<MultiGreet>();

        multiGreetsEntity.Property(x => x.GreetBots)
            .HasDefaultValue(false);

        multiGreetsEntity.Property(x => x.DeleteTime)
            .HasDefaultValue(1);

        multiGreetsEntity.Property(x => x.Disabled)
            .HasDefaultValue(false);

        multiGreetsEntity.Property(x => x.WebhookUrl)
            .HasDefaultValue(null);

        #endregion

        #region Self Assignable Roles

        var selfassignableRolesEntity = modelBuilder.Entity<SelfAssignedRole>();

        selfassignableRolesEntity
            .HasIndex(s => new
            {
                s.GuildId, s.RoleId
            })
            .IsUnique();

        selfassignableRolesEntity
            .Property(x => x.Group)
            .HasDefaultValue(0);

        #endregion

        #region Permission

        var permissionEntity = modelBuilder.Entity<Permission>();
        permissionEntity
            .HasOne(p => p.Next)
            .WithOne(p => p.Previous)
            .IsRequired(false);

        #endregion

        #region MusicPlaylists

        var musicPlaylistEntity = modelBuilder.Entity<MusicPlaylist>();

        musicPlaylistEntity.HasKey(x => x.Id);
        musicPlaylistEntity.Property(x => x.Name).IsRequired().HasMaxLength(200);
        musicPlaylistEntity.Property(x => x.DateAdded).ValueGeneratedOnAdd();

        musicPlaylistEntity.HasIndex(x => new { x.GuildId, x.Name }).IsUnique();

        musicPlaylistEntity.HasMany(x => x.Tracks)
            .WithOne(x => x.Playlist)
            .HasForeignKey(x => x.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        var musicPlaylistTracksEntity = modelBuilder.Entity<MusicPlaylistTrack>();

        musicPlaylistTracksEntity.HasKey(x => x.Id);
        musicPlaylistTracksEntity.Property(x => x.Title).IsRequired().HasMaxLength(200);
        musicPlaylistTracksEntity.Property(x => x.Uri).IsRequired().HasMaxLength(500);
        musicPlaylistTracksEntity.Property(x => x.DateAdded).ValueGeneratedOnAdd();

        musicPlaylistTracksEntity.HasIndex(x => new { x.PlaylistId, x.Index });

        #endregion

        #region DiscordUser

        var du = modelBuilder.Entity<DiscordUser>();
        du.HasAlternateKey(w => w.UserId);

        du.Property(x => x.LastLevelUp)
            .HasDefaultValue(new DateTime(2017, 9, 21, 20, 53, 13, 305, DateTimeKind.Local));

        du.HasIndex(x => x.TotalXp);

        du.Property(x => x.PronounsDisabled)
            .HasDefaultValue(false);

        du.Property(x => x.StatsOptOut)
            .HasDefaultValue(false);

        du.Property(x => x.IsDragon)
            .HasDefaultValue(false);

        du.Property(x => x.NotifyOnLevelUp)
            .HasDefaultValue(XpNotificationLocation.None);

        du.Property(x => x.ProfilePrivacy)
            .HasDefaultValue(ProfilePrivacyEnum.Public);

        du.Property(x => x.BirthdayDisplayMode)
            .HasDefaultValue(BirthdayDisplayModeEnum.Default);

        #endregion

        #region Warnings

        var warn = modelBuilder.Entity<Warning>();
        warn.HasIndex(x => x.GuildId);
        warn.HasIndex(x => x.UserId);
        warn.HasIndex(x => x.DateAdded);

        #endregion

        #region XpStuff

        // Configure GuildUserXp entity
        modelBuilder.Entity<GuildUserXp>(entity =>
        {
            // Create a composite primary key on GuildId and UserId
            entity.HasKey(e => new { e.GuildId, e.UserId });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TotalXp);
            entity.HasIndex(e => e.LastActivity);
        });

        // Configure XpCompetitionEntry entity
        modelBuilder.Entity<XpCompetitionEntry>(entity =>
        {
            // Create a composite primary key on CompetitionId and UserId
            entity.HasKey(e => new { e.CompetitionId, e.UserId });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.CompetitionId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CurrentXp);
            entity.HasIndex(e => e.AchievedTargetAt);
        });

        // Configure XpExcludedItem entity
        modelBuilder.Entity<XpExcludedItem>(entity =>
        {
            // Create a composite primary key on GuildId, ItemId and ItemType
            entity.HasKey(e => new { e.GuildId, e.ItemId, e.ItemType });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => new { e.ItemId, e.ItemType });
        });

        // Configure XpRoleMultiplier entity
        modelBuilder.Entity<XpRoleMultiplier>(entity =>
        {
            // Create a composite primary key on GuildId and RoleId
            entity.HasKey(e => new { e.GuildId, e.RoleId });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
        });

        // Configure XpChannelMultiplier entity
        modelBuilder.Entity<XpChannelMultiplier>(entity =>
        {
            // Create a composite primary key on GuildId and ChannelId
            entity.HasKey(e => new { e.GuildId, e.ChannelId });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
        });

        // Configure XpRoleReward entity
        modelBuilder.Entity<XpRoleReward>(entity =>
        {
            // Create a composite primary key on GuildId and Level
            entity.HasKey(e => new { e.GuildId, e.Level });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
        });

        // Configure XpCurrencyReward entity
        modelBuilder.Entity<XpCurrencyReward>(entity =>
        {
            // Create a composite primary key on GuildId and Level
            entity.HasKey(e => new { e.GuildId, e.Level });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
        });

        // Configure XpUserSnapshot entity
        modelBuilder.Entity<XpUserSnapshot>(entity =>
        {
            // Create indexes for efficient lookups
            entity.HasIndex(e => new { e.GuildId, e.UserId });
            entity.HasIndex(e => e.Timestamp);
        });

        // Configure XpRoleTracking entity
        modelBuilder.Entity<XpRoleTracking>(entity =>
        {
            // Create a composite key on GuildId, RoleId, and StartTracking
            entity.HasKey(e => new { e.GuildId, e.RoleId, e.StartTracking });

            // Create indexes for efficient lookups
            entity.HasIndex(e => e.GuildId);
            entity.HasIndex(e => e.RoleId);
            entity.HasIndex(e => e.EndTracking);
        });

        // Configure Template relationships
        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasOne(e => e.TemplateUser)
                .WithOne()
                .HasForeignKey<Template>("TemplateUserId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TemplateGuild)
                .WithOne()
                .HasForeignKey<Template>("TemplateGuildId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TemplateClub)
                .WithOne()
                .HasForeignKey<Template>("TemplateClubId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TemplateBar)
                .WithOne()
                .HasForeignKey<Template>("TemplateBarId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.GuildId).IsUnique();
        });
        #endregion

        #region Music

        modelBuilder.Entity<MusicPlayerSettings>()
            .HasIndex(x => x.GuildId)
            .IsUnique();

        modelBuilder.Entity<MusicPlayerSettings>()
            .Property(x => x.Volume)
            .HasDefaultValue(100);

        #endregion

        #region Polls

        modelBuilder.Entity<Polls>()
            .HasIndex(x => x.GuildId)
            .IsUnique();

        #endregion

        #region Reminders

        modelBuilder.Entity<Reminder>()
            .HasIndex(x => x.When);

        #endregion

        #region BanTemplate

        modelBuilder.Entity<BanTemplate>()
            .HasIndex(x => x.GuildId)
            .IsUnique();

        #endregion

        #region Perm Override

        modelBuilder.Entity<DiscordPermOverride>()
            .HasIndex(x => new
            {
                x.GuildId, x.Command
            })
            .IsUnique();

        #endregion

        #region Tickets

        modelBuilder.Entity<TicketPanel>()
            .HasMany(p => p.Buttons)
            .WithOne()
            .HasForeignKey(b => b.Id);


        #endregion
    }
}