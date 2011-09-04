using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TestProject.NntpClient {
    public class NntpArticle {       
        internal NntpArticle() { }

        public Dictionary<string, string> Headers { get; internal set; }
        public Stream Body { get; internal set; }
        public string Filename { get; internal set; }
        public int Part { get; internal set; }
        public int ExpectedSize { get; internal set; }
        public bool IsValid { get { return Body.Length == ExpectedSize; } }
    }
}
