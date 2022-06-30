﻿using BeatTogether.DedicatedServer.Kernel.Abstractions;
using BeatTogether.DedicatedServer.Kernel.Enums;
using BeatTogether.DedicatedServer.Kernel.Managers.Abstractions;
using BeatTogether.DedicatedServer.Messaging.Enums;
using BeatTogether.DedicatedServer.Messaging.Models;
using BeatTogether.DedicatedServer.Messaging.Packets.MultiplayerSession.GameplayRpc;
using BeatTogether.LiteNetLib.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/*Gameplay manager code
 * Waits for players to have loaded the beatmap and be ready
 * Tells clients when to start beatmap
 * Waits for clients to finish before setting the dedicated server back to Lobby state
 * Handles when a client leaves gameplay mode
 */

namespace BeatTogether.DedicatedServer.Kernel.Managers
{
    public sealed class GameplayManager : IGameplayManager, IDisposable
    {
        public string SessionGameId { get; private set; } = null!;
        public GameplayManagerState State { get; private set; } = GameplayManagerState.None;
        public BeatmapIdentifier? CurrentBeatmap { get; private set; }
        public GameplayModifiers CurrentModifiers { get; private set; } = new();

        private const float SongStartDelay = 0.5f;
        private const float SceneLoadTimeLimit = 10.0f;
        private const float SongLoadTimeLimit = 10.0f;

        private float _songStartTime;
        IPlayer[]? PlayersAtStart = null;

        private CancellationTokenSource? _requestReturnToMenuCts;

        private readonly IDedicatedInstance _instance;
        private readonly IPlayerRegistry _playerRegistry;
        private readonly IPacketDispatcher _packetDispatcher;

        private readonly ConcurrentDictionary<string, PlayerSpecificSettings> _playerSpecificSettings = new();
        private readonly ConcurrentDictionary<string, LevelCompletionResults> _levelCompletionResults = new();

        private readonly ConcurrentDictionary<string, TaskCompletionSource> _levelFinishedTcs = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource> _sceneReadyTcs = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource> _songReadyTcs = new();

        public GameplayManager(
            IDedicatedInstance instance,
            IPlayerRegistry playerRegistry,
            IPacketDispatcher packetDispatcher)
        {
            _instance = instance;
            _playerRegistry = playerRegistry;
            _packetDispatcher = packetDispatcher;

            _instance.PlayerDisconnectedEvent += HandlePlayerLeaveGameplay;
        }

        public void Dispose()
        {
            _instance.PlayerDisconnectedEvent -= HandlePlayerLeaveGameplay;
        }

