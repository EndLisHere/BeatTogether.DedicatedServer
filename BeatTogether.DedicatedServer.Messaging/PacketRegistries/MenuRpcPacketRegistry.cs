﻿using BeatTogether.DedicatedServer.Messaging.Enums;
using BeatTogether.DedicatedServer.Messaging.Packets.MultiplayerSession.MenuRpc;

namespace BeatTogether.DedicatedServer.Messaging.PacketRegistries
{
    public sealed class MenuRpcPacketRegistry : BasePacketRegistry
    {
        public override void Register()
        {
            AddPacket<GetMultiplayerGameStatePacket>(MenuRpcPacketType.GetMultiplayerGameState);
            AddPacket<SetMultiplayerGameStatePacket>(MenuRpcPacketType.SetMultiplayerGameState);
            AddPacket<GetIsReadyPacket>(MenuRpcPacketType.GetIsReady);
            AddPacket<SetIsReadyPacket>(MenuRpcPacketType.SetIsReady);
            AddPacket<GetIsInLobbyPacket>(MenuRpcPacketType.GetIsInLobby);
            AddPacket<SetIsInLobbyPacket>(MenuRpcPacketType.SetIsInLobby);
            AddPacket<GetOwnedSongPacksPacket>(MenuRpcPacketType.GetOwnedSongPacks);
            AddPacket<SetOwnedSongPacksPacket>(MenuRpcPacketType.SetOwnedSongPacks);
            AddPacket<GetPlayersPermissionConfigurationPacket>(MenuRpcPacketType.GetPermissionConfiguration);
            AddPacket<SetPlayersPermissionConfigurationPacket>(MenuRpcPacketType.SetPermissionConfiguration);
            AddPacket<GetRecommendedBeatmapPacket>(MenuRpcPacketType.GetRecommendedBeatmap);
            AddPacket<SetRecommendedBeatmapPacket>(MenuRpcPacketType.RecommendBeatmap);
            AddPacket<GetRecommendedGameplayModifiersPacket>(MenuRpcPacketType.GetRecommendedGameplayModifiers);
            AddPacket<GetStartedLevelPacket>(MenuRpcPacketType.GetStartedLevel);
            AddPacket<SetIsEntitledToLevelPacket>(MenuRpcPacketType.SetIsEntitledToLevel);
            AddPacket<GetIsEntitledToLevelPacket>(MenuRpcPacketType.GetIsEntitledToLevel);
            AddPacket<GetCountdownEndTimePacket>(MenuRpcPacketType.GetCountdownEndTime);
            AddPacket<SetCountdownEndTimePacket>(MenuRpcPacketType.SetCountdownEndTime);
            AddPacket<GetIsStartButtonEnabledPacket>(MenuRpcPacketType.GetIsStartButtonEnabled);
            AddPacket<SetIsStartButtonEnabledPacket>(MenuRpcPacketType.SetIsStartButtonEnabled);
            AddPacket<SetStartGameTimePacket>(MenuRpcPacketType.SetStartGameTime);
            AddPacket<CancelCountdownPacket>(MenuRpcPacketType.CancelCountdown);
            AddPacket<CancelLevelStartPacket>(MenuRpcPacketType.CancelLevelStart);
            AddPacket<StartLevelPacket>(MenuRpcPacketType.StartLevel);
            AddPacket<ClearRecommendedBeatmapPacket>(MenuRpcPacketType.ClearRecommendedBeatmap);
            AddPacket<ClearRecommendedModifiersPacket>(MenuRpcPacketType.ClearRecommendedGameplayModifiers);
            AddPacket<SetPlayersMissingEntitlementsToLevelPacket>(MenuRpcPacketType.SetPlayersMissingEntitlementsToLevel);
        }
    }
}
