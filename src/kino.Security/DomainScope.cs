using System.Collections.Generic;

namespace kino.Security
{
    public class DomainScope
    {
        public string Domain { get; set; }

        public IEnumerable<string> MessageIdentities { get; set; }
    }
}