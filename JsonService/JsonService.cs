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
            this.AllowDescribe = true;
            this.LogOutput = Console.Out;
            this.listener = new HttpListener();
        }

        /// <summary>
        /// Starts the service, without opening the default browser.
        /// </summary>
        public void Start() {
            Start(false);
        }
        /// <summary>
        /// Starts the service, with the option to open the default browser.
        /// </summary>
        public void Start(bool OpenBrowser) {
            if(!listener.IsListening) {
                InitService();
                Log("Server started @ " + Url);
                listener.Start();
                listener.BeginGetContext(NewRequest, null);
                if(OpenBrowser)
                    System.Diagnostics.Process.Start(Url);
            }
        }
        /// <summary>
        /// Stops the service
        /// </summary>
        public void Stop() {
            if(listener.IsListening) {
                listener.Stop();
                Url = null;
                Log("Server stopped");
            }
        }

        void InitService() {
            if(!System.Net.HttpListener.IsSupported)
                throw new ApplicationException("HttpListener is not supported on this version of Windows.");
            if(string.IsNullOrWhiteSpace(Host))
                Host = "+";
            if(this.Port <= 0 || this.Port > 65535)
                throw new ArgumentException("Port must be greater than 0 and less than 65535");

            Log("Initializing server");
            listener.Prefixes.Clear();
            listener.Prefixes.Add(string.Format("http://{0}:{1}/", this.Host, this.Port));
            Url = listener.Prefixes.First().Replace("+", "localhost");

            Log("Obtaining method information");
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
            if(AllowDescribe && Context.Request.Url.LocalPath.Equals("/help", StringComparison.InvariantCultureIgnoreCase)) {
                Log("Describing service.");
                Respond(Context.Response, Describe());
                return;
            }

            JsonMethod m = methods.Where(jm => jm.IsMatch(Context.Request.Url.LocalPath)).FirstOrDefault();
            BindingFlags flags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance;

            if(Authorize) {
                if(!AuthorizeRequest(Context.Request)) {
                    Log("Unauthorized request");
                    Respond(Context.Response, Unauthorized());
                    return;
                }
            }

            if(m == null) {
                Log("No suitable method found");
                Respond(Context.Response, NoMatchingMethod());
            } else {
                if(!Context.Request.HttpMethod.Equals(m.Attribute.Verb, StringComparison.InvariantCultureIgnoreCase)) {
                    Log("Invalid HTTP verb");
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

                        Log("Valid request, response returned");
                        Respond(Context.Response, o);
                    } catch(ArgumentException ae) {
                        Log("Parameter value invalid");
                        Respond(Context.Response, ParameterFailure(ae.InnerException.Message, ae.ParamName));
                    } catch(Exception e) {
                        Log("Failure to execute method");
                        Respond(Context.Response, CallFailure(e.Message));
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
        void Log(string msg) {
            if(LogOutput != null) {
                LogOutput.WriteLine(string.Format("[{0:MM/dd/yyyy HH:mm:ss}] {1}", DateTime.Now, msg));
                LogOutput.Flush();
            }
        }
        /// <summary>
        /// Returns all valid request urls for the service.
        /// </summary>
        /// <returns></returns>
        object Describe() {
            Uri absUri = null;
            return (from m in methods
                    let ps = m.MethodInfo.GetParameters()
                    let b = !string.IsNullOrWhiteSpace(m.Attribute.Example) && Uri.TryCreate(new Uri(Url), m.Attribute.Example, out absUri)
                    select new {
                        path = m.Attribute.Path,
                        desc = m.Attribute.Description,
                        parameters = from ph in m.Attribute.Placeholders
                                     let k = m.Attribute.GetKey(ph)
                                     let p = ps.Single(pi => pi.Name == ph)
                                     let r = p.DefaultValue == System.DBNull.Value
                                     select new {
                                         name = k,
                                         type = p.ParameterType.Name.ToLower(),
                                         required = r,
                                         @default = r ? null : p.DefaultValue
                                     },
                        example = b ? absUri.AbsoluteUri : string.Empty
                    }).ToArray();
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
        /// Returns the json for an error when a parameter value is invalid.
        /// </summary>
        /// <param name="Message">Exception message</param>
        /// <param name="ParameterName">Parameter name</param>
        /// <returns></returns>
        protected virtual object ParameterFailure(string Message, string ParameterName) {
            return new {
                status = "failed",
                error = Message,
                parameter = ParameterName
            };
        }
        /// <summary>
        /// Returns the json for an error calling the requested method.
        /// </summary>
        /// <param name="Message">Exception message</param>
        /// <returns></returns>        
        protected virtual object CallFailure(string Message) {
            return new {
                status = "failed",
                error = Message
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
        /// Gets the url the service is listening on
        /// </summary>
        public string Url {
            get;
            private set;
        }
        /// <summary>
        /// Gets and sets whether or not to authorize requests.
        /// </summary>
        /// <remarks>Override the AuthorizeRequest method for custom authentication.</remarks>
        public bool Authorize {
            get;
            set;
        }
        /// <summary>
        /// Gets and sets whether or not to allow the service to describe its methods to a client by requesting '/help'
        /// </summary>
        public bool AllowDescribe {
            get;
            set;
        }
        /// <summary>
        /// Gets and sets the output for logging
        /// </summary>
        public TextWriter LogOutput {
            get;
            set;
        }
    }
}
