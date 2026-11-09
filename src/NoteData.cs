using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml.Serialization;

namespace Stickies_Window
{
    // One note's saved settings. The note's text is saved separately as <Id>.rtf
    public class NoteData
    {
        public string Id;
        public int X, Y, Width, Height;
        public int ColorIndex;
        public bool OnTop;
        public bool Translucent;
        public bool Collapsed;
        public DateTime Created;
    }

    // Everything saved in notes.xml
    public class StoreData
    {
        public List<NoteData> Notes = new List<NoteData>();
        public int DefaultColor = 0;
        public string DefaultFontName = "Segoe UI";
        public float DefaultFontSize = 11f;
    }

    public static class Store
    {
        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stickies_Window");

        static string IndexPath { get { return Path.Combine(Folder, "notes.xml"); } }

        public static string RtfPath(string id) { return Path.Combine(Folder, id + ".rtf"); }

        public static StoreData Load()
        {
            try
            {
                if (File.Exists(IndexPath))
                {
                    using (var s = File.OpenRead(IndexPath))
                    {
                        var d = (StoreData)new XmlSerializer(typeof(StoreData)).Deserialize(s);
                        if (d != null) return d;
                    }
                }
            }
            catch { }
            return new StoreData();
        }

        public static void Save(StoreData d)
        {
            Directory.CreateDirectory(Folder);
            string tmp = IndexPath + ".tmp";
            using (var s = File.Create(tmp))
                new XmlSerializer(typeof(StoreData)).Serialize(s, d);
            File.Copy(tmp, IndexPath, true);
            File.Delete(tmp);
        }

        public static void Delete(string id)
        {
            try { File.Delete(RtfPath(id)); } catch { }
        }
    }

    // The 6 macOS Stickies colors, softened
    public static class Palette
    {
        public static readonly string[] Names = { "Yellow", "Blue", "Green", "Pink", "Purple", "Gray" };

        public static readonly Color[] Colors =
        {
            Color.FromArgb(0xF3, 0xE4, 0xAA),
            Color.FromArgb(0xC9, 0xDB, 0xE8),
            Color.FromArgb(0xCF, 0xE0, 0xC3),
            Color.FromArgb(0xEE, 0xD3, 0xD3),
            Color.FromArgb(0xDC, 0xD3, 0xE8),
            Color.FromArgb(0xE2, 0xE0, 0xDC)
        };

        public static readonly Color Text = Color.FromArgb(0x3A, 0x35, 0x30);

        public static Color Get(int i)
        {
            if (i < 0 || i >= Colors.Length) i = 0;
            return Colors[i];
        }

        // Title bar: the note color, slightly darker
        public static Color Bar(int i)
        {
            Color c = Get(i);
            return Color.FromArgb(Mix(c.R, Text.R), Mix(c.G, Text.G), Mix(c.B, Text.B));
        }

        static int Mix(int a, int b) { return (int)(a * 0.92 + b * 0.08); }
    }
}
