using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Reflection;
using System.IO;

namespace TestProject.JsonService {
    /// <summary>
    /// Abstract class for json based web services.
    /// </summary>
    public abstract class JsonService {
        IEnumerable<JsonMethod> methods;
        HttpListener listener;

        public JsonService() {
            this.Host = "+";
            this.Port = 5678;
            this.Authorize = false;
            this.listener = new HttpListener();
        }
        
        /// <summary>
        /// Starts the service
        /// </summary>
        public void Start() {
            if(!listener.IsListening) {
                InitService();
                listener.Start();
                listener.BeginGetContext(NewRequest, null);
            }
        }
        /// <summary>
        /// Stops the service
        /// </summary>
        public void Stop() {
            if(listener.IsListening) {
                listener.Stop();
            }
        }

        void InitService() {
            if(!System.Net.HttpListener.IsSupported)
                throw new ApplicationException("HttpListener is not supported on this version of Windows.");
            if(string.IsNullOrWhiteSpace(Host))
                Host = "+";
            if(this.Port <= 0 || this.Port > 65535)
                throw new ArgumentException("Port must be greater than 0 and less than 65535");

            listener.Prefixes.Clear();
            listener.Prefixes.Add(string.Format("http://{0}:{1}/", this.Host, this.Port));
            InitMethods();
        }
        void InitMethods() {
            methods = from mi in GetType().GetMethods().OfType<MethodInfo>()
                      let attribs = mi.GetCustomAttributes(false).OfType<VerbAttribute>()
                      where attribs.Count() > 0
                      select new JsonMethod {
                          MethodInfo = (MethodInfo)mi,
                          Attribute = attribs.Single()
                      };
        }
        void NewRequest(IAsyncResult Result) {
            if(listener.IsListening) {
                HttpListenerContext context = listener.EndGetContext(Result);
                listener.BeginGetContext(NewRequest, null);
                ProcessRequest(context);
            }
        }
        void ProcessRequest(HttpListenerContext Context) {
            JsonMethod m = methods.Where(jm => jm.IsMatch(Context.Request.Url.LocalPath)).FirstOrDefault();
            BindingFlags flags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;

            if(Authorize) {
                if(!AuthorizeRequest(Context.Request)) {
                    Respond(Context.Response, Unauthorized());
                    return;
                }
            }

            if(m == null) {
                Respond(Context.Response, NoMatchingMethod());
            } else {
                if(!Context.Request.HttpMethod.Equals(m.Attribute.Verb, StringComparison.InvariantCultureIgnoreCase)) {
                    Respond(Context.Response, InvalidVerb());
                } else {
                    try {
                        object o = GetType().InvokeMember(
                            name: m.MethodInfo.Name,
                            invokeAttr: flags,
                            binder: Type.DefaultBinder,
                            target: this,
                            args: m.GetArgs(Context.Request.QueryString)
                        );

                        Respond(Context.Response, o);
                    } catch {
                        Respond(Context.Response, CallFailure());
                    }
                }
            }
        }
        void Respond(HttpListenerResponse Response, object content) {
            JsonDocument doc = new JsonDocument(content);
            doc.Formatting = JsonDocument.JsonFormat.None;
            string raw = doc.ToString();

            Response.ContentType = "application/json";
            Response.ContentLength64 = raw.Length;
            using(StreamWriter sw = new StreamWriter(Response.OutputStream)) {
                sw.Write(raw);
            }
            Response.Close();
        }
        /// <summary>
        /// Performs authorization &amp; returns a boolean representing the result.
        /// </summary>
        /// <param name="Request"></param>
        /// <returns></returns>
        protected virtual bool AuthorizeRequest(HttpListenerRequest Request) {
            return true;
        }
        /// <summary>
        /// Returns the json for when no method exists.
        /// </summary>
        /// <returns></returns>
        protected virtual object NoMatchingMethod() {
            return new {
                status = "failed",
                error = "Invalid method call"
            };
        }
        /// <summary>
        /// Returns the json for an error calling the requested method.
        /// </summary>
        /// <returns></returns>
        protected virtual object CallFailure() {
            return new {
                status = "failed",
                error = "There was an error with the parameters of your request."
            };
        }
        /// <summary>
        /// Returns the json for an invalid verb for the request.
        /// </summary>
        /// <returns></returns>
        protected virtual object InvalidVerb() {
            return new {
                status = "failed",
                error = "http verb specified is not allowed for this method."
            };
        }
        /// <summary>
        /// Returns the json for an unauthorized request.
        /// </summary>
        /// <returns></returns>
        protected virtual object Unauthorized() {
            return new {
                status = "failed",
                error = "Missing or invalid authorization key."
            };
        }
        /// <summary>
        /// Gets and sets the host the service will run on.  Defaults to localhost.
        /// </summary>
        public string Host {
            get;
            set;
        }
        /// <summary>
        /// Gets and sets the port the service will run on.  Defaults to 5678.
        /// </summary>
        public int Port {
            get;
            set;
        }        
        /// <summary>
        /// Gets and sets whether or not to authorize requests.
        /// </summary>
        /// <remarks>Override the AuthorizeRequest method for custom authentication.</remarks>
        public bool Authorize {
            get;
            set;
        }
    }
}
