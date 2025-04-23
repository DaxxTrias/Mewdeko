using System.IO;
using System.Net.Http;
using DataModel;
using Humanizer;
using LinqToDB;
using Mewdeko.Modules.Xp.Models;
using Serilog;
using SkiaSharp;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     A service for generating XP cards using the SkiaSharp graphics library.
/// </summary>
public class XpCardGenerator : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly XpService xpService;
    private readonly byte[] defaultBackground;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpCardGenerator" /> class.
    /// </summary>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="xpService">The XP service.</param>
    public XpCardGenerator(
        IDataConnectionFactory dbFactory,
        XpService xpService)
    {
        this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        this.xpService = xpService ?? throw new ArgumentNullException(nameof(xpService));
        defaultBackground = xpService.GetDefaultBackgroundImage();
    }

    /// <summary>
    ///     Generates an XP image for a user based on their statistics.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <returns>A stream containing the generated image.</returns>
    public async Task<Stream> GenerateXpImageAsync(IGuildUser user)
    {
        var stats = await GetFullUserStatsAsync(user);
        var template = await GetTemplateAsync(user.Guild.Id);
        return await GenerateXpImageAsync(stats, template);
    }

    /// <summary>
    ///     Gets full user XP statistics.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <returns>The full user statistics.</returns>
    private async Task<FullUserStats> GetFullUserStatsAsync(IGuildUser user)
    {
        var xpStats = await xpService.GetUserXpStatsAsync(user.GuildId, user.Id);
        var timeOnLevel = await xpService.GetTimeOnCurrentLevelAsync(user.GuildId, user.Id);

        await using var db = await dbFactory.CreateConnectionAsync();
        var userXp = await db.GuildUserXps
            .FirstOrDefaultAsync(x => x.GuildId == user.GuildId && x.UserId == user.Id);

        if (userXp == null)
        {
            userXp = new GuildUserXp
            {
                GuildId = user.GuildId, UserId = user.Id, LastActivity = DateTime.UtcNow, LastLevelUp = DateTime.UtcNow
            };
        }

        return new FullUserStats
        {
            User = user,
            Guild = new UserLevelStats
            {
                Level = xpStats.Level, LevelXp = xpStats.LevelXp, RequiredXp = xpStats.RequiredXp
            },
            GuildRanking = xpStats.Rank,
            FullGuildStats = userXp
        };
    }

    /// <summary>
    ///     Generates an XP image for a user based on their statistics and template.
    /// </summary>
    /// <param name="stats">The user statistics.</param>
    /// <param name="template">The template to use.</param>
    /// <returns>A stream containing the generated image.</returns>
    private async Task<Stream> GenerateXpImageAsync(FullUserStats stats, Template template)
    {
        // Load the background image
        await using var xpstream = new MemoryStream();
        var xpImage = await GetXpImageAsync(stats.FullGuildStats.GuildId);
        if (xpImage is not null)
        {
            using var httpClient = new HttpClient();
            var httpResponse = await httpClient.GetAsync(xpImage);
            if (httpResponse.IsSuccessStatusCode)
            {
                await httpResponse.Content.CopyToAsync(xpstream);
                xpstream.Position = 0;
            }
        }
        else
        {
            await xpstream.WriteAsync(defaultBackground.AsMemory(0, defaultBackground.Length));
            xpstream.Position = 0;
        }

        var imgData = SKData.Create(xpstream);
        var originalImg = SKBitmap.Decode(imgData);

        // Use template dimensions for the canvas
        var canvasWidth = template.OutputSizeX;
        var canvasHeight = template.OutputSizeY;

        // Create a surface with template dimensions
        using var surface = SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight));
        var canvas = surface.Canvas;


        // Scale the background image to fit template dimensions
        var destRect = new SKRect(0, 0, canvasWidth, canvasHeight);
        var srcRect = new SKRect(0, 0, originalImg.Width, originalImg.Height);
        canvas.DrawBitmap(originalImg, srcRect, destRect);

        // Create general paint for drawing
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill
        };

        // Draw the username
        if (template.TemplateUser.ShowText)
        {
            var color = SKColor.Parse(template.TemplateUser.TextColor);
            paint.Color = color;

            // Create a font for the username using modern APIs
            using var font = new SKFont
            {
                Size = template.TemplateUser.FontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            var username = stats.User.Username;
            canvas.DrawText(username, template.TemplateUser.TextX, template.TemplateUser.TextY,
                SKTextAlign.Left, font, paint);
        }

        // Draw the guild level
        if (template.TemplateGuild.ShowGuildLevel)
        {
            var color = SKColor.Parse(template.TemplateGuild.GuildLevelColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.TemplateGuild.GuildLevelFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            canvas.DrawText(stats.Guild.Level.ToString(), template.TemplateGuild.GuildLevelX,
                template.TemplateGuild.GuildLevelY, SKTextAlign.Left, font, paint);
        }

        var guild = stats.Guild;

        // Draw the XP bar
        if (template.TemplateBar.ShowBar)
        {
            var xpPercent = guild.LevelXp / (float)guild.RequiredXp;
            DrawXpBar(xpPercent, template.TemplateBar, canvas);
        }

        // Draw awarded XP
        if (stats.FullGuildStats.BonusXp != 0 && template.ShowAwarded)
        {
            var sign = stats.FullGuildStats.BonusXp > 0 ? "+ " : "";
            var color = SKColor.Parse(template.AwardedColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.AwardedFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            var text = $"({sign}{stats.FullGuildStats.BonusXp})";
            canvas.DrawText(text, template.AwardedX, template.AwardedY,
                SKTextAlign.Left, font, paint);
        }

        // Draw guild rank
        if (template.TemplateGuild.ShowGuildRank)
        {
            var color = SKColor.Parse(template.TemplateGuild.GuildRankColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.TemplateGuild.GuildRankFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            canvas.DrawText(stats.GuildRanking.ToString(), template.TemplateGuild.GuildRankX,
                template.TemplateGuild.GuildRankY, SKTextAlign.Left, font, paint);
        }

        // Draw time on level
        if (template.ShowTimeOnLevel)
        {
            var color = SKColor.Parse(template.TimeOnLevelColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.TimeOnLevelFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            var text = GetTimeSpent(stats.FullGuildStats.LastLevelUp);
            canvas.DrawText(text, template.TimeOnLevelX, template.TimeOnLevelY,
                SKTextAlign.Left, font, paint);
        }

        // Draw user avatar
        if (stats.User.GetAvatarUrl() != null && template.TemplateUser.ShowIcon)
        {
            try
            {
                var avatarUrl = GetAvatarUrl(stats.User);

                using var httpClient = new HttpClient();
                var httpResponse = await httpClient.GetAsync(avatarUrl);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var avatarData = await httpResponse.Content.ReadAsByteArrayAsync();
                    await using var avatarStream = new MemoryStream(avatarData);
                    var avatarImgData = SKData.Create(avatarStream);
                    var avatarImg = SKBitmap.Decode(avatarImgData);

                    // Create a new bitmap with the desired size
                    var targetSize = new SKImageInfo(
                        template.TemplateUser.IconSizeX,
                        template.TemplateUser.IconSizeY);

                    // Create sampling options for scaling the avatar
                    var avatarSamplingOptions = new SKSamplingOptions(
                        SKFilterMode.Linear,
                        SKMipmapMode.Nearest);

                    var resizedAvatar = new SKBitmap(targetSize);
                    avatarImg.ScalePixels(resizedAvatar, avatarSamplingOptions);

                    // Apply rounded corners
                    var roundedAvatar = ApplyRoundedCorners(resizedAvatar, template.TemplateUser.IconSizeX / 2);

                    // Draw the avatar onto the main image
                    canvas.DrawImage(roundedAvatar, template.TemplateUser.IconX, template.TemplateUser.IconY);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error drawing avatar image: {Message}", ex.Message);
            }
        }

        // Convert to Stream and return
        var finalImage = surface.Snapshot();
        var finalData = finalImage.Encode(SKEncodedImageFormat.Png, 100);
        return finalData.AsStream();
    }

    /// <summary>
    ///     Applies rounded corners to an image.
    /// </summary>
    /// <param name="src">The source image.</param>
    /// <param name="cornerRadius">The corner radius.</param>
    /// <returns>An image with rounded corners.</returns>
    private static SKImage ApplyRoundedCorners(SKBitmap src, float cornerRadius)
    {
        var width = src.Width;
        var height = src.Height;
        var info = new SKImageInfo(width, height);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        using var paint = new SKPaint
        {
            IsAntialias = true, Color = SKColors.White
        };

        var rect = new SKRect(0, 0, width, height);
        using var roundRect = new SKRoundRect();
        roundRect.SetRectRadii(rect, new[]
        {
            new SKPoint(cornerRadius, cornerRadius), new SKPoint(cornerRadius, cornerRadius),
            new SKPoint(cornerRadius, cornerRadius), new SKPoint(cornerRadius, cornerRadius)
        });

        // Clear canvas and create clipping region
        canvas.Clear(SKColors.Transparent);
        canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, true);

        // Draw the bitmap
        canvas.DrawBitmap(src, 0, 0, paint);

        return surface.Snapshot();
    }

    /// <summary>
    ///     Draws the XP progress bar.
    /// </summary>
    /// <param name="percent">The completion percentage.</param>
    /// <param name="info">The template bar information.</param>
    /// <param name="canvas">The canvas to draw on.</param>
    private static void DrawXpBar(float percent, TemplateBar info, SKCanvas canvas)
    {
        var x1 = info.BarPointAx;
        var y1 = info.BarPointAy;

        var x2 = info.BarPointBx;
        var y2 = info.BarPointBy;

        var length = info.BarLength * percent;

        float x3, x4, y3, y4;

        switch ((XpTemplateDirection)info.BarDirection)
        {
            case XpTemplateDirection.Down:
                x3 = x1;
                x4 = x2;
                y3 = y1 + length;
                y4 = y2 + length;
                break;
            case XpTemplateDirection.Up:
                x3 = x1;
                x4 = x2;
                y3 = y1 - length;
                y4 = y2 - length;
                break;
            case XpTemplateDirection.Left:
                x3 = x1 - length;
                x4 = x2 - length;
                y3 = y1;
                y4 = y2;
                break;
            default: // Right
                x3 = x1 + length;
                x4 = x2 + length;
                y3 = y1;
                y4 = y2;
                break;
        }

        using var path = new SKPath();
        path.MoveTo(x1, y1);
        path.LineTo(x3, y3);
        path.LineTo(x4, y4);
        path.LineTo(x2, y2);
        path.Close();

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill
        };

        var color = SKColor.Parse(info.BarColor);
        // Fixed bug: was using Green twice instead of Blue
        paint.Color = new SKColor(color.Red, color.Green, color.Blue, (byte)info.BarTransparency);
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    ///     Gets the template for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The template.</returns>
    public async Task<Template> GetTemplateAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var template = await db.Templates
            .LoadWithAsTable(t => t.TemplateUser)
            .LoadWithAsTable(t => t.TemplateBar)
            .LoadWithAsTable(t => t.TemplateClub)
            .LoadWithAsTable(t => t.TemplateGuild)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (template != null)
            return template;

        // Create related entities with default values
        var templateBar = new TemplateBar
        {
            // Default values are set in the class properties
            BarColor = "FF000000",
            BarPointAx = 319,
            BarPointAy = 119,
            BarPointBx = 284,
            BarPointBy = 250,
            BarLength = 452,
            BarTransparency = 90,
            BarDirection = (int)XpTemplateDirection.Right,
            ShowBar = true
        };

        var templateClub = new TemplateClub
        {
            // Default values are set in the class properties
            ClubIconX = 717,
            ClubIconY = 37,
            ClubIconSizeX = 49,
            ClubIconSizeY = 49,
            ShowClubIcon = true,
            ClubNameColor = "FF000000",
            ClubNameFontSize = 32,
            ClubNameX = 649,
            ClubNameY = 50,
            ShowClubName = true
        };

        var templateGuild = new TemplateGuild
        {
            // Default values are set in the class properties
            GuildLevelColor = "FF000000",
            GuildLevelFontSize = 27,
            GuildLevelX = 42,
            GuildLevelY = 206,
            ShowGuildLevel = true,
            GuildRankColor = "FF000000",
            GuildRankFontSize = 25,
            GuildRankX = 148,
            GuildRankY = 211,
            ShowGuildRank = true
        };

        var templateUser = new TemplateUser
        {
            // Default values are set in the class properties
            TextColor = "FF000000",
            FontSize = 50,
            TextX = 120,
            TextY = 70,
            ShowText = true,
            IconX = 27,
            IconY = 24,
            IconSizeX = 73,
            IconSizeY = 74,
            ShowIcon = true
        };

        // Important: Insert the related entities FIRST to get their IDs
        templateBar.Id = await db.InsertWithInt32IdentityAsync(templateBar);
        templateClub.Id = await db.InsertWithInt32IdentityAsync(templateClub);
        templateGuild.Id = await db.InsertWithInt32IdentityAsync(templateGuild);
        templateUser.Id = await db.InsertWithInt32IdentityAsync(templateUser);

        // Now create the Template with proper foreign key IDs
        var toAdd = new Template
        {
            GuildId = guildId,

            // Set the default values for Template
            OutputSizeX = 797,
            OutputSizeY = 279,
            TimeOnLevelFormat = "{0}d{1}h{2}m",
            TimeOnLevelX = 50,
            TimeOnLevelY = 204,
            TimeOnLevelFontSize = 20,
            TimeOnLevelColor = "FF000000",
            ShowTimeOnLevel = true,
            AwardedX = 445,
            AwardedY = 347,
            AwardedFontSize = 25,
            AwardedColor = "ffffffff",
            ShowAwarded = false,

            // Set the foreign key IDs
            TemplateBarId = templateBar.Id,
            TemplateClubId = templateClub.Id,
            TemplateGuildId = templateGuild.Id,
            TemplateUserId = templateUser.Id,

            // Set the navigation properties
            TemplateBar = templateBar,
            TemplateClub = templateClub,
            TemplateGuild = templateGuild,
            TemplateUser = templateUser
        };

        // Finally, insert the Template with the correct foreign keys
        await db.InsertAsync(toAdd);

        return await db.Templates
            .LoadWithAsTable(t => t.TemplateUser)
            .LoadWithAsTable(t => t.TemplateBar)
            .LoadWithAsTable(t => t.TemplateClub)
            .LoadWithAsTable(t => t.TemplateGuild)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);
    }

    /// <summary>
    ///     Gets the URL for a custom XP background image.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The image URL or null.</returns>
    private async Task<string> GetXpImageAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await db.GuildXpSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings != null && !string.IsNullOrEmpty(settings.CustomXpImageUrl))
        {
            return settings.CustomXpImageUrl;
        }

        return null;
    }

    /// <summary>
    ///     Gets the avatar URL for a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The avatar URL.</returns>
    private static string GetAvatarUrl(IUser user)
    {
        return user.GetAvatarUrl(ImageFormat.Png, 256) ?? user.GetDefaultAvatarUrl();
    }

    /// <summary>
    ///     Gets a formatted string for time spent on a level.
    /// </summary>
    /// <param name="time">The time to format.</param>
    /// <returns>A formatted time string.</returns>
    private static string GetTimeSpent(DateTime time)
    {
        var offset = DateTime.UtcNow - time;
        return $"{offset.Humanize()} ago";
    }
}

/// <summary>
///     Represents the full statistics for a user in a guild.
/// </summary>
public class FullUserStats
{
    /// <summary>
    ///     Gets or sets the user information.
    /// </summary>
    public IUser User { get; set; }

    /// <summary>
    ///     Gets or sets the guild level statistics.
    /// </summary>
    public UserLevelStats Guild { get; set; }

    /// <summary>
    ///     Gets or sets the user's ranking in the guild.
    /// </summary>
    public int GuildRanking { get; set; }

    /// <summary>
    ///     Gets or sets the full guild statistics.
    /// </summary>
    public GuildUserXp FullGuildStats { get; set; }
}

/// <summary>
///     Represents a user's level statistics.
/// </summary>
public class UserLevelStats
{
    /// <summary>
    ///     Gets or sets the user's level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the XP in the current level.
    /// </summary>
    public long LevelXp { get; set; }

    /// <summary>
    ///     Gets or sets the XP required for the next level.
    /// </summary>
    public long RequiredXp { get; set; }
}