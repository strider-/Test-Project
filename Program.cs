using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Data.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.IO.Ports;
using System.Dynamic;
using System.Net;
using TestProject.JsonService;

namespace TestProject {    
    class Program {        
        [STAThread]
        static void Main(string[] args) {           
            TestService ts = new TestService();
            ts.Authorize = false;
            ts.Start();
            while(Console.ReadKey(true).Key != ConsoleKey.Escape)
                ;
            ts.Stop();

            //RSACryptoServiceProvider p = new RSACryptoServiceProvider(2048);
            //string pem = RSAKeyReader.ToPEM(p.ExportParameters(false));
            //System.IO.File.WriteAllText(@"h:\downloads\rsa.pem", pem);

            //byte[] toSign = System.IO.File.ReadAllBytes(@"h:\downloads\Red Robin 020 (2011) (c2c) (The Last Kryptonian-DCP).cbr");
            //byte[] signed = p.SignData(toSign, new SHA1CryptoServiceProvider());

            //System.IO.File.WriteAllBytes(@"h:\downloads\sig.bin", signed);
            
            //bool verified = p.VerifyData(toSign, new SHA1CryptoServiceProvider(), signed);
        
        }
    }
    
    public class TestService : TestProject.JsonService.JsonService {
        [Get("add?value1={num1}&value2={num2}",
             Description="returns the sum of 2 numbers.",
             Example="add?value1=2&value2=2")]
        public object Sum(int num1, int num2) {
            return new {
                sum = num1 + num2
            };
        }

        [Get("greet?name={name}",
             Description = "gives you a shout out.",
             Example = "greet?name=you%20silly%20goose")]
        public object ReportName(string name) {
            return new {
                message = "Hello, " + name
            };
        }
        
        public object CantBeCalled(string sup) {
            return new {
                UhOh = "This won't work."
            };
        }
    }    

    class DynamicDictionary<TValue> : DynamicObject {
        private Dictionary<string, TValue> dict;

        public DynamicDictionary() {
            dict = new Dictionary<string, TValue>();            
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            string key = binder.Name;
            if(dict.ContainsKey(key)) {
                result = dict[key];
                return true;
            } else {
                result = default(TValue);
                return false;
            }
        }
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            string key = binder.Name;
            dict[key] = (TValue)value;
            return true;
        }
    }

    class Hasher {
        public string MD5(string value, byte[] salt) {
            if(salt == null) {
                Random rnd = new Random();
                salt = new byte[rnd.Next(4, 8)];
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
                rng.GetNonZeroBytes(salt);
            }

            byte[] valueBytes = Encoding.UTF8.GetBytes(value),
                   valueWithSaltBytes = new byte[valueBytes.Length + salt.Length],
                   hashBytes, hashWithSaltBytes;
            
            Array.Copy(valueBytes, valueWithSaltBytes, valueBytes.Length);
            Array.Copy(salt, 0, valueWithSaltBytes, valueBytes.Length, salt.Length);

            HashAlgorithm h = new MD5CryptoServiceProvider();

            hashBytes = h.ComputeHash(valueWithSaltBytes);
            hashWithSaltBytes = new byte[hashBytes.Length + salt.Length];

            Array.Copy(hashBytes, hashWithSaltBytes, hashBytes.Length);
            Array.Copy(salt, 0, hashWithSaltBytes, hashBytes.Length, salt.Length);

            return Convert.ToBase64String(hashWithSaltBytes);
        }
        public bool Verify(string value, string hash) {
            int hashSizeInBytes = 16;
            byte[] hashWithSaltBytes = Convert.FromBase64String(hash),
                   saltBytes = new byte[hashWithSaltBytes.Length - hashSizeInBytes];

            if(hashWithSaltBytes.Length < hashSizeInBytes)
                return false;

            Array.Copy(hashWithSaltBytes, hashSizeInBytes, saltBytes, 0, saltBytes.Length);

            string expected = MD5(value, saltBytes);

            return expected == hash;
        }
    }

    static class Extentions {
        public static T Get<T>(this Hashtable table, string Key) {
            if(!table.ContainsKey(Key))
                return default(T);
            return (T)Convert.ChangeType(table[Key], typeof(T));
        }
    }
    class SQLHasher : DataContext {
        public SQLHasher() : 
            base("server=.;database='iidx';trusted_connection=true;") {
        }

        public Hash GetHash(string input) {
            try {
                string sql = string.Format("SELECT HASHBYTES('md5', '{0}') [MD5], HASHBYTES('sha1', '{0}') [SHA1]", input);
                return ExecuteQuery<Hash>(sql).First();
            } catch {
                return null;
            }
        }
    }
    class Hash {
        public byte[] MD5 {
            get;
            set;
        }
        public byte[] SHA1 {
            get;
            set;
        }
    }
}
