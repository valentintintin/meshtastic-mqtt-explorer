using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class Missing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
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
                    MqttServer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PrimaryChannel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Ignored = table.Column<bool>(type: "boolean", nullable: false),
                    HopStart = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NeighborInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    NodePositionId = table.Column<long>(type: "bigint", nullable: true),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    NeighborId = table.Column<long>(type: "bigint", nullable: false),
                    NeighborPositionId = table.Column<long>(type: "bigint", nullable: true),
                    Snr = table.Column<double>(type: "double precision", nullable: false),
                    Distance = table.Column<double>(type: "double precision", nullable: true),
                    DataSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeighborInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeighborInfos_Nodes_NeighborId",
                        column: x => x.NeighborId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NeighborInfos_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    Payload = table.Column<byte[]>(type: "bytea", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    WantResponse = table.Column<bool>(type: "boolean", nullable: true),
                    RequestId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyId = table.Column<long>(type: "bigint", nullable: true),
                    MqttServer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MqttTopic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GatewayDistanceKm = table.Column<double>(type: "double precision", nullable: true),
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
                    Channel1Voltage = table.Column<float>(type: "real", nullable: true),
                    Channel1Current = table.Column<float>(type: "real", nullable: true),
                    Channel2Voltage = table.Column<float>(type: "real", nullable: true),
                    Channel2Current = table.Column<float>(type: "real", nullable: true),
                    Channel3Voltage = table.Column<float>(type: "real", nullable: true),
                    Channel3Current = table.Column<float>(type: "real", nullable: true),
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
                    ToId = table.Column<long>(type: "bigint", nullable: true),
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
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TextMessages_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Traceroutes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeFromId = table.Column<long>(type: "bigint", nullable: false),
                    NodeToId = table.Column<long>(type: "bigint", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    Hop = table.Column<int>(type: "integer", nullable: false),
                    Snr = table.Column<double>(type: "double precision", nullable: true),
                    PacketId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traceroutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeFromId",
                        column: x => x.NodeFromId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeToId",
                        column: x => x.NodeToId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Packets_PacketId",
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

            migrationBuilder.CreateIndex(
                name: "IX_Channels_Name",
                table: "Channels",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_CreatedAt",
                table: "NeighborInfos",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NeighborId",
                table: "NeighborInfos",
                column: "NeighborId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NeighborPositionId",
                table: "NeighborInfos",
                column: "NeighborPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodeId",
                table: "NeighborInfos",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodePositionId",
                table: "NeighborInfos",
                column: "NodePositionId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_PacketId",
                table: "NeighborInfos",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_UpdatedAt",
                table: "NeighborInfos",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_AllNames",
                table: "Nodes",
                column: "AllNames");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_CreatedAt",
                table: "Nodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_HardwareModel",
                table: "Nodes",
                column: "HardwareModel");

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
                name: "IX_Packets_MqttServer",
                table: "Packets",
                column: "MqttServer");

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
                name: "IX_Packets_PositionId",
                table: "Packets",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_ToId",
                table: "Packets",
                column: "ToId");

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
                column: "UpdatedAt");

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
                name: "IX_Telemetries_UpdatedAt",
                table: "Telemetries",
                column: "UpdatedAt");

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
                name: "IX_Traceroutes_CreatedAt",
                table: "Traceroutes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeFromId",
                table: "Traceroutes",
                column: "NodeFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeId",
                table: "Traceroutes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeToId",
                table: "Traceroutes",
                column: "NodeToId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_PacketId",
                table: "Traceroutes",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_UpdatedAt",
                table: "Traceroutes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_CreatedAt",
                table: "Waypoints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_ExpiresAt",
                table: "Waypoints",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_Name",
                table: "Waypoints",
                column: "Name");

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

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NeighborPositionId",
                table: "NeighborInfos",
                column: "NeighborPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodePositionId",
                table: "NeighborInfos",
                column: "NodePositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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
                name: "Telemetries");

            migrationBuilder.DropTable(
                name: "TextMessages");

            migrationBuilder.DropTable(
                name: "Traceroutes");

            migrationBuilder.DropTable(
                name: "Waypoints");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropTable(
                name: "Packets");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