        public async void StartSong(BeatmapIdentifier beatmap, GameplayModifiers modifiers, CancellationToken cancellationToken)
        {
            if (State != GameplayManagerState.None)
            {
                _requestReturnToMenuCts!.Cancel();
                return;
            }
            _instance.SetState(MultiplayerGameState.Game);

            //Reset
            ResetValues(beatmap, modifiers);

            SessionGameId = Guid.NewGuid().ToString();
            _requestReturnToMenuCts = new CancellationTokenSource();

            State = GameplayManagerState.SceneLoad;

            PlayersAtStart = _playerRegistry.Players.ToArray(); // Should only wait for players that were already connected

            // Create level finished tasks (players may send these at any time during gameplay)
            var levelFinishedCts = new CancellationTokenSource();
            var linkedLevelFinishedCts = CancellationTokenSource.CreateLinkedTokenSource(levelFinishedCts.Token, _requestReturnToMenuCts.Token);
            IEnumerable<Task> levelFinishedTasks = PlayersAtStart.Select(p =>
            {
                var task = _levelFinishedTcs.GetOrAdd(p.UserId, _ => new());
                linkedLevelFinishedCts.Token.Register(() => task.TrySetResult());
                return task.Task;
            });
            
            // Create scene ready tasks
            var sceneReadyCts = new CancellationTokenSource();
            var linkedSceneReadyCts = CancellationTokenSource.CreateLinkedTokenSource(sceneReadyCts.Token, _requestReturnToMenuCts.Token);
            IEnumerable<Task> sceneReadyTasks = PlayersAtStart.Select(p => {
                var task = _sceneReadyTcs.GetOrAdd(p.UserId, _ => new());
                linkedSceneReadyCts.Token.Register(() => task.TrySetResult());
                return task.Task;
            });

            // Wait for scene ready
            _packetDispatcher.SendToNearbyPlayers(new GetGameplaySceneReadyPacket(), DeliveryMethod.ReliableOrdered);
            sceneReadyCts.CancelAfter((int)(SceneLoadTimeLimit * 1000));
            await Task.WhenAll(sceneReadyTasks);
            if (sceneReadyCts.IsCancellationRequested)//If it took over waiting for scene ready to load
                _requestReturnToMenuCts.Cancel();
            // Set scene sync finished
            State = GameplayManagerState.SongLoad;

            _packetDispatcher.SendToNearbyPlayers(new SetGameplaySceneSyncFinishedPacket
            {
                SessionGameId = SessionGameId,
                PlayersAtStart = new PlayerSpecificSettingsAtStart
                {
                    ActivePlayerSpecificSettingsAtStart = _playerSpecificSettings.Values.ToArray()
                }
            }, DeliveryMethod.ReliableOrdered);

            // Create song ready tasks
            
            var songReadyCts = new CancellationTokenSource();
            var linkedSongReadyCts = CancellationTokenSource.CreateLinkedTokenSource(songReadyCts.Token, _requestReturnToMenuCts.Token);
            IEnumerable<Task> songReadyTasks = PlayersAtStart.Select(p => {
                var task = _songReadyTcs.GetOrAdd(p.UserId, _ => new());
                linkedSongReadyCts.Token.Register(() => task.TrySetResult());
                return task.Task;
            });

            //Wait for players to have the song ready
            _packetDispatcher.SendToNearbyPlayers(new GetGameplaySongReadyPacket(), DeliveryMethod.ReliableOrdered);
            songReadyCts.CancelAfter((int)(SongLoadTimeLimit * 1000));
            await Task.WhenAll(songReadyTasks);
            if (songReadyCts.IsCancellationRequested) //If it took over Song load time limit to load
                _requestReturnToMenuCts.Cancel();

            // If no players are actually playing, or not all players are not in the lobby(if at least one player is then true)
            if (PlayersAtStart.All(player => !player.InGameplay) || PlayersAtStart.Any(player => player.InLobby))
                _requestReturnToMenuCts.Cancel(); //this will cancel the gameplay if someone is in the lobby


            // Start song and wait for finish
            State = GameplayManagerState.Gameplay;
            _songStartTime = _instance.RunTime + SongStartDelay;

            _packetDispatcher.SendToNearbyPlayers(new SetSongStartTimePacket
            {
                StartTime = _songStartTime
            }, DeliveryMethod.ReliableOrdered);

            await Task.WhenAll(levelFinishedTasks);
            State = GameplayManagerState.Results;

            // Wait at results screen if anyone cleared or skip if the countdown is set to 0.
            if (_levelCompletionResults.Values.Any(result => result.LevelEndStateType == LevelEndStateType.Cleared) && _instance.Configuration.CountdownConfig.ResultsScreenTime > 0)
                await Task.Delay((int)(_instance.Configuration.CountdownConfig.ResultsScreenTime * 1000), cancellationToken);

            // End gameplay and reset
            SignalRequestReturnToMenu();
            State = GameplayManagerState.None;
            _packetDispatcher.SendToNearbyPlayers(new ReturnToMenuPacket(), DeliveryMethod.ReliableOrdered);
            _instance.SetState(MultiplayerGameState.Lobby);
        }

        private void ResetValues(BeatmapIdentifier? map, GameplayModifiers modifiers)
        {
            CurrentBeatmap = map;
            CurrentModifiers = modifiers;
            _levelFinishedTcs.Clear();
            _sceneReadyTcs.Clear();
            _songReadyTcs.Clear();
            _songStartTime = 0;
            _playerSpecificSettings.Clear();
            _levelCompletionResults.Clear();
            PlayersAtStart = null;
        }


