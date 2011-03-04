using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Text;

namespace TestProject {
    class RSAKeyReader {
        MemoryStream ms;
        List<byte[]> values;
        bool hasPrivate;

        public RSAKeyReader(byte[] DERData) {
            init(DERData);
        }
        public RSAKeyReader(string PEMKey) {
            string[] lines = PEMKey.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string b64der = string.Join("\n", lines, 1, lines.Length - 2);
            init(Convert.FromBase64String(b64der));
        }
        void init(byte[] input) {
            Provider = new RSACryptoServiceProvider();
            ms = new MemoryStream(input);
            values = new List<byte[]>();

            readBytes();
            byte[] seq = values[0];
            values.Clear();

            ms = new MemoryStream(seq);
            readBytes();

            if(values[0].Length == 1 && values[0][0] == 0x00)
                values.RemoveAt(0);

            hasPrivate = values.Count > 2;
            Provider.FromXmlString(ToXmlString());
        }
        void readBytes() {
            while(ms.Position < ms.Length) {
                byte tag = (byte)ms.ReadByte();
                byte first = (byte)ms.ReadByte();
                int size = 0, len = 0;

                if(first < 127) {
                    size = first;
                } else if(first > 127) {
                    len = first - 0x80;

                    for(int i = 0; i < len; i++) {
                        byte cur = (byte)ms.ReadByte();
                        size += cur << ((len - i - 1) * 8);
                    }
                } else {
                    throw new Exception("Invalid ASN.1 content length");
                }

                byte[] val = new byte[size];
                ms.Read(val, 0, size);

                if(val[0] == 0x00 && val.Length > 1) {
                    byte[] tmp = new byte[val.Length - 1];
                    Array.Copy(val, 1, tmp, 0, tmp.Length);
                    val = tmp;
                }

                values.Add(val);
            }
        }

        string ToXmlString() {
            return new XElement("RSAKeyValue",
                new XElement("Modulus", Convert.ToBase64String(values[0])),
                new XElement("Exponent", Convert.ToBase64String(values[1])),
                hasPrivate ? new XElement("P", Convert.ToBase64String(values[3])) : null,
                hasPrivate ? new XElement("Q", Convert.ToBase64String(values[4])) : null,
                hasPrivate ? new XElement("DP", Convert.ToBase64String(values[5])) : null,
                hasPrivate ? new XElement("DQ", Convert.ToBase64String(values[6])) : null,
                hasPrivate ? new XElement("InverseQ", Convert.ToBase64String(values[7])) : null,
                hasPrivate ? new XElement("D", Convert.ToBase64String(values[2])) : null
            ).ToString();
        }

        public static string ToPEM(RSAParameters Parameters) {
            StringBuilder sb = new StringBuilder();
            MemoryStream ms = new MemoryStream(),
                         all = new MemoryStream();
            bool hasPrivate = Parameters.D != null;
            string type = hasPrivate ? "PRIVATE" : "PUBLIC";
            byte[] noise = new byte[] { 0x00 };

            sb.AppendFormat("-----BEGIN RSA {0} KEY-----\r\n", type);
            writeBytes(ms, 0x02, noise);
            writeBytes(ms, 0x02, Parameters.Modulus);
            writeBytes(ms, 0x02, Parameters.Exponent);
            if(hasPrivate) {
                writeBytes(ms, 0x02, Parameters.D);
                writeBytes(ms, 0x02, Parameters.P);
                writeBytes(ms, 0x02, Parameters.Q);
                writeBytes(ms, 0x02, Parameters.DP);
                writeBytes(ms, 0x02, Parameters.DQ);
                writeBytes(ms, 0x02, Parameters.InverseQ);
            }

            writeBytes(all, 0x30, ms.ToArray());

            sb.AppendLine(Convert.ToBase64String(all.ToArray(), Base64FormattingOptions.InsertLineBreaks));
            sb.AppendFormat("-----END RSA {0} KEY-----", type);

            return sb.ToString();
        }
        static void writeBytes(MemoryStream ms, byte tag, byte[] input) {
            ms.WriteByte(tag);

            if((input[0] & 0x80) == 0x80) {
                byte[] tmp = new byte[input.Length + 1];
                Array.Copy(input, 0, tmp, 1, input.Length);
                input = tmp;
            }

            if(input.Length < 127) {
                ms.WriteByte((byte)input.Length);
            } else {
                int size = input.Length;
                byte cur;
                List<byte> len = new List<byte>();

                do {
                    cur = (byte)(size % 256);
                    len.Add(cur);
                    size = (size - cur) / 256;
                } while(size > 0);
                len.Reverse();

                byte first = (byte)(0x80 + (byte)len.Count);
                ms.WriteByte(first);
                ms.Write(len.ToArray(), 0, len.Count);
            }

            ms.Write(input, 0, input.Length);
        }

        public RSACryptoServiceProvider Provider {
            get;
            private set;
        }
    }
}
