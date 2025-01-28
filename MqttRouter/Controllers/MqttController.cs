// using System.IdentityModel.Tokens.Jwt;
// using System.Security.Claims;
// using Common.Context.Entities.Router;
// using Common.Exceptions;
// using Common.Extensions;
// using Common.Models;
// using Common.Services;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using MqttRouter.Services;
//
// namespace MqttRouter.Controllers;
//
// [ApiController]
// [Route("mqtt")]
// public class MqttController(ILogger<AController> logger) : AController(logger)
// {
//     [Authorize]
//     [HttpGet("")]
//     public async Task Action()
//     {
//     }
// }