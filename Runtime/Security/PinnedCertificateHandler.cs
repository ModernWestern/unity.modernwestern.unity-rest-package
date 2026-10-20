using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UnityEngine.Networking;

namespace UnityREST.Security
{
    /// <summary>
    /// Public key pinning (the HPKP/OkHttp "sha256/..." format). Accepts the server only when the
    /// SHA-256 of its certificate's SubjectPublicKeyInfo matches a pin and the certificate is within
    /// its validity period. Pins survive certificate renewals that keep the same key.
    /// </summary>
    /// <remarks>
    /// Generate a pin with:
    /// <c>openssl s_client -connect host:443 | openssl x509 -pubkey -noout | openssl pkey -pubin -outform der | openssl dgst -sha256 -binary | base64</c>
    /// </remarks>
    public sealed class PinnedCertificateHandler : CertificateHandler
    {
        private readonly HashSet<string> _pins = new HashSet<string>(StringComparer.Ordinal);

        public PinnedCertificateHandler(IEnumerable<string> pins)
        {
            foreach (var pin in pins)
            {
                var value = pin?.Trim();

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                _pins.Add(value.StartsWith("sha256/", StringComparison.OrdinalIgnoreCase) ? value.Substring(7) : value);
            }
        }

        public int PinCount => _pins.Count;

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            try
            {
                using (var certificate = new X509Certificate2(certificateData))
                {
                    var now = DateTime.Now;

                    if (now < certificate.NotBefore || now > certificate.NotAfter)
                    {
                        return false;
                    }

                    return _pins.Contains(Convert.ToBase64String(ComputeSpkiSha256(certificate)));
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        /// <summary>SHA-256 of the DER-encoded SubjectPublicKeyInfo, rebuilt from the certificate fields.</summary>
        public static byte[] ComputeSpkiSha256(X509Certificate2 certificate)
        {
            var key = certificate.PublicKey;
            var parameters = key.EncodedParameters?.RawData;

            // RSA keys carry an explicit NULL parameter.
            if (parameters == null || parameters.Length == 0)
            {
                parameters = new byte[] { 0x05, 0x00 };
            }

            var algorithm = Der(0x30, Concat(EncodeOid(key.Oid.Value), parameters));
            var bitString = Der(0x03, Concat(new byte[] { 0x00 }, key.EncodedKeyValue.RawData));
            var spki = Der(0x30, Concat(algorithm, bitString));

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(spki);
            }
        }

        private static byte[] EncodeOid(string dotted)
        {
            var parts = dotted.Split('.');
            var body = new List<byte> { (byte)(int.Parse(parts[0]) * 40 + int.Parse(parts[1])) };

            for (var i = 2; i < parts.Length; i++)
            {
                var value = long.Parse(parts[i]);
                var start = body.Count;

                body.Insert(start, (byte)(value & 0x7F));

                while ((value >>= 7) > 0)
                {
                    body.Insert(start, (byte)(0x80 | (value & 0x7F)));
                }
            }

            return Der(0x06, body.ToArray());
        }

        private static byte[] Der(byte tag, byte[] content)
        {
            var length = content.Length;
            byte[] header;

            if (length < 0x80)
            {
                header = new[] { tag, (byte)length };
            }
            else if (length <= 0xFF)
            {
                header = new byte[] { tag, 0x81, (byte)length };
            }
            else if (length <= 0xFFFF)
            {
                header = new byte[] { tag, 0x82, (byte)(length >> 8), (byte)length };
            }
            else
            {
                header = new byte[] { tag, 0x83, (byte)(length >> 16), (byte)(length >> 8), (byte)length };
            }

            return Concat(header, content);
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
