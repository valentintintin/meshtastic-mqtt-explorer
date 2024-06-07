﻿// <auto-generated />
using System;
using MeshtasticMqttExplorer.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20240607163307_MqttGateway")]
    partial class MqttGateway
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Channel", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("Name");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.NeighborInfo", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("NeighborId")
                        .HasColumnType("bigint");

                    b.Property<long>("NodeId")
                        .HasColumnType("bigint");

                    b.Property<long>("PacketId")
                        .HasColumnType("bigint");

                    b.Property<double>("Snr")
                        .HasColumnType("double precision");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("NeighborId");

                    b.HasIndex("NodeId");

                    b.HasIndex("PacketId");

                    b.HasIndex("UpdatedAt");

                    b.ToTable("NeighborInfos");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Node", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<string>("AllNames")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<int?>("Altitude")
                        .HasColumnType("integer");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("FirmwareVersion")
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<string>("HardwareModel")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<bool?>("HasDefaultChannel")
                        .HasColumnType("boolean");

                    b.Property<bool?>("IsMqttGateway")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastSeen")
                        .HasColumnType("timestamp with time zone");

                    b.Property<double?>("Latitude")
                        .HasColumnType("double precision");

                    b.Property<string>("LongName")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<double?>("Longitude")
                        .HasColumnType("double precision");

                    b.Property<string>("ModemPreset")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<long>("NodeId")
                        .HasColumnType("bigint");

                    b.Property<string>("NodeIdString")
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<int?>("NumOnlineLocalNodes")
                        .HasColumnType("integer");

                    b.Property<string>("RegionCode")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Role")
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<string>("ShortName")
                        .HasMaxLength(4)
                        .HasColumnType("character varying(4)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AllNames");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("HardwareModel");

                    b.HasIndex("LastSeen")
                        .IsDescending();

                    b.HasIndex("LongName");

                    b.HasIndex("ModemPreset");

                    b.HasIndex("NodeId");

                    b.HasIndex("NodeIdString");

                    b.HasIndex("RegionCode");

                    b.HasIndex("Role");

                    b.HasIndex("ShortName");

                    b.ToTable("Nodes");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Packet", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<long>("ChannelId")
                        .HasColumnType("bigint");

                    b.Property<long?>("ChannelIndex")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("Encrypted")
                        .HasColumnType("boolean");

                    b.Property<long>("FromId")
                        .HasColumnType("bigint");

                    b.Property<long>("GatewayId")
                        .HasColumnType("bigint");

                    b.Property<long?>("HopLimit")
                        .HasColumnType("bigint");

                    b.Property<string>("MqttServer")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<long>("PacketId")
                        .HasColumnType("bigint");

                    b.Property<byte[]>("Payload")
                        .HasColumnType("bytea");

                    b.Property<string>("PayloadJson")
                        .HasColumnType("TEXT");

                    b.Property<string>("PortNum")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Priority")
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<long?>("ReplyId")
                        .HasColumnType("bigint");

                    b.Property<float?>("RxRssi")
                        .HasColumnType("real");

                    b.Property<float?>("RxSnr")
                        .HasColumnType("real");

                    b.Property<DateTimeOffset?>("RxTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("ToId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool?>("WantAck")
                        .HasColumnType("boolean");

                    b.Property<bool?>("WantResponse")
                        .HasColumnType("boolean");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("FromId");

                    b.HasIndex("GatewayId");

                    b.HasIndex("MqttServer");

                    b.HasIndex("PacketId");

                    b.HasIndex("PortNum");

                    b.HasIndex("ToId");

                    b.ToTable("Packets");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Position", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<int?>("Altitude")
                        .HasColumnType("integer");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<double>("Latitude")
                        .HasColumnType("double precision");

                    b.Property<double>("Longitude")
                        .HasColumnType("double precision");

                    b.Property<long>("NodeId")
                        .HasColumnType("bigint");

                    b.Property<long>("PacketId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("NodeId");

                    b.HasIndex("PacketId");

                    b.ToTable("Positions");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Telemetry", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<float?>("AirUtilTx")
                        .HasColumnType("real");

                    b.Property<float?>("BarometricPressure")
                        .HasColumnType("real");

                    b.Property<long?>("BatteryLevel")
                        .HasColumnType("bigint");

                    b.Property<float?>("ChannelUtilization")
                        .HasColumnType("real");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("NodeId")
                        .HasColumnType("bigint");

                    b.Property<long>("PacketId")
                        .HasColumnType("bigint");

                    b.Property<float?>("RelativeHumidity")
                        .HasColumnType("real");

                    b.Property<float?>("Temperature")
                        .HasColumnType("real");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<TimeSpan?>("Uptime")
                        .HasColumnType("interval");

                    b.Property<float?>("Voltage")
                        .HasColumnType("real");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("NodeId");

                    b.HasIndex("PacketId");

                    b.HasIndex("Type");

                    b.ToTable("Telemetries");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.TextMessage", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<long>("ChannelId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("FromId")
                        .HasColumnType("bigint");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<long>("PacketId")
                        .HasColumnType("bigint");

                    b.Property<long?>("ToId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("FromId");

                    b.HasIndex("PacketId");

                    b.HasIndex("ToId");

                    b.ToTable("TextMessages");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.NeighborInfo", b =>
                {
                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "Neighbor")
                        .WithMany("NeighborsFor")
                        .HasForeignKey("NeighborId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "Node")
                        .WithMany("MyNeighbors")
                        .HasForeignKey("NodeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Packet", "Packet")
                        .WithMany()
                        .HasForeignKey("PacketId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Neighbor");

                    b.Navigation("Node");

                    b.Navigation("Packet");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Packet", b =>
                {
                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Channel", "Channel")
                        .WithMany()
                        .HasForeignKey("ChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "From")
                        .WithMany("PacketsFrom")
                        .HasForeignKey("FromId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "Gateway")
                        .WithMany()
                        .HasForeignKey("GatewayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "To")
                        .WithMany("PacketsTo")
                        .HasForeignKey("ToId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Channel");

                    b.Navigation("From");

                    b.Navigation("Gateway");

                    b.Navigation("To");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Position", b =>
                {
                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "Node")
                        .WithMany("Positions")
                        .HasForeignKey("NodeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Packet", "Packet")
                        .WithMany()
                        .HasForeignKey("PacketId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Node");

                    b.Navigation("Packet");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Telemetry", b =>
                {
                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "Node")
                        .WithMany("Telemetries")
                        .HasForeignKey("NodeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Packet", "Packet")
                        .WithMany()
                        .HasForeignKey("PacketId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Node");

                    b.Navigation("Packet");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.TextMessage", b =>
                {
                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Channel", "Channel")
                        .WithMany("TextMessages")
                        .HasForeignKey("ChannelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "From")
                        .WithMany("TextMessagesFrom")
                        .HasForeignKey("FromId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Packet", "Packet")
                        .WithMany()
                        .HasForeignKey("PacketId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MeshtasticMqttExplorer.Context.Entities.Node", "To")
                        .WithMany("TextMessagesTo")
                        .HasForeignKey("ToId");

                    b.Navigation("Channel");

                    b.Navigation("From");

                    b.Navigation("Packet");

                    b.Navigation("To");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Channel", b =>
                {
                    b.Navigation("TextMessages");
                });

            modelBuilder.Entity("MeshtasticMqttExplorer.Context.Entities.Node", b =>
                {
                    b.Navigation("MyNeighbors");

                    b.Navigation("NeighborsFor");

                    b.Navigation("PacketsFrom");

                    b.Navigation("PacketsTo");

                    b.Navigation("Positions");

                    b.Navigation("Telemetries");

                    b.Navigation("TextMessagesFrom");

                    b.Navigation("TextMessagesTo");
                });
#pragma warning restore 612, 618
        }
    }
}
