﻿using Autobus;
using BeatTogether.DedicatedServer.Interface;
using BeatTogether.DedicatedServer.Interface.Events;
using BeatTogether.DedicatedServer.Interface.Requests;
using BeatTogether.DedicatedServer.Interface.Responses;
using BeatTogether.DedicatedServer.Kernel.Encryption;
using BeatTogether.DedicatedServer.Node.Abstractions;
using BeatTogether.DedicatedServer.Node.Configuration;
using BeatTogether.DedicatedServer.Interface.Models;
using BeatTogether.DedicatedServer.Interface.Enums;
using BeatTogether.DedicatedServer.Kernel.Abstractions;
using Serilog;
using System;
using System.Threading.Tasks;
using BeatTogether.DedicatedServer.Kernel.Managers.Abstractions;

namespace BeatTogether.DedicatedServer.Node
{
    public sealed class NodeService : IMatchmakingService
    {
        private readonly NodeConfiguration _configuration;
        private readonly IInstanceFactory _instanceFactory;
        private readonly IInstanceRegistry _instanceRegistry;
        private readonly PacketEncryptionLayer _packetEncryptionLayer;
        private readonly IAutobus _autobus;
        private readonly ILogger _logger = Log.ForContext<NodeService>();

        public NodeService(
            NodeConfiguration configuration,
            IInstanceFactory instanceFactory,
            IInstanceRegistry instanceRegistry,
            PacketEncryptionLayer packetEncryptionLayer,
            IAutobus autobus)
        {
            _configuration = configuration;
            _instanceFactory = instanceFactory;
            _instanceRegistry = instanceRegistry;
            _packetEncryptionLayer = packetEncryptionLayer;
            _autobus = autobus;
        }

        public async Task<CreateMatchmakingServerResponse> CreateMatchmakingServer(CreateMatchmakingServerRequest request)
        {
            _logger.Debug($"Received request to create matchmaking server. " +
                $"(Secret={request.Secret}, " +
                $"ManagerId={request.ManagerId}, " +
                $"MaxPlayerCount={request.Configuration.MaxPlayerCount}, " +
                $"DiscoveryPolicy={request.Configuration.DiscoveryPolicy}, " +
                $"InvitePolicy={request.Configuration.InvitePolicy}, " +
                $"GameplayServerMode={request.Configuration.GameplayServerMode}, " +
                $"SongSelectionMode={request.Configuration.SongSelectionMode}, " +
                $"GameplayServerControlSettings={request.Configuration.GameplayServerControlSettings})");

            var matchmakingServer = _instanceFactory.CreateInstance(
                request.Secret,
                request.ManagerId,
                request.Configuration,
                request.PermanentManager,
                request.Timeout,
                request.ServerName
            );
            if (matchmakingServer is null) // TODO: can also be no available slots
                return new CreateMatchmakingServerResponse(CreateMatchmakingServerError.InvalidSecret, string.Empty, Array.Empty<byte>(), Array.Empty<byte>());

            await matchmakingServer.Start();
            //_autobus.Publish(new MatchmakingServerStartedEvent(request.Secret, request.ManagerId, request.Configuration));//Tells the master server to add a server, NOT USED
            matchmakingServer.StopEvent += () => _autobus.Publish(new MatchmakingServerStoppedEvent(request.Secret));//Tells the master server when the newly added server has stopped

            return new CreateMatchmakingServerResponse(
                CreateMatchmakingServerError.None,
                $"{_configuration.HostName}:{matchmakingServer.Port}",
                _packetEncryptionLayer.Random,
                _packetEncryptionLayer.KeyPair.PublicKey
            );
        }

        public async Task<StopMatchmakingServerResponse> StopMatchmakingServer(StopMatchmakingServerRequest request)
        {
            if (_instanceRegistry.TryGetInstance(request.Secret, out var instance))
            {
                await instance.Stop();
                return new StopMatchmakingServerResponse(true);
            }
            return new StopMatchmakingServerResponse(false);
        }

        public Task<PublicMatchmakingServerListResponse> GetPublicMatchmakingServerList(GetPublicMatchmakingServerListRequest request)
        {
            return Task.FromResult(new PublicMatchmakingServerListResponse(_instanceRegistry.ListPublicInstanceSecrets()));
        }

        public Task<ServerCountResponse> GetServerCount(GetMatchmakingServerCountRequest request)
        {
            return Task.FromResult(new ServerCountResponse(_instanceRegistry.GetServerCount()));
        }

        public Task<PublicServerCountResponse> GetPublicServerCount(GetPublicMatchmakingServerCountRequest request)
        {
            return Task.FromResult(new PublicServerCountResponse(_instanceRegistry.GetPublicServerCount()));
        }

        public Task<SimplePlayersListResponce> GetSimplePlayerList(GetPlayersSimple request)
        {
            if (_instanceRegistry.TryGetInstance(request.Secret, out var instance))
            {
                SimplePlayer[] simplePlayers = new SimplePlayer[instance.GetPlayerRegistry().Players.Count - 1];
                for (int i = 0; i < instance.GetPlayerRegistry().Players.Count - 1; i++)
                {
                    simplePlayers[i] = new SimplePlayer(instance.GetPlayerRegistry().Players[i].UserName, instance.GetPlayerRegistry().Players[i].UserId);
                }
                return Task.FromResult(new SimplePlayersListResponce(simplePlayers));
            }
            return Task.FromResult(new SimplePlayersListResponce(null));
        }

