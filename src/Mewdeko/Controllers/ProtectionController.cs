using Mewdeko.Controllers.Common.Protection;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for comprehensive protection system management
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ProtectionController(ProtectionService protectionService, ImageHashingService imageHashing) : Controller
{
    /// <summary>
    ///     Gets comprehensive protection status for all protection types
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetProtectionStatus(ulong guildId)
    {
        await Task.CompletedTask;

        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats, antiMassPostStats,
                antiPostChannelStats) =
            protectionService.GetAntiStats(guildId);
        var imageHashStats = protectionService.GetAntiImageHashStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                userThreshold = antiRaidStats?.AntiRaidSettings?.UserThreshold ?? 0,
                seconds = antiRaidStats?.AntiRaidSettings?.Seconds ?? 0,
                action = antiRaidStats?.AntiRaidSettings?.Action ?? 0,
                punishDuration = antiRaidStats?.AntiRaidSettings?.PunishDuration ?? 0,
                usersCount = antiRaidStats?.UsersCount ?? 0
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                messageThreshold = antiSpamStats?.AntiSpamSettings?.MessageThreshold ?? 0,
                action = antiSpamStats?.AntiSpamSettings?.Action ?? 0,
                muteTime = antiSpamStats?.AntiSpamSettings?.MuteTime ?? 0,
                roleId = antiSpamStats?.AntiSpamSettings?.RoleId ?? 0,
                ignoredChannels = new List<ulong>(), // Ignored channels are retrieved separately
                userCount = antiSpamStats?.UserStats?.Count ?? 0
            },
            antiAlt = new
            {
                enabled = antiAltStats != null,
                minAge = antiAltStats?.MinAge ?? "",
                minAgeMinutes = int.TryParse(antiAltStats?.MinAge ?? "0", out var minAge) ? minAge : 0,
                action = antiAltStats?.Action ?? 0,
                actionDuration = antiAltStats?.ActionDurationMinutes ?? 0,
                roleId = antiAltStats?.RoleId ?? 0,
                counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null,
                mentionThreshold = antiMassMentionStats?.AntiMassMentionSettings?.MentionThreshold ?? 0,
                maxMentionsInTimeWindow = antiMassMentionStats?.AntiMassMentionSettings?.MaxMentionsInTimeWindow ?? 0,
                timeWindowSeconds = antiMassMentionStats?.AntiMassMentionSettings?.TimeWindowSeconds ?? 0,
                action = antiMassMentionStats?.AntiMassMentionSettings?.Action ?? 0,
                muteTime = antiMassMentionStats?.AntiMassMentionSettings?.MuteTime ?? 0,
                roleId = antiMassMentionStats?.AntiMassMentionSettings?.RoleId ?? 0,
                ignoreBots = antiMassMentionStats?.AntiMassMentionSettings?.IgnoreBots ?? false,
                userCount = antiMassMentionStats?.UserStats?.Count ?? 0
            },
            antiPattern = new
            {
                enabled = antiPatternStats != null,
                action = antiPatternStats?.AntiPatternSettings?.Action ?? 0,
                punishDuration = antiPatternStats?.AntiPatternSettings?.PunishDuration ?? 0,
                roleId = antiPatternStats?.AntiPatternSettings?.RoleId ?? 0,
                checkAccountAge = antiPatternStats?.AntiPatternSettings?.CheckAccountAge ?? false,
                maxAccountAgeMonths = antiPatternStats?.AntiPatternSettings?.MaxAccountAgeMonths ?? 6,
                checkJoinTiming = antiPatternStats?.AntiPatternSettings?.CheckJoinTiming ?? false,
                maxJoinHours = antiPatternStats?.AntiPatternSettings?.MaxJoinHours ?? 48.0,
                checkBatchCreation = antiPatternStats?.AntiPatternSettings?.CheckBatchCreation ?? false,
                checkOfflineStatus = antiPatternStats?.AntiPatternSettings?.CheckOfflineStatus ?? false,
                checkNewAccounts = antiPatternStats?.AntiPatternSettings?.CheckNewAccounts ?? false,
                newAccountDays = antiPatternStats?.AntiPatternSettings?.NewAccountDays ?? 7,
                minimumScore = antiPatternStats?.AntiPatternSettings?.MinimumScore ?? 15,
                patternCount = antiPatternStats?.AntiPatternSettings?.AntiPatternPatterns?.Count() ?? 0,
                counter = antiPatternStats?.Counter ?? 0
            },
            antiMassPost = new
            {
                enabled = antiMassPostStats != null,
                action = antiMassPostStats?.AntiMassPostSettings?.Action ?? 0,
                channelThreshold = antiMassPostStats?.AntiMassPostSettings?.ChannelThreshold ?? 3,
                timeWindowSeconds = antiMassPostStats?.AntiMassPostSettings?.TimeWindowSeconds ?? 60,
                contentSimilarityThreshold = antiMassPostStats?.AntiMassPostSettings?.ContentSimilarityThreshold ?? 0.8,
                minContentLength = antiMassPostStats?.AntiMassPostSettings?.MinContentLength ?? 20,
                checkLinksOnly = antiMassPostStats?.AntiMassPostSettings?.CheckLinksOnly ?? true,
                checkDuplicateContent = antiMassPostStats?.AntiMassPostSettings?.CheckDuplicateContent ?? true,
                requireIdenticalContent = antiMassPostStats?.AntiMassPostSettings?.RequireIdenticalContent ?? false,
                caseSensitive = antiMassPostStats?.AntiMassPostSettings?.CaseSensitive ?? false,
                deleteMessages = antiMassPostStats?.AntiMassPostSettings?.DeleteMessages ?? true,
                notifyUser = antiMassPostStats?.AntiMassPostSettings?.NotifyUser ?? true,
                punishDuration = antiMassPostStats?.AntiMassPostSettings?.PunishDuration ?? 0,
                roleId = antiMassPostStats?.AntiMassPostSettings?.RoleId ?? 0,
                ignoreBots = antiMassPostStats?.AntiMassPostSettings?.IgnoreBots ?? true,
                maxMessagesTracked = antiMassPostStats?.AntiMassPostSettings?.MaxMessagesTracked ?? 50,
                userCount = antiMassPostStats?.UserStats?.Count ?? 0,
                counter = antiMassPostStats?.Counter ?? 0
            },
            antiPostChannel = new
            {
                enabled = antiPostChannelStats != null,
                action = antiPostChannelStats?.AntiPostChannelSettings?.Action ?? 0,
                deleteMessages = antiPostChannelStats?.AntiPostChannelSettings?.DeleteMessages ?? true,
                notifyUser = antiPostChannelStats?.AntiPostChannelSettings?.NotifyUser ?? true,
                punishDuration = antiPostChannelStats?.AntiPostChannelSettings?.PunishDuration ?? 0,
                roleId = antiPostChannelStats?.AntiPostChannelSettings?.RoleId ?? 0,
                ignoreBots = antiPostChannelStats?.AntiPostChannelSettings?.IgnoreBots ?? true,
                channelCount = antiPostChannelStats?.AntiPostChannelSettings?.AntiPostChannelChannels?.Count() ?? 0,
                counter = antiPostChannelStats?.Counter ?? 0
            },
            antiImageHash = new
            {
                enabled = imageHashStats != null,
                action = imageHashStats?.AntiImageHashSettings?.Action ?? 2,
                punishDuration = imageHashStats?.AntiImageHashSettings?.PunishDuration ?? 0,
                roleId = imageHashStats?.AntiImageHashSettings?.RoleId ?? 0,
                hashThreshold = imageHashStats?.AntiImageHashSettings?.HashThreshold ?? 31,
                deleteMessages = imageHashStats?.AntiImageHashSettings?.DeleteMessages ?? true,
                notifyUser = imageHashStats?.AntiImageHashSettings?.NotifyUser ?? true,
                ignoreBots = imageHashStats?.AntiImageHashSettings?.IgnoreBots ?? true,
                checkEmbeds = imageHashStats?.AntiImageHashSettings?.CheckEmbeds ?? true,
                checkBorders = imageHashStats?.AntiImageHashSettings?.CheckBorders ?? true,
                usePresetList = imageHashStats?.AntiImageHashSettings?.UsePresetList ?? false,
                presetTriggers = imageHashStats?.AntiImageHashSettings?.PresetTriggers ?? 0,
                blockedImageCount = imageHashStats?.Hashes?.Count ?? 0,
                maxImageSizeMb = imageHashStats?.AntiImageHashSettings?.MaxImageSizeMb ?? 8,
                counter = imageHashStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Configures anti-raid protection
    /// </summary>
    [HttpPut("anti-raid")]
    public async Task<IActionResult> ConfigureAntiRaid(ulong guildId, [FromBody] AntiRaidConfigRequest request)
    {
        if (request.Enabled)
        {
            var result = await protectionService.StartAntiRaidAsync(
                guildId,
                request.UserThreshold,
                request.Seconds,
                request.Action,
                request.PunishDuration);

            if (result == null)
                return BadRequest("Failed to start anti-raid protection");

            return Ok(new
            {
                success = true, settings = result
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiRaid(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Configures anti-spam protection
    /// </summary>
    [HttpPut("anti-spam")]
    public async Task<IActionResult> ConfigureAntiSpam(ulong guildId, [FromBody] AntiSpamConfigRequest request)
    {
        if (request.Enabled)
        {
            await protectionService.StartAntiSpamAsync(
                guildId,
                request.MessageThreshold,
                request.Action,
                request.MuteTime,
                request.RoleId);

            return Ok(new
            {
                success = true
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiSpam(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Manages ignored channels for anti-spam
    /// </summary>
    [HttpPost("anti-spam/ignored-channels/{channelId}")]
    public async Task<IActionResult> ToggleAntiSpamIgnoredChannel(ulong guildId, ulong channelId)
    {
        var added = await protectionService.AntiSpamIgnoreAsync(guildId, channelId);

        return Ok(new
        {
            added
            // Note: To get the complete list of ignored channels, query the database directly
        });
    }

    /// <summary>
    ///     Configures anti-alt protection
    /// </summary>
    [HttpPut("anti-alt")]
    public async Task<IActionResult> ConfigureAntiAlt(ulong guildId, [FromBody] AntiAltConfigRequest request)
    {
        if (request.Enabled)
        {
            await protectionService.StartAntiAltAsync(
                guildId,
                request.MinAgeMinutes,
                request.Action,
                request.ActionDurationMinutes,
                request.RoleId);

            return Ok(new
            {
                success = true
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiAlt(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Configures anti-mass mention protection
    /// </summary>
    [HttpPut("anti-mass-mention")]
    public async Task<IActionResult> ConfigureAntiMassMention(ulong guildId,
        [FromBody] AntiMassMentionConfigRequest request)
    {
        if (request.Enabled)
        {
            await protectionService.StartAntiMassMentionAsync(
                guildId,
                request.MentionThreshold,
                request.TimeWindowSeconds,
                request.MaxMentionsInTimeWindow,
                request.IgnoreBots,
                request.Action,
                request.MuteTime,
                request.RoleId);

            return Ok(new
            {
                success = true
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiMassMention(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Gets protection statistics and recent triggers
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetProtectionStatistics(ulong guildId)
    {
        await Task.CompletedTask;

        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats, antiMassPostStats,
                antiPostChannelStats) =
            protectionService.GetAntiStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                usersCount = antiRaidStats?.UsersCount ?? 0,
                recentUsers = antiRaidStats?.RaidUsers?.Select(u => u.Id).TakeLast(10).ToList() ?? new List<ulong>()
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                userCount = antiSpamStats?.UserStats?.Count ?? 0,
                topOffenders = antiSpamStats?.UserStats?
                    .OrderByDescending(x => x.Value.Count)
                    .Take(10)
                    .ToDictionary(x => x.Key, x => x.Value.Count) ?? new Dictionary<ulong, int>()
            },
            antiAlt = new
            {
                enabled = antiAltStats != null, counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null, userCount = antiMassMentionStats?.UserStats?.Count ?? 0
            },
            antiPattern = new
            {
                enabled = antiPatternStats != null, counter = antiPatternStats?.Counter ?? 0
            },
            antiMassPost = new
            {
                enabled = antiMassPostStats != null,
                userCount = antiMassPostStats?.UserStats?.Count ?? 0,
                counter = antiMassPostStats?.Counter ?? 0
            },
            antiPostChannel = new
            {
                enabled = antiPostChannelStats != null, counter = antiPostChannelStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Configures anti-pattern protection
    /// </summary>
    [HttpPut("anti-pattern")]
    public async Task<IActionResult> ConfigureAntiPattern(ulong guildId, [FromBody] AntiPatternConfigRequest request)
    {
        if (request.Enabled)
        {
            var result = await protectionService.StartAntiPatternAsync(
                guildId,
                request.Action,
                request.PunishDuration,
                request.RoleId,
                request.CheckAccountAge,
                request.MaxAccountAgeMonths,
                request.CheckJoinTiming,
                request.MaxJoinHours,
                request.CheckBatchCreation,
                request.CheckOfflineStatus,
                request.CheckNewAccounts,
                request.NewAccountDays,
                request.MinimumScore);

            if (result == null)
                return BadRequest("Failed to start anti-pattern protection");

            return Ok(new
            {
                success = true, settings = result
            });
        }

        var success = await protectionService.TryStopAntiPattern(guildId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Adds a pattern to anti-pattern protection
    /// </summary>
    [HttpPost("anti-pattern/patterns")]
    public async Task<IActionResult> AddPattern(ulong guildId, [FromBody] AddPatternRequest request)
    {
        var success = await protectionService.AddPatternAsync(
            guildId,
            request.Pattern,
            request.Name,
            request.CheckUsername,
            request.CheckDisplayName);

        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Removes a pattern from anti-pattern protection
    /// </summary>
    [HttpDelete("anti-pattern/patterns/{patternId}")]
    public async Task<IActionResult> RemovePattern(ulong guildId, int patternId)
    {
        var success = await protectionService.RemovePatternAsync(guildId, patternId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Updates anti-pattern configuration
    /// </summary>
    [HttpPatch("anti-pattern/config")]
    public async Task<IActionResult> UpdateAntiPatternConfig(ulong guildId,
        [FromBody] UpdateAntiPatternConfigRequest request)
    {
        var success = await protectionService.UpdateAntiPatternConfigAsync(
            guildId,
            request.CheckAccountAge,
            request.MaxAccountAgeMonths,
            request.CheckJoinTiming,
            request.MaxJoinHours,
            request.CheckBatchCreation,
            request.CheckOfflineStatus,
            request.CheckNewAccounts,
            request.NewAccountDays,
            request.MinimumScore);

        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Gets all anti-pattern patterns for a guild
    /// </summary>
    [HttpGet("anti-pattern/patterns")]
    public async Task<IActionResult> GetAntiPatternPatterns(ulong guildId)
    {
        var patterns = await protectionService.GetAntiPatternPatternsAsync(guildId);
        return Ok(patterns);
    }

    /// <summary>
    ///     Configures anti-image-hash protection
    /// </summary>
    [HttpPut("anti-image-hash")]
    public async Task<IActionResult> ConfigureAntiImageHash(ulong guildId,
        [FromBody] AntiImageHashConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (!request.Enabled)
        {
            var stopped = await protectionService.TryStopAntiImageHash(guildId);
            return Ok(new
            {
                success = stopped
            });
        }

        var result = await protectionService.StartAntiImageHashAsync(
            guildId,
            request.Action,
            request.PunishDuration,
            request.RoleId,
            request.HashThreshold,
            request.DeleteMessages,
            request.NotifyUser,
            request.IgnoreBots,
            request.CheckEmbeds,
            request.CheckBorders,
            request.UsePresetList,
            request.MaxImageSizeMb);

        if (result == null)
            return BadRequest("Failed to start anti-image-hash protection");

        return Ok(new
        {
            success = true, settings = result.AntiImageHashSettings
        });
    }

    /// <summary>
    ///     Gets blocked image hashes for a guild
    /// </summary>
    [HttpGet("anti-image-hash/hashes")]
    public async Task<IActionResult> GetBannedImageHashes(ulong guildId)
    {
        var hashes = await protectionService.GetBannedImageHashesAsync(guildId);
        return Ok(hashes);
    }

    /// <summary>
    ///     Adds a blocked image hash for a guild
    /// </summary>
    [HttpPost("anti-image-hash/hashes")]
    public async Task<IActionResult> AddBannedImageHash(ulong guildId, [FromBody] AddBannedImageHashRequest? request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        var hashSet = await ResolveHashSetAsync(request);
        if (hashSet is null)
            return BadRequest("Provide a valid PDQ hash, imageUrl, or imageBase64");

        if (hashSet.Quality < ImageHashingService.MinReliableQuality)
            return BadRequest("Image quality is too low for reliable matching");

        var entry = await protectionService.AddBannedImageHashAsync(
            guildId,
            hashSet,
            request.Name,
            request.ImageUrl,
            request.AddedBy,
            request.Action,
            request.PunishDuration,
            request.RoleId);

        if (entry == null)
            return Conflict("Image hash already exists or is invalid");

        return Ok(entry);
    }

    /// <summary>
    ///     Removes a blocked image hash from a guild
    /// </summary>
    [HttpDelete("anti-image-hash/hashes/{hashId:int}")]
    public async Task<IActionResult> RemoveBannedImageHash(ulong guildId, int hashId)
    {
        var success = await protectionService.RemoveBannedImageHashAsync(guildId, hashId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Computes a PDQ hash for an image without adding it to the blocklist
    /// </summary>
    [HttpPost("anti-image-hash/compute")]
    public async Task<IActionResult> ComputeImageHash(ulong guildId, [FromBody] AddBannedImageHashRequest? request)
    {
        await Task.CompletedTask;

        if (request == null)
            return BadRequest("Request body is required");

        var hashSet = await ResolveHashSetAsync(request);
        if (hashSet is null)
            return BadRequest("Provide a valid imageUrl, imageBase64, or PDQ hash");

        return Ok(new
        {
            hashSet.Hash,
            hashSet.Quality,
            hashSet.Variants,
            reliable = hashSet.Quality >= ImageHashingService.MinReliableQuality,
            minQuality = ImageHashingService.MinReliableQuality
        });
    }

    /// <summary>
    ///     Toggles the shipped known scam image preset list for a guild
    /// </summary>
    [HttpPost("anti-image-hash/preset/{enabled:bool}")]
    public async Task<IActionResult> SetAntiImageHashPreset(ulong guildId, bool enabled)
    {
        var success = await protectionService.SetPresetScamImagesAsync(guildId, enabled);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Toggles a role as exempt from anti-image-hash protection
    /// </summary>
    [HttpPost("anti-image-hash/ignored-roles/{roleId}")]
    public async Task<IActionResult> ToggleAntiImageHashIgnoredRole(ulong guildId, ulong roleId)
    {
        var added = await protectionService.ToggleAntiImageHashIgnoredRoleAsync(guildId, roleId);
        return Ok(new
        {
            added
        });
    }

    /// <summary>
    ///     Toggles a channel as exempt from anti-image-hash protection
    /// </summary>
    [HttpPost("anti-image-hash/ignored-channels/{channelId}")]
    public async Task<IActionResult> ToggleAntiImageHashIgnoredChannel(ulong guildId, ulong channelId)
    {
        var added = await protectionService.ToggleAntiImageHashIgnoredChannelAsync(guildId, channelId);
        return Ok(new
        {
            added
        });
    }

    private async Task<ImageHashSet?> ResolveHashSetAsync(AddBannedImageHashRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            return await imageHashing.ComputeHashSetFromUrlAsync(request.ImageUrl).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            var data = request.ImageBase64;
            var commaIndex = data.IndexOf(',');
            if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
                data = data[(commaIndex + 1)..];

            try
            {
                var bytes = Convert.FromBase64String(data);
                return imageHashing.ComputeHashSet(bytes);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return !string.IsNullOrWhiteSpace(request.Hash) &&
               ImageHashingService.TryParseHash(request.Hash, out _)
            ? new ImageHashSet(request.Hash.Trim().ToLowerInvariant(), 100, [])
            : null;
    }
}