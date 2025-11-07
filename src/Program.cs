
ShowHelp();




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
    Console.WriteLine("    local folder                          Show the current local media folder");
    Console.WriteLine("    local folder set <path>               Set the local media folder path\n");
    Console.WriteLine("  Remote Media Libraries:");
    Console.WriteLine("    remote list                           List available remote nodes");
    Console.WriteLine("    remote catalog <id>                   View the catalog of a specific remote node");
    Console.WriteLine("    remote import -node <id> -song <id>   Import a media file from a remote node\n");
    Console.WriteLine("  General:");
    Console.WriteLine("    -h                                    Show this help message");
    Console.WriteLine("    exit                                  Quit the application");
    Console.WriteLine("=================================================");
}
