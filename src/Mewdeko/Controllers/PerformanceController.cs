using System;
using System.Linq;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers
{
    /// <summary>
    /// Controller providing API endpoints for accessing performance monitoring data.
    /// Only accessible to bot owners.
    /// </summary>
    [ApiController]
    [Route("botapi/[controller]")]
    [Authorize("ApiKeyPolicy")]
    public class PerformanceController : ControllerBase
    {
        private readonly PerformanceMonitorService performanceService;
        private readonly BotCredentials credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceController"/> class.
        /// </summary>
        /// <param name="performanceService">The performance monitoring service.</param>
        /// <param name="credentials">The bot credentials for owner verification.</param>
        public PerformanceController(
            PerformanceMonitorService performanceService,
            BotCredentials credentials)
        {
            this.performanceService = performanceService;
            this.credentials = credentials;
        }

        /// <summary>
        /// Gets the performance data for methods tracked by the performance monitoring service.
        /// </summary>
        /// <param name="userId">The Discord user ID to verify for owner permissions.</param>
        /// <returns>An array of performance data for the top CPU-intensive methods.</returns>
        [HttpGet]
        public IActionResult GetPerformanceData([FromQuery] ulong userId)
        {
            // Check if the provided user ID is a bot owner
            if (!credentials.IsOwner(userId))
            {
                return Forbid();
            }

            var topMethods = performanceService.GetTopCpuMethods(20);

            var result = topMethods.Select(m => new
            {
                methodName = m.MethodName,
                callCount = m.CallCount,
                totalTime = m.TotalExecutionTime.TotalMilliseconds,
                avgExecutionTime = m.AvgExecutionTime,
                lastExecuted = m.LastExecuted
            }).ToArray();

            return Ok(result);
        }

        /// <summary>
        /// Clears all performance monitoring data.
        /// </summary>
        /// <param name="userId">The Discord user ID to verify for owner permissions.</param>
        /// <returns>An action result indicating success or failure.</returns>
        [HttpPost("clear")]
        public IActionResult ClearPerformanceData([FromQuery] ulong userId)
        {
            // Check if the provided user ID is a bot owner
            if (!credentials.IsOwner(userId))
            {
                return Forbid();
            }

            performanceService.ClearPerformanceData();
            return Ok();
        }
    }
}