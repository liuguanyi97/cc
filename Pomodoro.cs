using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Media;

namespace Pomodoro
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PomodoroForm());
        }
    }

    // ---- 设置（持久化到 %APPDATA%\Pomodoro\settings.txt）----
    public class Settings
    {
        private static string FilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pomodoro", "settings.txt");

        public int Focus = 25;
        public int Short = 5;
        public int Long = 15;
        public int Interval = 4;
        public bool AutoStart = false;
        public bool Sound = true;
        public bool Notify = true;
        public bool TopMost = true;
        public int WinX = -1;
        public int WinY = -1;
        public int TodayCount = 0;
        public string TodayKey = "";

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        int iv;
                        if (k == "Focus") { if (int.TryParse(v, out iv)) s.Focus = iv; }
                        else if (k == "Short") { if (int.TryParse(v, out iv)) s.Short = iv; }
                        else if (k == "Long") { if (int.TryParse(v, out iv)) s.Long = iv; }
                        else if (k == "Interval") { if (int.TryParse(v, out iv)) s.Interval = iv; }
                        else if (k == "AutoStart") s.AutoStart = v == "1";
                        else if (k == "Sound") s.Sound = v == "1";
                        else if (k == "Notify") s.Notify = v == "1";
                        else if (k == "TopMost") s.TopMost = v == "1";
                        else if (k == "WinX") { if (int.TryParse(v, out iv)) s.WinX = iv; }
                        else if (k == "WinY") { if (int.TryParse(v, out iv)) s.WinY = iv; }
                        else if (k == "TodayCount") { if (int.TryParse(v, out iv)) s.TodayCount = iv; }
                        else if (k == "TodayKey") s.TodayKey = v;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string[] lines = new string[]
                {
                    "Focus=" + Focus,
                    "Short=" + Short,
                    "Long=" + Long,
                    "Interval=" + Interval,
                    "AutoStart=" + (AutoStart ? "1" : "0"),
                    "Sound=" + (Sound ? "1" : "0"),
                    "Notify=" + (Notify ? "1" : "0"),
                    "TopMost=" + (TopMost ? "1" : "0"),
                    "WinX=" + WinX,
                    "WinY=" + WinY,
                    "TodayCount=" + TodayCount,
                    "TodayKey=" + TodayKey
                };
                File.WriteAllLines(FilePath, lines, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }

    // ---- 圆环倒计时面板 ----
    public class RingPanel : Control
    {
        public double Fraction = 1.0;
        public string TimeText = "25:00";
        public string PhaseLabel = "专注";
        public Color Accent = Color.FromArgb(231, 76, 60);
        private Color _track = Color.FromArgb(228, 232, 238);
        private Color _text = Color.FromArgb(44, 52, 64);
        private Color _muted = Color.FromArgb(150, 158, 172);

        public RingPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            int cx = w / 2, cy = h / 2;
            int size = Math.Min(w, h) - 20;
            Rectangle r = new Rectangle(cx - size / 2, cy - size / 2, size, size);
            Rectangle inner = Rectangle.Inflate(r, -8, -8);

            // 背景圆环
            using (Pen p = new Pen(_track, 12))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawEllipse(p, inner);
            }
            // 进度弧
            float sweep = (float)(360.0 * Fraction);
            if (sweep > 0)
            {
                using (Pen p = new Pen(Accent, 12))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, inner, -90f, sweep);
                }
            }

            // 时间文字
            using (Font f = new Font("Segoe UI", 28f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(_text))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                Rectangle tr = new Rectangle(r.X, cy - 44, size, 56);
                g.DrawString(TimeText, f, b, tr, sf);
            }
            // 阶段文字
            using (Font f2 = new Font("Microsoft YaHei", 9f))
            using (SolidBrush b2 = new SolidBrush(_muted))
            {
                StringFormat sf2 = new StringFormat();
                sf2.Alignment = StringAlignment.Center; sf2.LineAlignment = StringAlignment.Center;
                Rectangle lr = new Rectangle(r.X, cy + 32, size, 20);
                g.DrawString(PhaseLabel, f2, b2, lr, sf2);
            }
        }
    }

    // ---- 设置弹窗 ----
    public class SettingsForm : Form
    {
        private Settings _s;
        private NumericUpDown _focus, _short, _long, _interval;
        private CheckBox _auto, _sound, _notify, _top;
        private bool _ok;

        public SettingsForm(Settings s)
        {
            _s = s;
            Text = "设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei", 9f);

            int y = 16;
            _focus = AddNumRow("专注时长（分钟）", s.Focus, ref y, 1, 180);
            _short = AddNumRow("短休息（分钟）", s.Short, ref y, 1, 60);
            _long = AddNumRow("长休息（分钟）", s.Long, ref y, 1, 60);
            _interval = AddNumRow("长休息间隔（个）", s.Interval, ref y, 1, 12);
            y += 6;
            _auto = AddCheckRow("自动开始下一阶段", s.AutoStart, ref y);
            _sound = AddCheckRow("提示音", s.Sound, ref y);
            _notify = AddCheckRow("桌面通知", s.Notify, ref y);
            _top = AddCheckRow("窗口置顶", s.TopMost, ref y);
            y += 12;

            Button ok = new Button();
            ok.Text = "保存"; ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Color.FromArgb(231, 76, 60); ok.ForeColor = Color.White;
            ok.FlatAppearance.BorderSize = 0;
            ok.Location = new Point(20, y); ok.Size = new Size(110, 34);
            ok.Cursor = Cursors.Hand;
            ok.Click += delegate { _ok = true; Close(); };

            Button cancel = new Button();
            cancel.Text = "取消"; cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Color.FromArgb(236, 239, 243); cancel.ForeColor = Color.FromArgb(80, 88, 100);
            cancel.FlatAppearance.BorderSize = 0;
            cancel.Location = new Point(150, y); cancel.Size = new Size(110, 34);
            cancel.Cursor = Cursors.Hand;
            cancel.Click += delegate { Close(); };

            Controls.Add(ok);
            Controls.Add(cancel);
            ClientSize = new Size(280, y + 50);
        }

        private NumericUpDown AddNumRow(string text, int val, ref int y, int min, int max)
        {
            Label l = new Label();
            l.Text = text; l.AutoSize = true;
            l.Location = new Point(20, y + 3); l.ForeColor = Color.FromArgb(60, 66, 76);
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max; n.Value = val;
            n.Width = 70; n.Location = new Point(190, y);
            n.TextAlign = HorizontalAlignment.Center;
            Controls.Add(l); Controls.Add(n);
            y += 40;
            return n;
        }

        private CheckBox AddCheckRow(string text, bool val, ref int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text; c.Checked = val;
            c.AutoSize = true; c.Location = new Point(20, y);
            c.ForeColor = Color.FromArgb(60, 66, 76);
            Controls.Add(c);
            y += 28;
            return c;
        }

        public bool Apply()
        {
            if (!_ok) return false;
            _s.Focus = (int)_focus.Value;
            _s.Short = (int)_short.Value;
            _s.Long = (int)_long.Value;
            _s.Interval = (int)_interval.Value;
            _s.AutoStart = _auto.Checked;
            _s.Sound = _sound.Checked;
            _s.Notify = _notify.Checked;
            _s.TopMost = _top.Checked;
            return true;
        }
    }

    // ---- 主窗口 ----
    public class PomodoroForm : Form
    {
        private static string[] Modes = { "focus", "short", "long" };
        private static string[] ModeLabels = { "专注", "短休息", "长休息" };

        private Settings _s = Settings.Load();

        private string _mode = "focus";
        private int _total;
        private int _left;
        private bool _running;
        private DateTime _endTime;
        private Timer _timer;
        private int _focusCount = 0;
        private int _focusToday = 0;

        private Label _title;
        private Button _btnMin, _btnClose, _btnSettings;
        private Button[] _modeBtns = new Button[3];
        private RingPanel _ring;
        private Label _dots, _stat;
        private Button _btnReset, _btnToggle, _btnSkip;
        private NotifyIcon _tray;

        private Point _dragStart;
        private bool _dragging;

        public PomodoroForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(247, 248, 250);
            Font = new Font("Microsoft YaHei", 9f);
            Text = "番茄钟";
            KeyPreview = true;

            Color dark = Color.FromArgb(40, 48, 60);
            Color muted = Color.FromArgb(150, 158, 172);
            Color chip = Color.FromArgb(236, 239, 243);

            // 顶部标题
            _title = new Label();
            _title.Text = "番茄钟";
            _title.AutoSize = true;
            _title.Font = new Font("Microsoft YaHei", 11f, FontStyle.Bold);
            _title.ForeColor = dark;
            _title.Location = new Point(14, 8);
            Controls.Add(_title);
            AttachDrag(_title);

            // 右上角按钮
            _btnMin = MakeButton("—", 186, 4, 26, 26, chip, muted, delegate { HideToTray(); });
            _btnSettings = MakeButton("⚙", 218, 4, 26, 26, chip, muted, delegate { OpenSettings(); });
            _btnClose = MakeButton("×", 250, 4, 26, 26, chip, muted, delegate { ExitApp(); });
            _btnClose.ForeColor = Color.FromArgb(200, 60, 60);

            // 模式切换
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _modeBtns[i] = MakeButton(ModeLabels[i], 12 + i * 88, 44, 80, 30, chip, muted, delegate { SetMode(Modes[idx], false); });
            }

            // 圆环
            _ring = new RingPanel();
            _ring.Location = new Point(36, 84);
            _ring.Size = new Size(208, 208);
            Controls.Add(_ring);
            AttachDrag(_ring);

            // 循环进度点
            _dots = new Label();
            _dots.TextAlign = ContentAlignment.MiddleCenter;
            _dots.Font = new Font("Segoe UI", 11f);
            _dots.ForeColor = Color.FromArgb(231, 76, 60);
            _dots.Location = new Point(0, 296);
            _dots.Size = new Size(280, 20);
            Controls.Add(_dots);
            AttachDrag(_dots);

            // 今日统计
            _stat = new Label();
            _stat.TextAlign = ContentAlignment.MiddleCenter;
            _stat.ForeColor = muted;
            _stat.Location = new Point(0, 318);
            _stat.Size = new Size(280, 18);
            Controls.Add(_stat);
            AttachDrag(_stat);

            // 控制按钮
            _btnReset = MakeButton("重置", 14, 348, 64, 34, chip, dark, delegate { Reset(); });
            _btnToggle = MakeButton("开始", 92, 344, 96, 42, Color.FromArgb(231, 76, 60), Color.White, delegate { Toggle(); });
            _btnToggle.Font = new Font("Microsoft YaHei", 11f, FontStyle.Bold);
            _btnSkip = MakeButton("跳过", 202, 348, 64, 34, chip, dark, delegate { Skip(); });

            Size = new Size(280, 400);

            // 托盘
            BuildTray();

            // 快捷键
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space) { Toggle(); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.R) Reset();
                else if (e.KeyCode == Keys.S) Skip();
            };

            // 初始化今日计数
            string today = TodayKey();
            if (_s.TodayKey != today) { _s.TodayKey = today; _s.TodayCount = 0; }
            _focusToday = _s.TodayCount;

            // 恢复窗口位置
            if (_s.WinX >= 0 && _s.WinY >= 0)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                if (_s.WinX < wa.Right - 40 && _s.WinY < wa.Bottom - 40 && _s.WinX > wa.Left - 200 && _s.WinY > wa.Top - 40)
                    Location = new Point(_s.WinX, _s.WinY);
                else
                    StartPosition = FormStartPosition.CenterScreen;
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }

            TopMost = _s.TopMost;

            _timer = new Timer();
            _timer.Interval = 200;
            _timer.Tick += Tick;

            SetMode("focus", false);
        }

        // ---------- 控件辅助 ----------
        private Button MakeButton(string text, int x, int y, int w, int h, Color bg, Color fg, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = bg;
            b.BackColor = bg;
            b.ForeColor = fg;
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            b.Click += onClick;
            Controls.Add(b);
            return b;
        }

        private void AttachDrag(Control c)
        {
            c.MouseDown += DragDown;
            c.MouseMove += DragMove;
            c.MouseUp += DragUp;
        }
        private void DragDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
        }
        private void DragMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
                Location = new Point(Left + e.X - _dragStart.X, Top + e.Y - _dragStart.Y);
        }
        private void DragUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            _tray.Text = "番茄钟";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowFromTray(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示 / 隐藏", null, delegate { ToggleVisible(); });
            menu.Items.Add("开始 / 暂停", null, delegate { Toggle(); });
            menu.Items.Add("-");

            ToolStripMenuItem miTop = new ToolStripMenuItem("窗口置顶");
            miTop.CheckOnClick = true;
            miTop.Checked = _s.TopMost;
            miTop.Click += delegate { _s.TopMost = miTop.Checked; TopMost = _s.TopMost; _s.Save(); };
            menu.Items.Add(miTop);

            menu.Items.Add("设置", null, delegate { OpenSettings(); });
            menu.Items.Add("-");
            menu.Items.Add("退出", null, delegate { ExitApp(); });
            _tray.ContextMenuStrip = menu;
        }

        private void OpenSettings()
        {
            SettingsForm dlg = new SettingsForm(_s);
            dlg.ShowDialog(this);
            if (dlg.Apply())
            {
                _s.Save();
                TopMost = _s.TopMost;
                if (!_running) { _total = DurationOf(_mode); _left = _total; }
                UpdateDots();
                UpdateModeTabs();
                Render();
            }
        }

        // ---------- 核心流程 ----------
        private int DurationOf(string mode)
        {
            if (mode == "focus") return _s.Focus * 60;
            if (mode == "short") return _s.Short * 60;
            return _s.Long * 60;
        }
        private Color ColorOf(string mode)
        {
            if (mode == "focus") return Color.FromArgb(231, 76, 60);
            if (mode == "short") return Color.FromArgb(39, 174, 96);
            return Color.FromArgb(41, 128, 185);
        }
        private string LabelOf(string mode)
        {
            if (mode == "focus") return "专注";
            if (mode == "short") return "短休息";
            return "长休息";
        }

        private void SetMode(string mode, bool auto)
        {
            _mode = mode;
            _total = DurationOf(mode);
            _left = _total;
            StopTimer();
            UpdateModeTabs();
            _ring.Accent = ColorOf(mode);
            _ring.PhaseLabel = LabelOf(mode);
            _ring.Invalidate();
            Render();
            if (auto && _s.AutoStart) Start();
        }

        private void Start()
        {
            if (_running) return;
            _running = true;
            _endTime = DateTime.Now.AddSeconds(_left);
            _timer.Start();
            Render();
        }
        private void Pause()
        {
            if (!_running) return;
            _left = Math.Max(0, (int)Math.Round((_endTime - DateTime.Now).TotalSeconds));
            StopTimer();
            Render();
        }
        private void StopTimer()
        {
            _running = false;
            _timer.Stop();
        }
        private void Toggle() { if (_running) Pause(); else Start(); }
        private void Reset()
        {
            StopTimer();
            _left = _total;
            Render();
        }
        private void Skip() { OnEnd(); }

        private void Tick(object sender, EventArgs e)
        {
            _left = Math.Max(0, (int)Math.Round((_endTime - DateTime.Now).TotalSeconds));
            if (_left <= 0) OnEnd();
            else Render();
        }

        private void OnEnd()
        {
            string finished = _mode;
            StopTimer();
            PlayChime();
            if (finished == "focus")
            {
                _focusCount++;
                _focusToday++;
                SaveToday();
                string next = (_focusCount % _s.Interval == 0) ? "long" : "short";
                Notify("专注结束", "干得漂亮！进入" + LabelOf(next) + "吧");
                SetMode(next, true);
            }
            else
            {
                Notify("休息结束", "开始下一个专注吧");
                SetMode("focus", true);
            }
        }

        // ---------- 渲染 ----------
        private void Render()
        {
            _ring.TimeText = Fmt(_left);
            _ring.Fraction = _total > 0 ? (double)_left / _total : 0;
            _ring.Invalidate();
            _btnToggle.Text = _running ? "暂停" : (_left == _total ? "开始" : "继续");
            _btnToggle.BackColor = ColorOf(_mode);
            UpdateDots();
            _stat.Text = "今日完成 " + _focusToday + " 个番茄";
        }
        private void UpdateDots()
        {
            int n = _s.Interval;
            int filled = _mode == "long" ? n : (_focusCount % n);
            string s = "";
            for (int i = 0; i < n; i++) s += (i < filled) ? "●" : "○";
            _dots.Text = s;
            _dots.ForeColor = ColorOf(_mode);
        }
        private void UpdateModeTabs()
        {
            for (int i = 0; i < 3; i++)
            {
                bool active = Modes[i] == _mode;
                _modeBtns[i].BackColor = active ? ColorOf(_mode) : Color.FromArgb(236, 239, 243);
                _modeBtns[i].ForeColor = active ? Color.White : Color.FromArgb(120, 128, 140);
            }
        }
        private string Fmt(int sec)
        {
            int m = sec / 60, s = sec % 60;
            if (sec >= 3600)
            {
                int h = sec / 3600;
                return h + ":" + (m % 60).ToString("00") + ":" + s.ToString("00");
            }
            return m.ToString("00") + ":" + s.ToString("00");
        }

        // ---------- 声音 / 通知 ----------
        private void PlayChime()
        {
            if (!_s.Sound) return;
            try
            {
                int rate = 44100;
                int n = (int)(rate * 0.75);
                int[] freq = { 880, 660, 990 };
                short[] samples = new short[n];
                int per = n / 3;
                for (int i = 0; i < n; i++)
                {
                    int seg = i / per; if (seg > 2) seg = 2;
                    int pos = i % per;
                    double env = Math.Exp(-4.0 * pos / per);
                    double val = Math.Sin(2 * Math.PI * freq[seg] * pos / rate) * 0.35 * env;
                    samples[i] = (short)(val * 32767);
                }
                byte[] wav = BuildWav(samples, rate);
                using (MemoryStream ms = new MemoryStream(wav))
                using (SoundPlayer sp = new SoundPlayer(ms))
                {
                    sp.Play();
                }
            }
            catch { }
        }
        private byte[] BuildWav(short[] data, int rate)
        {
            int dataLen = data.Length * 2;
            using (MemoryStream ms = new MemoryStream())
            using (System.IO.BinaryWriter w = new System.IO.BinaryWriter(ms))
            {
                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataLen);
                w.Write(new char[] { 'W', 'A', 'V', 'E' });
                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(dataLen);
                foreach (short s in data) w.Write(s);
                w.Flush();
                return ms.ToArray();
            }
        }
        private void Notify(string title, string msg)
        {
            if (!_s.Notify) return;
            try { _tray.ShowBalloonTip(3000, title, msg, ToolTipIcon.Info); }
            catch { }
        }

        // ---------- 窗口 / 退出 ----------
        private void HideToTray() { Hide(); }
        private void ShowFromTray() { Show(); TopMost = _s.TopMost; Activate(); }
        private void ToggleVisible() { if (Visible) HideToTray(); else ShowFromTray(); }

        private void ExitApp()
        {
            SaveWindowPos();
            _s.Save();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            Application.Exit();
        }

        private void SaveWindowPos()
        {
            _s.WinX = Left;
            _s.WinY = Top;
        }
        private void SaveToday()
        {
            _s.TodayCount = _focusToday;
            _s.TodayKey = TodayKey();
            _s.Save();
        }
        private string TodayKey()
        {
            DateTime d = DateTime.Now;
            return d.Year + "-" + d.Month + "-" + d.Day;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveWindowPos();
            _s.Save();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        }
    }
}
