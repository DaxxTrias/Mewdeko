using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;
using static Mewdeko.Modules.Utility.Services.AiService;

namespace Mewdeko.Modules.Utility;

public partial class SlashUtility
{
    /// <summary>
    ///     Commands for configuring and managing AI functionality.
    /// </summary>
    [Group("ai", "Configure and manage AI settings")]
    public class AiSlashCommands : MewdekoSlashSubmodule<AiService>
    {
        /// <summary>
        ///     Handles the button interaction for setting an AI API key, displaying a modal for secure input.
        /// </summary>
        /// <returns>A task representing the modal response operation.</returns>
        [ComponentInteraction("setaikey:*", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public Task AiKeyButton(int provider)
        {
            return RespondWithModalAsync<AiKeyModal>($"aikeymodal:{provider}");
        }

        /// <summary>
        ///     Legacy fallback for API key button interactions without an explicit provider payload.
        /// </summary>
        [ComponentInteraction("setaikey", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKeyButtonLegacy()
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            await RespondWithModalAsync<AiKeyModal>($"aikeymodal:{config.Provider}");
        }

        /// <summary>
        ///     Processes the submitted AI API key from the modal and updates the configuration.
        /// </summary>
        /// <param name="provider">The provider encoded in the modal custom ID.</param>
        /// <param name="modal">The modal containing the submitted API key.</param>
        /// <returns>A task representing the asynchronous configuration update operation.</returns>
        [ModalInteraction("aikeymodal:*", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKeyModal(int provider, AiKeyModal modal)
        {
            if (!Enum.IsDefined((AiProvider)provider))
            {
                await ctx.Interaction.SendErrorAsync("Unsupported provider selected for API key update.", Config);
                return;
            }

            var selectedProvider = (AiProvider)provider;
            await Service.SetProviderApiKey(ctx.Guild.Id, selectedProvider, modal.ApiKey);

            await ctx.Interaction.SendConfirmAsync(Strings.AiApiKeyUpdated(ctx.Guild.Id, selectedProvider));
        }

        /// <summary>
        ///     Legacy fallback for API key modal submissions without provider payload.
        /// </summary>
        /// <param name="modal">The modal containing the submitted API key.</param>
        [ModalInteraction("aikeymodal", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKeyModalLegacy(AiKeyModal modal)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            await Service.SetProviderApiKey(ctx.Guild.Id, (AiProvider)config.Provider, modal.ApiKey);
            await ctx.Interaction.SendConfirmAsync(Strings.AiApiKeyUpdated(ctx.Guild.Id, config.Provider));
        }

        /// <summary>
        ///     Configures AI functionality for a specific channel.
        /// </summary>
        /// <param name="channel">The channel to configure AI for. Defaults to current channel if not specified.</param>
        /// <param name="enabled">Whether to enable or disable AI in the channel.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("channel", "Configure AI for a channel")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiChannel(ITextChannel? channel = null, bool enabled = true)
        {
            channel ??= (ITextChannel)ctx.Channel;
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            config.Enabled = enabled;
            config.ChannelId = channel.Id;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiConfigUpdated(ctx.Guild.Id, channel.Mention))
                .Build());
        }

        /// <summary>
        ///     Sets or lists available AI models for a provider.
        /// </summary>
        /// <param name="provider">The AI provider to use.</param>
        /// <param name="model">The model ID to set. If null, lists available models.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("model", "Set the AI provider and model")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiModel(
            AiService.AiProvider provider,
            [Autocomplete(typeof(AiModelAutoCompleter))]
            string? model = null)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            var providerApiKey = await Service.GetProviderApiKey(ctx.Guild.Id, provider);
            if (string.IsNullOrWhiteSpace(providerApiKey))
            {
                await ctx.Interaction.SendErrorAsync($"No API key linked for {provider}. Set one using /ai key.",
                    Config);
                return;
            }

            var models = await Service.GetSupportedModels(provider, providerApiKey);

            if (model == null)
            {
                var modelList = string.Join("\n", models.Select(m => $"• {m.Name} (`{m.Id}`)"));
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(Strings.AiModelList(ctx.Guild.Id, provider.ToString(), modelList))
                    .Build());
                return;
            }

            if (!models.Any(m => m.Id.Equals(model, StringComparison.OrdinalIgnoreCase)))
            {
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.AiInvalidModel(ctx.Guild.Id, model, provider.ToString()))
                    .Build());
                return;
            }

            var updated = await Service.SetProviderModel(ctx.Guild.Id, provider, model);
            if (!updated)
            {
                await ctx.Interaction.SendErrorAsync(
                    $"Couldn't set model for {provider} because that provider is not linked yet.",
                    Config);
                return;
            }

            config.Provider = (int)provider;
            config.Model = model;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiModelChanged(ctx.Guild.Id, model))
                .Build());
        }

        /// <summary>
        ///     Sets the default provider route for AI chat.
        /// </summary>
        [SlashCommand("default-provider", "Set the default provider used when no selector is given")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiDefaultProvider(AiService.AiProvider provider)
        {
            var success = await Service.SetDefaultProvider(ctx.Guild.Id, provider);
            if (!success)
            {
                await ctx.Interaction.SendErrorAsync(
                    $"Provider {provider} is not linked yet. Set an API key for it first.",
                    Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync($"Default AI provider set to {provider}.");
        }

        /// <summary>
        ///     Sets the API key for the configured AI provider.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("key", "Set the API key for the AI service")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKey(AiService.AiProvider provider = AiService.AiProvider.OpenAi)
        {
            var component = new ComponentBuilder()
                .WithButton($"{Strings.AiKeyClickToSet(ctx.Guild.Id)} ({provider})",
                    $"setaikey:{(int)provider}")
                .Build();
            await ctx.Interaction.RespondAsync(Strings.EmptyResponse(ctx.Guild.Id), components: component);
        }

        /// <summary>
        ///     Sets the system prompt used for AI conversations.
        /// </summary>
        /// <param name="prompt">The system prompt to set.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("prompt", "Set the system prompt for the AI")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiPrompt(string prompt)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            config.SystemPrompt = prompt;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiSystemPromptUpdated(ctx.Guild.Id))
                .Build());
        }

        /// <summary>
        ///     Shows the current AI configuration for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("config", "Show current AI configuration")]
        [RequireContext(ContextType.Guild)]
        public async Task AiConfig()
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            var links = await Service.GetProviderLinks(ctx.Guild.Id);

            // Get provider enum value and its integer index
            var providerEnum = (AiProvider)config.Provider;
            var providerIndex = (int)providerEnum;
            var providerLabel = $"{providerEnum} ({providerIndex})";
            var linkedSummary = links.Count == 0
                ? "None"
                : string.Join("\n", links
                    .OrderByDescending(x => x.IsDefault)
                    .Select(x =>
                        $"{(x.IsDefault ? "⭐" : "•")} {(AiService.AiProvider)x.Provider}: `{x.DefaultModel ?? "No model"}` ({(x.IsEnabled ? "enabled" : "disabled")})"));

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithTitle(Strings.AiConfigTitle(ctx.Guild.Id))
                .WithDescription(Strings.AiConfigDescription(
                    ctx.Guild.Id,
                    config.Enabled,
                    config.ChannelId,
                    providerLabel,
                    config.Model ?? "Not Set",
                    config.TokensUsed))
                .AddField("Linked providers", linkedSummary)
                .WithOkColor()
                .Build());
        }
    }
}