using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "router");

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Index = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MqttServers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Host = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Password = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Topics = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsARelayType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RelayPositionPrecision = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UseWorker = table.Column<bool>(type: "boolean", nullable: false),
                    IsHighLoad = table.Column<bool>(type: "boolean", nullable: false),
                    MqttPostJson = table.Column<bool>(type: "boolean", nullable: false),
                    ShouldBeRelayed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "router",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "router",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TempBP = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    NodeIdString = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LongName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ShortName = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    AllNames = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OldAllNames = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    HardwareModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RegionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModemPreset = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Altitude = table.Column<int>(type: "integer", nullable: true),
                    NumOnlineLocalNodes = table.Column<int>(type: "integer", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HasDefaultChannel = table.Column<bool>(type: "boolean", nullable: true),
                    IsMqttGateway = table.Column<bool>(type: "boolean", nullable: true),
                    MqttServerId = table.Column<long>(type: "bigint", nullable: true),
                    PrimaryChannel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Ignored = table.Column<bool>(type: "boolean", nullable: false),
                    HopStart = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nodes_MqttServers_MqttServerId",
                        column: x => x.MqttServerId,
                        principalTable: "MqttServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    UrlToEditMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeHopsDetails = table.Column<bool>(type: "boolean", nullable: false),
                    IncludePayloadDetails = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeStats = table.Column<bool>(type: "boolean", nullable: false),
                    AllowDuplication = table.Column<bool>(type: "boolean", nullable: false),
                    AllowByHimSelf = table.Column<bool>(type: "boolean", nullable: false),
                    OnlyWhenDifferentMqttServer = table.Column<bool>(type: "boolean", nullable: false),
                    PortNum = table.Column<int>(type: "integer", nullable: true),
                    From = table.Column<long>(type: "bigint", nullable: true),
                    To = table.Column<long>(type: "bigint", nullable: true),
                    Gateway = table.Column<long>(type: "bigint", nullable: true),
                    FromOrTo = table.Column<long>(type: "bigint", nullable: true),
                    MqttServerId = table.Column<long>(type: "bigint", nullable: true),
                    Channel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    DistanceAroundPositionKm = table.Column<int>(type: "integer", nullable: true),
                    MinimumMinutesBetweenPacketsWhenIncludeStats = table.Column<int>(type: "integer", nullable: true),
                    MinimumNumberOfPacketsWhenIncludeStats = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Webhooks_MqttServers_MqttServerId",
                        column: x => x.MqttServerId,
                        principalTable: "MqttServers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "router",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "router",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "router",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "router",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "router",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "router",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "router",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "router",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "router",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "router",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "router",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NodeConfigurations",
                schema: "router",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MqttId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenOnMqtt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Forbidden = table.Column<bool>(type: "boolean", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Department = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NodeConfigurations_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NodeConfigurations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "router",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NeighborInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeReceiverId = table.Column<long>(type: "bigint", nullable: false),
                    NodeReceiverPositionId = table.Column<long>(type: "bigint", nullable: true),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    NodeHeardId = table.Column<long>(type: "bigint", nullable: false),
                    NodeHeardPositionId = table.Column<long>(type: "bigint", nullable: true),
                    Snr = table.Column<float>(type: "real", nullable: false),
                    Rssi = table.Column<float>(type: "real", nullable: true),
                    Distance = table.Column<double>(type: "double precision", nullable: true),
                    DataSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeighborInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeighborInfos_Nodes_NodeHeardId",
                        column: x => x.NodeHeardId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NeighborInfos_Nodes_NodeReceiverId",
                        column: x => x.NodeReceiverId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PacketActivities",
                schema: "router",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiverIds = table.Column<string>(type: "text", nullable: false),
                    IsBroadcast = table.Column<bool>(type: "boolean", nullable: false),
                    HopLimit = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacketActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: false),
                    GatewayId = table.Column<long>(type: "bigint", nullable: false),
                    GatewayPositionId = table.Column<long>(type: "bigint", nullable: true),
                    PositionId = table.Column<long>(type: "bigint", nullable: true),
                    FromId = table.Column<long>(type: "bigint", nullable: false),
                    ToId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelIndex = table.Column<long>(type: "bigint", nullable: true),
                    Encrypted = table.Column<bool>(type: "boolean", nullable: false),
                    RxSnr = table.Column<float>(type: "real", nullable: true),
                    RxRssi = table.Column<float>(type: "real", nullable: true),
                    RxTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HopStart = table.Column<int>(type: "integer", nullable: true),
                    HopLimit = table.Column<int>(type: "integer", nullable: true),
                    WantAck = table.Column<bool>(type: "boolean", nullable: true),
                    ViaMqtt = table.Column<bool>(type: "boolean", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PortNum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PortNumVariant = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    WantResponse = table.Column<bool>(type: "boolean", nullable: true),
                    RequestId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyId = table.Column<long>(type: "bigint", nullable: true),
                    MqttServerId = table.Column<long>(type: "bigint", nullable: true),
                    MqttTopic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GatewayDistanceKm = table.Column<double>(type: "double precision", nullable: true),
                    RelayNode = table.Column<long>(type: "bigint", nullable: true),
                    RelayNodeId = table.Column<long>(type: "bigint", nullable: true),
                    NextHop = table.Column<long>(type: "bigint", nullable: true),
                    NextHopId = table.Column<long>(type: "bigint", nullable: true),
                    PacketDuplicatedId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Packets_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Packets_MqttServers_MqttServerId",
                        column: x => x.MqttServerId,
                        principalTable: "MqttServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Packets_Nodes_FromId",
                        column: x => x.FromId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Packets_Nodes_GatewayId",
                        column: x => x.GatewayId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Packets_Nodes_NextHopId",
                        column: x => x.NextHopId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Packets_Nodes_RelayNodeId",
                        column: x => x.RelayNodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Packets_Nodes_ToId",
                        column: x => x.ToId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Packets_Packets_PacketDuplicatedId",
                        column: x => x.PacketDuplicatedId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PaxCounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    Wifi = table.Column<long>(type: "bigint", nullable: false),
                    Ble = table.Column<long>(type: "bigint", nullable: false),
                    Uptime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaxCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaxCounters_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaxCounters_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Altitude = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Positions_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SignalHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeReceiverId = table.Column<long>(type: "bigint", nullable: false),
                    NodeHeardId = table.Column<long>(type: "bigint", nullable: false),
                    Snr = table.Column<float>(type: "real", nullable: false),
                    Rssi = table.Column<float>(type: "real", nullable: true),
                    PacketId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalHistories_Nodes_NodeHeardId",
                        column: x => x.NodeHeardId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalHistories_Nodes_NodeReceiverId",
                        column: x => x.NodeReceiverId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalHistories_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Telemetries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    BatteryLevel = table.Column<long>(type: "bigint", nullable: true),
                    Voltage = table.Column<float>(type: "real", nullable: true),
                    ChannelUtilization = table.Column<float>(type: "real", nullable: true),
                    AirUtilTx = table.Column<float>(type: "real", nullable: true),
                    Uptime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Temperature = table.Column<float>(type: "real", nullable: true),
                    RelativeHumidity = table.Column<float>(type: "real", nullable: true),
                    BarometricPressure = table.Column<float>(type: "real", nullable: true),
                    GasResistance = table.Column<float>(type: "real", nullable: true),
                    Iaq = table.Column<long>(type: "bigint", nullable: true),
                    Channel1Voltage = table.Column<float>(type: "real", nullable: true),
                    Channel1Current = table.Column<float>(type: "real", nullable: true),
                    Channel2Voltage = table.Column<float>(type: "real", nullable: true),
                    Channel2Current = table.Column<float>(type: "real", nullable: true),
                    Channel3Voltage = table.Column<float>(type: "real", nullable: true),
                    Channel3Current = table.Column<float>(type: "real", nullable: true),
                    NumPacketsRx = table.Column<long>(type: "bigint", nullable: true),
                    NumPacketsRxBad = table.Column<long>(type: "bigint", nullable: true),
                    NumTxRelayCanceled = table.Column<long>(type: "bigint", nullable: true),
                    NumPacketsTx = table.Column<long>(type: "bigint", nullable: true),
                    NumRxDupe = table.Column<long>(type: "bigint", nullable: true),
                    NumTxRelay = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Telemetries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Telemetries_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Telemetries_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TextMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromId = table.Column<long>(type: "bigint", nullable: false),
                    ToId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextMessages_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TextMessages_Nodes_FromId",
                        column: x => x.FromId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TextMessages_Nodes_ToId",
                        column: x => x.ToId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TextMessages_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Waypoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    WaypointId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Icon = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waypoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Waypoints_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Waypoints_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WebhooksHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: false),
                    WebhookId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhooksHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhooksHistories_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebhooksHistories_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_Name",
                table: "Channels",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_UpdatedAt",
                table: "Channels",
                column: "UpdatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MqttServers_Name",
                table: "MqttServers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_DataSource",
                table: "NeighborInfos",
                column: "DataSource");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodeHeardId",
                table: "NeighborInfos",
                column: "NodeHeardId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodeHeardPositionId",
                table: "NeighborInfos",
                column: "NodeHeardPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodeReceiverId",
                table: "NeighborInfos",
                column: "NodeReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodeReceiverPositionId",
                table: "NeighborInfos",
                column: "NodeReceiverPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_PacketId",
                table: "NeighborInfos",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_UpdatedAt",
                table: "NeighborInfos",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_Department",
                schema: "router",
                table: "NodeConfigurations",
                column: "Department");

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_MqttId",
                schema: "router",
                table: "NodeConfigurations",
                column: "MqttId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_NodeId",
                schema: "router",
                table: "NodeConfigurations",
                column: "NodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_UserId",
                schema: "router",
                table: "NodeConfigurations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_AllNames",
                table: "Nodes",
                column: "AllNames");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_CreatedAt",
                table: "Nodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_LastSeen",
                table: "Nodes",
                column: "LastSeen",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_LongName",
                table: "Nodes",
                column: "LongName");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ModemPreset",
                table: "Nodes",
                column: "ModemPreset");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_MqttServerId",
                table: "Nodes",
                column: "MqttServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_NodeId",
                table: "Nodes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_NodeIdString",
                table: "Nodes",
                column: "NodeIdString");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_RegionCode",
                table: "Nodes",
                column: "RegionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Role",
                table: "Nodes",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ShortName",
                table: "Nodes",
                column: "ShortName");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_UpdatedAt",
                table: "Nodes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_PacketId",
                schema: "router",
                table: "PacketActivities",
                column: "PacketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packets_ChannelId",
                table: "Packets",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_CreatedAt",
                table: "Packets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_FromId",
                table: "Packets",
                column: "FromId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_GatewayId",
                table: "Packets",
                column: "GatewayId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_GatewayPositionId",
                table: "Packets",
                column: "GatewayPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_MqttServerId",
                table: "Packets",
                column: "MqttServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_NextHopId",
                table: "Packets",
                column: "NextHopId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PacketDuplicatedId",
                table: "Packets",
                column: "PacketDuplicatedId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PacketId",
                table: "Packets",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PortNum",
                table: "Packets",
                column: "PortNum");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PortNumVariant",
                table: "Packets",
                column: "PortNumVariant");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PositionId",
                table: "Packets",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_RelayNodeId",
                table: "Packets",
                column: "RelayNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_ToId",
                table: "Packets",
                column: "ToId");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_CreatedAt",
                table: "PaxCounters",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_NodeId",
                table: "PaxCounters",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_PacketId",
                table: "PaxCounters",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CreatedAt",
                table: "Positions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_NodeId",
                table: "Positions",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_PacketId",
                table: "Positions",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions",
                column: "UpdatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "router",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "router",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_CreatedAt",
                table: "SignalHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_NodeHeardId",
                table: "SignalHistories",
                column: "NodeHeardId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_NodeReceiverId",
                table: "SignalHistories",
                column: "NodeReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_PacketId",
                table: "SignalHistories",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_CreatedAt",
                table: "Telemetries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_NodeId",
                table: "Telemetries",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_PacketId",
                table: "Telemetries",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_Type",
                table: "Telemetries",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_ChannelId",
                table: "TextMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_CreatedAt",
                table: "TextMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_FromId",
                table: "TextMessages",
                column: "FromId");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_PacketId",
                table: "TextMessages",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_ToId",
                table: "TextMessages",
                column: "ToId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "router",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "router",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "router",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "router",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                schema: "router",
                table: "Users",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "router",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_ExpiresAt",
                table: "Waypoints",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_NodeId",
                table: "Waypoints",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_PacketId",
                table: "Waypoints",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_UpdatedAt",
                table: "Waypoints",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_WaypointId",
                table: "Waypoints",
                column: "WaypointId");

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_MqttServerId",
                table: "Webhooks",
                column: "MqttServerId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhooksHistories_PacketId",
                table: "WebhooksHistories",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhooksHistories_WebhookId",
                table: "WebhooksHistories",
                column: "WebhookId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodeHeardPositionId",
                table: "NeighborInfos",
                column: "NodeHeardPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodeReceiverPositionId",
                table: "NeighborInfos",
                column: "NodeReceiverPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PacketActivities_Packets_PacketId",
                schema: "router",
                table: "PacketActivities",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets",
                column: "GatewayPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Positions_PositionId",
                table: "Packets",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_FromId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_GatewayId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_NextHopId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_RelayNodeId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_ToId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Nodes_NodeId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions");

            migrationBuilder.DropTable(
                name: "NeighborInfos");

            migrationBuilder.DropTable(
                name: "NodeConfigurations",
                schema: "router");

            migrationBuilder.DropTable(
                name: "PacketActivities",
                schema: "router");

            migrationBuilder.DropTable(
                name: "PaxCounters");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "router");

            migrationBuilder.DropTable(
                name: "SignalHistories");

            migrationBuilder.DropTable(
                name: "Telemetries");

            migrationBuilder.DropTable(
                name: "TextMessages");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "router");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "router");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "router");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "router");

            migrationBuilder.DropTable(
                name: "Waypoints");

            migrationBuilder.DropTable(
                name: "WebhooksHistories");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "router");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "router");

            migrationBuilder.DropTable(
                name: "Webhooks");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropTable(
                name: "Packets");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "MqttServers");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
