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

using libsignal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;
using Windows.Security.Cryptography.Core;
using libtextsecure.util;
using System.Runtime.InteropServices.WindowsRuntime;

namespace libtextsecure.src.api.crypto
{
    /// <summary>
    /// Class for streaming an encrypted push attachment off disk.
    /// </summary>
    public class AttachmentCipherInputStream
    {
        private static readonly int BLOCK_SIZE = 16;
        private static readonly int CIPHER_KEY_SIZE = 32;
        private static readonly int MAC_KEY_SIZE = 32;

        private SymmetricKeyAlgorithmProvider cipher;
        private bool done;
        private long totalDataSize;
        private long totalRead;
        private byte[] overflowBuffer;

        public AttachmentCipherInputStream(StorageFile file, byte[] combinedKeyMaterial)
        {
            byte[][] parts = Util.split(combinedKeyMaterial, CIPHER_KEY_SIZE, MAC_KEY_SIZE);
            MacAlgorithmProvider mac = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
            IBuffer keyMaterial = parts[1].AsBuffer();
            CryptographicKey key = mac.CreateKey(keyMaterial);
        }
    }
}
