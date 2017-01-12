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
using Google.ProtocolBuffers;
using libsignalservice.push;
using Windows.Storage.Streams;

namespace libsignalservice.messages.multidevice
{
    public class DeviceGroupsOutputStream : ChunkedOutputStream
    {

        public DeviceGroupsOutputStream(IOutputStream output)
            : base(output)
        {
        }

        public void write(DeviceGroup group)
        {
            writeGroupDetails(group);
            writeAvatarImage(group);
        }

        public void close()
        {
            //output.close();
        }

        private void writeAvatarImage(DeviceGroup contact)
        {
            if (contact.getAvatar().HasValue)
            {
                contact.getAvatar().Match(e => e, () => { throw new Exception(); }).getInputStream();
            }
        }

        private void writeGroupDetails(DeviceGroup group)// throws IOException
        {
            SignalServiceProtos.GroupDetails.Builder groupDetails = SignalServiceProtos.GroupDetails.CreateBuilder();
            groupDetails.SetId(ByteString.CopyFrom(group.getId()));

            if (group.getName().HasValue)
            {
                groupDetails.SetName(group.getName().Match(e => e, () => { throw new Exception(); }));
            }

            if (group.getAvatar().HasValue)
            {
                SignalServiceProtos.GroupDetails.Types.Avatar.Builder avatarBuilder = SignalServiceProtos.GroupDetails.Types.Avatar.CreateBuilder();
                SignalServiceAttachmentStream avatar = group.getAvatar().Match(e => e, () => { throw new Exception(); });
                avatarBuilder.SetContentType(avatar.getContentType());
                avatarBuilder.SetLength((uint)avatar.getLength());
                groupDetails.SetAvatar(avatarBuilder);
            }

            groupDetails.AddRangeMembers(group.getMembers());
            groupDetails.SetActive(group.isActive());

            byte[] serializedContactDetails = groupDetails.Build().ToByteArray();

            writeVarint32(serializedContactDetails.Length);
            output.Write(serializedContactDetails, 0, serializedContactDetails.Length);
        }
    }
}
