using Backend;
using BitRuisseau;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using TagLib.Id3v2;
using ProtocolMessage = BitRuisseau.Message;

namespace BitStream
{
    internal class Protocol : IProtocol
    {
        // 1. Déclaration des champs de classe (Pour _communicator et _senderHostname)
        private readonly MqttCommunicator _communicator;
        private readonly string _senderHostname;

        // 2. Déclaration de la constante (Pour GlobalRecipient)
        private const string GlobalRecipient = "0.0.0.0";

        // 3. Constructeur pour initialiser les champs
        public Protocol(MqttCommunicator communicator, string senderHostname)
        {
            _communicator = communicator;
            _senderHostname = senderHostname;
        }
        public List<ISong> AskCatalog(string name)
        {
            var tcs = new TaskCompletionSource<List<ISong>>();
            var prevHandler = _communicator.OnMessageReceived;

            _communicator.OnMessageReceived = (msg) =>
            {
                // Preserve any existing handler behavior
                try { prevHandler?.Invoke(msg); } catch { }

                // Handle sendCatalog from requested sender
                if (msg != null && string.Equals(msg.Action, "sendCatalog", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(msg.Sender, name, StringComparison.OrdinalIgnoreCase)
                    && msg.SongList != null)
                {
                    tcs.TrySetResult(msg.SongList);
                }
            };

            var ask = new ProtocolMessage
            {
                Recipient = name,
                Sender = _senderHostname,
                Action = "askCatalog",
                StartByte = null,
                EndByte = null,
                SongList = null,
                SongData = null,
                Hash = null
            };
            _communicator.Send(ask);

            // Wait up to 10 seconds for response
            var completed = Task.WhenAny(tcs.Task, Task.Delay(10000)).Result;

            // Restore previous handler
            _communicator.OnMessageReceived = prevHandler;

            if (completed == tcs.Task)
            {
                return tcs.Task.Result;
            }

            // No catalog received
            return new List<ISong>();
        }


        public void AskMedia(string name, int startByte, int endByte)
        {
            var message = new ProtocolMessage
            {
                Recipient = name,
                Sender = _senderHostname,
                Action = "askMedia",
                StartByte = startByte,
                EndByte = endByte,
                SongList = null,
                SongData = null,
                Hash = null
            };
            _communicator.Send(message);
            // TODO: wait and process 'sendMedia' response via OnMessageReceived.
        }

        public string[] GetOnlineMediatheque()
        {
            // 1. Envoyer le message askOnline
            var message = new ProtocolMessage
            {
                Recipient = GlobalRecipient,
                Sender = _senderHostname,
                Action = "askOnline",
                StartByte = null,
                EndByte = null,
                SongList = null,
                SongData = null,
                Hash = null
            };
            _communicator.Send(message);

            // 2. L'implémentation complète devrait attendre les réponses 'online' 
            //    via OnMessageReceived du MqttCommunicator.
            //    Pour l'instant, on retourne un tableau vide et on lève une exception pour signaler 
            //    que la partie réception n'est pas implémentée.
            throw new NotImplementedException("La gestion de la réception des messages 'online' et l'attente des réponses ne sont pas implémentées.");
        }

        public void SayOnline()
        {
            var message = new ProtocolMessage
            {
                Recipient = GlobalRecipient,
                Sender = _senderHostname,
                Action = "online",
                StartByte = null,
                EndByte = null,
                SongList = null,
                SongData = null,
                Hash = null
            };
            _communicator.Send(message);
        }
        private List<ISong> GetLocalCatalog()
        {
            string appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitRuisseau");
            string SavefileName = "data.txt";
            string SavefilePath = Path.Combine(appDirectory, SavefileName);

            List<ISong> catalog = new List<ISong>();
            string folderPath = SavefilePath;

            string[] files = Directory.GetFiles(folderPath, "*.mp3");
            foreach (string file in files)
            {
                try
                {
                    var tfile = TagLib.File.Create(file);
                    FileInfo info = new FileInfo(file);

                    string title = tfile.Tag.Title ?? Path.GetFileNameWithoutExtension(file);
                    string artist = tfile.Tag.FirstPerformer ?? "Inconnu";
                    int year = (int)tfile.Tag.Year;
                    TimeSpan duration = tfile.Properties.Duration;

                    // Taille en bytes
                    int sizeInBytes = (int)info.Length;

                    // Featuring en tableau
                    string[] featuring = tfile.Tag.Performers;

                    // Création de l'objet Song (use TagLib metadata)
                    ISong song = Song.FromFile(file);
                    catalog.Add(song);
                }
                catch
                {
                    continue;
                }
            }
            return catalog;
        }
        public void SendCatalog(string name)
        {
            var myLocalSongList = GetLocalCatalog();

            var message = new ProtocolMessage
            {
                Recipient = name, // Destinataire spécifique
                Sender = _senderHostname,
                Action = "sendCatalog",
                StartByte = null,
                EndByte = null,
                SongList = myLocalSongList, // Le catalogue de chansons
                SongData = null,
                Hash = null
            };
            _communicator.Send(message);
        }

        private string ReadAndEncodeMediaChunk(ISong song, int startByte, int endByte)
        {
            // TODO: Lire le fichier local correspondant au hash de la chanson,
            // extraire les bytes de startByte à endByte, et les encoder en Base64.
            // Simuler un morceau de données encodé en Base64
            return $"Base64Data_Hash:{song.Hash}_Start:{startByte}_End:{endByte}";
        }
        public void SendMedia(string name, int startByte, int endByte)
        {
            // TODO: need song context (hash + file path) to fulfill sendMedia fully.
            // Placeholder message without payload to satisfy interface.
            var message = new ProtocolMessage
            {
                Recipient = name,
                Sender = _senderHostname,
                Action = "sendMedia",
                StartByte = startByte,
                EndByte = endByte,
                SongList = null,
                SongData = null,
                Hash = null
            };
            _communicator.Send(message);
        }
    }
}