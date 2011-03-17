using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace TestProject.JsonService {
    class JsonMethod {
        public MethodInfo MethodInfo {
            get;
            set;
        }
        public VerbAttribute Attribute {
            get;
            set;
        }
        public bool IsMatch(string path) {
            return Attribute.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase);
        }
        public object[] GetArgs(System.Collections.Specialized.NameValueCollection qs) {
            List<object> args = new List<object>();

            foreach(var pm in MethodInfo.GetParameters()) {
                string key = Attribute.GetKey(pm.Name);
                string val = qs[key];

                if(val == null && pm.DefaultValue != System.DBNull.Value)
                    args.Add(pm.DefaultValue);
                else
                    args.Add(Convert.ChangeType(val, pm.ParameterType));
            }

            return args.ToArray();
        }
        public bool IsGet {
            get {
                if(this.Attribute != null)
                    return this.Attribute is GetAttribute;
                return false;
            }
        }
        public bool IsPost {
            get {
                if(this.Attribute != null)
                    return this.Attribute is PostAttribute;
                return false;
            }
        }
    }
}
