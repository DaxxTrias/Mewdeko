using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;
using Fergun.Interactive;
using Lavalink4NET.Extensions;
using MartineApiNet;
using Mewdeko.AuthHandlers;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Constraints;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Database.EF.EFCore;
using Mewdeko.Database.Impl;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Mewdeko.Modules.Nsfw;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NekosBestApiNet;
using Serilog;
using ZiggyCreatures.Caching.Fusion;
using RunMode = Discord.Commands.RunMode;

namespace Mewdeko;

/// <summary>
/// The main entry point class for the Mewdeko application.
/// Handles initialization, dependency injection, and starting the bot or web host.
/// </summary>
public class Program
{
    /// <summary>
    /// Gets or sets the shared data cache instance.
    /// </summary>
    private static IDataCache Cache { get; set; } = null!;

    /// <summary>
    /// The entry point of the application. Configures logging, dependencies, migrations,
    /// and starts either the web host and bot or just the bot based on configuration.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation of running the application.</returns>
    public static async Task Main(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var log = LogSetup.SetupLogger("Startup"); // Initial logger name
        var credentials = new BotCredentials();
        DependencyInstaller.CheckAndInstallDependencies(credentials.PsqlConnectionString);

        var discordRestClient = new DiscordRestClient();
        await discordRestClient.LoginAsync(TokenType.Bot, credentials.Token);
        var botGatewayInfo = await discordRestClient.GetBotGatewayAsync();
        await discordRestClient.LogoutAsync();
        Cache = new RedisCache(credentials, botGatewayInfo.Shards);

        if (!Uri.TryCreate(credentials.LavalinkUrl, UriKind.Absolute, out _))
        {
            Log.Error("The Lavalink URL is invalid! Please check the Lavalink URL in the configuration");
            Helpers.ReadErrorAndExit(5);
        }

        try
        {
            await using var dbMigrationContext = new MewdekoPostgresContext(new DbContextOptions<MewdekoPostgresContext>());
            await dbMigrationContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database migration failed: {ErrorMessage}", ex.Message);
            Helpers.ReadErrorAndExit(6);
        }

        if (credentials.IsApiEnabled)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();

            ConfigureServices(builder.Services, credentials, Cache);

            builder.WebHost.UseUrls($"http://localhost:{credentials.ApiPort}");
            builder.Services.Configure<RouteOptions>(options =>
            {
                options.ConstraintMap["ulong"] = typeof(UlongRouteConstraint);
            });
            builder.Services.AddTransient<IApiKeyValidation, ApiKeyValidation>();
            builder.Services.AddAuthorization();

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.DefaultIgnoreCondition =
                        JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressModelStateInvalidFilter = true;
                });;
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(x =>
            {
                x.AddSecurityDefinition("ApiKeyHeader", new OpenApiSecurityScheme
                {
                    Name = "x-api-key",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Description = "Authorization by x-api-key inside request's header"
                });
                x.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme, Id = "ApiKeyHeader"
                            }
                        },
                        []
                    }
                });
            });

            var auth = builder.Services.AddAuthentication(options =>
            {
                options.AddScheme<AuthHandler>(AuthHandler.SchemeName, AuthHandler.SchemeName);
            });
            auth.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("ApiKeyPolicy", policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ApiKey"))
                .AddPolicy("TopggPolicy", policy => policy.RequireClaim(AuthHandler.TopggClaim).AddAuthenticationSchemes(AuthHandler.SchemeName));

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("BotInstancePolicy", policy =>
                {
                    policy
                        .WithOrigins($"http://localhost:{credentials.ApiPort}", $"https://localhost:{credentials.ApiPort}", "https://mewdeko.tech")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            var app = builder.Build();

            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing request: {Method} {Path}",
                        context.Request.Method,
                        context.Request.Path);
                    throw;
                }
            });
            app.UseCors("BotInstancePolicy");
            app.UseSerilogRequestLogging(options =>
            {
                options.IncludeQueryInRequestPath = true;
                options.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms\n{RequestBody}";
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    try
                    {
                        var requestBody = string.Empty;
                        if (httpContext.Request.ContentLength > 0)
                        {
                            httpContext.Request.EnableBuffering();
                            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, false, -1, true);
                            requestBody = reader.ReadToEndAsync().Result;
                            httpContext.Request.Body.Position = 0;
                        }
                        diagnosticContext.Set("RequestBody", requestBody);
                        diagnosticContext.Set("QueryString", httpContext.Request.QueryString);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error reading request body for logging");
                        diagnosticContext.Set("RequestBody", "Error reading request body");
                    }
                };
            });

            if (builder.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(120) });
            app.UseAuthorization();
            app.MapControllers();

            foreach (var address in app.Urls) Log.Information("API Listening on {Address}", address);
            await app.RunAsync();
        }
        else
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureServices((_, services) =>
                {
                    ConfigureServices(services, credentials, Cache);
                })
                .Build();

            Log.Information("API is disabled. Starting bot only.");
            await host.RunAsync();
        }
    }

    /// <summary>
    /// Configures the shared services for the application (both bot and API).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="credentials">The bot credentials.</param>
    /// <param name="cache">The shared data cache instance.</param>
    private static void ConfigureServices(IServiceCollection services, BotCredentials credentials, IDataCache cache)
    {
        var client = new DiscordShardedClient(new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            LogLevel = LogSeverity.Info,
            ConnectionTimeout = int.MaxValue,
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All,
            FormatUsersInBidirectionalUnicode = false,
            LogGatewayIntentWarnings = false,
            DefaultRetryMode = RetryMode.RetryRatelimit,
            TotalShards = credentials.TotalShards
        });

        services.AddSerilog(LogSetup.SetupLogger("Mewdeko"));
        services.AddSingleton(client);
        services.AddSingleton(credentials);
        services.AddSingleton(cache);
        services.AddSingleton(cache.Redis);

        services.AddSingleton<FontProvider>();
        services.AddSingleton<IBotCredentials>(credentials);

        services.AddDbContext<MewdekoPostgresContext>(options =>
            options.UseNpgsql(credentials.PsqlConnectionString));

        services.AddSingleton<IDataConnectionFactory>(sp =>
            new PostgreSqlConnectionFactory(credentials.PsqlConnectionString));

        services.AddSingleton<EventHandler>();
        services.AddSingleton(new CommandService(new CommandServiceConfig
        {
            CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async
        }));
        services.AddSingleton(new MartineApi());
        services.AddTransient<ISeria, JsonSeria>();
        services.AddTransient<IPubSub, RedisPubSub>();
        services.AddTransient<IConfigSeria, YamlSeria>();
        services.AddSingleton(new InteractiveService(client, new InteractiveConfig
        {
            ReturnAfterSendingPaginator = true
        }));
        services.AddSingleton(new NekosBestApi("Mewdeko"));
        services.AddSingleton(p => new InteractionService(p.GetRequiredService<DiscordShardedClient>()));
        services.AddSingleton<Localization>();
        services.AddSingleton<GeneratedBotStrings>();
        services.AddSingleton<BotConfigService>();
        services.AddSingleton<BotConfig>();
        services.AddConfigServices();
        services.AddBotStringsServices(credentials.TotalShards);
        services.AddMemoryCache();
        services.AddLavalink()
            .ConfigureLavalink(x =>
            {
                x.Passphrase = "Hope4a11";
                x.BaseAddress = new Uri(credentials.LavalinkUrl);
            });
        services.AddSingleton<ISearchImagesService, SearchImagesService>();
        services.AddSingleton<ToneTagService>();
        services.AddTransient<GuildSettingsService>();

        services.AddFusionCache().TryWithAutoSetup();

        if (credentials.UseGlobalCurrency)
            services.AddTransient<ICurrencyService, GlobalCurrencyService>();
        else
            services.AddTransient<ICurrencyService, GuildCurrencyService>();

        services.AddHttpClient();
        services.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.Scan(scan => scan.FromAssemblyOf<IReadyExecutor>()
            .AddClasses(classes => classes.AssignableToAny(
                typeof(INService),
                typeof(IEarlyBehavior),
                typeof(ILateBlocker),
                typeof(IInputTransformer),
                typeof(ILateExecutor)))
            .AsSelfWithInterfaces()
            .WithSingletonLifetime()
        );

        services.AddSingleton<Mewdeko>();
        services.AddHostedService<MewdekoService>();
    }
}