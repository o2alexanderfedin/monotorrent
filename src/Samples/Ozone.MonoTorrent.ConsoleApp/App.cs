using System.Net;
using System.Reflection;

using Humanizer;
using MonoTorrent;
using MonoTorrent.Client;
using Ozone.MonoTorrent.ConsoleApp;
// ReSharper disable HeapView.ObjectAllocation.Possible
// ReSharper disable HeapView.DelegateAllocation
// ReSharper disable AccessToModifiedClosure
// ReSharper disable AccessToDisposedClosure
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable UseObjectOrCollectionInitializer
// ReSharper disable HeapView.ClosureAllocation
// ReSharper disable HeapView.ObjectAllocation
// ReSharper disable HeapView.ObjectAllocation.Evident

Console.WriteLine(Environment.CommandLine);
var hashToAnnounce = new byte[20];
Random.Shared.NextBytes(hashToAnnounce);
var infoHashToAnnounce = new InfoHash(hashToAnnounce);

var sharedFileTorrentFile = SharedFileTorrentFile();
var torrentFileSource = new TorrentFileSource(sharedFileTorrentFile);
// if (File.Exists(sharedFileTorrentFile)) File.Delete(sharedFileTorrentFile);

var settingsFile = SettingsFile();
var saveDirectory = Path.Combine(AppDir(), "incoming");
using var engine = await BuildEngine(settingsFile);

engine.PeersFound += (s, e) => Console.WriteLine ($"Engine.PeersFound | For hash [{e.InfoHash}] {e.Peers.Count} peers were found.");
engine.LocalPeerDiscovery.PeerFound += (s, e) => Console.WriteLine ($"Engine.LocalPeerDiscovery.PeerFound | For hash [{e.InfoHash}] {e.Uri} peer was found.");
engine.DhtEngine.PeersFound += (s, e) => Console.WriteLine ($"Engine.DhtEngine.PeersFound | For hash [{e.InfoHash}] {e.Peers.Count} peers were found.");

await engine.StartAllAsync();

using var cancellationSource = new CancellationTokenSource();

Dictionary<ConsoleKey, (int i, string title, ConsoleKey key, Func<Task> func)> actions = default!;
actions = new[] {
        [Action ("Download Torrents", ConsoleKey.D)] () => HandleStartDownloadTorrentsAsync (engine, saveDirectory),
        [Action ("Download Single Torrent", ConsoleKey.T)] () => HandleStartDownloadTorrentAsync (engine, torrentFileSource, sharedFileTorrentFile, saveDirectory),
        [Action ("Save Nodes", ConsoleKey.N)] () => HandleSaveNodesAsync (engine, AppDir ()),
        [Action ("Announce Hash", ConsoleKey.A)] () => HandleAnnounceInfoHashAsync (engine, infoHashToAnnounce),
        [Action ("Lookup Hash", ConsoleKey.L)] () => HandleLookupInfoHashAsync (engine, infoHashToAnnounce),
        [Action] () => Task.CompletedTask,
        [Action ("Help", ConsoleKey.F1)] () => ShowHelpAsync (),
        [Action ("Exit", ConsoleKey.Q)] () => SignalStopAsync (cancellationSource),
    }
    .SelectMany (func => func.GetMethodInfo ().GetCustomAttributes<ActionAttribute> ().Select (attr => (attr, func)))
    .Select ((x, i) => (i: i + 1, x.attr, x.func))
    .ToDictionary(
        x => x.attr.Key,
        x => (
            x.i,
            title: x.attr.IsDelimiter ? "" : $"{Enum.GetName(x.attr.Key)} => {x.attr.Title}",
            key: x.attr.Key,
            x.func
        )
    );

await ShowHelpAsync ();
try
{
    while (!cancellationSource.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey (true);
            await (
                actions
                    .TryGetValue (keyInfo.Key, out var action)
                    ? action.func ()
                    : Task.CompletedTask
            );
        }

        await HandleCompletedTorrentsAsync (engine);
        await Task.Delay (1.Seconds ());
    }
}
catch (Exception error)
{
    Console.WriteLine ($"Error while running loop:\n{error}");
}
finally
{
    Console.WriteLine ($"Completed loop");
}

await engine.SaveStateAsync(settingsFile);
await engine.StopAllAsync();

return;

static string SettingsFile() => Path.Combine(AppDir(), "settings.ben");

static async Task<ClientEngine> BuildEngine(string settingsFile)
{
    try
    {
        if (File.Exists(settingsFile)) {
            var clientEngine = await ClientEngine.RestoreStateAsync(settingsFile);
            var settingsBuilder = new EngineSettingsBuilder (clientEngine.Settings)
            {
                AllowLocalPeerDiscovery = true
            };
            clientEngine = new ClientEngine(settingsBuilder.ToSettings());
            return clientEngine;
        }
    }
    catch (Exception error)
    {
        Console.WriteLine(error);
    }

    return CreateDefault();

    static ClientEngine CreateDefault()
    {
        var settings = new EngineSettingsBuilder
            {
                AllowPortForwarding = true,
                CacheDirectory = Path.Combine(AppDir(), "cache"),
                FastResumeMode = FastResumeMode.BestEffort,
                ConnectionTimeout = 30.Seconds(),
                AllowLocalPeerDiscovery = true,
                DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
                ListenEndPoints = new()
                {
                    ["ip"] = new IPEndPoint (IPAddress.Any, 0),
                    ["ipv6"] = new IPEndPoint (IPAddress.IPv6Any, 0),
                }
            }
            .ToSettings();
        return new ClientEngine(settings);
    }
}

