using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestProject.NntpClient {
    public class NntpGroup {
        internal NntpGroup() { }

        public override string ToString() {
            return Name;
        }

        public static NntpGroup Parse(string line) {
            string[] group = line.Split(' ');

            return new NntpGroup {
                Name = group[0],
                LastArticle = ulong.Parse(group[1]),
                FirstArticle = ulong.Parse(group[2]),
                IsPostingAllowed = group[3] == "y"
            };
        }

        public string Name { get; internal set; }
        public ulong LastArticle { get; internal set; }
        public ulong FirstArticle { get; internal set; }
        public bool IsPostingAllowed { get; internal set; }
    }
}
