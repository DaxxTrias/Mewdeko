using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers
{
    /// <summary>
    /// Controller providing API endpoints for checking bot ownership.
    /// </summary>
    [ApiController]
    [Route("botapi/[controller]")]
    [Authorize("ApiKeyPolicy")]
    public class OwnershipController : ControllerBase
    {
        private readonly BotCredentials credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwnershipController"/> class.
        /// </summary>
        /// <param name="credentials">The bot credentials containing owner information.</param>
        public OwnershipController(BotCredentials credentials)
        {
            this.credentials = credentials;
        }

        /// <summary>
        /// Checks if a user is a bot owner.
        /// </summary>
        /// <param name="userId">The Discord user ID to check.</param>
        /// <returns>A boolean indicating whether the user is a bot owner.</returns>
        [HttpGet("{userId}")]
        public IActionResult CheckOwnership(ulong userId)
        {
            return Ok(credentials.IsOwner(userId));
        }
    }
}