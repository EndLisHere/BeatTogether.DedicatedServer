﻿using System;
using System.Collections.Concurrent;
using System.Net;
using BeatTogether.Core.Messaging.Abstractions;
using BeatTogether.Core.Messaging.Messages;
using BeatTogether.DedicatedServer.Kernel.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Sources;
using Krypton.Buffers;
using Serilog;

namespace BeatTogether.DedicatedServer.Kernel.Handshake
{
    public class UnconnectedSource : UnconnectedMessageSource
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IUnconnectedDispatcher _unconnectedDispatcher;
        private readonly IMessageReader _messageReader;

        private readonly ConcurrentDictionary<EndPoint, HandshakeSession> _sessions;

        private readonly ILogger _logger;

        public UnconnectedSource(IServiceProvider serviceProvider, IUnconnectedDispatcher unconnectedDispatcher,
            IMessageReader messageReader)
        {
            _serviceProvider = serviceProvider;
            _unconnectedDispatcher = unconnectedDispatcher;
            _messageReader = messageReader;

            _sessions = new();

            _logger = Log.ForContext<UnconnectedSource>();
        }

        #region Receive

        public override void OnReceive(EndPoint remoteEndPoint, ref SpanBufferReader reader,
            UnconnectedMessageType type)
        {
            var session = _sessions.GetOrAdd(remoteEndPoint, (ep) => new HandshakeSession(ep));
            var message = _messageReader.ReadFrom(ref reader);

            HandleMessage(session, message);
        }

        public void HandleMessage(HandshakeSession session, IMessage message)
        {
            var messageType = message.GetType();

            // Skip previously handled messages
            if (message is IRequest request && !session.ShouldHandleRequest(request.RequestId))
            {
                _logger.Warning("Skipping duplicate request (MessageType={Type}, RequestId={RequestId})",
                    messageType.Name, request.RequestId
                );
                return;
            }

            // Acknowledge reliable messages
            if (message is IReliableRequest reliableRequest)
            {
                _unconnectedDispatcher.Send(session, new AcknowledgeMessage()
                {
                    ResponseId = reliableRequest.RequestId,
                    MessageHandled = true
                });
            }

            // Dispatch to handler
            _logger.Information("Handling handshake message of type {MessageType} (EndPoint={EndPoint})",
                messageType.Name, session.EndPoint.ToString());
            
            var targetHandlerType = typeof(IHandshakeMessageHandler<>).MakeGenericType(messageType);
            var messageHandler = _serviceProvider.GetService(targetHandlerType);

            if (messageHandler is null)
            {
                _logger.Warning("No handler exists for handshake message {MessageType}",
                    messageType.Name);
                return;
            }

            ((IHandshakeMessageHandler) messageHandler).Handle(session, message);
        }

        #endregion
    }
}