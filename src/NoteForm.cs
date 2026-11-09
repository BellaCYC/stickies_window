using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Stickies_Window
{
    // One sticky note window
    partial class NoteForm : Form
    {
        // ---------- screen scaling ----------
        static float dpiScale = 0f;
        static float DpiScale
        {
            get
            {
                if (dpiScale == 0f)
                    using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = g.DpiX / 96f;
                return dpiScale;
            }
        }
        public static int S(int v) { return (int)Math.Round(v * DpiScale); }

        // ---------- Windows API ----------
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        const int WM_NCHITTEST = 0x84, WM_NCLBUTTONDOWN = 0xA1, HTCLIENT = 1;
        public const int HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
            HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        // ---------- state ----------
        public readonly NoteData Data;
        readonly TitleBar bar;
        readonly NoteText text;
        bool dirty;
        bool closingSilently;
        int expandedHeight;

        public NoteForm(NoteData d, bool isNew)
        {
            Data = d;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Stickies_Window";
            MinimumSize = new Size(S(120), S(TitleBar.H));
            Padding = new Padding(S(6), 0, S(6), S(6));

            var store = NoteManager.Instance.Data;
            text = new NoteText();
            text.Note = this;
            text.Dock = DockStyle.Fill;
            text.BorderStyle = BorderStyle.None;
            text.DetectUrls = true;
            text.AcceptsTab = true;
            text.ScrollBars = RichTextBoxScrollBars.Vertical;
            text.ForeColor = Palette.Text;
            text.Font = MakeFont(store.DefaultFontName, store.DefaultFontSize);
            text.AllowDrop = true;
            text.EnableAutoDragDrop = false;
            text.TextChanged += (s, e) => { dirty = true; bar.Invalidate(); NoteManager.Instance.QueueSave(); };
            text.LinkClicked += (s, e) => { try { System.Diagnostics.Process.Start(e.LinkText); } catch { } };
            text.DragEnter += OnFileDragEnter;
            text.DragDrop += OnFileDrop;

            bar = new TitleBar(this);
            bar.Dock = DockStyle.Top;
            bar.Height = S(TitleBar.H);

            Controls.Add(text);
            Controls.Add(bar);

            Bounds = new Rectangle(d.X, d.Y, Math.Max(d.Width, S(120)), Math.Max(d.Height, S(60)));

            // If the note is off every screen, move it back onto the main screen
            bool onScreen = false;
            var grab = new Rectangle(Left, Top, Width, S(TitleBar.H));
            foreach (Screen sc in Screen.AllScreens)
                if (sc.WorkingArea.IntersectsWith(grab)) onScreen = true;
            if (!onScreen)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Left + S(80), wa.Top + S(80));
            }
            expandedHeight = Height;

            string path = Store.RtfPath(d.Id);
            if (!isNew && File.Exists(path))
            {
                try { text.LoadFile(path, RichTextBoxStreamType.RichText); } catch { }
            }
            dirty = false;

            ApplyColor();
            TopMost = d.OnTop;
            Opacity = d.Translucent ? 0.75 : 1.0;
            BuildMenu();
            if (d.Collapsed) SetCollapsed(true, false);

            Move += (s, e) => NoteManager.Instance.QueueSave();
            Resize += (s, e) => { if (!Data.Collapsed) expandedHeight = Height; NoteManager.Instance.QueueSave(); };
            Activated += (s, e) => bar.Invalidate();
            Deactivate += (s, e) => bar.Invalidate();
        }

        static Font MakeFont(string name, float size)
        {
            try { return new Font(name, size); }
            catch { return new Font(SystemFonts.MessageBoxFont.FontFamily, 11f); }
        }

        // Soft shadow + rounded corners (Windows 11)
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        }

        // Resize from the left, right and bottom edges
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                long lp = (long)m.LParam;
                var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int g = S(6);
                bool l = p.X < g, r = p.X >= Width - g, b = !Data.Collapsed && p.Y >= Height - g;
                if (b && l) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (b && r) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (l) m.Result = (IntPtr)HTLEFT;
                else if (r) m.Result = (IntPtr)HTRIGHT;
                else if (b) m.Result = (IntPtr)HTBOTTOM;
            }
        }

        // Called by the title bar to move or resize the window
        public void BeginNc(int hit)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)hit, IntPtr.Zero);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleKey(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- look ----------
        void ApplyColor()
        {
            BackColor = Palette.Get(Data.ColorIndex);
            text.BackColor = BackColor;
            bar.Invalidate();
        }

        public string FirstLine
        {
            get
            {
                foreach (string line in text.Lines)
                {
                    string t = line.Trim();
                    if (t.Length > 0) return t;
                }
                return "";
            }
        }

        // ---------- collapse ----------
        public void ToggleCollapse() { SetCollapsed(!Data.Collapsed, true); }

        void SetCollapsed(bool collapse, bool save)
        {
            if (collapse)
            {
                expandedHeight = Height;
                Data.Collapsed = true;
                text.Visible = false;
                Padding = new Padding(0);
                Height = S(TitleBar.H);
            }
            else
            {
                Data.Collapsed = false;
                Padding = new Padding(S(6), 0, S(6), S(6));
                text.Visible = true;
                Height = expandedHeight;
                text.Focus();
            }
            bar.Invalidate();
            if (save) NoteManager.Instance.QueueSave();
        }

        // ---------- saving ----------
        public void SyncData()
        {
            Data.X = Left;
            Data.Y = Top;
            Data.Width = Width;
            Data.Height = Data.Collapsed ? expandedHeight : Height;
        }

        public void SaveContent()
        {
            if (!dirty) return;
            try
            {
                Directory.CreateDirectory(Store.Folder);
                text.SaveFile(Store.RtfPath(Data.Id), RichTextBoxStreamType.RichText);
                dirty = false;
            }
            catch { }
        }

        public void LoadRtf(string rtf)
        {
            try { text.Rtf = rtf; } catch { text.Text = rtf; }
        }

        public void LoadPlain(string plain) { text.Text = plain; }

        // ---------- closing ----------
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (closingSilently || e.CloseReason != CloseReason.UserClosing)
            {
                SaveContent();
                base.OnFormClosing(e);
                return;
            }
            e.Cancel = true;
            CloseNote();
        }

        public void CloseSilently()
        {
            closingSilently = true;
            Close();
        }

        // Same as macOS Stickies: closing a note deletes it, so ask first
        public void CloseNote()
        {
            if (text.TextLength > 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "Do you want to save the contents of this note before closing it?\n\n" +
                    "If you don't save, the note's contents will be lost.",
                    "Stickies_Window", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes && !ExportText()) return;
            }
            NoteManager.Instance.Remove(this);
            dirty = false;
            closingSilently = true;
            Close();
        }

        // ---------- drag files in ----------
        void OnFileDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnFileDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            foreach (string f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                try
                {
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp")
                    {
                        using (var img = Image.FromFile(f)) InsertImage(new Bitmap(img));
                    }
                    else if (ext == ".rtf") text.SelectedRtf = File.ReadAllText(f);
                    else if (ext == ".txt") text.SelectedText = File.ReadAllText(f);
                }
                catch { }
            }
        }

        // Pastes an image, then puts the old clipboard back
        void InsertImage(Image img)
        {
            DataObject backup = null;
            try
            {
                IDataObject cur = Clipboard.GetDataObject();
                if (cur != null)
                {
                    backup = new DataObject();
                    foreach (string fmt in cur.GetFormats(false))
                    {
                        try { object o = cur.GetData(fmt); if (o != null) backup.SetData(fmt, o); } catch { }
                    }
                }
            }
            catch { }

            try { Clipboard.SetImage(img); text.Paste(); } catch { }
            try { if (backup != null) Clipboard.SetDataObject(backup, true); } catch { }
        }

        // ---------- shared drawing helper ----------
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // The note's text area; sends shortcuts to the note first
    class NoteText : RichTextBox
    {
        public NoteForm Note;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Note != null && Note.HandleKey(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // Thin title bar: close box (left), collapse box (right)
    class TitleBar : Control
    {
        public const int H = 22;
        static readonly Font TitleFont = new Font("Segoe UI", 9f);

        readonly NoteForm note;
        bool hover;
        Point downAt;
        bool maybeDrag;

        public TitleBar(NoteForm n)
        {
            note = n;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        Rectangle CloseBox
        {
            get { int s = NoteForm.S(11); return new Rectangle(NoteForm.S(7), (Height - s) / 2, s, s); }
        }

        Rectangle CollapseBox
        {
            get { int s = NoteForm.S(11); return new Rectangle(Width - NoteForm.S(7) - s, (Height - s) / 2, s, s); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Palette.Bar(note.Data.ColorIndex)))
                g.FillRectangle(b, ClientRectangle);

            // Like macOS Stickies, the boxes show when the note is active
            if (hover || note.ContainsFocus || Form.ActiveForm == note)
            {
                DrawBox(g, CloseBox);
                DrawBox(g, CollapseBox);
            }

            if (note.Data.Collapsed)
            {
                var r = Rectangle.FromLTRB(CloseBox.Right + NoteForm.S(8), 0, CollapseBox.Left - NoteForm.S(8), Height);
                TextRenderer.DrawText(g, note.FirstLine, TitleFont, r, Palette.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        static void DrawBox(Graphics g, Rectangle r)
        {
            using (var path = NoteForm.RoundRect(r, NoteForm.S(3)))
            using (var b = new SolidBrush(Color.FromArgb(45, Palette.Text)))
                g.FillPath(b, path);
        }

        int Edge(Point p)
        {
            int e = NoteForm.S(5);
            bool top = p.Y < e, l = p.X < e, r = p.X >= Width - e;
            if (note.Data.Collapsed)
            {
                if (l) return NoteForm.HTLEFT;
                if (r) return NoteForm.HTRIGHT;
                return 0;
            }
            if (top && l) return NoteForm.HTTOPLEFT;
            if (top && r) return NoteForm.HTTOPRIGHT;
            if (top) return NoteForm.HTTOP;
            if (l) return NoteForm.HTLEFT;
            if (r) return NoteForm.HTRIGHT;
            return 0;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (CloseBox.Contains(e.Location)) { note.CloseNote(); return; }
            if (CollapseBox.Contains(e.Location)) { note.ToggleCollapse(); return; }
            if (e.Clicks == 2) { maybeDrag = false; note.ToggleCollapse(); return; }

            int edge = Edge(e.Location);
            if (edge != 0) { note.BeginNc(edge); return; }

            // Start moving only after the mouse moves a little, so double-click still works
            downAt = e.Location;
            maybeDrag = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (maybeDrag && e.Button == MouseButtons.Left)
            {
                Size d = SystemInformation.DragSize;
                if (Math.Abs(e.X - downAt.X) > d.Width / 2 || Math.Abs(e.Y - downAt.Y) > d.Height / 2)
                {
                    maybeDrag = false;
                    note.BeginNc(NoteForm.HTCAPTION);
                }
                return;
            }

            int edge = Edge(e.Location);
            if (edge == NoteForm.HTLEFT || edge == NoteForm.HTRIGHT) Cursor = Cursors.SizeWE;
            else if (edge == NoteForm.HTTOP) Cursor = Cursors.SizeNS;
            else if (edge == NoteForm.HTTOPLEFT) Cursor = Cursors.SizeNWSE;
            else if (edge == NoteForm.HTTOPRIGHT) Cursor = Cursors.SizeNESW;
            else Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            maybeDrag = false;
        }
    }
}
