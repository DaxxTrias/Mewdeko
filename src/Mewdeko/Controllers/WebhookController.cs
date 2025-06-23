using Mewdeko.Modules.Votes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <inheritdoc />
[ApiController]
[Route("votes/")]
[Authorize("TopggPolicy")]
public class WebhookController(VoteService service, ILogger<WebhookController> logger) : Controller
{
    /// <summary>
    ///     The vote webhook handler
    /// </summary>
    /// <param name="data">The data for the webhook</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> TopggWebhook([FromBody] VoteModel data)
    {
        logger.LogInformation("User {UserId} has voted for Bot {BotId} on {Platform}",
            data.User,
            data.Bot,
            "top.gg");

        await service.RunVoteStuff(new CompoundVoteModal
        {
            Password = Request.Headers.Authorization, VoteModel = data
        });

        return Ok();
    }
}