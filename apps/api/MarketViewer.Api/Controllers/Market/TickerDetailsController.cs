using MarketViewer.Api.Authorization;
using MarketViewer.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Polygon.Client.Models;
using System.Net;

namespace MarketViewer.Api.Controllers.Market
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TickerDetailsController(IMemoryCache memoryCache, IHttpClientFactory httpClientFactory, ILogger<TickerDetailsController> logger) : ControllerBase
    {
        [HttpGet("{ticker}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [RequiredPermissions([UserRole.None, UserRole.Basic, UserRole.Advanced, UserRole.Premium, UserRole.Admin])]
        public async Task<IActionResult> HandleTickerDetailsRequest(string ticker)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                return BadRequest(new List<string> { "Invalid ticker." });
            }

            try
            {
                var tickerDetails = memoryCache.Get<TickerDetails>($"TickerDetails_{ticker}");

                if (tickerDetails is not null)
                {
                    return Ok(tickerDetails);
                }

                var client = httpClientFactory.CreateClient("marketdataprovider");

                var url = $"/api/tickerdetails/{ticker}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                return response.StatusCode switch
                {
                    HttpStatusCode.OK => Ok(json),
                    HttpStatusCode.BadRequest => BadRequest(json),
                    _ => StatusCode(StatusCodes.Status500InternalServerError, json)
                };
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new List<string> { "Internal server error." });
            }
        }
    }
}
