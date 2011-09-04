using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestProject.NntpClient {
    public class NntpArticle {       
        internal NntpArticle() { }

        public Dictionary<string, string> Headers { get; internal set; }
        public string Body { get; internal set; }
    }
}
