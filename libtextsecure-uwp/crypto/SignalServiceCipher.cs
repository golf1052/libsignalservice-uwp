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
using Google.ProtocolBuffers;
using libsignal;
using libsignal.protocol;
using libsignal.state;
using libtextsecure.messages;
using libtextsecure.messages.multidevice;
using libtextsecure.push;
using libtextsecure.util;
using Strilanc.Value;
using static libtextsecure.push.SignalServiceProtos;

namespace libtextsecure.crypto
{
    /// <summary>
    /// This is used to decrypt received <see cref="SignalServiceEnvelope"/>s
    /// </summary>
    public class SignalServiceCipher
    {
        private static readonly string TAG = "SignalServiceCipher";

        private readonly SignalProtocolStore signalProtocolStore;
        private readonly SignalServiceAddress localAddress;

        public SignalServiceCipher(SignalServiceAddress localAddress, SignalProtocolStore signalProtocolStore)
        {
            this.signalProtocolStore = signalProtocolStore;
            this.localAddress = localAddress;
        }

        public OutgoingPushMessage encrypt(SignalProtocolAddress destination, byte[] unpaddedMessage, bool legacy)
        {
            SessionCipher sessionCipher = new SessionCipher(signalProtocolStore, destination);
            PushTransportDetails transportDetails = new PushTransportDetails(sessionCipher.getSessionVersion());
            CiphertextMessage message = sessionCipher.encrypt(transportDetails.getPaddedMessageBody(unpaddedMessage));
            uint remoteRegistrationId = sessionCipher.getRemoteRegistrationId();
            String body = Base64.encodeBytes(message.serialize());

            uint type;

            switch (message.getType())
            {
                case CiphertextMessage.PREKEY_TYPE: type = (uint)Envelope.Types.Type.PREKEY_BUNDLE; break; // todo check
                case CiphertextMessage.WHISPER_TYPE: type = (uint)Envelope.Types.Type.CIPHERTEXT; break; // todo check
                default: throw new Exception("Bad type: " + message.getType());
            }

            return new OutgoingPushMessage(type, destination.getDeviceId(), remoteRegistrationId, legacy ? body : null, legacy ? null : body);
        }

        /// <summary>
        /// Decrypt a received <see cref="SignalServiceEnvelope"/>
        /// </summary>
        /// <param name="envelope">The received SignalServiceEnvelope</param>
        /// <returns>a decrypted SignalServiceContent</returns>
        public TextSecureContent decrypt(SignalServiceEnvelope envelope)
        {
            try
            {
                TextSecureContent content = new TextSecureContent();

                if (envelope.hasLegacyMessage())
                {
                    DataMessage message = DataMessage.ParseFrom(decrypt(envelope, envelope.getLegacyMessage()));
                    content = new TextSecureContent(createSignalServiceMessage(envelope, message));
                }
                else if (envelope.hasContent())
                {
                    Content message = Content.ParseFrom(decrypt(envelope, envelope.getContent()));

                    if (message.HasDataMessage)
                    {
                        content = new TextSecureContent(createSignalServiceMessage(envelope, message.DataMessage));
                    }
                    else if (message.HasSyncMessage && localAddress.getNumber().Equals(envelope.getSource()))
                    {
                        content = new TextSecureContent(createSynchronizeMessage(envelope, message.SyncMessage));
                    }
                }

                return content;
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new InvalidMessageException(e);
            }
        }

        private byte[] decrypt(SignalServiceEnvelope envelope, byte[] ciphertext)

        {
            SignalProtocolAddress sourceAddress = new SignalProtocolAddress(envelope.getSource(), (uint)envelope.getSourceDevice());
            SessionCipher sessionCipher = new SessionCipher(signalProtocolStore, sourceAddress);

            byte[] paddedMessage;

            if (envelope.isPreKeySignalMessage())
            {
                paddedMessage = sessionCipher.decrypt(new PreKeySignalMessage(ciphertext));
            }
            else if (envelope.isSignalMessage())
            {
                paddedMessage = sessionCipher.decrypt(new SignalMessage(ciphertext));
            }
            else
            {
                throw new InvalidMessageException("Unknown type: " + envelope.getType());
            }

            PushTransportDetails transportDetails = new PushTransportDetails(sessionCipher.getSessionVersion());
            return transportDetails.getStrippedPaddingMessageBody(paddedMessage);
        }

