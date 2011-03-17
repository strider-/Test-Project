using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TestProject.JsonService {
    /// <summary>
    /// Represents an http verb and template for json requests.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public abstract class VerbAttribute : Attribute {
        const string REGEX = @"(?<Key>[^?&=]+)=(?<Value>[^&]*)";

        /// <summary>
        /// Sets the type of verb this method will accept.
        /// </summary>
        /// <param name="UriTemplate">Sets the template for a service method call</param>
        public VerbAttribute(string UriTemplate) {
            this.UriTemplate = UriTemplate;
            MatchCollection mc = Regex.Matches(this.UriTemplate, REGEX, RegexOptions.Singleline);

            Path = Regex.Split(UriTemplate, REGEX)[0].TrimEnd('?');
            if(!Path.StartsWith("/"))
                Path = "/" + Path;

            Keys = mc.OfType<Match>().Select(m => m.Groups["Key"].Value).ToArray();
            Placeholders = mc.OfType<Match>().Select(m => m.Groups["Value"].Value.Trim('{', '}')).ToArray();
        }
        /// <summary>
        /// Gets the template for a service method call
        /// </summary>
        public string UriTemplate {
            get;
            private set;
        }
        /// <summary>
        /// Gets the path for the method call
        /// </summary>
        public string Path {
            get;
            private set;
        }
        /// <summary>
        /// Gets the keys for the method call
        /// </summary>
        public string[] Keys {
            get;
            private set;
        }
        /// <summary>
        /// Gets the placeholder values for the method call
        /// </summary>
        public string[] Placeholders {
            get;
            private set;
        }
        /// <summary>
        /// Gets the key for a given placeholder
        /// </summary>
        /// <param name="Placeholder">Placeholder name, which should match the parameter name in the actual method.</param>
        /// <returns></returns>
        public string GetKey(string Placeholder) {
            for(int i = 0; i < Placeholders.Length; i++) {
                if(Placeholders[i].Equals(Placeholder, StringComparison.InvariantCultureIgnoreCase))
                    return Keys[i];
            }
            return null;
        }
        /// <summary>
        /// Gets the http verb
        /// </summary>
        public abstract string Verb {
            get;
        }
    }

    /// <summary>
    /// Represents the GET http verb for json service requests.
    /// </summary>
    public class GetAttribute : VerbAttribute {
        public GetAttribute(string UriTemplate)
            : base(UriTemplate) {
        }
        /// <summary>
        /// Gets the http verb (GET)
        /// </summary>
        public override string Verb {
            get {
                return "GET";
            }
        }
    }

    /// <summary>
    /// Represents the POST http verb for json service requests.
    /// </summary>
    public class PostAttribute : VerbAttribute {
        public PostAttribute(string UriTemplate)
            : base(UriTemplate) {
        }
        /// <summary>
        /// Gets the http verb (POST)
        /// </summary>
        public override string Verb {
            get {
                return "POST";
            }
        }
    }    
}
