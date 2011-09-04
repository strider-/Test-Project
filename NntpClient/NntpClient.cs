using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net.Security;
using System.Text.RegularExpressions;

namespace TestProject.NntpClient {
    public class NntpClient : IDisposable {
        byte[] buffer;
        TcpClient client;
        StreamReader sr;
        StreamWriter sw;
        Encoding enc;

        public NntpClient() {
            client = new TcpClient();
            enc = Encoding.GetEncoding(1252);
        }

        public void Connect(string hostname, int port, bool ssl) {            
            client.Connect(hostname, port);
            Stream stream = client.GetStream();
            buffer = new byte[0x8000];
            
            if(ssl) {
                SslStream sslStream = new SslStream(client.GetStream(), true);
                sslStream.AuthenticateAsClient(hostname);
                stream = sslStream;
            }

            sr = new StreamReader(stream, enc);
            sw = new StreamWriter(stream, enc);
            sw.AutoFlush = true;

            ReadLine();
        }
        public void Close() {
            WriteLine("QUIT");

            sr.Close();
            sr = null;
            sw.Close();
            sw = null;
        }
        public void Dispose() {
            Close();
        }
        private string ReadLine() {
            string line = sr.ReadLine();
            return line;
        }
        private string WriteLine(string line, params object[] args) {
            sw.WriteLine(line, args);
            return ReadLine();
        }
        private Dictionary<string, string> ReadHeader() {
            var dict = new Dictionary<string, string>();
            string header = null;

            while((header = ReadLine()) != string.Empty && header != ".") {                
                string key = header.Substring(0, header.IndexOf(':'));
                string value = header.Substring(header.IndexOf(':') + 1);
                dict[key] = value.Trim();
            }

            if(header == string.Empty)
                ReadLine();

            return dict;
        }

        public bool Authenticate(string user, string pass) {
            string result;

            result = WriteLine("AUTHINFO USER {0}", user);
            result = WriteLine("AUTHINFO PASS {0}", pass);

            if(result.StartsWith("2")) {
                WriteLine("MODE READER");
                return true;
            }

            return false;
        }
        public IEnumerable<NntpGroup> GetGroups() {
            var groups = new List<NntpGroup>();
            string group;

            WriteLine("LIST");

            while((group = ReadLine()) != ".") {
                groups.Add(NntpGroup.Parse(group));
            }

            return groups.OrderBy(g => g.Name);
        }
        public bool SetGroup(string groupName) {
            string result = WriteLine("GROUP {0}", groupName);

            if(result.StartsWith("211")) {
                CurrentGroup = groupName;
                return true;
            }

            return false;
        }
        public bool SetGroup(NntpGroup group) {
            return SetGroup(group.Name);
        }

        public Dictionary<string, string> GetHeaders(string articleId) {
            var dict = new Dictionary<string, string>();
            string result = WriteLine("HEAD <{0}>", articleId.Trim('<', '>'));

            if(result.StartsWith("4")) {
                throw new Exception("Article not found.");
            }

            return ReadHeader();
        }
        public NntpArticle GetArticle(string articleId) {
            string result = WriteLine("ARTICLE <{0}>", articleId.Trim('<', '>')),
                   line = null;

            var dict = ReadHeader();

            string yEncHeader = ReadLine();
            string file = null;
            int part=0, exSize=0;

            var mc = Regex.Matches(yEncHeader, @"(?<Key>[^\s=]+)=(?<Value>[^\s]*)", RegexOptions.Singleline);
            foreach(Match m in mc) {
                string val = m.Groups["Value"].Value;
                switch(m.Groups["Key"].Value) {                    
                    case "name":
                        file = val;
                        break;
                    case "part":
                        part = int.Parse(val);
                        break;
                    case "size":
                        exSize = int.Parse(val);
                        break;
                }
            }

            string yEncBegin = ReadLine();
            MemoryStream ms = new MemoryStream();
            while((line = ReadLine()) != ".") {
                YEncDecode(line, ms);
            }
            
            ms.Position = 0;
            return new NntpArticle { Headers = dict, Body = ms, Filename = file, Part = part, ExpectedSize = exSize };
        }

        private void YEncDecode(string line, Stream destination) {
            if(line.StartsWith("=yend")) {
                return;
            }

            byte[] raw = enc.GetBytes(line);
            byte[] decoded = new byte[line.Length];
            int length = 0;

            for(int i = raw[0] == 0x2e && raw[1] == 0x2e ? 1 : 0; i < raw.Length; i++) {
                if(raw[i] == '=') {
                    i++;
                    decoded[length++] = (byte)((raw[i] - 0x40) - 0x2a);
                } else {
                    decoded[length++] = (byte)(raw[i] - 0x2a);
                }
            }
            
            destination.Write(decoded, 0, length);
        }

        public string CurrentGroup { get; private set; }
    }
}
