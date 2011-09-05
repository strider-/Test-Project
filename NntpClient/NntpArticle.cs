using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TestProject.NntpClient {
    public class NntpArticle {       
        internal NntpArticle() { }

        public string Store(string path) {
            if(!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string location = Path.Combine(path, string.Format("{0}.{1}", Filename, Part));
            
            File.WriteAllBytes(location, Body.ToArray());
            Body.Close();
            Body.Dispose();
            
            return location;
        }

        public Dictionary<string, string> Headers { get; internal set; }
        public MemoryStream Body { get; internal set; }
        public string Filename { get; internal set; }
        public int Part { get; internal set; }
        public int ExpectedSize { get; internal set; }
        public bool IsValid { get { return Body.Length == ExpectedSize; } }
    }
}
