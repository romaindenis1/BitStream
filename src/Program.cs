using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using BitRuisseau;
using Backend;

namespace BitStream
{
    //sans globals, tres complique a faire une variable qui marche par tout
    static class Globals
    {
        public static string Path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    class Program
    {
        /*
         * Todo: make sharing only on ask, will do later when other 
         * implementations can ask
         */

        // Guard to prevent the faulty Communicator from triggering the logic twice
        private static bool _hasConnectedOnce = false;
        private static readonly object _connectionLock = new object();

        static async Task Main(string[] args)
        {
            // Setup MQTT communicator early so we can broadcast our song list on connect
            // Broker is a real remote server (hardcoded as requested)
            var brokerHost = "blue.section-inf.ch";
            var nodeId = Environment.MachineName;
            var mqttTopic = "BitRuisseau";

            var broadcastFinished = new TaskCompletionSource<bool>();
            var globalCommunicator = new MqttCommunicator(brokerHost, nodeId, mqttTopic);

            globalCommunicator.OnConnected = () =>
            {
                // LOCK = lancer seulement une fois, meme si la classe fait >1 apelles
                lock (_connectionLock)
                {
                    if (_hasConnectedOnce) return;
                    _hasConnectedOnce = true;
                }

                // Send the song list
                Task.Run(async () =>
                {
                    try
                    {
                        // Wait or else problems
                        await Task.Delay(3000);

                        Console.WriteLine("Connected to Broker. Sending library...");
                        SendSongListToBroker(globalCommunicator, mqttTopic);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while sending song list on connect: {ex.Message}");
                    }
                    finally
                    {
                        // Stop connection so can continue working in local
                        try { globalCommunicator.Stop(); } catch { }
                        broadcastFinished.TrySetResult(true);
                    }
                });
            };

            // Start communicator in background so startup doesn't block if broker is unreachable
            Console.WriteLine("Initializing BitStream...");

            Task.Run(() =>
            {
                try
                {
                    globalCommunicator.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: MQTT communicator failed to start: {ex.Message}");
                    broadcastFinished.TrySetResult(false);
                }
            });

            // WAIT here until timeout or done
            var completedTask = await Task.WhenAny(broadcastFinished.Task, Task.Delay(10000));

            if (completedTask != broadcastFinished.Task)
            {
                Console.WriteLine("Connection timed out. Skipping broadcast...");
                try { globalCommunicator.Stop(); } catch { }
            }
            else
            {
                Console.WriteLine("Initialization complete.");
            }

            Console.WriteLine(); // Spacing

            // Liste
            var argsList = args ?? Array.Empty<string>();

            if (argsList.Length == 0 || argsList[0] == "-h" || argsList[0] == "--help")
            {
                ShowHelp();
                return;
            }

            // met tout en lowercase pour eviter erreures possibles
            var cmd = argsList[0].ToLowerInvariant();

            switch (cmd)
            {
                case "local":
                    HandleLocal(SubArray(argsList, 1));
                    break;
                case "remote":
                    HandleRemote(SubArray(argsList, 1));
                    break;
                // En cas de erreure user, show help
                default:
                    Console.WriteLine($"Unknown command: {cmd}");
                    ShowHelp();
                    break;
            }
        }

        // Volé de stackoverflow
        static string[] SubArray(string[] arr, int start)
        {
            if (arr.Length <= start)
            {
                return Array.Empty<string>();
            }
            var len = arr.Length - start;
            var res = new string[len];
            Array.Copy(arr, start, res, 0, len);
            return res;
        }

        // Sub switch pour commandes en dessous de local
        static void HandleLocal(string[] a)
        {
            if (a.Length == 0)
            {
                Console.WriteLine("local: missing subcommand (list|play|folder)");
                return;
            }

            switch (a[0].ToLowerInvariant())
            {
                case "list":
                    LocalList();
                    break;
                case "play":
                    if (a.Length < 2 || !int.TryParse(a[1], out var id))
                    {
                        Console.WriteLine("Usage: local play <id>");
                        return;
                    }
                    LocalPlay(id);
                    break;
                case "broadcast":
                    LocalBroadcast(SubArray(a, 1));
                    break;
                case "folder":
                    if (a.Length == 1 || a[1].ToLowerInvariant() == "show")
                    {
                        LocalFolderShow();
                        return;
                    }
                    if (a[1].ToLowerInvariant() == "set")
                    {
                        if (a.Length < 3)
                        {
                            Console.WriteLine("Usage: local folder set <path>");
                            return;
                        }
                        LocalFolderSet(a[2]);
                        return;
                    }
                    Console.WriteLine("local folder: unknown subcommand");
                    break;
                default:
                    Console.WriteLine($"local: unknown subcommand {a[0]}");
                    break;
            }
        }

        // Sub switch pour commandes en dessous de remote
        static void HandleRemote(string[] a)
        {
            if (a.Length == 0)
            {
                Console.WriteLine("remote: missing subcommand (list|catalog|import)");
                return;
            }

            switch (a[0].ToLowerInvariant())
            {
                case "list":
                    RemoteList();
                    break;
                case "catalog":
                    if (a.Length < 2 || !int.TryParse(a[1], out var id))
                    {
                        Console.WriteLine("Usage: remote catalog <id>");
                        return;
                    }
                    RemoteCatalog(id);
                    break;
                case "import":
                    int? node = null;
                    int? song = null;
                    for (int i = 1; i < a.Length; i++)
                    {
                        var token = a[i].ToLowerInvariant();
                        if ((token == "--node" || token == "-node") && i + 1 < a.Length && int.TryParse(a[i + 1], out var n))
                        {
                            node = n;
                            i++;
                        }
                        else if ((token == "--song" || token == "-song") && i + 1 < a.Length && int.TryParse(a[i + 1], out var s))
                        {
                            song = s;
                            i++;
                        }
                        else
                        {
                            Console.WriteLine($"Unknown option for import: {a[i]}");
                        }
                    }
                    RemoteImport(node, song);
                    break;
                default:
                    Console.WriteLine($"remote: unknown subcommand {a[0]}");
                    break;
            }
        }

        static void LocalList()
        {
            string[] extensions = new[] { "*.mp3", "*.wav" };
            var files = extensions.SelectMany(ext => EnumerateFilesSafe(Globals.Path, ext));
            int idx = 0;
            foreach (var file in files)
            {
                idx++;
                ISong s = Song.FromFile(file, idx);

                //Format size
                long size = s.Size;
                string sizeHuman;
                if (size >= 1 << 20) sizeHuman = $"{(size / (double)(1 << 20)):0.##} MB";
                else if (size >= 1 << 10) sizeHuman = $"{(size / (double)(1 << 10)):0.##} KB";
                else sizeHuman = $"{size} B";

                string length = s.Duration.TotalHours >= 1 ? s.Duration.ToString(@"hh\:mm\:ss") : s.Duration.ToString(@"mm\:ss");

                Console.WriteLine($"[{idx}] Title: {s.Title} | Album: {s.Album} | Size: {sizeHuman} | Length: {length} | Artist: {s.Artist}");
            }
        }

        // Enumerate files recursively but skip directories we can't access.
        static IEnumerable<string> EnumerateFilesSafe(string root, string searchPattern)
        {
            var dirs = new Stack<string>();
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                var current = dirs.Pop();

                string[] files = Array.Empty<string>();
                try
                {
                    files = Directory.GetFiles(current, searchPattern);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                foreach (var f in files)
                    yield return f;

                string[] subdirs = Array.Empty<string>();
                try
                {
                    subdirs = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                foreach (var d in subdirs)
                    dirs.Push(d);
            }
        }

        static void LocalPlay(int id) { }

        static void LocalBroadcast(string[] a)
        {
            // local broadcast kept for manual trigger, but prefer automatic broker broadcast on connect
            string broker = (a.Length >= 1 && !string.IsNullOrWhiteSpace(a[0])) ? a[0] : "localhost";
            string topic = (a.Length >= 2 && !string.IsNullOrWhiteSpace(a[1])) ? a[1] : "BitRuisseau";
            var nodeId = Environment.MachineName;

            Console.WriteLine($"Broadcasting local songs to MQTT broker '{broker}' on topic '{topic}'...");

            var communicator = new MqttCommunicator(broker, nodeId, topic);
            try
            {
                communicator.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start MQTT communicator: {ex.Message}");
                return;
            }

            string[] extensions = new[] { "*.mp3", "*.wav" };
            var files = extensions.SelectMany(ext => EnumerateFilesSafe(Globals.Path, ext));
            int idx = 0;
            var list = new List<ISong>();
            foreach (var file in files)
            {
                idx++;
                ISong s = Song.FromFile(file, idx);
                list.Add(s);
            }

            var message = new Message
            {
                Sender = nodeId,
                Recipient = "all",
                Action = "songlist",
                SongList = list
            };

            try
            {
                communicator.Send(message, topic);
                Console.WriteLine($"Broadcasted {list.Count} songs.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex.Message}");
            }
            finally
            {
                communicator.Stop();
            }
        }

        static void SendSongListToBroker(MqttCommunicator communicator, string topic)
        {
            string[] extensions = new[] { "*.mp3", "*.wav" };
            var files = extensions.SelectMany(ext => EnumerateFilesSafe(Globals.Path, ext));
            int idx = 0;
            var list = new List<ISong>();
            foreach (var file in files)
            {
                idx++;
                ISong s = Song.FromFile(file, idx);
                list.Add(s);
            }

            var message = new Message
            {
                Sender = Environment.MachineName,
                Recipient = "all",
                Action = "songlist",
                SongList = list
            };

            communicator.Send(message, topic);
            Console.WriteLine($"Sent {list.Count} songs to broker on topic '{topic}'.");
        }

        static void LocalFolderShow()
        {
            Process.Start("explorer.exe", Globals.Path);
        }

        static void LocalFolderSet(string path)
        {
            Globals.Path = "C:\\Users\\" + Environment.UserName + "\\" + path;
            Console.WriteLine(Globals.Path);
        }

        static void RemoteList() { }

        static void RemoteCatalog(int id) { }

        static void RemoteImport(int? node, int? song) { }

        static void ShowHelp()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("                    BitStream                    ");
            Console.WriteLine("=================================================\n");
            Console.WriteLine("Usage:");
            Console.WriteLine("  bitstream <command> [options]\n");
            Console.WriteLine("Available Commands:");
            Console.WriteLine("  Local Media Library:");
            Console.WriteLine("    local list                            List all local audio files");
            Console.WriteLine("    local play <id>                       Play a local audio file by ID");
            Console.WriteLine("    local broadcast [broker] [topic]      Broadcast local song metadata to MQTT broker");
            Console.WriteLine("    local folder show                    Show the current local media folder");
            Console.WriteLine("    local folder set <path>               Set the local media folder path\n");
            Console.WriteLine("  Remote Media Libraries:");
            Console.WriteLine("    remote list                           List available remote nodes");
            Console.WriteLine("    remote catalog <id>                   View the catalog of a specific remote node");
            Console.WriteLine("    remote import --node <id> --song <id> Import a media file from a remote node\n");
            Console.WriteLine("  General:");
            Console.WriteLine("    -h, --help                            Show this help message");
            Console.WriteLine("=================================================");
        }
    }
}