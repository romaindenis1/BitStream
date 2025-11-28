using System;
using System.IO;
using TagLib;

namespace BitRuisseau
{
    public class Song : ISong
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Year { get; set; }
        public TimeSpan Duration { get; set; }
        public int Size { get; set; }
        public string[] Featuring { get; set; }
        public string Hash { get; private set; }

        private Song()
        {
            Title = string.Empty;
            Artist = string.Empty;
            Album = string.Empty;
            Year = 0;
            Duration = TimeSpan.Zero;
            Size = 0;
            Featuring = Array.Empty<string>();
            Hash = string.Empty;
        }

        public static ISong FromFile(string path, int indexForArtist = -1)
        {
            var s = new Song();
            var fi = new FileInfo(path);
            s.Size = (int)Math.Min(int.MaxValue, fi.Length);

            // Defaults
            s.Title = Path.GetFileNameWithoutExtension(path);
            s.Album = new DirectoryInfo(Path.GetDirectoryName(path) ?? string.Empty).Name;

            try
            {
                using var tf = TagLib.File.Create(path);
                if (!string.IsNullOrWhiteSpace(tf.Tag.Title)) s.Title = tf.Tag.Title;
                if (!string.IsNullOrWhiteSpace(tf.Tag.Album)) s.Album = tf.Tag.Album;
                if (tf.Tag.Performers != null && tf.Tag.Performers.Length > 0) s.Featuring = tf.Tag.Performers;
                s.Duration = tf.Properties.Duration;
                if (tf.Tag.Year > 0) s.Year = (int)tf.Tag.Year;
            }
            catch
            {
                // ignore tag read errors
            }

            // Artist can be set from index if provided, otherwise try tag
            if (indexForArtist >= 0)
                s.Artist = indexForArtist.ToString("D2");
            else
                s.Artist = (s.Featuring.Length > 0) ? s.Featuring[0] : string.Empty;

            // Skip computing file hash here to keep listing fast; leave empty.
            s.Hash = string.Empty;

            return s;
        }
    }
}
