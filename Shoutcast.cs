using System;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;

namespace TestProject {
    /// <summary>
    /// Reads stream information and song data from a Shoutcast stream.
    /// </summary>
    public class Shoutcast {
        const string REGEX_METADATA = @"(?<Key>.*?)='(?<Value>.*?)';";

        HttpWebRequest req;
        WebResponse resp;
        System.Threading.Thread streamThread;
        object padLock = new object();
        DateTime opened;

        /// <summary>
        /// Fired when new song metadata is read.
        /// </summary>
        public event EventHandler OnTitleChanged = delegate {
        };
        /// <summary>
        /// Fired with the stream information is read.
        /// </summary>
        public event EventHandler OnGotStreamInfo = delegate {
        };
        /// <summary>
        /// Fired when the stream has opened.
        /// </summary>
        public event EventHandler OnStreamOpen = delegate {
        };
        /// <summary>
        /// Fired when the stream has been closed, either by the user or if there is a problem with the stream.
        /// </summary>
        public event EventHandler OnStreamClose = delegate {
        };

        /// <summary>
        /// Initializes a new instance with a stream url.
        /// </summary>
        /// <param name="Url"></param>
        public Shoutcast(string Url) {
            StreamUrl = Url;
            IsOpen = false;
            RipLocation = "stream.mp3";
            RipStream = false;
        }

        /// <summary>
        /// Opens the stream and begins reading data.
        /// </summary>
        public void Open() {
            lock(padLock) {
                if(!IsOpen) {                    
                    req = (HttpWebRequest)WebRequest.Create(StreamUrl);
                    req.Headers["icy-metadata"] = "1";
                    req.UserAgent = "WinampMPEG/5.57";
                    IsOpen = true;
                    streamThread = new Thread(new ThreadStart(doinThangs));
                    streamThread.Start();                    
                    OnStreamOpen(this, EventArgs.Empty);
                }
            }
        }
        void doinThangs() {
            resp = req.GetResponse();
            
            Genre = resp.Headers["icy-genre"];
            StreamName = resp.Headers["icy-name"];
            IsPublic = resp.Headers["icy-pub"] == "1";
            HomeUrl = resp.Headers["icy-url"];
            BitRate = int.Parse(resp.Headers["icy-br"]);
            Server = resp.Headers["Server"];
            NowPlaying = "Waiting for stream...";
            Notices = resp.Headers.AllKeys.Where(k => k.StartsWith("icy-notice")).Select(k => resp.Headers[k]).ToArray();
            opened = DateTime.Now;

            OnGotStreamInfo(this, EventArgs.Empty);

            using(Stream stream = resp.GetResponseStream()) {
                int mblockInterval = int.Parse(resp.Headers["icy-metaint"]);
                byte[] buffer = new byte[mblockInterval];
                int bRead = 0;
                FileStream fs = null;

                if(RipStream)
                    fs = new FileStream(RipLocation, FileMode.Create, FileAccess.Write, FileShare.None);

                while(IsOpen) {
                    try {
                        int r = stream.Read(buffer, bRead, buffer.Length - bRead);
                        if(fs != null)
                            fs.Write(buffer, bRead, r);
                        bRead += r;
                    } catch(Exception ex) {
                        Console.WriteLine("[{0}] -- Error: {1} --", ElapsedTime, ex.Message);
                        Close();
                    }

                    if(bRead == mblockInterval) {
                        int mLen = stream.ReadByte() * 16;
                        if(mLen > 0) {
                            byte[] raw = new byte[mLen];
                            stream.Read(raw, 0, mLen);
                            string metadata = Encoding.UTF8.GetString(raw);
                            NowPlaying = Regex.Matches(metadata, REGEX_METADATA).OfType<Match>().Single(m => m.Groups["Key"].Value == "StreamTitle").Groups["Value"].Value;
                            OnTitleChanged(this, EventArgs.Empty);
                        }
                        bRead = 0;
                    }               
                }
                stream.Close();
                if(fs != null)
                    fs.Close();
            }
            resp.Close();
        }
        /// <summary>
        /// Closes the stream.  May block for up to 5 seconds to perform clean-up.
        /// </summary>
        public void Close() {
            if(IsOpen) {
                IsOpen = false;
                streamThread.Join(5000);
                streamThread = null;
                NowPlaying = null;
                OnStreamClose(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the genre of music being streamed.
        /// </summary>
        public string Genre {
            get;
            private set;
        }
        /// <summary>
        /// Gets the name of the stream.
        /// </summary>
        public string StreamName {
            get;
            private set;
        }
        /// <summary>
        /// Gets whether or not this stream is public.
        /// </summary>
        public bool IsPublic {
            get;
            private set;
        }
        /// <summary>
        /// Gets the original url passed into the constructor.
        /// </summary>
        public string StreamUrl {
            get;
            private set;
        }
        /// <summary>
        /// Gets the url associated with the stream.
        /// </summary>
        public string HomeUrl {
            get;
            private set;
        }
        /// <summary>
        /// Gets any and all informational messages from the stream.
        /// </summary>
        public string[] Notices {
            get;
            private set;
        }
        /// <summary>
        /// Gets the bit rate of the stream.
        /// </summary>
        public int BitRate {
            get;
            private set;
        }
        /// <summary>
        /// Gets information about the Shoutcast server providing the stream.
        /// </summary>
        public string Server {
            get;
            private set;
        }
        /// <summary>
        /// Gets the title of the current song being streamed.
        /// </summary>
        public string NowPlaying {
            get;
            private set;
        }
        /// <summary>
        /// Gets whether or not this stream is currently open.
        /// </summary>
        public bool IsOpen {
            get;
            private set;
        }
        /// <summary>
        /// Gets and sets whether or not to store the stream to disc as it's running.
        /// </summary>
        public bool RipStream {
            get;
            set;
        }
        /// <summary>
        /// Gets and sets the filename of the stream rip, if RipStream is true.  WILL OVERWRITE EXISTING FILE.
        /// </summary>
        public string RipLocation {
            get;
            set;
        }
        /// <summary>
        /// Gets the length of time the stream has been open.  Returns TimeSpan.Zero if the stream is not open.
        /// </summary>
        public TimeSpan ElapsedTime {
            get {
                if(IsOpen)
                    return DateTime.Now.Subtract(opened);
                return TimeSpan.Zero;
            }
        }
    }
}

/* *** EXAMPLE CODE ***
 * 
    Shoutcast sc = new Shoutcast("http://u15.di.fm/di_liquiddnb");

    sc.OnStreamOpen += (s, e) => Console.WriteLine("[{0}] -- {1} --", DateTime.Now, "Stream Opened");
    sc.OnGotStreamInfo += delegate(object sender, EventArgs e) {
        Console.WriteLine(sc.StreamName);
        Console.WriteLine(sc.Genre);
        foreach(var n in sc.Notices) {
            Console.WriteLine(n);
        }
        Console.WriteLine(new string('-', 30));
    };
    sc.OnTitleChanged += (s, e) => Console.WriteLine("[{0}] Title Change: {1}", sc.ElapsedTime, sc.NowPlaying);
    sc.OnStreamClose += (s, e) => Console.WriteLine("[{0}] -- Stream Closed --", DateTime.Now);

    sc.RipStream = false;
    sc.RipLocation = @"H:\Downloads\stream.mp3";

    sc.Open();
    while(Console.ReadKey(true).Key != ConsoleKey.Escape)
        ;
    sc.Close();

    Console.WriteLine("\r\nPress Enter to close console.");
    Console.ReadLine();
 * 
*/