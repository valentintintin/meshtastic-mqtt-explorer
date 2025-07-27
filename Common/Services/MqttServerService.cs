using System.Security.Claims;
using System.Text;
using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Exceptions;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class MqttServerService (
    ILogger<MqttServerService> logger,
    IDbContextFactory<DataContext> contextFactory)
    : AService(logger, contextFactory)
{
    public async Task<MqttServer> CreateMqttServer(ServerCreateUpdateDto createDto)
    {
        ModelUtils.ValidateModel(createDto);
        
        Logger.LogInformation("Create new server {name}", createDto.Name);

        MqttServer server = new()
        {
            Name = createDto.Name,
            Type = createDto.Type,
            Host = createDto.Host,
            Port = createDto.Port,
            Username = createDto.Username,
            Password = createDto.Password,
            Enabled = createDto.Enabled,
            Topics = createDto.Topics?.Split(", ").ToList() ?? [],
            IsARelayType = createDto.IsARelayType,
            IsHighLoad = createDto.IsHighLoad,
            MqttPostJson = createDto.MqttPostJson,
            RelayPositionPrecision = createDto.RelayPositionPrecision,
            ShouldBeRelayed = createDto.ShouldBeRelayed,
            UseWorker = createDto.UseWorker
        };

        Context.Add(server);
        await Context.SaveChangesAsync();
        
        Logger.LogInformation("Create new server#{id} {name} OK", server.Id, createDto.Name);

        return server;
    }

    public async Task UpdateMqttServer(long serverId, ServerCreateUpdateDto updateDto)
    {
        ModelUtils.ValidateModel(updateDto);
        
        Logger.LogInformation("Update server#{id}", serverId);

        var server = await Context.MqttServers.FindByIdAsync(serverId);

        if (server == null)
        {
            throw new NotFoundException<MqttServer>(serverId);
        }

        server.Name = updateDto.Name;
        server.Type = updateDto.Type;
        server.Host = updateDto.Host;
        server.Port = updateDto.Port;
        server.Username = updateDto.Username;
        server.Password = updateDto.Password;
        server.Enabled = updateDto.Enabled;
        server.Topics = updateDto.Topics?.Split(", ").ToList() ?? [];
        server.IsARelayType = updateDto.IsARelayType;
        server.IsHighLoad = updateDto.IsHighLoad;
        server.MqttPostJson = updateDto.MqttPostJson;
        server.RelayPositionPrecision = updateDto.RelayPositionPrecision;
        server.ShouldBeRelayed = updateDto.ShouldBeRelayed;
        server.UseWorker = updateDto.UseWorker;

        Context.Update(server);
        await Context.SaveChangesAsync();

        Logger.LogInformation("Update server#{id} {name} OK", server.Id, updateDto.Name);
    }
}