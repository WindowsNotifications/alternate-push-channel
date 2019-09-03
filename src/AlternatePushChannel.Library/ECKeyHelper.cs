using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlternatePushChannel.Library
{
    /// <summary>
    /// From https://github.com/web-push-libs/web-push-csharp/blob/master/WebPush/Util/ECKeyHelper.cs
    /// </summary>
    internal static class ECKeyHelper
    {
        public static ECPublicKeyParameters GetPublicKey(byte[] publicKey)
        {
            Asn1Object keyTypeParameters = new DerSequence(new DerObjectIdentifier(@"1.2.840.10045.2.1"),
                new DerObjectIdentifier(@"1.2.840.10045.3.1.7"));
            Asn1Object derEncodedKey = new DerBitString(publicKey);

            Asn1Object derSequence = new DerSequence(keyTypeParameters, derEncodedKey);

            var base64EncodedDerSequence = Convert.ToBase64String(derSequence.GetDerEncoded());
            var pemKey = "-----BEGIN PUBLIC KEY-----\n";
            pemKey += base64EncodedDerSequence;
            pemKey += "\n-----END PUBLIC KEY-----";

            var reader = new StringReader(pemKey);
            var pemReader = new PemReader(reader);
            var keyPair = pemReader.ReadObject();
            return (ECPublicKeyParameters)keyPair;
        }
    }
}
