using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stickies_Window
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        const string MutexName = "Stickies_Window-SingleInstance-7f3a";
        public const string ShowEventName = "Stickies_Window-ShowNotes-7f3a";

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool firstInstance;
            using (var mutex = new System.Threading.Mutex(true, MutexName, out firstInstance))
            {
                // Already running: ask that copy to show its notes
                if (!firstInstance)
                {
                    try
                    {
                        using (var ev = System.Threading.EventWaitHandle.OpenExisting(ShowEventName)) ev.Set();
                    }
                    catch
                    {
                        MessageBox.Show(
                            "Stickies_Window is already running but not responding.\n\n" +
                            "Open Task Manager, end \"Stickies_Window\", then open it again.",
                            "Stickies_Window", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                }

                // Any crash shows a message instead of closing silently
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => ShowError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

                try
                {
                    try { SetProcessDPIAware(); } catch { }
                    Application.Run(new NoteManager());
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        // Shows the error and saves it to error.txt in the notes folder
        static void ShowError(Exception ex)
        {
            string msg = ex == null ? "Unknown error" : ex.ToString();
            try
            {
                System.IO.Directory.CreateDirectory(Store.Folder);
                System.IO.File.WriteAllText(System.IO.Path.Combine(Store.Folder, "error.txt"), msg);
            }
            catch { }
            MessageBox.Show("Stickies_Window hit an error:\n\n" + msg, "Stickies_Window",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Owns all notes, the tray icon and saving
    class NoteManager : ApplicationContext
    {
        public static NoteManager Instance;
        public StoreData Data;

        readonly List<NoteForm> notes = new List<NoteForm>();
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer saveTimer;
        readonly Control uiThread = new Control();
        readonly System.Threading.EventWaitHandle showEvent;

        public NoteManager()
        {
            Instance = this;
            Data = Store.Load();

            // Opening the app again brings all notes to the front
            uiThread.CreateControl();
            showEvent = new System.Threading.EventWaitHandle(false,
                System.Threading.EventResetMode.AutoReset, Program.ShowEventName);
            System.Threading.ThreadPool.RegisterWaitForSingleObject(showEvent, (st, timedOut) =>
            {
                try { uiThread.BeginInvoke((Action)BringAllToFront); } catch { }
            }, null, -1, false);

            saveTimer = new System.Windows.Forms.Timer();
            saveTimer.Interval = 800;
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); SaveAll(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("New Note", null, (s, e) => NewNote());
            var arrange = new ToolStripMenuItem("Arrange By");
            AddArrangeItems(arrange);
            menu.Items.Add(arrange);
            menu.Items.Add("Bring All to Front", null, (s, e) => BringAllToFront());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Stickies_Window", null, (s, e) => Quit());

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "Stickies_Window";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) BringAllToFront(); };
            tray.Visible = true;

            foreach (var d in Data.Notes.ToList()) Open(d, false);
            if (notes.Count == 0) NewNote();

            // Save everything when Windows shuts down or you sign out
            Microsoft.Win32.SystemEvents.SessionEnding += (s, e) => SaveAll();
        }

        public NoteForm NewNote(string rtf = null, string plain = null)
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int offset = (notes.Count % 10) * NoteForm.S(24);
            var d = new NoteData();
            d.Id = Guid.NewGuid().ToString("N");
            d.Width = NoteForm.S(260);
            d.Height = NoteForm.S(220);
            d.X = wa.Left + NoteForm.S(80) + offset;
            d.Y = wa.Top + NoteForm.S(80) + offset;
            d.ColorIndex = Data.DefaultColor;
            d.Created = DateTime.Now;
            Data.Notes.Add(d);

            NoteForm f = Open(d, true);
            if (rtf != null) f.LoadRtf(rtf);
            else if (plain != null) f.LoadPlain(plain);
            f.Activate();
            QueueSave();
            return f;
        }

        NoteForm Open(NoteData d, bool isNew)
        {
            var f = new NoteForm(d, isNew);
            notes.Add(f);
            f.Show();
            return f;
        }

        // Deletes a note for good
        public void Remove(NoteForm f)
        {
            notes.Remove(f);
            Data.Notes.Remove(f.Data);
            Store.Delete(f.Data.Id);
            SaveIndex();
        }

        public void QueueSave()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        public void SaveAll()
        {
            foreach (var n in notes) n.SaveContent();
            SaveIndex();
        }

        void SaveIndex()
        {
            foreach (var n in notes) n.SyncData();
            try { Store.Save(Data); } catch { }
        }

        public void BringAllToFront()
        {
            foreach (var n in notes) { n.Show(); n.BringToFront(); }
            if (notes.Count > 0) notes[notes.Count - 1].Activate();
        }

        public void AddArrangeItems(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Add("Color", null, (s, e) =>
                Arrange((a, b) => a.Data.ColorIndex.CompareTo(b.Data.ColorIndex)));
            parent.DropDownItems.Add("Content", null, (s, e) =>
                Arrange((a, b) => string.Compare(a.FirstLine, b.FirstLine, StringComparison.CurrentCultureIgnoreCase)));
            parent.DropDownItems.Add("Date", null, (s, e) =>
                Arrange((a, b) => a.Data.Created.CompareTo(b.Data.Created)));
            parent.DropDownItems.Add("Position on Screen", null, (s, e) =>
                Arrange((a, b) => a.Top != b.Top ? a.Top.CompareTo(b.Top) : a.Left.CompareTo(b.Left)));
        }

        // Stacks notes from the top-left corner in the chosen order
        void Arrange(Comparison<NoteForm> order)
        {
            var list = notes.ToList();
            list.Sort(order);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int step = NoteForm.S(26);
            int x = wa.Left + NoteForm.S(20), y = wa.Top + NoteForm.S(20);
            foreach (var n in list)
            {
                if (y + NoteForm.S(60) > wa.Bottom) { y = wa.Top + NoteForm.S(20); x += NoteForm.S(60); }
                n.Location = new Point(x, y);
                n.BringToFront();
                x += step;
                y += step;
            }
            QueueSave();
        }

        public void Quit()
        {
            saveTimer.Stop();
            SaveAll();
            tray.Visible = false;
            tray.Dispose();
            foreach (var n in notes.ToList()) n.CloseSilently();
            ExitThread();
        }

        // A small yellow note for the tray
        static Icon MakeIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var path = NoteForm.RoundRect(new Rectangle(3, 3, 26, 26), 5))
                using (var b = new SolidBrush(Palette.Colors[0]))
                using (var p = new Pen(Color.FromArgb(120, Palette.Text), 1.5f))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
                using (var p = new Pen(Color.FromArgb(150, Palette.Text), 2f))
                {
                    g.DrawLine(p, 9, 13, 23, 13);
                    g.DrawLine(p, 9, 19, 19, 19);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}
