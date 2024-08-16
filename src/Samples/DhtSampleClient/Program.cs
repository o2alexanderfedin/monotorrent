using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Humanizer;
using MonoTorrent;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

namespace DhtSample;

internal static class Program
{
    static async Task Main (string[] args)
    {
        // Create a DHT engine, and register a listener on port 15000
        var engine = new DhtEngine ();
        var listener = new DhtListener (new IPEndPoint (IPAddress.Any, 15000));
        await engine.SetListenerAsync (listener);

        // Load up the node cache from the prior invocation (if there is any)
        var nodes = ReadOnlyMemory<byte>.Empty;
        const string myNodesPathDefault = "/Users/alexanderfedin/Projects/Torrent/monotorrent/src/Samples/Ozone.MonoTorrent.ConsoleApp/bin/Debug/net8.0/mynodes";
        const string myNodesPathApp = "mynodes";
        if (File.Exists (myNodesPathApp))
            nodes = File.ReadAllBytes (myNodesPathApp);
        else if (File.Exists (myNodesPathDefault))
            nodes = File.ReadAllBytes (myNodesPathDefault);

        engine.PeersFound += async delegate (object o, PeersFoundEventArgs e) {
            Console.WriteLine ("Found peers: {0}", e.Peers.Count);
            File.WriteAllBytes (myNodesPathApp, (await engine.SaveNodesAsync ()).ToArray ());
        };

        // Bootstrap into the DHT engine.
        await engine.StartAsync (nodes).ConfigureAwait(false);

        // Begin querying for random 20 byte infohashes
        var b = new byte[20];
        Random.Shared.NextBytes (b);

        // Kick off the firs search. Discovered peers will be returned via the 'PeersFound'
        // event in batches, as they're discovered.
        engine.GetPeers (new InfoHash (b));
        while (Console.ReadLine () != "q")
        {
            for (var i = 0; i < 30; i++) {
                Console.WriteLine("Waiting: {0} seconds left", (30 - i));
                await Task.Delay(1.Seconds());
            }
            // Get some peers for the torrent
            engine.GetPeers (new InfoHash (b));
            Random.Shared.NextBytes (b);
        }
    }
}
