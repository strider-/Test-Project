using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Data.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Dynamic;
using System.Xml.Linq;
using Ninject.Modules;
using System.Data.SqlClient;
using System.Reflection;
using System.Xml;
using System.Net.Sockets;
using System.Net.Security;
using System.Configuration;

namespace TestProject {
    class Program {
        [STAThread]
        static void Main(string[] args) {
            var settings = ConfigurationManager.AppSettings;
            string host = settings["NntpHost"];
            int port = int.Parse(settings["NntpPort"]);

            using(var nntp = new NntpClient.NntpClient()) {
                nntp.Connect(host, port, true);
                nntp.Authenticate(settings["NntpUser"], settings["NntpPass"]);
               
                var groups = nntp.GetGroups();

                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }
    }

    public class NZB {
        XDocument nzb;
        XNamespace ns;

        public NZB(string raw) {
            var settings = new XmlReaderSettings {
                ValidationType = ValidationType.DTD,
                DtdProcessing = DtdProcessing.Parse
            };

            XmlReader r = XmlReader.Create(new StringReader(raw), settings);
            nzb = XDocument.Load(r);
            ns = nzb.Root.GetDefaultNamespace();
        }
    }

    public class RuleEvaluator<T> {
        public RuleEvaluator() {
            Rules = new Dictionary<string, Rule<T>>();
        }

        public void AddRule(Rule<T> rule) {
            Rules[rule.RuleName] = rule;
        }

        public IEnumerable<EvaluationResult> Evaluate(T obj) {
            List<EvaluationResult> list = new List<EvaluationResult>();

            Rules.ForEach(r => {
                var er = r.Value.Evaluate(obj);
                list.Add(new EvaluationResult { RuleName = r.Key, Result = er.Item1, Message = er.Item2 });
            });

            return list;
        }

        public Dictionary<string, Rule<T>> Rules { get; set; }
    }
    public class EvaluationResult {
        public string RuleName { get; set; }
        public bool Result { get; set; }
        public string Message { get; set; }
    }
    public class Rule<T> {
        List<Func<T, bool>> conditions;
        string name, failure, success;

        public Rule() {
            conditions = new List<Func<T, bool>>();
            name = null;
            failure = string.Empty;
            success = string.Empty;
        }

        public Rule<T> Name(string name) {
            this.name = name;
            return this;
        }

        public Rule<T> FailureMessage(string msg) {
            this.failure = msg;
            return this;
        }

        public Rule<T> SuccessMessage(string msg) {
            this.success = msg;
            return this;
        }

        public Rule<T> Condition(Func<T, bool> custom) {
            conditions.Add(custom);
            return this;
        }

        public Tuple<bool, string> Evaluate(T obj) {
            bool result = conditions.All(c => c.Invoke(obj));
            string msg = result ? success : failure;

            return Tuple.Create(result, msg);
        }

        public string RuleName { get { return name; } }
    }

    public class Person {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Birthday { get; set; }
        public int Age { get { return DateTime.Now.Year - Birthday.Year; } }
    }

    public class FluentPropertySetter<T> : DynamicObject {
        Queue<KeyValuePair<PropertyInfo, object>> _queue, _old;

        public FluentPropertySetter(T obj) {
            Me = obj;                     
        }
        
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {            
            var prop = typeof(T)
                .GetProperties()
                .SingleOrDefault(p => p.Name.Equals(binder.Name));            

            if(args.Length > 0) {                
                if(_queue != null) {
                    Enqueue(prop, args[0]);
                } else {
                    SetProperty(prop, args[0]);
                }
            }
            
            result = this;
            return true;
        }

        private void Enqueue(PropertyInfo prop, object value) {
            if(prop != null) {
                _old.Enqueue(new KeyValuePair<PropertyInfo, object>(prop, prop.GetValue(Me, null)));
            }
            _queue.Enqueue(new KeyValuePair<PropertyInfo, object>(prop, value));
        }
        private bool SetProperty(PropertyInfo Property, object Value) {
            if(Property == null)
                return false;
                //throw new ArgumentException("Property not found.");

            if(!Property.CanWrite)
                return false;
                //throw new ArgumentException("Property does not support writing.");

            if(Value != null && Property.PropertyType != Value.GetType())
                return false;
                //throw new ArgumentException("Value does not match the expected data type.");
            
            Property.SetValue(Me, Value, null);
            return true;
        }        

