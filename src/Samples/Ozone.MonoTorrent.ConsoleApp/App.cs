using Humanizer;
using MonoTorrent;
using MonoTorrent.Client;
using Ozone.MonoTorrent.ConsoleApp;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable HeapView.ObjectAllocation
// ReSharper disable HeapView.ObjectAllocation.Evident

Console.WriteLine("Hello from MonoTorrent!");

var hashToAnnounce = new byte[20];
Random.Shared.NextBytes(hashToAnnounce);
var infoHashToAnnounce = new InfoHash(hashToAnnounce);

var sharedFile = SharedFile();
var sharedFileTorrentFile = SharedFileTorrentFile();
var torrentFileSource = new TorrentFileSource(sharedFile);
if (File.Exists(sharedFileTorrentFile)) File.Delete(sharedFileTorrentFile);

var settingsFile = SettingsFile();
var saveDirectory = Path.Combine(AppDir(), "incoming");
using var engine = await BuildEngine(settingsFile);

await engine.StartAllAsync();

Console.WriteLine("Press 'X' to exit...");
using var cancellationSource = new CancellationTokenSource();

var actions = new Dictionary<ConsoleKey, Func<Task>>
{
    { ConsoleKey.Q, async () => { cancellationSource.Cancel(); await Task.CompletedTask; } },
    { ConsoleKey.F, () => HandleFKeyAsync(engine, saveDirectory) },
    { ConsoleKey.S, () => HandleSKeyAsync(engine, torrentFileSource, sharedFileTorrentFile, saveDirectory) },
    { ConsoleKey.L, () => HandleLKeyAsync(engine, infoHashToAnnounce, saveDirectory) },
    { ConsoleKey.M, () => HandleMKeyAsync(engine, infoHashToAnnounce) }
};

while (!cancellationSource.IsCancellationRequested)
{
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (actions.TryGetValue(key, out var action))
        {
            await action();
        }
    }

    foreach (var torrent in engine.Torrents.ToList())
    {
        Console.WriteLine($"{torrent.Name}: {torrent.Progress}");
        if (torrent.Complete)
        {
            await torrent.StopAsync();
            await engine.RemoveAsync(torrent);
        }
    }
    await Task.Delay(1.Seconds());
}

File.WriteAllBytes(Path.Combine(AppDir(), "mynodes"), (await engine.DhtEngine.SaveNodesAsync()).ToArray());
await engine.SaveStateAsync(settingsFile);
await engine.StopAllAsync();

return;

static string SettingsFile() => Path.Combine(AppDir(), "settings.ben");

static async Task<ClientEngine> BuildEngine(string settingsFile)
{
    try
    {
        if (File.Exists(settingsFile))
            return await ClientEngine.RestoreStateAsync(settingsFile);
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
                ConnectionTimeout = 30.Seconds()
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

static string AppDir()
{
    var dir = Environment.CurrentDirectory;
    Directory.CreateDirectory(dir);
    return dir;
}

static string SharedFile()
{
    var file = Path.Combine(AppDir(), typeof(Anchor).Namespace);
    return File.Exists(file)
        ? file
        : throw new FileNotFoundException(file);
}

static string SharedFileTorrentFile()
{
    var name = Path.GetFileName(SharedFile());
    var file = Path.Combine(TestTorrentsDir(), name + ".torrent");
    return file;
}

static void DumpPeers(InfoHash infoHash, IEnumerable<PeerInfo> peers)
    => Console.WriteLine($"\nPeersFound | {infoHash.ToHex()} => {string.Join("", peers.Select(x => $"\n\t{x.PeerId} - {x.ConnectionUri}"))}\n");

// Key action methods
static async Task HandleFKeyAsync(ClientEngine engine, string saveDirectory)
{
    var torrentFiles = Directory
        .EnumerateFiles(TestTorrentsDir(), "*.torrent", SearchOption.AllDirectories)
        .ToArray();
    foreach (var torrentFile in torrentFiles)
    {
        var torrent1 = await Torrent.LoadAsync(torrentFile);
        if (engine.Torrents.Any(x => x.Torrent?.Equals(torrent1) ?? false))
            continue;

        var torrent = await engine.AddAsync(torrentFile, saveDirectory);
        await torrent.StartAsync();
        Console.WriteLine(torrent.Name);
    }
}

static async Task HandleSKeyAsync(ClientEngine engine, TorrentFileSource torrentFileSource, string sharedFileTorrentFile, string saveDirectory)
{
    var torrentCreator = new TorrentCreator(TorrentType.V2Only, Factories.Default)
    {
        CreatedBy = "AF @ Oxygen",
        StoreMD5 = true,
        StoreSHA1 = true,
        Publisher = "Oxygen",
    };
    torrentCreator.Create(torrentFileSource, sharedFileTorrentFile);
    var torrent = await engine.AddAsync(sharedFileTorrentFile, saveDirectory);
    await torrent.StartAsync();
    Console.WriteLine(torrent.Name);
}

static async Task HandleLKeyAsync(ClientEngine engine, InfoHash infoHashToAnnounce, string saveDirectory)
{
    // var magnetLink = new MagnetLink(infoHashToAnnounce);
    // var torrentSettings = new TorrentSettingsBuilder(new())
    //     {
    //         AllowInitialSeeding = true
    //     }
    //     .ToSettings();
    // var torrent = await engine.AddAsync(magnetLink, saveDirectory, torrentSettings);
    // await torrent.StartAsync();
    // Console.WriteLine(torrent.Name);
    await engine.AnnounceAsync (infoHashToAnnounce);
}

static async Task HandleMKeyAsync(ClientEngine engine, InfoHash infoHashToAnnounce)
{
    var peers = await engine
        .GetPeersAsync(infoHashToAnnounce)
        .Take(1)
        .ToArrayAsync();
    DumpPeers(infoHashToAnnounce, peers);
}
