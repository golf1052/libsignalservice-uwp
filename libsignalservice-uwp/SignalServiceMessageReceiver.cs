/** 
 * Copyright (C) 2017 smndtrl, golf1052
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using libsignalservice.messages;
using libsignalservice.push;
using libsignalservice.util;
using libsignalservice.websocket;
using Windows.Storage;
using static libsignalservice.messages.SignalServiceAttachment;

namespace libsignalservice
{
    /// <summary>
    /// The primary interface for receiving Signal Service messages.
    /// </summary>
    public class SignalServiceMessageReceiver
    {
        private readonly PushServiceSocket socket;
        private readonly TrustStore trustStore;
        private readonly String url;
        private readonly CredentialsProvider credentialsProvider;
        private readonly string userAgent;

        /// <summary>
        /// Construct a SignalServiceMessageReceiver
        /// </summary>
        /// <param name="url">The URL of the Signal Service.</param>
        /// <param name="trustStore">The <see cref="libsignalservice.push.TrustStore"/> containing
        /// the server's TLS signing certificate</param>
        /// <param name="user">The Signal Service username (eg. phone number).</param>
        /// <param name="password">The Signal Service user password.</param>
        /// <param name="signalingKey">The 52 byte signaling key assigned to this user at registration</param>
        /// <param name="userAgent"></param>
        public SignalServiceMessageReceiver(String url, TrustStore trustStore,
                                         String user, String password, String signalingKey, string userAgent)
            : this(url, trustStore, new StaticCredentialsProvider(user, password, signalingKey), userAgent)
        {
        }

        /// <summary>
        /// Construct a SignalServiceMessageReceiver.
        /// </summary>
        /// <param name="url">The URL of the Signal Service.</param>
        /// <param name="trustStore">The <see cref="libsignalservice.push.TrustStore"/> containing
        /// the server's TLS signing certificate</param>
        /// <param name="credentials">The Signal Service user's credentials</param>
        /// <param name="userAgent"></param>
        public SignalServiceMessageReceiver(String url, TrustStore trustStore, CredentialsProvider credentials, string userAgent)
        {
            this.url = url;
            this.trustStore = trustStore;
            this.credentialsProvider = credentials;
            this.socket = new PushServiceSocket(url, trustStore, credentials, userAgent);
            this.userAgent = userAgent;
        }

        /// <summary>
        /// Retrieves a SignalServiceAttachment
        /// </summary>
        /// <param name="pointer">The <see cref="SignalServiceAttachmentPointer"/>
        /// received in a <see cref="SignalServiceDataMessage"/></param>
        /// <param name="destination">The download destination for this attachment.</param>
        /// <returns>A Stream that streams the plaintext attachment contents.</returns>
        public Stream retrieveAttachment(SignalServiceAttachmentPointer pointer, StorageFile destination)
        {
            throw new NotImplementedException();
            return retrieveAttachment(pointer, destination, null);
        }

        /// <summary>
        /// Retrieves a SignalServiceAttachment
        /// </summary>
        /// <param name="pointer">The <see cref="SignalServiceAttachmentPointer"/>
        /// received in a <see cref="SignalServiceDataMessage"/></param>
        /// <param name="destination">The download destination for this attachment.</param>
        /// <param name="listener">An optional listener (may be null) to receive callbacks on download progress.</param>
        /// <returns>A Stream that streams the plaintext attachment contents.</returns>
        public Stream retrieveAttachment(SignalServiceAttachmentPointer pointer, StorageFile destination, ProgressListener listener)
        {
            throw new NotImplementedException();
            return new MemoryStream();
        }

        /// <summary>
        /// Creates a pipe for receiving SignalService messages.
        /// 
        /// Callers must call <see cref="SignalServiceMessagePipe.shutdown()"/> when finished with the pipe.
        /// </summary>
        /// <returns>A SignalServiceMessagePipe for receiving Signal Service messages.</returns>
        public SignalServiceMessagePipe createMessagePipe()
        {
            WebSocketConnection webSocket = new WebSocketConnection(url, trustStore, credentialsProvider, userAgent);
            return new SignalServiceMessagePipe(webSocket, credentialsProvider);
        }

        public async Task<List<SignalServiceEnvelope>> retrieveMessages()
        {
            return await retrieveMessages(new NullMessageReceivedCallback());
        }

        public async Task<List<SignalServiceEnvelope>> retrieveMessages(MessageReceivedCallback callback)
        {
            List<SignalServiceEnvelope> results = new List<SignalServiceEnvelope>();
            List<SignalServiceEnvelopeEntity> entities = await socket.getMessages();

            foreach (SignalServiceEnvelopeEntity entity in entities)
            {
                SignalServiceEnvelope envelope = new SignalServiceEnvelope((int)entity.getType(), entity.getSource(),
                                                                      (int)entity.getSourceDevice(), entity.getRelay(),
                                                                      (int)entity.getTimestamp(), entity.getMessage(),
                                                                      entity.getContent());

                callback.onMessage(envelope);
                results.Add(envelope);

                socket.acknowledgeMessage(entity.getSource(), entity.getTimestamp());
            }

            return results;
        }


        public interface MessageReceivedCallback
        {
            void onMessage(SignalServiceEnvelope envelope);
        }

        public class NullMessageReceivedCallback : MessageReceivedCallback
        {
            public void onMessage(SignalServiceEnvelope envelope) { }
        }

    }
}