        public FluentPropertySetter<T> InTransaction() {
            _queue = new Queue<KeyValuePair<PropertyInfo, object>>();
            _old = new Queue<KeyValuePair<PropertyInfo, object>>();
            return this;
        }
        public FluentPropertySetter<T> Commit() {
            try {
                while(_queue.Count > 0) {
                    var item = _queue.Dequeue();
                    if(!SetProperty(item.Key, item.Value)) {
                        Rollback();
                        break;
                    }
                }
            } catch {
                Rollback();
            } finally {
                _queue = null;
                _old = null;
            }

            return this;
        }
        private void Rollback() {
            while(_old.Count > 0) {
                var item = _old.Dequeue();
                SetProperty(item.Key, item.Value);
            }
        }

        public T Me { get; private set; }
    }

    static class Extentions {
        public static dynamic Set<T>(this T obj) {
            return new FluentPropertySetter<T>(obj);
        }

        public static T Get<T>(this Hashtable table, string Key) {
            if(!table.ContainsKey(Key))
                return default(T);
            return (T)Convert.ChangeType(table[Key], typeof(T));
        }

        public static T Mutate<T>(this T obj, Action<T> method) {
            method(obj);
            return obj;
        }
    }

    public class SongInfo : DynamicSQL {
        public SongInfo()
            : base("Data Source=.;Initial Catalog=IIDX;Integrated Security=True") {
        }
    }
    public abstract class DynamicSQL : DynamicObject {
        string connString;

        public DynamicSQL(string connectionString, string table = null) {
            connString = connectionString;
            TableName = string.IsNullOrWhiteSpace(table) ? GetType().Name : table;
        }

        private IEnumerable<dynamic> LazyExecute(string sql, string[] parameterNames, object[] parameterValues) {
            using(var conn = new SqlConnection(connString)) {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                for(int i = 0; i < parameterNames.Length; i++)
                    cmd.Parameters.AddWithValue(parameterNames[i], parameterValues[i]);
                var reader = cmd.ExecuteReader();

                while(reader.Read()) {
                    dynamic result = new ExpandoObject();
                    IDictionary<string, object> dict = (IDictionary<string, object>)result;
                    for(int i = 0; i < reader.FieldCount; i++)
                        dict.Add(reader.GetName(i), reader[i]);
                    yield return result;
                }

                conn.Close();
            }
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            if(binder.CallInfo.ArgumentCount == 0) {
                throw new ArgumentException("Named arguments are required.");
            }
            var info = binder.CallInfo;

            switch(binder.Name) {
                case "Where":
                    var clause = new List<string>();
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("SELECT * FROM {0} WHERE ", TableName);
                    for(int i = 0; i < info.ArgumentCount; i++) {
                        var n = info.ArgumentNames[i];                        
                        var v = args[i];                        
                        clause.Add(string.Format("{0} = @{0}", n));
                    }
                    sb.Append(string.Join(" AND ", clause.ToArray()));
                    result = LazyExecute(sb.ToString(), info.ArgumentNames.ToArray(), args);
                    break;
                default:
                    result = null;
                    break;
            }

            return true;
        }

        public string TableName { get; set; }
    }

    class TestObj {
        public int id;
        public string name;
    }
    class TestModule : NinjectModule {
        public override void Load() {
            Bind<ITest>().To<MyTest>();
        }
    }
    interface ITest {
        string Name {
            get;
            set;
        }
    }
    class MyTest : ITest {
        #region ITest Members

        public string Name {
            get {
                return "Mike";
            }
            set {
                
            }
        }

        #endregion
    }
    class What {
        public What(ITest test) {
            Huh = test.Name;
        }
        public string Huh {
            get;
            private set;
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
