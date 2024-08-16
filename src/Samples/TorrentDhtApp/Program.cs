using MonoTorrent.Dht;

namespace TorrentDhtApp;

class Program
{
    static async Task Main (string[] args)
    {
        Console.WriteLine ("Torrent DHT starting...");

        using (var dhtEngine = new DhtEngine ()) {
            await dhtEngine.StartAsync ();
            Console.WriteLine ("Torrent DHT started.");

            Console.WriteLine ("Torrent DHT stopping...");
        }

        Console.WriteLine ("Torrent DHT stopped.");
    }
}
