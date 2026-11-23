// KeepAwake - a tiny system-tray utility that nudges the mouse by a few
// pixels on a fixed interval (default: every 10 minutes) so Windows never
// sees the session as idle and therefore does not lock the workstation.
//
// Build without any SDK:  build.cmd  (uses the .NET Framework compiler
// that ships with every Windows installation).
// Or with the .NET SDK:  dotnet build src\KeepAwake\KeepAwake.csproj
//
// The tray logo is drawn programmatically (mouse silhouette + motion
// chevrons on a teal gradient badge; grey badge with pause bars when paused).

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KeepAwake
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, "Local\\KeepAwake.SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Keep Awake is already running - look for its icon in the notification area.",
                    "Keep Awake", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new KeepAwakeContext());
            }
            finally
            {
                GC.KeepAlive(mutex);
            }
        }
    }

    /// <summary>
    /// Sends small relative mouse moves using SendInput (the same channel
    /// physical input uses), so the move also resets the Windows idle timer
    /// that triggers screen lock / screensaver.
    /// </summary>
    internal static class MouseMover
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        private const int INPUT_MOUSE = 0;
        private const int MOUSEEVENTF_MOVE = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static void Nudge(int dx, int dy)
        {
            INPUT[] input = new INPUT[1];
            input[0].type = INPUT_MOUSE;
            input[0].mi.dx = dx;
            input[0].mi.dy = dy;
            input[0].mi.dwFlags = MOUSEEVENTF_MOVE;

            if (SendInput(1, input, Marshal.SizeOf(typeof(INPUT))) != 1)
            {
                // Fallback (does not reset the idle timer on all Windows
                // versions, but at least moves the cursor).
                Point p = Cursor.Position;
                Cursor.Position = new Point(p.X + dx, p.Y + dy);
            }
        }
    }

    /// <summary>Draws the tray logo at runtime - no icon file needed.</summary>
    internal static class IconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon Create(bool active)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    DrawLogo(g, active);
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
                    return icon;
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private static void DrawLogo(Graphics g, bool active)
        {
            // Rounded-square gradient badge
            using (GraphicsPath bg = RoundedRect(1f, 1f, 30f, 30f, 8f))
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, 32, 32),
                active ? Color.FromArgb(30, 58, 95) : Color.FromArgb(84, 94, 110),
                active ? Color.FromArgb(38, 166, 154) : Color.FromArgb(58, 68, 84),
                45f))
            {
                g.FillPath(bgBrush, bg);
            }

            // Computer-mouse silhouette
            float mx = 5.0f, my = 6.0f, mw = 13f, mh = 19.5f;
            using (GraphicsPath body = MouseBodyPath(mx, my, mw, mh))
            {
                using (SolidBrush b = new SolidBrush(
                    active ? Color.FromArgb(250, 250, 255) : Color.FromArgb(220, 226, 236)))
                {
                    g.FillPath(b, body);
                }
                using (Pen p = new Pen(Color.FromArgb(20, 35, 52), 1.1f))
                {
                    g.DrawPath(p, body);
                }
            }

            // Button split line + scroll wheel
            float domeH = mw * 0.62f;
            using (Pen p = new Pen(Color.FromArgb(20, 35, 52), 1.0f))
            {
                g.DrawLine(p, mx + mw / 2f, my + 0.5f, mx + mw / 2f, my + domeH / 2f + 0.5f);
            }
            using (GraphicsPath wheel = RoundedRect(mx + mw / 2f - 1.4f, my + 1.3f, 2.8f, 4.0f, 1.4f))
            using (SolidBrush wb = new SolidBrush(
                active ? Color.FromArgb(255, 140, 66) : Color.FromArgb(150, 160, 172)))
            {
                g.FillPath(wb, wheel);
            }

            if (active)
            {
                // Motion chevrons ("nudge" direction)
                using (Pen cp = new Pen(Color.FromArgb(255, 209, 102), 2.4f))
                {
                    cp.StartCap = LineCap.Round;
                    cp.EndCap = LineCap.Round;
                    cp.LineJoin = LineJoin.Round;
                    DrawChevron(g, cp, 21.0f, 16.0f, 3.8f);
                    DrawChevron(g, cp, 26.2f, 16.0f, 3.8f);
                }
            }
            else
            {
                // Paused: two pause bars
                using (SolidBrush wb = new SolidBrush(Color.FromArgb(235, 240, 246)))
                using (GraphicsPath b1 = RoundedRect(21.0f, 15.5f, 3f, 8f, 1.5f))
                using (GraphicsPath b2 = RoundedRect(26.5f, 15.5f, 3f, 8f, 1.5f))
                {
                    g.FillPath(wb, b1);
                    g.FillPath(wb, b2);
                }
            }
        }

        private static GraphicsPath MouseBodyPath(float x, float y, float w, float h)
        {
            float domeH = w * 0.62f;
            float r = 3f;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, w, domeH, 180f, 180f);            // rounded dome (top)
            p.AddLine(x + w, y + domeH / 2f, x + w, y + h - r);
            p.AddArc(x + w - r, y + h - r, r, r, 0f, 90f);   // bottom-right corner
            p.AddLine(x + w - r, y + h, x + r, y + h);
            p.AddArc(x, y + h - r, r, r, 90f, 90f);          // bottom-left corner
            p.CloseFigure();
            return p;
        }

        private static void DrawChevron(Graphics g, Pen pen, float x, float midY, float halfH)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddLine(x, midY - halfH, x + 3.4f, midY);
                p.AddLine(x + 3.4f, midY, x, midY + halfH);
                g.DrawPath(pen, p);
            }
        }

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, r, r, 180f, 90f);
            p.AddArc(x + w - r, y, r, r, 270f, 90f);
            p.AddArc(x + w - r, y + h - r, r, r, 0f, 90f);
            p.AddArc(x, y + h - r, r, r, 90f, 90f);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Registers/unregisters the app in the current user's Startup list.</summary>
    internal static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "KeepAwake";

        public static bool IsRegistered()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(AppName) != null;
            }
        }

        public static void SetRegistered(bool register)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (register)
                    key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                else
                    key.DeleteValue(AppName, false);
            }
        }
    }

    /// <summary>Tray application core: icon, menu, timers.</summary>
    internal sealed class KeepAwakeContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly Icon iconActive;
        private readonly Icon iconPaused;

        private readonly System.Windows.Forms.Timer nudgeTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer undoTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();

        private readonly Random random = new Random(Environment.TickCount);
        private bool enabled = true;
        private int intervalMinutes = 10;
        private DateTime lastNudge = DateTime.Now;
        private int lastDx;
        private int lastDy;

        private ToolStripMenuItem menuEnabled;
        private ToolStripMenuItem menuStartup;
        private ToolStripMenuItem[] menuIntervals;

        public KeepAwakeContext()
        {
            iconActive = IconFactory.Create(true);
            iconPaused = IconFactory.Create(false);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = iconActive;
            trayIcon.Text = "Keep Awake";
            trayIcon.ContextMenuStrip = BuildMenu();
            trayIcon.Visible = true;

            nudgeTimer.Interval = intervalMinutes * 60000;
            nudgeTimer.Tick += OnNudgeTimerTick;
            nudgeTimer.Start();

            undoTimer.Interval = 250;
            undoTimer.Tick += OnUndoTimerTick;

            uiTimer.Interval = 1000;
            uiTimer.Tick += OnUiTimerTick;
            uiTimer.Start();

            UpdateTooltip();
            trayIcon.ShowBalloonTip(2500, "Keep Awake is running",
                string.Format("The mouse is nudged a few pixels every {0} minutes so Windows will not lock.", intervalMinutes),
                ToolTipIcon.Info);
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            menuEnabled = new ToolStripMenuItem("Enabled");
            menuEnabled.Checked = enabled;
            menuEnabled.Click += delegate { ToggleEnabled(); };

            ToolStripMenuItem intervalMenu = new ToolStripMenuItem("Nudge interval");
            int[] choices = new int[] { 5, 10, 15, 30 };
            menuIntervals = new ToolStripMenuItem[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                int minutes = choices[i];
                ToolStripMenuItem item = new ToolStripMenuItem(minutes + " minutes");
                item.Tag = minutes;
                item.Checked = (minutes == intervalMinutes);
                item.Click += delegate { SetInterval(minutes); };
                intervalMenu.DropDownItems.Add(item);
                menuIntervals[i] = item;
            }

            ToolStripMenuItem nudgeNow = new ToolStripMenuItem("Nudge mouse now");
            nudgeNow.Click += delegate { Nudge(); };

            menuStartup = new ToolStripMenuItem("Start with Windows");
            menuStartup.Checked = StartupHelper.IsRegistered();
            menuStartup.Click += delegate
            {
                StartupHelper.SetRegistered(!menuStartup.Checked);
                menuStartup.Checked = StartupHelper.IsRegistered();
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { ExitApp(); };

            menu.Items.Add(menuEnabled);
            menu.Items.Add(intervalMenu);
            menu.Items.Add(nudgeNow);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuStartup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            return menu;
        }

        private void OnNudgeTimerTick(object sender, EventArgs e)
        {
            if (enabled)
                Nudge();
            else
                lastNudge = DateTime.Now;
        }

        private void Nudge()
        {
            lastDx = random.Next(3, 7) * (random.Next(0, 2) == 0 ? -1 : 1);
            lastDy = random.Next(3, 7) * (random.Next(0, 2) == 0 ? -1 : 1);
            MouseMover.Nudge(lastDx, lastDy);
            undoTimer.Start();
            lastNudge = DateTime.Now;
            UpdateTooltip();
        }

        private void OnUndoTimerTick(object sender, EventArgs e)
        {
            undoTimer.Stop();
            MouseMover.Nudge(-lastDx, -lastDy);
        }

        private void OnUiTimerTick(object sender, EventArgs e)
        {
            UpdateTooltip();
        }

        private void ToggleEnabled()
        {
            enabled = !enabled;
            menuEnabled.Checked = enabled;
            trayIcon.Icon = enabled ? iconActive : iconPaused;
            lastNudge = DateTime.Now;
            UpdateTooltip();
        }

        private void SetInterval(int minutes)
        {
            intervalMinutes = minutes;
            for (int i = 0; i < menuIntervals.Length; i++)
            {
                menuIntervals[i].Checked = ((int)menuIntervals[i].Tag) == intervalMinutes;
            }
            nudgeTimer.Interval = intervalMinutes * 60000;
            lastNudge = DateTime.Now;
            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            if (!enabled)
            {
                trayIcon.Text = "Keep Awake (paused)";
                return;
            }
            TimeSpan remain = TimeSpan.FromMilliseconds(nudgeTimer.Interval) - (DateTime.Now - lastNudge);
            if (remain < TimeSpan.Zero)
                remain = TimeSpan.Zero;
            trayIcon.Text = string.Format("Keep Awake - next nudge in {0:00}:{1:00}",
                (int)remain.TotalMinutes, remain.Seconds);
        }

        private void ExitApp()
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void ExitThreadCore()
        {
            nudgeTimer.Stop();
            undoTimer.Stop();
            uiTimer.Stop();
            trayIcon.Dispose();
            if (iconActive != null) iconActive.Dispose();
            if (iconPaused != null) iconPaused.Dispose();
            base.ExitThreadCore();
        }
    }
}
