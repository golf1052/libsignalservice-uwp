﻿/** 
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
using System.Diagnostics;
using System.Threading.Tasks;
using Google.ProtocolBuffers;
using libsignal;
using libsignal.state;
using libsignal.util;
using libtextsecure.crypto;
using libtextsecure.messages;
using libtextsecure.messages.multidevice;
using libtextsecure.push;
using libtextsecure.push.exceptions;
using libtextsecure.util;
using Strilanc.Value;
using static libtextsecure.push.SignalServiceProtos;

namespace libtextsecure
{
    /// <summary>
    /// The main interface for sending Signal Service messages.
    /// </summary>
    public class SignalServiceMessageSender
    {
        private static string TAG = "SignalServiceMessageSender";

        private readonly PushServiceSocket socket;
        private readonly SignalProtocolStore store;
        private readonly SignalServiceAddress localAddress;
        private readonly May<EventListener> eventListener;
        private readonly string userAgent;

        /// <summary>
        /// Construct a SignalServiceMessageSender
        /// </summary>
        /// <param name="url">The URL of the Signal Service.</param>
        /// <param name="trustStore">The trust store containing the Signal Service's signing TLS certificate.</param>
        /// <param name="user">The Signal Service username (eg phone number).</param>
        /// <param name="password">The Signal Service user password</param>
        /// <param name="store">The SignalProtocolStore.</param>
        /// <param name="eventListener">An optional event listener, which fires whenever sessions are
        /// setup or torn down for a recipient.</param>
        /// <param name="userAgent"></param>
        public SignalServiceMessageSender(String url, TrustStore trustStore,
                                       String user, String password,
                                       SignalProtocolStore store,
                                       May<EventListener> eventListener, String userAgent)
        {
            this.socket = new PushServiceSocket(url, trustStore, new StaticCredentialsProvider(user, password, null), userAgent);
            this.store = store;
            this.localAddress = new SignalServiceAddress(user);
            this.eventListener = eventListener;
        }

        /// <summary>
        /// Send a delivery receipt for a received message.  It is not necessary to call this
        /// when receiving messages through <see cref="SignalServiceMessagePipe"/>
        /// </summary>
        /// <param name="recipient">The sender of the received message you're acknowledging.</param>
        /// <param name="messageId">The message id of the received message you're acknowledging.</param>
        public void sendDeliveryReceipt(SignalServiceAddress recipient, ulong messageId)
        {
            this.socket.sendReceipt(recipient.getNumber(), messageId, recipient.getRelay());
        }

        /// <summary>
        /// Send a message to a single recipient.
        /// </summary>
        /// <param name="recipient">The message's destination.</param>
        /// <param name="message">The message.</param>
        public async Task sendMessage(SignalServiceAddress recipient, SignalServiceDataMessage message)
        {
            byte[] content = await createMessageContent(message);
            long timestamp = message.getTimestamp();
            SendMessageResponse response = await sendMessage(recipient, (ulong)timestamp, content, true);

            if (response != null && response.getNeedsSync())
            {
                byte[] syncMessage = createMultiDeviceSentTranscriptContent(content, new May<SignalServiceAddress>(recipient), (ulong)timestamp);
                await sendMessage(localAddress, (ulong)timestamp, syncMessage, false);
            }

            if (message.isEndSession())
            {
                store.DeleteAllSessions(recipient.getNumber());

                if (eventListener.HasValue)
                {
                    eventListener.ForceGetValue().onSecurityEvent(recipient);
                }
            }
        }

        /// <summary>
        /// Send a message to a group.
        /// </summary>
        /// <param name="recipients">The group members.</param>
        /// <param name="message">The group message.</param>
        public async Task sendMessage(List<SignalServiceAddress> recipients, SignalServiceDataMessage message)
        {
            byte[] content = await createMessageContent(message);
            long timestamp = message.getTimestamp();
            SendMessageResponse response = sendMessage(recipients, (ulong)timestamp, content, true);

            try
            {
                if (response != null && response.getNeedsSync())
                {
                    byte[] syncMessage = createMultiDeviceSentTranscriptContent(content, May<SignalServiceAddress>.NoValue, (ulong)timestamp);
                    await sendMessage(localAddress, (ulong)timestamp, syncMessage, false);
                }
            }
            catch (UntrustedIdentityException e)
            {
                throw new EncapsulatedExceptions(e);
            }
        }

        public async Task sendMessage(TextSecureSyncMessage message)
        {
            byte[] content;

            if (message.getContacts().HasValue)
            {
                content = await createMultiDeviceContactsContent(message.getContacts().ForceGetValue().asStream());
            }
            else if (message.getGroups().HasValue)
            {
                content = await createMultiDeviceGroupsContent(message.getGroups().ForceGetValue().asStream());
            }
            else if (message.getRead().HasValue)
            {
                content = createMultiDeviceReadContent(message.getRead().ForceGetValue());
            }
            else
            {
                throw new Exception("Unsupported sync message!");
            }

            await sendMessage(localAddress, KeyHelper.getTime(), content, false);
        }

        private async Task<byte[]> createMessageContent(SignalServiceDataMessage message)// throws IOException
        {
            DataMessage.Builder builder = DataMessage.CreateBuilder();
            /*List<AttachmentPointer> pointers = createAttachmentPointers(message.getAttachments());

            if (!pointers.Any()) // TODO:check
            {
                builder.AddRangeAttachments(pointers);
            }*/

            if (message.getBody().HasValue)
            {
                builder.SetBody(message.getBody().ForceGetValue());
            }

            if (message.getGroupInfo().HasValue)
            {
                builder.SetGroup(await createGroupContent(message.getGroupInfo().ForceGetValue()));
            }

            if (message.isEndSession())
            {
                builder.SetFlags((uint)DataMessage.Types.Flags.END_SESSION);
            }

            return builder.Build().ToByteArray();
        }
        private async Task<byte[]> createMultiDeviceContactsContent(SignalServiceAttachmentStream contacts)
        {
            Content.Builder container = Content.CreateBuilder();
            SyncMessage.Builder builder = SyncMessage.CreateBuilder();
            builder.SetContacts(SyncMessage.Types.Contacts.CreateBuilder()
                                            .SetBlob(await createAttachmentPointer(contacts)));

            return container.SetSyncMessage(builder).Build().ToByteArray();
        }

        private async Task<byte[]> createMultiDeviceGroupsContent(SignalServiceAttachmentStream groups)
        {
            Content.Builder container = Content.CreateBuilder();
            SyncMessage.Builder builder = SyncMessage.CreateBuilder();
            builder.SetGroups(SyncMessage.Types.Groups.CreateBuilder()
                                        .SetBlob(await createAttachmentPointer(groups)));

            return container.SetSyncMessage(builder).Build().ToByteArray();
        }

        private byte[] createMultiDeviceSentTranscriptContent(byte[] content, May<SignalServiceAddress> recipient, ulong timestamp)
        {
            try
            {
                Content.Builder container = Content.CreateBuilder();
                SyncMessage.Builder syncMessage = SyncMessage.CreateBuilder();
                SyncMessage.Types.Sent.Builder sentMessage = SyncMessage.Types.Sent.CreateBuilder();

                sentMessage.SetTimestamp(timestamp);
                sentMessage.SetMessage(DataMessage.ParseFrom(content));

                if (recipient.HasValue)
                {
                    sentMessage.SetDestination(recipient.ForceGetValue().getNumber());
                }

                return container.SetSyncMessage(syncMessage.SetSent(sentMessage)).Build().ToByteArray();
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new Exception(e.Message);
            }
        }

        private byte[] createMultiDeviceReadContent(List<ReadMessage> readMessages)
        {
            Content.Builder container = Content.CreateBuilder();
            SyncMessage.Builder builder = SyncMessage.CreateBuilder();

            foreach (ReadMessage readMessage in readMessages)
            {
                builder.AddRead(SyncMessage.Types.Read.CreateBuilder()
                    .SetTimestamp((ulong)readMessage.getTimestamp())
                    .SetSender(readMessage.getSender()));
            }

            return container.SetSyncMessage(builder).Build().ToByteArray();
        }

        private byte[] createSentTranscriptMessage(byte[] content, May<SignalServiceAddress> recipient, ulong timestamp)
        {
            {
                try
                {
                    Content.Builder container = Content.CreateBuilder();
                    SyncMessage.Builder syncMessage = SyncMessage.CreateBuilder();
                    SyncMessage.Types.Sent.Builder sentMessage = SyncMessage.Types.Sent.CreateBuilder();

                    sentMessage.SetTimestamp(timestamp);
                    sentMessage.SetMessage(DataMessage.ParseFrom(content));

                    if (recipient.HasValue)
                    {
                        sentMessage.SetDestination(recipient.ForceGetValue().getNumber());
                    }

                    return container.SetSyncMessage(syncMessage.SetSent(sentMessage)).Build().ToByteArray(); ;
                }
                catch (InvalidProtocolBufferException e)
                {
                    throw new Exception(e.Message);
                }
            }
        }

        private async Task<GroupContext> createGroupContent(SignalServiceGroup group)
        {
            GroupContext.Builder builder = GroupContext.CreateBuilder();
            builder.SetId(ByteString.CopyFrom(group.getGroupId()));

            if (group.getType() != SignalServiceGroup.Type.DELIVER)
            {
                if (group.getType() == SignalServiceGroup.Type.UPDATE) builder.SetType(GroupContext.Types.Type.UPDATE);
                else if (group.getType() == SignalServiceGroup.Type.QUIT) builder.SetType(GroupContext.Types.Type.QUIT);
                else throw new Exception("Unknown type: " + group.getType());

                if (group.getName().HasValue) builder.SetName(group.getName().ForceGetValue());
                if (group.getMembers().HasValue) builder.AddRangeMembers(group.getMembers().ForceGetValue());

                if (group.getAvatar().HasValue && group.getAvatar().ForceGetValue().isStream())
                {
                    AttachmentPointer pointer = await createAttachmentPointer(group.getAvatar().ForceGetValue().asStream());
                    builder.SetAvatar(pointer);
                }
            }
            else
            {
                builder.SetType(GroupContext.Types.Type.DELIVER);
            }

            return builder.Build();
        }

        private SendMessageResponse sendMessage(List<SignalServiceAddress> recipients, ulong timestamp, byte[] content, bool legacy)
        {
            IList<UntrustedIdentityException> untrustedIdentities = new List<UntrustedIdentityException>(); // was linkedlist
            IList<UnregisteredUserException> unregisteredUsers = new List<UnregisteredUserException>();
            IList<NetworkFailureException> networkExceptions = new List<NetworkFailureException>();

            SendMessageResponse response = null;

            foreach (SignalServiceAddress recipient in recipients)
            {
                try
                {
                    response = sendMessage(recipients, timestamp, content, legacy);
                }
                catch (UntrustedIdentityException e)
                {
                    //Log.w(TAG, e);
                    untrustedIdentities.Add(e);
                }
                catch (UnregisteredUserException e)
                {
                    //Log.w(TAG, e);
                    unregisteredUsers.Add(e);
                }
                catch (PushNetworkException e)
                {
                    //Log.w(TAG, e);
                    networkExceptions.Add(new NetworkFailureException(recipient.getNumber(), e));
                }
            }

            if (!(untrustedIdentities.Count == 0) || !(unregisteredUsers.Count == 0) || !(networkExceptions.Count == 0))
            {
                throw new EncapsulatedExceptions(untrustedIdentities, unregisteredUsers, networkExceptions);
            }

            return response;
        }

        private async Task<SendMessageResponse> sendMessage(SignalServiceAddress recipient, ulong timestamp, byte[] content, bool legacy)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    OutgoingPushMessageList messages = await getEncryptedMessages(socket, recipient, timestamp, content, legacy);
                    return await socket.sendMessage(messages);
                }
                catch (MismatchedDevicesException mde)
                {
                    Debug.WriteLine(mde.Message, TAG);
                    handleMismatchedDevices(socket, recipient, mde.getMismatchedDevices());
                }
                catch (StaleDevicesException ste)
                {
                    //Log.w(TAG, ste);
                    handleStaleDevices(recipient, ste.getStaleDevices());
                }
            }

            throw new Exception("Failed to resolve conflicts after 3 attempts!");
        }

        private async Task<IList<AttachmentPointer>> createAttachmentPointers(May<LinkedList<SignalServiceAttachment>> attachments)
        {
            IList<AttachmentPointer> pointers = new List<AttachmentPointer>();

            if (!attachments.HasValue || attachments.ForceGetValue().Count == 0)
            {
                Debug.WriteLine("No attachments present...", TAG);
                return pointers;
            }

            foreach (SignalServiceAttachment attachment in attachments.ForceGetValue())
            {
                if (attachment.isStream())
                {
                    Debug.WriteLine("Found attachment, creating pointer...", TAG);
                    pointers.Add(await createAttachmentPointer(attachment.asStream()));
                }
            }

            return pointers;
        }

        private async Task<AttachmentPointer> createAttachmentPointer(SignalServiceAttachmentStream attachment)
        {
            byte[] attachmentKey = Util.getSecretBytes(64);
            PushAttachmentData attachmentData = new PushAttachmentData(attachment.getContentType(),
                                                                       attachment.getInputStream(),
                                                                       (ulong)attachment.getLength(),
                                                                       attachmentKey);

            ulong attachmentId = await socket.sendAttachment(attachmentData);

            var builder = AttachmentPointer.CreateBuilder()
                                    .SetContentType(attachment.getContentType())
                                    .SetId(attachmentId)
                                    .SetKey(ByteString.CopyFrom(attachmentKey))
                                    .SetSize((uint)attachment.getLength());

            if (attachment.getPreview().HasValue)
            {
                builder.SetThumbnail(ByteString.CopyFrom(attachment.getPreview().ForceGetValue()));
            }

            return builder.Build();
        }


        private async Task<OutgoingPushMessageList> getEncryptedMessages(PushServiceSocket socket,
                                                   SignalServiceAddress recipient,
                                                   ulong timestamp,
                                                   byte[] plaintext,
                                                   bool legacy)
        {
            List<OutgoingPushMessage> messages = new List<OutgoingPushMessage>();

            if (!recipient.Equals(localAddress))
            {
                messages.Add(await getEncryptedMessage(socket, recipient, SignalServiceAddress.DEFAULT_DEVICE_ID, plaintext, legacy));
            }

            foreach (uint deviceId in store.GetSubDeviceSessions(recipient.getNumber()))
            {
                messages.Add(await getEncryptedMessage(socket, recipient, deviceId, plaintext, legacy));
            }

            return new OutgoingPushMessageList(recipient.getNumber(), timestamp, recipient.getRelay().HasValue ? recipient.getRelay().ForceGetValue() : null, messages);
        }

        private async Task<OutgoingPushMessage> getEncryptedMessage(PushServiceSocket socket, SignalServiceAddress recipient, uint deviceId, byte[] plaintext, bool legacy)
        {
            SignalProtocolAddress signalProtocolAddress = new SignalProtocolAddress(recipient.getNumber(), deviceId);
            SignalServiceCipher cipher = new SignalServiceCipher(localAddress, store);

            if (!store.ContainsSession(signalProtocolAddress))
            {
                try
                {
                    List<PreKeyBundle> preKeys = await socket.getPreKeys(recipient, deviceId);

                    foreach (PreKeyBundle preKey in preKeys)
                    {
                        try
                        {
                            SignalProtocolAddress preKeyAddress = new SignalProtocolAddress(recipient.getNumber(), preKey.getDeviceId());
                            SessionBuilder sessionBuilder = new SessionBuilder(store, preKeyAddress);
                            sessionBuilder.process(preKey);
                        }
                        catch (libsignal.exceptions.UntrustedIdentityException e)
                        {
                            throw new UntrustedIdentityException("Untrusted identity key!", recipient.getNumber(), preKey.getIdentityKey());
                        }
                    }

                    if (eventListener.HasValue)
                    {
                        eventListener.ForceGetValue().onSecurityEvent(recipient);
                    }
                }
                catch (InvalidKeyException e)
                {
                    throw new Exception(e.Message);
                }
            }

            return cipher.encrypt(signalProtocolAddress, plaintext, legacy);
        }

        private async Task handleMismatchedDevices(PushServiceSocket socket, SignalServiceAddress recipient,
                                           MismatchedDevices mismatchedDevices)
        {
            try
            {
                foreach (uint extraDeviceId in mismatchedDevices.getExtraDevices())
                {
                    store.DeleteSession(new SignalProtocolAddress(recipient.getNumber(), extraDeviceId));
                }

                foreach (uint missingDeviceId in mismatchedDevices.getMissingDevices())
                {
                    PreKeyBundle preKey = await socket.getPreKey(recipient, missingDeviceId);

                    try
                    {
                        SessionBuilder sessionBuilder = new SessionBuilder(store, new SignalProtocolAddress(recipient.getNumber(), missingDeviceId));
                        sessionBuilder.process(preKey);
                    }
                    catch (libsignal.exceptions.UntrustedIdentityException e)
                    {
                        throw new UntrustedIdentityException("Untrusted identity key!", recipient.getNumber(), preKey.getIdentityKey());
                    }
                }
            }
            catch (InvalidKeyException e)
            {
                throw new Exception(e.Message);
            }
        }

        private void handleStaleDevices(SignalServiceAddress recipient, StaleDevices staleDevices)
        {
            foreach (uint staleDeviceId in staleDevices.getStaleDevices())
            {
                store.DeleteSession(new SignalProtocolAddress(recipient.getNumber(), staleDeviceId));
            }
        }

        public interface EventListener
        {
            void onSecurityEvent(SignalServiceAddress address);
        }

    }
}