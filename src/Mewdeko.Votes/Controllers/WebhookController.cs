﻿using Mewdeko.Votes.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Mewdeko.Votes.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    public readonly WebhookEvents Events;

    public WebhookController(ILogger<WebhookController> logger,
        WebhookEvents events)
    {
        _logger = logger;
        Events = events;
    }

    [HttpPost("/")]
    public Task<IActionResult> TopggWebhook([FromBody] VoteModel data)
    {
        _logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "top.gg");
        _ = Task.Run(async () =>
        {
            await Events.InvokeTopGg(data, Request.Headers.Authorization);
        });
        return Task.FromResult<IActionResult>(Ok());
    }
    
}