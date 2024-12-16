using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Exceptions;
using Common.Extensions;
using Common.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MqttRouter.Models;

namespace MqttRouter.Services;

public class UserService(
    ILogger<UserService> logger,
    IDbContextFactory<DataContext> contextFactory,
    UserManager<User> userManager, SignInManager<User> signInManager)
    : AService(logger, contextFactory)
{
    public async Task<User> CreateUser(UserDto dto)
    {
        Utils.ValidateModel(dto);
        
        var userSame = await userManager.FindByEmailAsync(dto.Email);

        if (userSame != null)
        {
            throw new DuplicateEmailException();
        }

        User user = new()
        {
            UserName = dto.Username,
            Email = dto.Email
        };

        var userResult = await userManager.CreateAsync(user, dto.Password);
        userResult.IsSucceedOrThrow();

        return user;
    }

    public async Task<User?> Login(string username, string password, string ip)
    {
        Logger.LogInformation("Login of {username}", username);
        
        var user = await userManager.FindByNameAsync(username);

        if (user == null)
        {
            Logger.LogWarning("Login of {username} KO. Not found", username);
            return null;
        }
        
        if (!await IsAuthorized(user.Id))
        {
            Logger.LogWarning("Login of {username}#{userId} KO. Locked out", username, user.Id);
            return null;
        }
        
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, false);

        if (!signInResult.Succeeded)
        {
            Logger.LogWarning("Login of {username}#{userId} KO. Wrong password : {password}", username, user.Id, password);
            return null;
        }
        
        Logger.LogInformation("Login of {username}#{userId} OK with IP {ip}", username, user.Id, ip);
        
        user.Ip = ip;
        user.ConnectedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return user;
    }

    public async Task<bool> IsAuthorized(long userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user != null && !await userManager.IsLockedOutAsync(user);
    }
}