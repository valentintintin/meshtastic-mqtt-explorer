using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Common.Context.Entities.Router;
using Common.Exceptions;
using Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MqttRouter.Models;
using MqttRouter.Services;

namespace MqttRouter.Controllers;

[ApiController]
[Route("user")]
public class UserController(ILogger<AController> logger, UserManager<User> userManager, UserService userService) : AController(logger)
{
    [Authorize]
    [HttpGet("create-mqtt-password")]
    public async Task<string> CreateMqttPassword()
    {
        var userExternalId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        Logger.LogInformation("Generate MQTT password for user external ID {userExternalId}", userExternalId);
        
        if (string.IsNullOrWhiteSpace(userExternalId))
        {
            throw new NotFoundException("UserViaExternalId", userExternalId);
        }
        
        var user = await userManager.Users.SingleOrDefaultAsync(a => a.ExternalId == userExternalId);

        var password = userService.GeneratePassword(10);
            
        if (user == null)
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var username = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;
         
            Logger.LogInformation("Create new user for external ID {userExternalId} ", userExternalId);

            await userService.CreateUser(new UserCreateDto
            {
                Email = email,
                Username = username,
                Password = password,
                ExternalId = userExternalId
            });
        }
        else
        {
            Logger.LogInformation("Generate MQTT password for user#{id} external ID {userExternalId}", user.Id, userExternalId);
            
            await userService.ChangePassword(user.Id, password);
        }

        return password;
    }
}