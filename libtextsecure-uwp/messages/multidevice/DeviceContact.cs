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
using Strilanc.Value;

namespace libtextsecure.messages.multidevice
{
    public class DeviceContact
    {

        private readonly String number;
        private readonly May<String> name;
        private readonly May<SignalServiceAttachmentStream> avatar;

        public DeviceContact(String number, May<String> name, May<SignalServiceAttachmentStream> avatar)
        {
            this.number = number;
            this.name = name;
            this.avatar = avatar;
        }

        public May<SignalServiceAttachmentStream> getAvatar()
        {
            return avatar;
        }

        public May<String> getName()
        {
            return name;
        }

        public String getNumber()
        {
            return number;
        }

    }
}
