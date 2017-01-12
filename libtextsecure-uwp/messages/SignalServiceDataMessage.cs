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
using libsignal.util;
using Strilanc.Value;

namespace libtextsecure.messages
{
    /// <summary>
    /// Represents a decrypted Signal Service data message.
    /// </summary>
    public class SignalServiceDataMessage
    {
        private readonly long timestamp;
        private readonly May<List<SignalServiceAttachment>> attachments;
        private readonly May<string> body;
        private readonly May<SignalServiceGroup> group;
        private readonly bool endSession;

        /// <summary>
        /// Construct a SignalServiceDataMessage with a body and no attachments.
        /// </summary>
        /// <param name="timestamp">The sent timestamp</param>
        /// <param name="body">The message contents.</param>
        public SignalServiceDataMessage(long timestamp, string body)
            : this(timestamp, (List<SignalServiceAttachment>)null, body)
        {
        }

        public SignalServiceDataMessage(long timestamp, SignalServiceAttachment attachment, string body)
            : this(timestamp, new List<SignalServiceAttachment>(new[] { attachment }), body)
        {
        }

        /// <summary>
        /// Construct a SignalServiceDataMessage with a body and list of attachments.
        /// </summary>
        /// <param name="timestamp">The sent timestamp.</param>
        /// <param name="attachments">The attachments.</param>
        /// <param name="body">The message contents.</param>
        public SignalServiceDataMessage(long timestamp, List<SignalServiceAttachment> attachments, string body)
            : this(timestamp, null, attachments, body)
        {
        }

        /// <summary>
        /// Construct a SignalServiceDataMessage group message with attachments and body.
        /// </summary>
        /// <param name="timestamp">The sent timestamp.</param>
        /// <param name="group">The group information.</param>
        /// <param name="attachments">The attachments.</param>
        /// <param name="body">The message contents.</param>
        public SignalServiceDataMessage(long timestamp, SignalServiceGroup group, List<SignalServiceAttachment> attachments, string body)
            : this(timestamp, group, attachments, body, false)
        {
        }

        /// <summary>
        /// Construct a SignalServiceDataMessage
        /// </summary>
        /// <param name="timestamp">The sent timestamp.</param>
        /// <param name="group">The group information.</param>
        /// <param name="attachments">The attachments.</param>
        /// <param name="body">The message contents.</param>
        /// <param name="endSession">Flag indicating whether this message should close a session.</param>
        public SignalServiceDataMessage(long timestamp, SignalServiceGroup group, List<SignalServiceAttachment> attachments, string body, bool endSession)
        {
            this.timestamp = timestamp;
            this.body = new May<string>(body);
            this.group = group == null ? May<SignalServiceGroup>.NoValue : new May<SignalServiceGroup>(group);
            this.endSession = endSession;

            if (attachments != null && !(attachments.Count == 0))
            {
                this.attachments = new May<List<SignalServiceAttachment>>(attachments);
            }
            else
            {
                this.attachments = May<List<SignalServiceAttachment>>.NoValue;
            }
        }

        public static SignalServiceDataMessageBuilder newBuilder()
        {
            return new SignalServiceDataMessageBuilder();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The message timestamp.</returns>
        public long getTimestamp()
        {
            return timestamp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The message attachments (if any).</returns>
        public May<List<SignalServiceAttachment>> getAttachments()
        {
            return attachments;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The message body (if any).</returns>
        public May<string> getBody()
        {
            return body;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The message group info (if any).</returns>
        public May<SignalServiceGroup> getGroupInfo()
        {
            return group;
        }

        public bool isEndSession()
        {
            return endSession;
        }

        public bool isGroupUpdate()
        {
            return group.HasValue && group.ForceGetValue().getType() != SignalServiceGroup.Type.DELIVER;
        }

    }

    public class SignalServiceDataMessageBuilder
    {
        private List<SignalServiceAttachment> attachments = new List<SignalServiceAttachment>();
        private long timestamp;
        private SignalServiceGroup group;
        private string body;
        private bool endSession;

        public SignalServiceDataMessageBuilder() { }

        public SignalServiceDataMessageBuilder withTimestamp(long timestamp)
        {
            this.timestamp = timestamp;
            return this;
        }

        public SignalServiceDataMessageBuilder asGroupMessage(SignalServiceGroup group)
        {
            this.group = group;
            return this;
        }

        public SignalServiceDataMessageBuilder withAttachment(SignalServiceAttachment attachment)
        {
            attachments.Add(attachment);
            return this;
        }

        public SignalServiceDataMessageBuilder withAttachments(List<SignalServiceAttachment> attachments)
        {
            foreach (SignalServiceAttachment attachment in attachments)
            {
                this.attachments.Add(attachment);
            }

            return this;
        }

        public SignalServiceDataMessageBuilder withBody(string body)
        {
            this.body = body;
            return this;
        }

        public SignalServiceDataMessageBuilder asEndSessionMessage()
        {
            this.endSession = true;
            return this;
        }

        public SignalServiceDataMessageBuilder asEndSessionMessage(bool endSession)
        {
            this.endSession = endSession;
            return this;
        }

        public SignalServiceDataMessage build()
        {
            if (timestamp == 0) timestamp = (long)KeyHelper.getTime();
            return new SignalServiceDataMessage(timestamp, group, attachments, body, endSession);
        }
    }
}