        public Task<AdvancedPlayersListResponce> GetAdvancedPlayerList(GetPlayersAdvanced request)
        {
            if (_instanceRegistry.TryGetInstance(request.Secret, out var instance))
            {
                AdvancedPlayer[] advancedPlayers = new AdvancedPlayer[instance.GetPlayerRegistry().Players.Count - 1];
                for (int i = 0; i < instance.GetPlayerRegistry().Players.Count - 1; i++)
                {
                    IPlayer player = instance.GetPlayerRegistry().Players[i];
                    advancedPlayers[i] = new AdvancedPlayer(
                        new SimplePlayer(
                            player.UserName,
                            player.UserId),
                        player.ConnectionId,
                        player.IsManager,
                        player.IsPlayer,
                        player.IsSpectating,
                        player.WantsToPlayNextLevel,
                        player.IsBackgrounded,
                        player.InGameplay,
                        player.WasActiveAtLevelStart,
                        player.IsActive,
                        player.FinishedLevel,
                        player.InMenu,
                        player.IsModded,
                        player.InLobby
                        );
                }
                return Task.FromResult(new AdvancedPlayersListResponce(advancedPlayers));
            }
            return Task.FromResult(new AdvancedPlayersListResponce(null));
        }

        public Task<AdvancedPlayerResponce> GetAdvancedPlayer(GetPlayerAdvanced request)
        {
            if (_instanceRegistry.TryGetInstance(request.Secret, out var instance))
            {

                IPlayer player = instance.GetPlayerRegistry().GetPlayer(request.UserId);
                AdvancedPlayer AdvancedPlayer = new(
                    new SimplePlayer(
                        player.UserName,
                        player.UserId),
                    player.ConnectionId,
                    player.IsManager,
                    player.IsPlayer,
                    player.IsSpectating,
                    player.WantsToPlayNextLevel,
                    player.IsBackgrounded,
                    player.InGameplay,
                    player.WasActiveAtLevelStart,
                    player.IsActive,
                    player.FinishedLevel,
                    player.InMenu,
                    player.IsModded,
                    player.InLobby
                    );
                return Task.FromResult(new AdvancedPlayerResponce(AdvancedPlayer));
            }
            return Task.FromResult(new AdvancedPlayerResponce(null));
        }

        public Task<KickPlayerResponse> KickPlayer(KickPlayerRequest request)
        {
            if (_instanceRegistry.TryGetInstance(request.Secret, out var instance))
            {
                instance.DisconnectPlayer(instance.GetPlayerRegistry().GetPlayer(request.UserId));                
                return Task.FromResult(new KickPlayerResponse(true));
            }
            return Task.FromResult(new KickPlayerResponse(false));
        }

        public Task<AdvancedInstanceResponce> GetAdvancedInstance(GetAdvancedInstanceRequest request)
        {
            if (_instanceRegistry.TryGetInstance(request.Secret, out var instance))
            {
                ILobbyManager lobby = (ILobbyManager)instance.GetServiceProvider().GetService(typeof(ILobbyManager))!;

                GameplayServerConfiguration config = new(
                    instance.Configuration.MaxPlayerCount,
                    (DiscoveryPolicy)instance.Configuration.DiscoveryPolicy,
                    (InvitePolicy)instance.Configuration.InvitePolicy,
                    (GameplayServerMode)instance.Configuration.GameplayServerMode,
                    (SongSelectionMode)instance.Configuration.SongSelectionMode,
                    (GameplayServerControlSettings)instance.Configuration.GameplayServerControlSettings
                    );

                GameplayModifiers modifiers = new((EnergyType)lobby.SelectedModifiers.Energy,
                    lobby.SelectedModifiers.NoFailOn0Energy,
                    lobby.SelectedModifiers.DemoNoFail,
                    lobby.SelectedModifiers.InstaFail,
                    lobby.SelectedModifiers.FailOnSaberClash,
                    (EnabledObstacleType)lobby.SelectedModifiers.EnabledObstacle,
                    lobby.SelectedModifiers.DemoNoObstacles,
                    lobby.SelectedModifiers.FastNotes,
                    lobby.SelectedModifiers.StrictAngles,
                    lobby.SelectedModifiers.DisappearingArrows,
                    lobby.SelectedModifiers.GhostNotes,
                    lobby.SelectedModifiers.NoBombs,
                    (SongSpeed)lobby.SelectedModifiers.Speed,
                    lobby.SelectedModifiers.NoArrows,
                    lobby.SelectedModifiers.ProMode,
                    lobby.SelectedModifiers.ZenMode,
                    lobby.SelectedModifiers.SmallCubes);

                AdvancedInstance advancedInstance = new(
                    config,
                    instance.IsRunning,
                    instance.RunTime,
                    instance.Port,
                    instance.UserId,
                    instance.UserName,
                    (MultiplayerGameState)instance.State,
                    instance.NoPlayersTime,
                    instance.DestroyInstanceTimeout,
                    instance.SetManagerFromUserId,
                    lobby.CountdownEndTime,
                    (CountdownState)lobby.CountDownState,
                    modifiers);
                Beatmap beatmap;
                if (lobby.SelectedBeatmap != null)
                {
                    beatmap = new(
                        lobby.SelectedBeatmap.LevelId,
                        lobby.SelectedBeatmap.Characteristic,
                        (BeatmapDifficulty)lobby.SelectedBeatmap.Difficulty);
                }
                else
                {
                    beatmap = new(
                        "NULL",
                        "NULL",
                        BeatmapDifficulty.Normal);
                }

                return Task.FromResult(new AdvancedInstanceResponce(advancedInstance, beatmap));
            }
            return Task.FromResult(new AdvancedInstanceResponce(null, null));
        }

    }
}
