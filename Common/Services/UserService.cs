using System.Security.Claims;
using System.Text;
using Common.Context;
using Common.Context.Entities.Router;
using Common.Exceptions;
using Common.Extensions;
using Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services;

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
        ModelUtils.ValidateModel(createDto);
        
        Logger.LogInformation("Create new user {mail}", createDto.Email);

        User user = new()
        {
            UserName = createDto.Username,
            Email = createDto.Email,
            ExternalId = createDto.ExternalId,
            TempBP = Convert.ToBase64String(Encoding.UTF8.GetBytes(createDto.Password))
        };

        var userResult = await userManager.CreateAsync(user, createDto.Password);
        userResult.IsSucceedOrThrow();
        
        Logger.LogInformation("Create new user#{id} {mail} OK", user.Id, createDto.Email);

        if (createDto.CanReceiveEverything)
        {
            userResult = await userManager.AddClaimAsync(user, new Claim(SecurityConstants.Claim.ReceiveEveryPackets.ToString(), "1"));
            userResult.IsSucceedOrThrow();
        }

        return user;
    }

    public async Task UpdateUser(long userId, UserUpdateDto updateDto)
    {
        ModelUtils.ValidateModel(updateDto);
        
        Logger.LogInformation("Update user#{id}", userId);

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            throw new NotFoundException<User>(userId);
        }
        
        user.UserName = updateDto.Username;
        user.Email = updateDto.Email;

        var identityResult = await userManager.UpdateAsync(user);
        identityResult.IsSucceedOrThrow();

        Logger.LogInformation("Update user#{id} {mail} OK", user.Id, updateDto.Email);
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
        
        if (!await IsAuthorized(user))
        {
            Logger.LogWarning("Login of {username}#{userId} KO. Locked out", username, user.Id);
            return null;
        }
        
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, false);

        if (!signInResult.Succeeded)
        {
            Logger.LogWarning("Login of {username}#{userId} KO. Wrong password : {password}", username, user.Id, password);
            // return null;
        }
        
        Logger.LogInformation("Login of {username}#{userId} OK with IP {ip}", username, user.Id, ip);
        
        user.Ip = ip;
        user.ConnectedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return user;
    }

    public async Task<bool> IsAuthorized(User user)
    {
        return !await userManager.IsLockedOutAsync(user);
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

    public async Task<bool> HasClaim(User user, SecurityConstants.Claim claimType)
    {
        return (await userManager.GetClaimsAsync(user)).Any(c => c.Type == claimType.ToString());
    }
}