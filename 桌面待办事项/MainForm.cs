using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace DesktopTodo
{
    public class TaskItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Done { get; set; }
        public string Summary { get; set; }
        public long CreatedAtMs { get; set; }
        public long? CompletedAtMs { get; set; }
        public long AccumulatedMs { get; set; }
        public bool Running { get; set; }
        public long? StartedAtMs { get; set; }
    }

    public class AppData
    {
        public Dictionary<string, List<TaskItem>> Days { get; set; } = new Dictionary<string, List<TaskItem>>();
        public int WinX { get; set; }
        public int WinY { get; set; }
        public int WinW { get; set; }
        public int WinH { get; set; }
    }

    public class MainForm : Form
    {
        // ---- 主题色：红 / 蓝 / 白 ----
        static readonly Color Red = Color.FromArgb(229, 57, 53);
        static readonly Color RedDeep = Color.FromArgb(198, 40, 40);
        static readonly Color RedSoft = Color.FromArgb(253, 236, 234);
        static readonly Color Blue = Color.FromArgb(21, 101, 192);
        static readonly Color BlueMid = Color.FromArgb(30, 136, 229);
        static readonly Color BlueDeep = Color.FromArgb(13, 71, 161);
        static readonly Color BlueSoft = Color.FromArgb(232, 240, 254);
        static readonly Color Ink = Color.FromArgb(31, 36, 48);
        static readonly Color Ink2 = Color.FromArgb(107, 114, 128);
        static readonly Color Ink3 = Color.FromArgb(154, 163, 178);
        static readonly Color Line = Color.FromArgb(230, 233, 240);
        static readonly Color Bg = Color.FromArgb(244, 246, 251);

        const int ROW_H = 56;
        const int ROW_H_EXPANDED = 156;

        // ---- 数据 ----
        AppData data;
        string dataFile;
        string currentDate;
        readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };

        // ---- 计时 ----
        readonly System.Windows.Forms.Timer tickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly Dictionary<string, Label> timeLabelById = new Dictionary<string, Label>();
        readonly HashSet<string> expandedIds = new HashSet<string>();

        // ---- UI ----
        Panel titleBar;
        Panel taskPanel;
        Panel progressTrack;
        Panel progressFill;
        DateTimePicker datePicker;
        TextBox newTaskBox;
        Label doneLabel;
        Label historyBadge;
        Label statusLabel;
        Label titleLabel;
        Label hintLabel;
        Button closeBtn, minBtn;
        bool dragging; Point dragOffset;
        bool ready = false;
        const int grip = 6;

        public MainForm()
        {
            LoadData();
            currentDate = TodayKey();
            BuildUi();
            ApplyWindowBounds();
            datePicker.Value = DateTime.Today;
            RenderTasks();
            ready = true;
            tickTimer.Tick += (s, e) => UpdateTimeLabels();
            tickTimer.Start();
        }

        /* ================= 存储 ================= */
        void LoadData()
        {
            dataFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "桌面待办事项", "data.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dataFile));
                if (File.Exists(dataFile))
                    data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(dataFile), jsonOpts) ?? new AppData();
                else
                    data = new AppData();
            }
            catch { data = new AppData(); }
        }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dataFile));
                File.WriteAllText(dataFile, JsonSerializer.Serialize(data, jsonOpts));
            }
            catch { }
        }

        /* ================= 工具 ================= */
        string TodayKey() => DateTime.Today.ToString("yyyy-MM-dd");
        string KeyOf(DateTime d) => d.ToString("yyyy-MM-dd");
        DateTime ParseKey(string k) => DateTime.ParseExact(k, "yyyy-MM-dd", null);
        long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        List<TaskItem> TasksFor(string key)
        {
            if (!data.Days.TryGetValue(key, out var list)) { list = new List<TaskItem>(); data.Days[key] = list; }
            return list;
        }

        TaskItem FindTask(string id) => TasksFor(currentDate).FirstOrDefault(x => x.Id == id);

        long Elapsed(TaskItem t)
        {
            long ms = t.AccumulatedMs;
            if (t.Running && t.StartedAtMs.HasValue) ms += NowMs() - t.StartedAtMs.Value;
            return ms;
        }

        string FormatMs(long ms)
        {
            var ts = TimeSpan.FromMilliseconds(ms);
            if (ts.TotalHours >= 1) return string.Format("{0}:{1:00}:{2:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            return string.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
        }

        string FormatClock(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString("HH:mm");

        void StopAllRunning()
        {
            foreach (var t in TasksFor(currentDate))
                if (t.Running)
                {
                    t.AccumulatedMs += NowMs() - (t.StartedAtMs ?? NowMs());
                    t.Running = false;
                    t.StartedAtMs = null;
                }
        }

        /* ================= 操作 ================= */
        void AddTask()
        {
            var title = newTaskBox.Text.Trim();
            if (title.Length == 0) return;
            TasksFor(currentDate).Add(new TaskItem { Id = Guid.NewGuid().ToString("N"), Title = title, CreatedAtMs = NowMs() });
            newTaskBox.Clear();
            Save();
            RenderTasks();
        }

        void ToggleDone(string id)
        {
            var t = FindTask(id); if (t == null) return;
            t.Done = !t.Done;
            if (t.Done)
            {
                if (t.Running) { t.AccumulatedMs += NowMs() - (t.StartedAtMs ?? NowMs()); t.Running = false; t.StartedAtMs = null; }
                t.CompletedAtMs = NowMs();
            }
            else t.CompletedAtMs = null;
            Save();
            RenderTasks();
        }

        void DeleteTask(string id)
        {
            data.Days[currentDate] = TasksFor(currentDate).Where(x => x.Id != id).ToList();
            Save();
            RenderTasks();
        }

        void ToggleTimer(string id)
        {
            var t = FindTask(id); if (t == null || t.Done) return;
            if (t.Running)
            {
                t.AccumulatedMs += NowMs() - (t.StartedAtMs ?? NowMs());
                t.Running = false; t.StartedAtMs = null;
            }
            else
            {
                StopAllRunning();
                t.Running = true; t.StartedAtMs = NowMs();
            }
            Save();
            RenderTasks();
        }

        void ToggleExpand(string id)
        {
            if (!expandedIds.Add(id)) expandedIds.Remove(id);
            RenderTasks();
        }

        /* ================= 界面 ================= */
        void BuildUi()
        {
            Text = "桌面待办事项";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Bg;
            DoubleBuffered = true;
            MinimumSize = new Size(360, 420);
            Font = new Font("Segoe UI", 9f);

            // 标题栏
            titleBar = new Panel { Dock = DockStyle.Top, Height = 40, Cursor = Cursors.SizeAll, BackColor = BlueDeep };
            titleBar.Paint += (s, e) =>
            {
                var r = titleBar.ClientRectangle;
                using (var b = new LinearGradientBrush(r, BlueDeep, BlueMid, 45f))
                    e.Graphics.FillRectangle(b, r);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var sb = new SolidBrush(Color.FromArgb(255, 82, 82)))
                    e.Graphics.FillEllipse(sb, 13, 15, 10, 10);
            };
            titleLabel = new Label { Text = "桌面待办事项", ForeColor = Color.White, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), BackColor = Color.Transparent, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            hintLabel = new Label { Text = "拖动标题栏 · 拖边角缩放", ForeColor = Color.FromArgb(210, 255, 255, 255), Font = new Font("Segoe UI", 8.5f), BackColor = Color.Transparent, AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
            closeBtn = MakeTitleButton("✕");
            closeBtn.Click += (s, e) => Close();
            minBtn = MakeTitleButton("—");
            minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(hintLabel);
            titleBar.Controls.Add(minBtn);
            titleBar.Controls.Add(closeBtn);
            titleBar.Resize += (s, e) => LayoutTitleBar();
            titleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragOffset = e.Location; } };
            titleBar.MouseMove += (s, e) => { if (dragging) Location = new Point(MousePosition.X - dragOffset.X, MousePosition.Y - dragOffset.Y); };
            titleBar.MouseUp += (s, e) => dragging = false;

            // 日期栏
            var dateBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 4, Padding = new Padding(12, 7, 12, 7), BackColor = Bg };
            dateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            dateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            dateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            var prevBtn = MakeIconButton("‹", BlueSoft, Blue, 12f);
            var nextBtn = MakeIconButton("›", BlueSoft, Blue, 12f);
            prevBtn.Click += (s, e) => ShiftDate(-1);
            nextBtn.Click += (s, e) => ShiftDate(1);
            datePicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0) };
            datePicker.ValueChanged += (s, e) =>
            {
                var k = KeyOf(datePicker.Value.Date);
                if (k != currentDate) { currentDate = k; RenderTasks(); }
            };
            var todayBtn = MakeOutlineButton("今天", Red);
            todayBtn.Click += (s, e) => { currentDate = TodayKey(); datePicker.Value = DateTime.Today; RenderTasks(); };
            dateBar.Controls.Add(prevBtn, 0, 0);
            dateBar.Controls.Add(datePicker, 1, 0);
            dateBar.Controls.Add(nextBtn, 2, 0);
            dateBar.Controls.Add(todayBtn, 3, 0);

            // 添加栏
            var addRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 48, ColumnCount = 2, Padding = new Padding(12, 7, 12, 7), BackColor = Bg };
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            newTaskBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), BorderStyle = BorderStyle.FixedSingle, Font = Font };
            newTaskBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { AddTask(); e.SuppressKeyPress = true; e.Handled = true; } };
            var addBtn = MakeSolidButton("添加", Red, Color.White);
            addBtn.Click += (s, e) => AddTask();
            addRow.Controls.Add(newTaskBox, 0, 0);
            addRow.Controls.Add(addBtn, 1, 0);

            // 完成情况
            var stats = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Bg };
            doneLabel = new Label { Dock = DockStyle.Left, Width = 108, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Blue, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(14, 0, 0, 0) };
            historyBadge = new Label { Dock = DockStyle.Right, Width = 46, Text = "历史", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Red, BackColor = RedSoft, Margin = new Padding(0, 5, 12, 5) };
            progressTrack = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(238, 241, 246), Margin = new Padding(4, 12, 4, 12) };
            progressFill = new Panel { Dock = DockStyle.Left, Width = 0, BackColor = BlueMid };
            progressFill.Paint += (s, e) =>
            {
                if (progressFill.Width <= 0) return;
                using (var b = new LinearGradientBrush(progressFill.ClientRectangle, BlueMid, Red, 0f))
                    e.Graphics.FillRectangle(b, progressFill.ClientRectangle);
            };
            progressTrack.Controls.Add(progressFill);
            stats.Controls.Add(progressTrack);
            stats.Controls.Add(doneLabel);
            stats.Controls.Add(historyBadge);

            // 任务列表（可滚动）
            taskPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
            taskPanel.Resize += (s, e) => { if (ready) RenderTasks(); };

            // 底部状态栏
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Bg, Padding = new Padding(14, 4, 14, 4) };
            statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink3, Font = new Font("Segoe UI", 8.5f), AutoEllipsis = true };

            Controls.Add(taskPanel);
            Controls.Add(footer);
            Controls.Add(stats);
            Controls.Add(addRow);
            Controls.Add(dateBar);
            Controls.Add(titleBar);
            footer.Controls.Add(statusLabel);

            LayoutTitleBar();
        }

        Button MakeTitleButton(string text)
        {
            var b = new Button
            {
                Text = text, Size = new Size(40, 40), FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
                BackColor = Color.Transparent, Font = new Font("Segoe UI", 11f), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 120, 190);
            return b;
        }

        Button MakeIconButton(string text, Color back, Color fore, float size)
        {
            var b = new Button
            {
                Text = text, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore,
                Dock = DockStyle.Fill, Margin = new Padding(2), Font = new Font("Segoe UI", size), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(214, 230, 252);
            return b;
        }

        Button MakeOutlineButton(string text, Color fore)
        {
            var b = new Button
            {
                Text = text, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = fore,
                Dock = DockStyle.Fill, Margin = new Padding(2), Font = Font, Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = fore;
            return b;
        }

        Button MakeSolidButton(string text, Color back, Color fore)
        {
            var b = new Button
            {
                Text = text, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore,
                Dock = DockStyle.Fill, Margin = new Padding(2), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = back == Red ? RedDeep : Color.FromArgb(60, 120, 200);
            return b;
        }

        void LayoutTitleBar()
        {
            closeBtn.Location = new Point(titleBar.Width - 40, 0);
            minBtn.Location = new Point(titleBar.Width - 80, 0);
            hintLabel.SetBounds(Math.Max(180, titleBar.Width - 80 - 180), 0, 170, 40);
            titleLabel.SetBounds(32, 0, 140, 40);
        }

        /* ================= 渲染 ================= */
        void RenderTasks()
        {
            taskPanel.SuspendLayout();
            taskPanel.Controls.Clear();
            timeLabelById.Clear();

            var tasks = TasksFor(currentDate);
            int w = Math.Max(200, taskPanel.ClientSize.Width - (taskPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 16);

            if (tasks.Count == 0)
            {
                var empty = new Label
                {
                    Text = currentDate == TodayKey() ? "还没有待办事项，先添加一条吧" : "这一天没有记录",
                    AutoSize = false, Width = w, Height = 60, TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Ink2, BackColor = Color.Transparent, Font = Font
                };
                empty.Location = new Point(8, 8);
                taskPanel.Controls.Add(empty);
                taskPanel.ResumeLayout();
                UpdateStats();
                return;
            }

            bool isToday = currentDate == TodayKey();
            int y = 8;
            foreach (var t in tasks)
            {
                var row = BuildTaskRow(t, isToday, w);
                row.Location = new Point(8, y);
                taskPanel.Controls.Add(row);
                y += row.Height + 8;
            }

            taskPanel.ResumeLayout();
            UpdateStats();
        }

        Panel BuildTaskRow(TaskItem t, bool isToday, int w)
        {
            bool expanded = expandedIds.Contains(t.Id);
            var row = new Panel { Width = w, Height = expanded ? ROW_H_EXPANDED : ROW_H, BackColor = Color.White, Tag = t.Id };
            row.Paint += (s, e) =>
            {
                using (var p = new Pen(Line))
                    e.Graphics.DrawRectangle(p, 0, 0, row.Width - 1, row.Height - 1);
            };

            // 完成勾选
            var done = new Button
            {
                FlatStyle = FlatStyle.Flat, Size = new Size(22, 22), Location = new Point(8, 17),
                Text = t.Done ? "✓" : "", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand,
                BackColor = t.Done ? Blue : Color.White, ForeColor = Color.White
            };
            done.FlatAppearance.BorderSize = 2;
            done.FlatAppearance.BorderColor = t.Done ? Blue : Color.FromArgb(196, 204, 216);
            done.Click += (s, e) => ToggleDone(t.Id);
            row.Controls.Add(done);

            // 标题
            int deleteX = w - 8 - 26;
            int summaryX = deleteX - 4 - 44;
            int timerX = isToday ? summaryX - 4 - 52 : summaryX;
            int timeX = timerX - 4 - 72;
            var title = new Label
            {
                AutoSize = false, Location = new Point(40, 17), Size = new Size(Math.Max(40, timeX - 40 - 8), 22),
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = t.Done ? Ink3 : Ink, Font = Font, Text = t.Title
            };
            if (t.Done) title.Font = new Font(Font, FontStyle.Strikeout);
            row.Controls.Add(title);

            // 用时
            var timeLbl = new Label
            {
                AutoSize = false, Size = new Size(72, 22), Location = new Point(timeX, 17),
                TextAlign = ContentAlignment.MiddleRight, ForeColor = t.Running ? Red : Blue,
                Font = new Font("Consolas", 9.5f, FontStyle.Bold), Text = FormatMs(Elapsed(t))
            };
            timeLabelById[t.Id] = timeLbl;
            row.Controls.Add(timeLbl);

            // 计时（仅今天）
            if (isToday)
            {
                var timerBtn = new Button
                {
                    FlatStyle = FlatStyle.Flat, Size = new Size(52, 26), Location = new Point(timerX, 15),
                    Text = t.Running ? "停止" : "计时", Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand,
                    BackColor = t.Running ? Red : Color.White, ForeColor = t.Running ? Color.White : Blue
                };
                timerBtn.FlatAppearance.BorderSize = 1;
                timerBtn.FlatAppearance.BorderColor = t.Running ? Red : Blue;
                timerBtn.FlatAppearance.MouseOverBackColor = t.Running ? RedDeep : BlueSoft;
                timerBtn.Click += (s, e) => ToggleTimer(t.Id);
                row.Controls.Add(timerBtn);
            }

            // 总结
            var sumBtn = new Button
            {
                FlatStyle = FlatStyle.Flat, Size = new Size(44, 26), Location = new Point(summaryX, 15),
                Text = "总结", Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand,
                BackColor = Color.White, ForeColor = expanded || !string.IsNullOrEmpty(t.Summary) ? Blue : Ink2
            };
            sumBtn.FlatAppearance.BorderSize = 0;
            sumBtn.FlatAppearance.MouseOverBackColor = BlueSoft;
            sumBtn.Click += (s, e) => ToggleExpand(t.Id);
            row.Controls.Add(sumBtn);

            // 删除
            var delBtn = new Button
            {
                FlatStyle = FlatStyle.Flat, Size = new Size(26, 26), Location = new Point(deleteX, 15),
                Text = "删", Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand,
                BackColor = Color.White, ForeColor = Ink3
            };
            delBtn.FlatAppearance.BorderSize = 0;
            delBtn.FlatAppearance.MouseOverBackColor = RedSoft;
            delBtn.Click += (s, e) => DeleteTask(t.Id);
            row.Controls.Add(delBtn);

            // 总结编辑区
            if (expanded)
            {
                var summaryBox = new TextBox
                {
                    Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(40, 46),
                    Size = new Size(w - 48, 84), Font = Font, BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.FromArgb(251, 252, 254), ForeColor = Ink
                };
                summaryBox.Text = t.Summary ?? "";
                summaryBox.TextChanged += (s, e) => { t.Summary = summaryBox.Text; Save(); };
                row.Controls.Add(summaryBox);

                var meta = new Label
                {
                    AutoSize = false, Location = new Point(40, 134), Size = new Size(w - 48, 16),
                    TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink3, Font = new Font("Segoe UI", 8.5f),
                    Text = "完成用时 " + FormatMs(t.AccumulatedMs) + (t.CompletedAtMs.HasValue ? " · 完成于 " + FormatClock(t.CompletedAtMs.Value) : "")
                };
                row.Controls.Add(meta);
            }

            return row;
        }

        void UpdateStats()
        {
            var tasks = TasksFor(currentDate);
            int done = tasks.Count(x => x.Done);
            doneLabel.Text = "完成 " + done + "/" + tasks.Count;
            int trackW = progressTrack.ClientSize.Width;
            progressFill.Width = tasks.Count > 0 ? (int)(trackW * done / (double)tasks.Count) : 0;
            historyBadge.Visible = currentDate != TodayKey();
            statusLabel.Text = "数据：" + dataFile;
        }

        void UpdateTimeLabels()
        {
            foreach (var t in TasksFor(currentDate))
                if (timeLabelById.TryGetValue(t.Id, out var lbl))
                {
                    lbl.Text = FormatMs(Elapsed(t));
                    lbl.ForeColor = t.Running ? Red : Blue;
                }
        }

        /* ================= 日期 / 窗口 ================= */
        void ShiftDate(int delta)
        {
            datePicker.Value = ParseKey(currentDate).AddDays(delta);
        }

        void ApplyWindowBounds()
        {
            if (data.WinW >= 360 && data.WinH >= 420)
            {
                var r = new Rectangle(data.WinX, data.WinY, data.WinW, data.WinH);
                if (Screen.FromRectangle(r).WorkingArea.IntersectsWith(r)) { Bounds = r; return; }
            }
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(460, 620);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopAllRunning();
            data.WinX = Left; data.WinY = Top; data.WinW = Width; data.WinH = Height;
            Save();
            base.OnFormClosing(e);
        }

        // 无边框窗口四边/四角缩放
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x84) return; // WM_NCHITTEST
            if ((int)m.Result != 0x01) return; // HTCLIENT 才处理

            long lp = m.LParam.ToInt64();
            int x = unchecked((short)(lp & 0xffff));
            int y = unchecked((short)((lp >> 16) & 0xffff));
            var p = PointToClient(new Point(x, y));

            bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
            bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;

            if (top && left) m.Result = (IntPtr)13;      // HTTOPLEFT
            else if (top && right) m.Result = (IntPtr)14; // HTTOPRIGHT
            else if (bottom && left) m.Result = (IntPtr)16; // HTBOTTOMLEFT
            else if (bottom && right) m.Result = (IntPtr)17; // HTBOTTOMRIGHT
            else if (top) m.Result = (IntPtr)12;          // HTTOP
            else if (bottom) m.Result = (IntPtr)15;       // HTBOTTOM
            else if (left) m.Result = (IntPtr)10;         // HTLEFT
            else if (right) m.Result = (IntPtr)11;        // HTRIGHT
        }
    }
}
