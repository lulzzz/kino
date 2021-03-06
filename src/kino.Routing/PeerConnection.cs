using kino.Cluster;
using kino.Core;

namespace kino.Routing
{
    public class PeerConnection
    {
        public Node Node { get; set; }

        public bool Connected { get; set; }

        public Health Health { get; set; }
    }
}