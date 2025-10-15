using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
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
using Mewdeko.Database.Impl;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Mewdeko.Modules.Nsfw;
using Mewdeko.Modules.Patreon.Services;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NekosBestApiNet;
using Serilog;
using ZiggyCreatures.Caching.Fusion;
using RunMode = Discord.Commands.RunMode;

namespace Mewdeko;

/// <summary>
///     The main entry point class for the Mewdeko application.
///     Handles initialization, dependency injection, and starting the bot or web host.
/// </summary>
public class Program
{
    /// <summary>
    ///     Gets or sets the shared data cache instance.
    /// </summary>
    private static IDataCache Cache { get; set; } = null!;

    /// <summary>
    ///     The entry point of the application. Configures logging, dependencies, migrations,
    ///     and starts either the web host and bot or just the bot based on configuration.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation of running the application.</returns>
    public static async Task Main(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var log = LogSetup.SetupLogger("Startup"); // Initial logger name

        // Load credentials first to check if setup was already completed
        var credentials = new BotCredentials();

        // Check and install dependencies (pass setup status to avoid prompting if already done)
        DependencyInstaller.CheckAndInstallDependencies(credentials.PsqlConnectionString,
            credentials.PostgresSetupCompleted, credentials.RedisConnections);
        var dbUpgrader = new DatabaseUpgrader(credentials.PsqlConnectionString);

        // Test connection first
        if (!dbUpgrader.TestConnection())
        {
            log.Error("Failed to connect to database! Check connection string.");
            Helpers.ReadErrorAndExit(6);
            return;
        }

        // Check if upgrade is needed
        if (dbUpgrader.IsUpgradeRequired())
        {
            log.Information("Database upgrade required. Running migrations...");
            var scriptsToExecute = dbUpgrader.GetScriptsToExecute();
            log.Information("Scripts to execute: {Scripts}", string.Join(", ", scriptsToExecute));

            var migrationResult = dbUpgrader.PerformUpgrade();
            if (!migrationResult.Successful)
            {
                log.Error("Database migration failed! Error: {Error}", migrationResult.Error);
                Helpers.ReadErrorAndExit(6);
                return;
            }

            log.Information("Database migrations completed successfully");
        }
        else
        {
            log.Information("Database is up to date, no migrations needed");
        }

        var discordRestClient = new DiscordRestClient();
        await discordRestClient.LoginAsync(TokenType.Bot, credentials.Token);
        var botGatewayInfo = await discordRestClient.GetBotGatewayAsync();
        var serverCount = (await discordRestClient.GetCurrentBotInfoAsync()).ApproximateGuildCount;
        await discordRestClient.LogoutAsync();
        Cache = new RedisCache(credentials, botGatewayInfo.Shards);

        if (!Uri.TryCreate(credentials.LavalinkUrl, UriKind.Absolute, out _))
        {
            log.Error("The Lavalink URL is invalid! Please check the Lavalink URL in the configuration");
            Helpers.ReadErrorAndExit(5);
        }

        if (credentials.IsApiEnabled)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Sentry for ASP.NET Core
            if (!string.IsNullOrWhiteSpace(credentials.SentryDsn))
            {
                builder.WebHost.UseSentry(o =>
                {
                    o.Dsn = credentials.SentryDsn;
                    o.TracesSampleRate = 1.0;
                    o.AttachStacktrace = true;
                    o.SendDefaultPii = false;
                    o.MaxBreadcrumbs = 100;
                });
            }

            builder.Logging.ClearProviders();
            builder.Services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
            ConfigureServices(builder.Services, credentials, Cache, serverCount.GetValueOrDefault());

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
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressModelStateInvalidFilter = true;
                });
            ;
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
                .AddPolicy("ApiKeyPolicy",
                    policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ApiKey"))
                .AddPolicy("TopggPolicy",
                    policy => policy.RequireClaim(AuthHandler.TopggClaim)
                        .AddAuthenticationSchemes(AuthHandler.SchemeName));

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("BotInstancePolicy", policy =>
                {
                    policy
                        .WithOrigins($"http://localhost:{credentials.ApiPort}",
                            $"https://localhost:{credentials.ApiPort}", "https://mewdeko.tech")
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
                    log.Error(ex, "Error processing request: {Method} {Path}",
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
                            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, false, -1,
                                true);
                            requestBody = reader.ReadToEndAsync().Result;
                            httpContext.Request.Body.Position = 0;
                        }

                        diagnosticContext.Set("RequestBody", requestBody);
                        diagnosticContext.Set("QueryString", httpContext.Request.QueryString);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Error reading request body for logging");
                        diagnosticContext.Set("RequestBody", "Error reading request body");
                    }
                };
            });

            if (builder.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120)
            });
            app.UseAuthorization();
            app.MapControllers();

            foreach (var address in app.Urls) log.Information("API Listening on {Address}", address);
            await app.RunAsync();
        }
        else
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureServices((_, services) =>
                {
                    ConfigureServices(services, credentials, Cache, serverCount.GetValueOrDefault());
                })
                .Build();

            log.Information("API is disabled. Starting bot only.");
            await host.RunAsync();
        }
    }

    /// <summary>
    ///     Configures the shared services for the application (both bot and API).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="credentials">The bot credentials.</param>
    /// <param name="cache">The shared data cache instance.</param>
    /// <param name="serverCount">The current server count for configuration scaling.</param>
    private static void ConfigureServices(IServiceCollection services, BotCredentials credentials, IDataCache cache,
        int serverCount)
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

        services.AddSerilog((serviceProvider, loggerConfiguration) =>
        {
            LogSetup.ConfigureLogger(loggerConfiguration, credentials.SentryDsn);
        });
        services.AddSingleton(client);
        services.AddSingleton(credentials);
        services.AddSingleton(cache);
        services.AddSingleton(cache.Redis);

        services.AddSingleton<FontProvider>();
            //.AddSingleton<ApiKeyRepository>(provider => new ApiKeyRepository("Data Source=mewdeko.db"))
            //.AddSingleton<ApiService>()
        services.AddSingleton<IBotCredentials>(credentials);

        services.AddSingleton<IDataConnectionFactory>(sp =>
            new PostgreSqlConnectionFactory(credentials.PsqlConnectionString));

        var options = serverCount switch
        {
            > 10000 => new EventHandlerOptions().CreateHighTrafficProfile(),
            > 1000 => new EventHandlerOptions
            {
                MaxQueueSize = 20000,
                PresenceUpdateRateLimit = 200,
                TypingRateLimit = 75,
                MessageBatchSize = 75,
                MessageBatchInterval = TimeSpan.FromMilliseconds(75)
            },
            _ => new EventHandlerOptions().CreateLowTrafficProfile()
        };

        options.Validate();
        services.AddSingleton(options);
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
        // Optimized MemoryCache configuration for performance
        services.AddMemoryCache(options =>
        {
            // Scan for expired entries every 2 minutes for responsiveness
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(2);

            // Disable size tracking for better performance
            options.TrackLinkedCacheEntries = false;
        });
        services.AddLavalink()
            .ConfigureLavalink(x =>
            {
                x.Passphrase = "Hope4a11";
                x.BaseAddress = new Uri(credentials.LavalinkUrl);
            });
        services.AddSingleton<ISearchImagesService, SearchImagesService>();
        services.AddSingleton<ToneTagService>();
        services.AddTransient<GuildSettingsService>();

        // Optimized FusionCache configuration for performance and reliability
        services.AddFusionCache()
            .WithOptions(options =>
            {
                // Circuit breaker for distributed cache stability
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(2);

                // Optimized logging levels to reduce noise
                options.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;
                options.DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Debug;
                options.FailSafeActivationLogLevel = LogLevel.Debug;
                options.FactoryErrorsLogLevel = LogLevel.Error;
                options.DistributedCacheErrorsLogLevel = LogLevel.Error;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Default cache duration
                Duration = TimeSpan.FromMinutes(10),

                // Fail-safe configuration for reliability
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

                // Factory timeouts scaled by server count
                FactorySoftTimeout = serverCount > 1000 ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(1),
                FactoryHardTimeout = serverCount > 1000 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(3),

                // Distributed cache timeouts
                DistributedCacheSoftTimeout = serverCount > 1000 ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(2),
                DistributedCacheHardTimeout = serverCount > 1000 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(3),

                // Background operations for better performance
                AllowBackgroundDistributedCacheOperations = true,

                // Jittering to prevent thundering herd
                JitterMaxDuration = TimeSpan.FromSeconds(2)
            })
            .TryWithAutoSetup();

        if (credentials.UseGlobalCurrency)
            services.AddTransient<ICurrencyService, GlobalCurrencyService>();
        else
            services.AddTransient<ICurrencyService, GuildCurrencyService>();

        services.AddHttpClient();
        services.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        services.AddHttpClient("openmeteo")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            })
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                client.DefaultRequestHeaders.Add("User-Agent", "MewdekoBot/1.0 (+https://github.com/)");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(15);
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
        services.AddSingleton<PatreonApiClient>();
        services.AddHostedService<MewdekoService>();
        services.AddHostedService<ScheduledDeletionService>();
        services.AddHostedService<PatreonService>();
    }
}