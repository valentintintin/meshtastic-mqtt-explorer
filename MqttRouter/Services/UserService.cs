using System.Text;
using Common.Context;
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
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+";
    private static readonly Random Random = new();
    
    public async Task<User> CreateUser(UserCreateDto createDto)
    {
        Utils.ValidateModel(createDto);
        
        Logger.LogInformation("Create new user {mail}", createDto.Email);

        User user = new()
        {
            UserName = createDto.Username,
            Email = createDto.Email,
            ExternalId = createDto.ExternalId,
            TempBP = Convert.ToBase64String(Encoding.UTF8.GetBytes(createDto.Password))
        };

        var userResult = await userManager.CreateAsync(user, createDto.Password);

        try
        {
            userResult.IsSucceedOrThrow();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Create new user {mail} KO", createDto.Email);
            throw;
        }            
        
        Logger.LogInformation("Create new user#{id} {mail} OK", user.Id, createDto.Email);

        return user;
    }

    public async Task<User?> Login(string username, string password, string ip)
    {
        Logger.LogInformation("Login of {username}", username);
        
        var user = await userManager.FindByNameAsync(username.Trim());

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

    public async Task ChangePassword(long userId, string password)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new NotFoundException<User>(userId);
        }

        await userManager.RemovePasswordAsync(user);
        await userManager.AddPasswordAsync(user, password);
    }

    public string GeneratePassword(int length)
    {
        return new string(Enumerable.Repeat(Chars, length).Select(s => s[Random.Next(s.Length)]).ToArray());
    }
}