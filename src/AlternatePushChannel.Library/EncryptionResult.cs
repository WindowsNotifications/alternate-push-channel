using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlternatePushChannel.Library
{
    /// <summary>
    /// https://github.com/LogicSoftware/WebPushEncryption/blob/master/src/EncryptionResult.cs
    /// </summary>
    internal class EncryptionResult
    {
        public byte[] PublicKey { get; set; }
        public byte[] Payload { get; set; }
        public byte[] Salt { get; set; }
    }
}
