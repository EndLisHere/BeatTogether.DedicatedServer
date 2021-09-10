﻿using BeatTogether.DedicatedServer.Kernel.Abstractions;
using BeatTogether.DedicatedServer.Messaging.Enums;
using BeatTogether.DedicatedServer.Messaging.Packets.MultiplayerSession.MenuRpc;
using LiteNetLib;
using Serilog;
using System.Threading.Tasks;

namespace BeatTogether.DedicatedServer.Kernel.PacketHandlers.MultiplayerSession.MenuRpc
{
	public sealed class SetRecommendedModifiersPacketHandler : BasePacketHandler<SetRecommendedModifiersPacket>
	{
		private readonly IPacketDispatcher _packetDispatcher;
		private readonly ILogger _logger = Log.ForContext<SetRecommendedModifiersPacket>();

		public SetRecommendedModifiersPacketHandler(IPacketDispatcher packetDispatcher)
		{
			_packetDispatcher = packetDispatcher;
		}

		public override Task Handle(IPlayer sender, SetRecommendedModifiersPacket packet)
		{
			_logger.Debug(
				$"Handling packet of type '{nameof(SetRecommendedModifiersPacket)}' " +
				$"(SenderId={sender.ConnectionId})."
			);

			sender.Modifiers = packet.Modifiers;

			return Task.CompletedTask;
		}
	}
}