        public void HandleGameSceneLoaded(IPlayer player, SetGameplaySceneReadyPacket packet)
        {
            if (_sceneReadyTcs.TryGetValue(player.UserId, out var tcs) && tcs.Task.IsCompleted)
                return;
            if(PlayersAtStart!= null && PlayersAtStart.Contains(player))
                _playerSpecificSettings[player.UserId] = packet.PlayerSpecificSettings;
            if (_instance.State == MultiplayerGameState.Game && State != GameplayManagerState.SceneLoad)
                _packetDispatcher.SendToNearbyPlayers(new SetPlayerDidConnectLatePacket
                {
                    UserId = player.UserId,
                    PlayersAtStart = new PlayerSpecificSettingsAtStart
                    {
                        ActivePlayerSpecificSettingsAtStart = _playerSpecificSettings.Values.ToArray()
                    },
                    SessionGameId = SessionGameId
                }, DeliveryMethod.ReliableOrdered);

            if (_instance.State != MultiplayerGameState.Game)
            {
                _packetDispatcher.SendToPlayer(player, new ReturnToMenuPacket(), DeliveryMethod.ReliableOrdered);
                HandlePlayerLeaveGameplay(player);
            }
            PlayerSceneReady(player);
        }

        public void HandleGameSongLoaded(IPlayer player)
        {
            if (_songReadyTcs.TryGetValue(player.UserId, out var tcs) && tcs.Task.IsCompleted)
                return;
            if (_instance.State == MultiplayerGameState.Game && State != GameplayManagerState.SongLoad)
                _packetDispatcher.SendToPlayer(player, new SetSongStartTimePacket
                {
                    StartTime = _songStartTime
                }, DeliveryMethod.ReliableOrdered);
            if (_instance.State != MultiplayerGameState.Game)
            {
                _packetDispatcher.SendToPlayer(player, new ReturnToMenuPacket(), DeliveryMethod.ReliableOrdered);
                HandlePlayerLeaveGameplay(player);
            }
            PlayerSongReady(player);
        }

        public void HandleLevelFinished(IPlayer player, LevelFinishedPacket packet)
        {
            if (_levelFinishedTcs.TryGetValue(player.UserId, out var tcs) && tcs.Task.IsCompleted)
                return;
            if(PlayersAtStart != null &&  PlayersAtStart.Contains(player))
                _levelCompletionResults[player.UserId] = packet.Results.LevelCompletionResults;
            PlayerFinishLevel(player);
        }

        object RequestReturnLock = new();
        public void SignalRequestReturnToMenu()
        {
            lock (RequestReturnLock)
            {
                if (_requestReturnToMenuCts != null && !_requestReturnToMenuCts.IsCancellationRequested)
                    {
                    ResetValues(null, new());
                    foreach (var p in _playerRegistry.Players)
                    {
                        HandlePlayerLeaveGameplay(p);
                    }
                    _requestReturnToMenuCts?.Cancel();
                }
            }
        }

        //will set players tasks as done if they leave gameplay due to disconnect or returning to the menu
        private void HandlePlayerLeaveGameplay(IPlayer player)
        {
            PlayerFinishLevel(player);
            PlayerSceneReady(player);
            PlayerSongReady(player);
        }

        private void PlayerFinishLevel(IPlayer player)
        {
            if (_levelFinishedTcs.TryGetValue(player.UserId, out var tcs) && !tcs.Task.IsCompleted)
                tcs.SetResult();
        }
        private void PlayerSceneReady(IPlayer player)
        {
            if (_sceneReadyTcs.TryGetValue(player.UserId, out var tcs) && !tcs.Task.IsCompleted)
                tcs.SetResult();
        }
        private void PlayerSongReady(IPlayer player)
        {
            if (_songReadyTcs.TryGetValue(player.UserId, out var tcs) && !tcs.Task.IsCompleted)
                tcs.SetResult();
        }
    }
}