static string TestTorrentsDir()
{
    var dir = Path.Combine(AppDir(), "TestTorrents");
    return dir;
}

static string AppDir() => Environment.CurrentDirectory;

static string SharedFileTorrentFile()
{
    var file = Path.Combine(TestTorrentsDir(), "ubuntu-24.10-desktop-amd64.iso.torrent");
    return File.Exists(file)
        ? file
        : throw new FileNotFoundException(file);
}

static void DumpPeers(InfoHash infoHash, IEnumerable<PeerInfo> peers)
    => Console.WriteLine($"\nPeersFound | {infoHash.ToHex()} => {string.Join("", peers.Select(x => $"\n\t{x.PeerId} - {x.ConnectionUri}"))}\n");

// Key action methods

static async Task HandleSaveNodesAsync (ClientEngine engine, string appDir)
{
    var serializedNodes = (await engine.DhtEngine.SaveNodesAsync()).ToArray();
    var pathToSaveNodes = Path.Combine(appDir, "mynodes");
    File.WriteAllBytes(pathToSaveNodes, serializedNodes);
}

static async Task HandleStartDownloadTorrentsAsync(ClientEngine engine, string saveDirectory)
{
    var testTorrentsDir = TestTorrentsDir();
    var torrentFiles = Directory
        .EnumerateFiles(testTorrentsDir, "*.torrent", SearchOption.AllDirectories)
        .ToArray();
    foreach (var torrentFile in torrentFiles)
    {
        var torrent1 = await Torrent.LoadAsync(torrentFile);
        if (engine.Torrents.Any(x => x.Torrent?.Equals(torrent1) ?? false))
            continue;

        var torrent = await engine.AddAsync(torrentFile, saveDirectory);
        AttachTorrentCompletionHandler (engine, torrent);

        await torrent.StartAsync();
        Console.WriteLine(torrent.Name);
    }
}

static async Task HandleStartDownloadTorrentAsync(ClientEngine engine, TorrentFileSource torrentFileSource, string sharedFileTorrentFile, string saveDirectory)
{
    var torrentCreator = new TorrentCreator(TorrentType.V2Only, Factories.Default)
    {
        CreatedBy = "AF @ Oxygen",
        StoreMD5 = true,
        StoreSHA1 = true,
        Publisher = "Oxygen",
    };
    var savePath = sharedFileTorrentFile + ".copy";
    torrentCreator.Create(torrentFileSource, savePath);
    var torrent = await engine.AddAsync(savePath, saveDirectory);
    AttachTorrentCompletionHandler (engine, torrent);
    await torrent.StartAsync();
    Console.WriteLine(torrent.Name);
}

static async Task HandleAnnounceInfoHashAsync(ClientEngine engine, InfoHash infoHashToAnnounce)
{
    await engine.AnnounceAsync (infoHashToAnnounce);
}

static async Task HandleLookupInfoHashAsync(ClientEngine engine, InfoHash infoHashToAnnounce)
{
    var peers = await engine
        .GetPeersAsync(infoHashToAnnounce)
        .Take(1)
        .ToArrayAsync();
    DumpPeers(infoHashToAnnounce, peers);
}

static async Task HandleCompletedTorrentsAsync(ClientEngine engine)
{
    foreach (var torrent in engine.Torrents.ToList())
    {
        Console.WriteLine($"{torrent.Name}: {torrent.Progress}");
        continue;

        if (!torrent.Complete)
            continue;

        await torrent.StopAsync();
        await engine.RemoveAsync(torrent);
    }
}

static void AttachTorrentCompletionHandler (ClientEngine engine, TorrentManager tm)
{
    var eventHolder = new RefHolder<EventHandler<TorrentStateChangedEventArgs>>();
    eventHolder.Target = TorrentOnTorrentStateChanged;
    tm.TorrentStateChanged += eventHolder.Target;
    return;

    async void TorrentOnTorrentStateChanged (object sender, TorrentStateChangedEventArgs e)
    {
        var torrent = e.TorrentManager;
        var torrentName = torrent.Name;
        switch (e)
        {
            case {NewState: TorrentState.Error or TorrentState.Stopped or TorrentState.Seeding}:
                LogTorrentStateChange();
                if (e.TorrentManager.Complete)
                {
                    Console.WriteLine($"Completed: {torrentName}");
                    if (torrent.State is not (TorrentState.Stopping or TorrentState.Stopped)) {
                        await torrent.StopAsync ();
                    }

                    await engine.RemoveAsync (torrent);
                    torrent.TorrentStateChanged -= eventHolder.Target;
                }
                break;

            default:
                LogTorrentStateChange();
                break;
        }

        void LogTorrentStateChange()
            => Console.WriteLine($"{torrentName}: {Enum.GetName(e.OldState)} => {Enum.GetName(e.NewState)}");
    }
}

async Task SignalStopAsync(CancellationTokenSource cancellationTokenSource)
{
    cancellationTokenSource.Cancel();
    await Task.CompletedTask;
}

async Task ShowHelpAsync()
{
    var lines = actions
        .OrderBy (x => x.Value.i)
        .Select (x => x.Value.title)
        .ToList ();
    await Console.Out.WriteLineAsync (
        $"""
         #====================================================
         |  Now: {DateTime.Now:F}
         |  MonoTorrent Experiments Help:
         |
         {string.Join(Environment.NewLine, lines.Select (x => $"|  {x}"))}
         #====================================================
         """
    );
}