        private SignalServiceDataMessage createSignalServiceMessage(SignalServiceEnvelope envelope, DataMessage content)
        {
            SignalServiceGroup groupInfo = createGroupInfo(envelope, content);
            List<SignalServiceAttachment> attachments = new List<SignalServiceAttachment>();
            bool endSession = ((content.Flags & (uint)DataMessage.Types.Flags.END_SESSION) != 0);

            foreach (AttachmentPointer pointer in content.AttachmentsList)
            {
                attachments.Add(new SignalServiceAttachmentPointer(pointer.Id,
                                                                pointer.ContentType,
                                                                pointer.Key.ToByteArray(),
                                                                envelope.getRelay(),
                                                                pointer.HasSize ? new May<uint>(pointer.Size) : May<uint>.NoValue,
                                                                pointer.HasThumbnail ? new May<byte[]>(pointer.Thumbnail.ToByteArray()) : May<byte[]>.NoValue));
            }

            return new SignalServiceDataMessage(envelope.getTimestamp(), groupInfo, attachments,
                                             content.Body, endSession);
        }

        private TextSecureSyncMessage createSynchronizeMessage(SignalServiceEnvelope envelope, SyncMessage content)
        {
            if (content.HasSent)
            {
                SyncMessage.Types.Sent sentContent = content.Sent;
                return TextSecureSyncMessage.forSentTranscript(new SentTranscriptMessage(sentContent.Destination,
                                                                           sentContent.Timestamp,
                                                                           createSignalServiceMessage(envelope, sentContent.Message)));
            }

            if (content.HasRequest)
            {
                return TextSecureSyncMessage.forRequest(new RequestMessage(content.Request));
            }

            if (content.ReadList.Count > 0)
            {
                List<ReadMessage> readMessages = new List<ReadMessage>();

                foreach (SyncMessage.Types.Read read in content.ReadList)
                {
                    readMessages.Add(new ReadMessage(read.Sender, (long)read.Timestamp));
                }

                return TextSecureSyncMessage.forRead(readMessages);
            }

            return TextSecureSyncMessage.empty();
        }

        private SignalServiceGroup createGroupInfo(SignalServiceEnvelope envelope, DataMessage content)
        {
            if (!content.HasGroup) return null;

            SignalServiceGroup.Type type;

            switch (content.Group.Type)
            {
                case GroupContext.Types.Type.DELIVER: type = SignalServiceGroup.Type.DELIVER; break;
                case GroupContext.Types.Type.UPDATE: type = SignalServiceGroup.Type.UPDATE; break;
                case GroupContext.Types.Type.QUIT: type = SignalServiceGroup.Type.QUIT; break;
                default: type = SignalServiceGroup.Type.UNKNOWN; break;
            }

            if (content.Group.Type != GroupContext.Types.Type.DELIVER)
            {
                String name = null;
                IList<String> members = null;
                SignalServiceAttachmentPointer avatar = null;

                if (content.Group.HasName)
                {
                    name = content.Group.Name;
                }

                if (content.Group.MembersCount > 0)
                {
                    members = content.Group.MembersList;
                }

                if (content.Group.HasAvatar)
                {
                    avatar = new SignalServiceAttachmentPointer(content.Group.Avatar.Id,
                                                             content.Group.Avatar.ContentType,
                                                             content.Group.Avatar.Key.ToByteArray(),
                                                             envelope.getRelay());
                }

                return new SignalServiceGroup(type, content.Group.Id.ToByteArray(), name, members, avatar);
            }

            return new SignalServiceGroup(content.Group.Id.ToByteArray());
        }


    }
}
