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

using Strilanc.Value;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace libtextsecure.messages.multidevice
{
    public class TextSecureSyncMessage
    {

        private readonly May<SentTranscriptMessage> sent;
        private readonly May<TextSecureAttachment> contacts;
        private readonly May<TextSecureAttachment> groups;
        private readonly May<RequestMessage> request;
        private readonly May<List<ReadMessage>> reads;

        private TextSecureSyncMessage(May<SentTranscriptMessage> sent,
                                      May<TextSecureAttachment> contacts,
                                      May<TextSecureAttachment> groups,
                                      May<RequestMessage> request,
                                      May<List<ReadMessage>> reads)
        {
            this.sent = sent;
            this.contacts = contacts;
            this.groups = groups;
            this.request = request;
            this.reads = reads;
        }

        public static TextSecureSyncMessage forSentTranscript(SentTranscriptMessage sent)
        {
            return new TextSecureSyncMessage(new May<SentTranscriptMessage>(sent),
                May<TextSecureAttachment>.NoValue,
                May<TextSecureAttachment>.NoValue,
                May<RequestMessage>.NoValue,
                May<List<ReadMessage>>.NoValue);
        }

        public static TextSecureSyncMessage forContacts(TextSecureAttachment contacts)
        {
            return new TextSecureSyncMessage(May<SentTranscriptMessage>.NoValue,
                                             new May<TextSecureAttachment>(contacts),
                                             May<TextSecureAttachment>.NoValue,
                                             May<RequestMessage>.NoValue,
                                             May<List<ReadMessage>>.NoValue);
        }

        public static TextSecureSyncMessage forGroups(TextSecureAttachment groups)
        {
            return new TextSecureSyncMessage(May<SentTranscriptMessage>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             new May<TextSecureAttachment>(groups),
                                             May<RequestMessage>.NoValue,
                                             May<List<ReadMessage>>.NoValue);
        }

        public static TextSecureSyncMessage forRequest(RequestMessage request)
        {
            return new TextSecureSyncMessage(May<SentTranscriptMessage>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             new May<RequestMessage>(request),
                                             May<List<ReadMessage>>.NoValue);
        }

        public static TextSecureSyncMessage forRead(List<ReadMessage> reads)
        {
            return new TextSecureSyncMessage(May<SentTranscriptMessage>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<RequestMessage>.NoValue,
                                             new May<List<ReadMessage>>(reads));
        }

        public static TextSecureSyncMessage forRead(ReadMessage read)
        {
            List<ReadMessage> reads = new List<ReadMessage>();
            reads.Add(read);

            return new TextSecureSyncMessage(May<SentTranscriptMessage>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<RequestMessage>.NoValue,
                                             new May<List<ReadMessage>>(reads));
        }

        public static TextSecureSyncMessage empty()
        {
            return new TextSecureSyncMessage(May<SentTranscriptMessage>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<TextSecureAttachment>.NoValue,
                                             May<RequestMessage>.NoValue,
                                             May<List<ReadMessage>>.NoValue);
        }

        public May<SentTranscriptMessage> getSent()
        {
            return sent;
        }

        public May<TextSecureAttachment> getGroups()
        {
            return groups;
        }

        public May<TextSecureAttachment> getContacts()
        {
            return contacts;
        }

        public May<RequestMessage> getRequest()
        {
            return request;
        }


        public May<List<ReadMessage>> getRead()
        {
            return reads;
        }
    }
}
