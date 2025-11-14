using System;

//Liste 
var argsList = args ?? Array.Empty<string>();

if (argsList.Length == 0 || argsList[0] == "-h" || argsList[0] == "--help")
{
    ShowHelp();
    return;
}
//met tout en lowercase pour eviter erreures possibles
var cmd = argsList[0].ToLowerInvariant();

switch (cmd)
{
    case "local":
        HandleLocal(SubArray(argsList, 1));
        break;
    case "remote":
        HandleRemote(SubArray(argsList, 1));
        break;
    //En cas de erreure user, show help
    default:
        Console.WriteLine($"Unknown command: {cmd}");
        ShowHelp();
        break;
}

//Volé de stackoverflow
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

//Sub switch pour commandes en dessous de local
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

//Sub switch pour commandes en dessous de remote
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
        /*
         * J'ai auncune idée si ca marche, TODO changer eventuellement
         * Juste du code placeholder from the top of my head
         * Les autres marchent par ce que c'est la meme syntaxe, celui la aucune idée
         */
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

static void LocalList() { Console.WriteLine("test"); }
static void LocalPlay(int id) { }
static void LocalFolderShow() { }
static void LocalFolderSet(string path) { }
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