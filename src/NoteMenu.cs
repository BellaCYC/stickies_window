using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Stickies_Window
{
    // The note's right-click menu and all its commands
    partial class NoteForm
    {
        ToolStripMenuItem miOnTop, miTranslucent, miCollapse, miBold, miItalic, miUnderline;
        readonly ToolStripMenuItem[] miColors = new ToolStripMenuItem[6];
        string lastFind = "";

        static ToolStripMenuItem Item(string name, string keys, EventHandler click)
        {
            var i = new ToolStripMenuItem(name, null, click);
            if (keys != null) i.ShortcutKeyDisplayString = keys;
            return i;
        }

        void BuildMenu()
        {
            var m = new ContextMenuStrip();

            m.Items.Add(Item("New Note", "Ctrl+N", (s, e) => NoteManager.Instance.NewNote()));
            m.Items.Add(Item("Close", "Ctrl+W", (s, e) => CloseNote()));
            m.Items.Add(new ToolStripSeparator());

            var file = new ToolStripMenuItem("File");
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Import Text…", null, (s, e) => ImportText()),
                Item("Export Text…", null, (s, e) => ExportText()),
                new ToolStripSeparator(),
                Item("Print…", "Ctrl+P", (s, e) => PrintNote())
            });

            var edit = new ToolStripMenuItem("Edit");
            edit.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Undo", "Ctrl+Z", (s, e) => text.Undo()),
                Item("Redo", "Ctrl+Y", (s, e) => text.Redo()),
                new ToolStripSeparator(),
                Item("Cut", "Ctrl+X", (s, e) => text.Cut()),
                Item("Copy", "Ctrl+C", (s, e) => text.Copy()),
                Item("Paste", "Ctrl+V", (s, e) => text.Paste()),
                Item("Select All", "Ctrl+A", (s, e) => text.SelectAll()),
                new ToolStripSeparator(),
                Item("Find…", "Ctrl+F", (s, e) => Find()),
                Item("Find Next", "Ctrl+G", (s, e) => FindNext())
            });

            miBold = Item("Bold", "Ctrl+B", (s, e) => ToggleStyle(FontStyle.Bold));
            miItalic = Item("Italic", "Ctrl+I", (s, e) => ToggleStyle(FontStyle.Italic));
            miUnderline = Item("Underline", "Ctrl+U", (s, e) => ToggleStyle(FontStyle.Underline));
            var font = new ToolStripMenuItem("Font");
            font.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Show Fonts", "Ctrl+T", (s, e) => ShowFonts()),
                new ToolStripSeparator(),
                miBold, miItalic, miUnderline,
                new ToolStripSeparator(),
                Item("Bigger", "Ctrl++", (s, e) => ChangeSize(1)),
                Item("Smaller", "Ctrl+-", (s, e) => ChangeSize(-1)),
                new ToolStripSeparator(),
                Item("Show Colors", "Ctrl+Shift+C", (s, e) => ShowColors())
            });

            var align = new ToolStripMenuItem("Text");
            align.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Align Left", "Ctrl+L", (s, e) => text.SelectionAlignment = HorizontalAlignment.Left),
                Item("Center", "Ctrl+E", (s, e) => text.SelectionAlignment = HorizontalAlignment.Center),
                Item("Align Right", "Ctrl+R", (s, e) => text.SelectionAlignment = HorizontalAlignment.Right)
            });

            var color = new ToolStripMenuItem("Color");
            for (int i = 0; i < Palette.Colors.Length; i++)
            {
                int idx = i;
                miColors[i] = Item(Palette.Names[i], null, (s, e) => SetColor(idx));
                miColors[i].Image = Swatch(Palette.Colors[i]);
                color.DropDownItems.Add(miColors[i]);
            }

            miOnTop = Item("Float on Top", "Ctrl+Alt+F", (s, e) => ToggleOnTop());
            miTranslucent = Item("Translucent", "Ctrl+Alt+T", (s, e) => ToggleTranslucent());
            miCollapse = Item("Collapse", "Ctrl+M", (s, e) => ToggleCollapse());
            var arrange = new ToolStripMenuItem("Arrange By");
            NoteManager.Instance.AddArrangeItems(arrange);
            var window = new ToolStripMenuItem("Window");
            window.DropDownItems.AddRange(new ToolStripItem[]
            {
                miOnTop, miTranslucent,
                new ToolStripSeparator(),
                miCollapse,
                new ToolStripSeparator(),
                Item("Use as Default", null, (s, e) => UseAsDefault()),
                arrange,
                Item("Bring All to Front", null, (s, e) => NoteManager.Instance.BringAllToFront())
            });

            m.Items.AddRange(new ToolStripItem[] { file, edit, font, align, color, window });

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item("Quit Stickies_Window", null, (s, e) => NoteManager.Instance.Quit()));

            // Show ticks for the current settings
            m.Opening += (s, e) =>
            {
                miOnTop.Checked = TopMost;
                miTranslucent.Checked = Data.Translucent;
                miCollapse.Text = Data.Collapsed ? "Expand" : "Collapse";
                Font f = text.SelectionFont ?? text.Font;
                miBold.Checked = f.Bold;
                miItalic.Checked = f.Italic;
                miUnderline.Checked = f.Underline;
                for (int i = 0; i < miColors.Length; i++) miColors[i].Checked = (i == Data.ColorIndex);
            };

            ContextMenuStrip = m;
            text.ContextMenuStrip = m;
            bar.ContextMenuStrip = m;
        }

        static Bitmap Swatch(Color c)
        {
            var b = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(b))
            using (var br = new SolidBrush(c))
            using (var p = new Pen(Color.FromArgb(90, Palette.Text)))
            {
                g.FillRectangle(br, 1, 1, 14, 14);
                g.DrawRectangle(p, 1, 1, 13, 13);
            }
            return b;
        }

        // ---------- keyboard shortcuts ----------
        public bool HandleKey(Keys k)
        {
            switch (k)
            {
                case Keys.Control | Keys.N: NoteManager.Instance.NewNote(); return true;
                case Keys.Control | Keys.W: CloseNote(); return true;
                case Keys.Control | Keys.P: PrintNote(); return true;
                case Keys.Control | Keys.Y: text.Redo(); return true;
                case Keys.Control | Keys.F: Find(); return true;
                case Keys.Control | Keys.G: FindNext(); return true;
                case Keys.Control | Keys.T: ShowFonts(); return true;
                case Keys.Control | Keys.B: ToggleStyle(FontStyle.Bold); return true;
                case Keys.Control | Keys.I: ToggleStyle(FontStyle.Italic); return true;
                case Keys.Control | Keys.U: ToggleStyle(FontStyle.Underline); return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Shift | Keys.Oemplus:
                case Keys.Control | Keys.Add: ChangeSize(1); return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract: ChangeSize(-1); return true;
                case Keys.Control | Keys.Shift | Keys.C: ShowColors(); return true;
                case Keys.Control | Keys.L: text.SelectionAlignment = HorizontalAlignment.Left; return true;
                case Keys.Control | Keys.E: text.SelectionAlignment = HorizontalAlignment.Center; return true;
                case Keys.Control | Keys.R: text.SelectionAlignment = HorizontalAlignment.Right; return true;
                case Keys.Control | Keys.Alt | Keys.F: ToggleOnTop(); return true;
                case Keys.Control | Keys.Alt | Keys.T: ToggleTranslucent(); return true;
                case Keys.Control | Keys.M: ToggleCollapse(); return true;
            }
            return false;
        }

        // ---------- Color / Window ----------
        void SetColor(int i)
        {
            Data.ColorIndex = i;
            ApplyColor();
            NoteManager.Instance.QueueSave();
        }

        void ToggleOnTop()
        {
            Data.OnTop = !Data.OnTop;
            TopMost = Data.OnTop;
            NoteManager.Instance.QueueSave();
        }

        void ToggleTranslucent()
        {
            Data.Translucent = !Data.Translucent;
            Opacity = Data.Translucent ? 0.75 : 1.0;
            NoteManager.Instance.QueueSave();
        }

        // New notes will use this note's color and font
        void UseAsDefault()
        {
            var store = NoteManager.Instance.Data;
            Font f = text.SelectionFont ?? text.Font;
            store.DefaultColor = Data.ColorIndex;
            store.DefaultFontName = f.Name;
            store.DefaultFontSize = f.Size;
            NoteManager.Instance.QueueSave();
        }

        // ---------- Font ----------
        void ToggleStyle(FontStyle style)
        {
            Font f = text.SelectionFont ?? text.Font;
            try { text.SelectionFont = new Font(f, f.Style ^ style); } catch { }
        }

        void ChangeSize(float delta)
        {
            Font f = text.SelectionFont ?? text.Font;
            float size = Math.Max(6f, Math.Min(96f, f.Size + delta));
            try { text.SelectionFont = new Font(f.FontFamily, size, f.Style); } catch { }
        }

        void ShowFonts()
        {
            using (var dlg = new FontDialog())
            {
                dlg.Font = text.SelectionFont ?? text.Font;
                dlg.ShowColor = true;
                dlg.Color = text.SelectionColor;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    text.SelectionFont = dlg.Font;
                    text.SelectionColor = dlg.Color;
                }
            }
        }

        void ShowColors()
        {
            using (var dlg = new ColorDialog())
            {
                dlg.Color = text.SelectionColor;
                dlg.FullOpen = true;
                if (dlg.ShowDialog(this) == DialogResult.OK) text.SelectionColor = dlg.Color;
            }
        }

        // ---------- Find ----------
        void Find()
        {
            string s = Prompt.Show(this, "Find", lastFind);
            if (s == null) return;
            lastFind = s;
            FindNext();
        }

        void FindNext()
        {
            if (lastFind.Length == 0) { Find(); return; }
            int start = Math.Min(text.SelectionStart + text.SelectionLength, text.TextLength);
            int i = text.Find(lastFind, start, RichTextBoxFinds.None);
            if (i < 0 && start > 0) i = text.Find(lastFind, 0, RichTextBoxFinds.None);
            if (i < 0) System.Media.SystemSounds.Beep.Play();
            else text.ScrollToCaret();
        }

        // ---------- File ----------
        void ImportText()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Text files (*.rtf;*.txt)|*.rtf;*.txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string content = File.ReadAllText(dlg.FileName);
                    if (Path.GetExtension(dlg.FileName).ToLowerInvariant() == ".rtf")
                        NoteManager.Instance.NewNote(content, null);
                    else
                        NoteManager.Instance.NewNote(null, content);
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Stickies_Window"); }
            }
        }

        public bool ExportText()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Rich Text (*.rtf)|*.rtf|Plain Text (*.txt)|*.txt";
                dlg.FileName = SafeName(FirstLine);
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                try
                {
                    if (dlg.FilterIndex == 2) File.WriteAllText(dlg.FileName, text.Text, Encoding.UTF8);
                    else text.SaveFile(dlg.FileName, RichTextBoxStreamType.RichText);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Stickies_Window");
                    return false;
                }
            }
        }

        static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, ' ');
            s = s.Trim();
            if (s.Length > 40) s = s.Substring(0, 40).Trim();
            return s.Length == 0 ? "Untitled" : s;
        }

        void PrintNote()
        {
            string all = text.Text;
            int pos = 0;
            Font f = text.Font;
            using (var doc = new System.Drawing.Printing.PrintDocument())
            using (var dlg = new PrintDialog())
            {
                doc.DocumentName = SafeName(FirstLine);
                doc.BeginPrint += (s, e) => pos = 0;
                doc.PrintPage += (s, e) =>
                {
                    string rest = all.Substring(pos);
                    int chars, lines;
                    e.Graphics.MeasureString(rest, f, e.MarginBounds.Size, StringFormat.GenericTypographic, out chars, out lines);
                    if (chars <= 0) { e.HasMorePages = false; return; }
                    e.Graphics.DrawString(rest.Substring(0, chars), f, Brushes.Black, e.MarginBounds, StringFormat.GenericTypographic);
                    pos += chars;
                    e.HasMorePages = pos < all.Length;
                };
                dlg.Document = doc;
                dlg.UseEXDialog = true;
                if (dlg.ShowDialog(this) == DialogResult.OK) doc.Print();
            }
        }
    }

    // Small text box dialog used by Find
    static class Prompt
    {
        public static string Show(IWin32Window owner, string title, string value)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.TopMost = true;
                f.AutoScaleMode = AutoScaleMode.Font;
                f.ClientSize = new Size(NoteForm.S(300), NoteForm.S(80));

                var box = new TextBox();
                box.Text = value;
                box.SetBounds(NoteForm.S(12), NoteForm.S(12), NoteForm.S(276), NoteForm.S(24));

                var ok = new Button();
                ok.Text = "Find";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(NoteForm.S(132), NoteForm.S(44), NoteForm.S(75), NoteForm.S(26));

                var cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(NoteForm.S(213), NoteForm.S(44), NoteForm.S(75), NoteForm.S(26));

                f.Controls.AddRange(new Control[] { box, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
            }
        }
    }
}
