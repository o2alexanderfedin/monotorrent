using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Threading;
using MonoTorrent;
using MonoTorrent.Dht;

namespace Ozone.MonoTorrent.Shared;

public static class DhtEngineExtensions
{
        public static async IAsyncEnumerable<PeerInfo> GetPeersAsync(
            this DhtEngine engine,
            InfoHash infoHash,
            [EnumeratorCancellation] CancellationToken cancellation = default
        )
        {
            var q = new AsyncQueue<PeerInfo>();
            engine.PeersFound += OnPeersFound;
            try
            {
                engine.GetPeers(infoHash.Truncate());
                while (!cancellation.IsCancellationRequested)
                {
                    while (q.TryDequeue(out var peer1))
                    {
                        yield return peer1;
                    }

                    PeerInfo peer2;
                    try
                    {
                        peer2 = await q.DequeueAsync(cancellation).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException error)
                    {
                        yield break;
                    }
                    yield return peer2;
                }
            }
            finally
            {
                engine.PeersFound -= OnPeersFound;
            }

            yield break;

            void OnPeersFound(object? sender, PeersFoundEventArgs e)
            {
                if (e.InfoHash != infoHash) return;
                foreach (var peer in e.Peers)
                {
                    q.Enqueue(peer);
                }
            }
        }
}