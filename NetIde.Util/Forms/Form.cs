﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NetIde.Shell.Interop;
using NetIde.Util.Win32;

namespace NetIde.Util.Forms
{
    public class Form : System.Windows.Forms.Form, IServiceProvider
    {
        private const int SizeGripSize = 16;

        private readonly FormHelper _fixer;
        private bool _showingCalled;
        private bool _renderSizeGrip;
        private readonly SizeGripStyle _lastSizeGripStyle;
        private bool _closeButtonEnabled = true;
        private bool _disposed;

        [Category("Behavior")]
        public event CancelPreviewEventHandler CancelPreview;

        protected virtual void OnCancelPreview(CancelPreviewEventArgs e)
        {
            var ev = CancelPreview;
            if (ev != null)
                ev(this, e);
        }

        [Category("Behavior")]
        public event EventHandler Showing;

        protected virtual void OnShowing(EventArgs e)
        {
            var ev = Showing;
            if (ev != null)
                ev(this, e);
        }

        [Category("Behavior")]
        public event BrowseButtonEventHandler BrowseButtonClick;

        protected virtual void OnBrowseButtonClick(BrowseButtonEventArgs e)
        {
            var ev = BrowseButtonClick;
            if (ev != null)
                ev(this, e);
        }

        [Category("Focus")]
        public event EventHandler ApplicationActivated;

        protected virtual void OnApplicationActivated(EventArgs e)
        {
            var handler = ApplicationActivated;
            if (handler != null)
                handler(this, e);
        }

        [Category("Focus")]
        public event EventHandler ApplicationDeactivated;

        protected virtual void OnApplicationDeactivated(EventArgs e)
        {
            var handler = ApplicationDeactivated;
            if (handler != null)
                handler(this, e);
        }

        [DefaultValue(true)]
        public bool CloseButtonEnabled
        {
            get { return _closeButtonEnabled; }
            set
            {
                if (_closeButtonEnabled != value)
                {
                    _closeButtonEnabled = value;

                    var systemMenu = NativeMethods.GetSystemMenu(new HandleRef(this, Handle), false);

                    NativeMethods.EnableMenuItem(
                        new HandleRef(this, systemMenu),
                        NativeMethods.SC_CLOSE,
                        value ? NativeMethods.MF_ENABLED : NativeMethods.MF_GRAYED
                    );

                    InvalidateNonClient();
                }
            }
        }

        protected string UserSettingsKey
        {
            get { return _fixer.KeyAddition; }
            set { _fixer.KeyAddition = value; }
        }

        protected bool EnableBoundsTracking
        {
            get { return _fixer.EnableBoundsTracking; }
            set { _fixer.EnableBoundsTracking = value; }
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public new AutoScaleMode AutoScaleMode
        {
            get { return base.AutoScaleMode; }
            set
            {
                // This value is set by the designer. To not have to manually change the
                // defaults set by the designer, it's silently ignored here at runtime.

                if (_fixer.InDesignMode)
                    base.AutoScaleMode = value;
            }
        }

        public Form()
        {
            _fixer = new FormHelper(this)
            {
                EnableBoundsTracking = true
            };

            _lastSizeGripStyle = SizeGripStyle;
        }

        protected void StoreUserSettings()
        {
            _fixer.StoreUserSettings();
        }

        protected override void SetVisibleCore(bool value)
        {
            _fixer.InitializeForm();

            if (value && !_showingCalled)
            {
                _showingCalled = true;

                OnShowing(EventArgs.Empty);
            }

            base.SetVisibleCore(value);
        }

        protected bool RestoreUserSettings()
        {
            return _fixer.RestoreUserSettings();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            _fixer.OnSizeChanged(e);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            _fixer.OnLocationChanged(e);
        }

        public void CenterOverParent(double relativeSize)
        {
            _fixer.CenterOverParent(relativeSize);
        }

        public void TrackProperty(Control control, string property)
        {
            _fixer.TrackProperty(control, property);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            //
            // Overrides the default handling of the Escape key. When a cancel
            // button is present, the Escape key is not published through
            // KeyPreview. Because of this, no alternative handling is possible.
            // This override catches the Escape key before it is processed by
            // the framework and cancels default handling when the CancelPreview
            // event signals it has handled the Escape key.
            //

            if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
            {
                Keys keyCode = keyData & Keys.KeyCode;

                if (keyCode == Keys.Escape)
                {
                    var e = new CancelPreviewEventArgs();

                    OnCancelPreview(e);

                    if (e.Handled)
                    {
                        return true;
                    }
                }
            }

            return base.ProcessDialogKey(keyData);
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            Screen screen;
            var location = Location;
            var size = Size;

            if ((specified & BoundsSpecified.X) != 0)
                location.X = x;
            if ((specified & BoundsSpecified.Y) != 0)
                location.Y = y;
            if ((specified & BoundsSpecified.Width) != 0)
                size.Width = width;
            if ((specified & BoundsSpecified.Height) != 0)
                size.Height = height;

            screen = FindScreen(location);

            if (screen == null)
            {
                location = new Point(location.X + size.Width, location.Y + size.Height);

                screen = FindScreen(location);
            }

            if (screen == null)
                screen = Screen.PrimaryScreen;

            if (screen.WorkingArea.X > x)
            {
                x = screen.WorkingArea.X;
                specified |= BoundsSpecified.X;
            }
            if (screen.WorkingArea.Y > y)
            {
                y = screen.WorkingArea.Y;
                specified |= BoundsSpecified.Y;
            }

            base.SetBoundsCore(x, y, width, height, specified);
        }

        private static Screen FindScreen(Point location)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.Contains(location))
                    return screen;
            }

            return null;
        }

        private void UpdateRenderSizeGrip()
        {
            if (_lastSizeGripStyle != SizeGripStyle)
            {
                switch (FormBorderStyle)
                {
                    case FormBorderStyle.None:
                    case FormBorderStyle.FixedSingle:
                    case FormBorderStyle.Fixed3D:
                    case FormBorderStyle.FixedDialog:
                    case FormBorderStyle.FixedToolWindow:
                        _renderSizeGrip = false;
                        break;

                    case FormBorderStyle.Sizable:
                    case FormBorderStyle.SizableToolWindow:
                        switch (SizeGripStyle)
                        {
                            case SizeGripStyle.Show:
                                _renderSizeGrip = true;
                                break;

                            case SizeGripStyle.Hide:
                                _renderSizeGrip = false;
                                break;

                            case SizeGripStyle.Auto:
                                _renderSizeGrip = Modal;
                                break;
                        }
                        break;
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            BrowseButtonEventArgs e;

            switch (m.Msg)
            {
                case NativeMethods.WM_NCHITTEST:
                    WmNCHitTest(ref m);
                    return;

                case NativeMethods.WM_APPCOMMAND:
                    switch (NativeMethods.Util.HIWORD(m.LParam) & ~0xf000)
                    {
                        case NativeMethods.APPCOMMAND_BROWSER_BACKWARD:
                            e = new BrowseButtonEventArgs(BrowseButton.Back);
                            OnBrowseButtonClick(e);
                            if (e.Handled)
                            {
                                m.Result = (IntPtr)1;
                                return;
                            }
                            break;

                        case NativeMethods.APPCOMMAND_BROWSER_FORWARD:
                            e = new BrowseButtonEventArgs(BrowseButton.Forward);
                            OnBrowseButtonClick(e);
                            if (e.Handled)
                            {
                                m.Result = (IntPtr)1;
                                return;
                            }
                            break;
                    }
                    break;

                case NativeMethods.WM_ACTIVATEAPP:
                    if (m.LParam == IntPtr.Zero)
                        OnApplicationActivated(EventArgs.Empty);
                    else
                        OnApplicationDeactivated(EventArgs.Empty);
                    break;
            }

            base.WndProc(ref m);
        }

        private void WmNCHitTest(ref Message m)
        {
            UpdateRenderSizeGrip();

            if (_renderSizeGrip)
            {
                int x = NativeMethods.Util.SignedLOWORD(m.LParam);
                int y = NativeMethods.Util.SignedHIWORD(m.LParam);

                // Convert to client coordinates
                //
                var pt = PointToClient(new Point(x, y));

                Size clientSize = ClientSize;

                // If the grip is not fully visible the grip area could overlap with the system control box; we need to disable
                // the grip area in this case not to get in the way of the control box.  We only need to check for the client's
                // height since the window width will be at least the size of the control box which is always bigger than the
                // grip width.
                if (pt.X >= (clientSize.Width - SizeGripSize) &&
                    pt.Y >= (clientSize.Height - SizeGripSize) &&
                    clientSize.Height >= SizeGripSize)
                {
                    m.Result = IsMirrored ? (IntPtr)NativeMethods.HTBOTTOMLEFT : (IntPtr)NativeMethods.HTBOTTOMRIGHT;
                    return;
                }
            }

            DefWndProc(ref m);

            // If we got any of the "edge" hits (bottom, top, topleft, etc),
            // and we're AutoSizeMode.GrowAndShrink, return non-resizable border
            // The edge values are the 8 values from HTLEFT (10) to HTBOTTOMRIGHT (17).
            if (AutoSizeMode == AutoSizeMode.GrowAndShrink)
            {
                int result = (int)m.Result;
                if (result >= NativeMethods.HTLEFT &&
                    result <= NativeMethods.HTBOTTOMRIGHT)
                {
                    m.Result = (IntPtr)NativeMethods.HTBORDER;
                }
            }
        }

        public static IntPtr ForegroundWindow
        {
            get { return NativeMethods.GetForegroundWindow(); }
            set { NativeMethods.SetForegroundWindow(new HandleRef(null, value)); }
        }

        private void InvalidateNonClient()
        {
            NativeMethods.SendMessage(new HandleRef(this, Handle), NativeMethods.WM_NCPAINT, (IntPtr)1, (IntPtr)0);
        }

        public new object GetService(Type serviceType)
        {
            return base.GetService(serviceType);
        }

        public void Show(IServiceProvider serviceProvider)
        {
            Show(serviceProvider, null);
        }

        public void Show(IServiceProvider serviceProvider, IWin32Window owner)
        {
            if (serviceProvider != null)
                Site = new SiteProxy(serviceProvider);

            Show(owner ?? FindDefaultOwnerWindow(serviceProvider));
        }

        public DialogResult ShowDialog(IServiceProvider serviceProvider)
        {
            return ShowDialog(serviceProvider, null);
        }

        public DialogResult ShowDialog(IServiceProvider serviceProvider, IWin32Window owner)
        {
            if (serviceProvider != null)
                Site = new SiteProxy(serviceProvider);

            return ShowDialog(owner ?? FindDefaultOwnerWindow(serviceProvider));
        }

        public static IWin32Window FindDefaultOwnerWindow(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException("serviceProvider");

            // If the main window is enabled (i.e. there is no modal dialog showing),
            // we return that.

            var mainWindow = ((INiEnv)serviceProvider.GetService(typeof(INiEnv))).MainWindow;
            if (NativeMethods.IsWindowEnabled(mainWindow.Handle))
                return mainWindow;

            // Otherwise iterate over all top level windows of the process and
            // return the first valid one that is enabled.

            uint processId = NativeMethods.GetCurrentProcessId();
            IntPtr foundHWnd = IntPtr.Zero;
            IntPtr foundOverlappedHWnd = IntPtr.Zero;

            NativeMethods.EnumWindows(
                (hWnd, lParam) =>
                {
                    if (
                        NativeMethods.IsWindow(hWnd) &&
                        NativeMethods.IsWindowVisible(hWnd) &&
                        NativeMethods.IsWindowEnabled(hWnd)
                    ) {
                        uint windowProcessId;
                        NativeMethods.GetWindowThreadProcessId(hWnd, out windowProcessId);

                        if (processId == windowProcessId)
                        {
                            uint style = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE);
                            if ((style & (NativeMethods.WS_CHILD | NativeMethods.WS_POPUP)) == 0)
                            {
                                foundOverlappedHWnd = hWnd;
                                return false;
                            }

                            foundHWnd = hWnd;
                        }
                    }

                    return true;
                },
                IntPtr.Zero
            );

            // We (very much) prefer overlapped windows over others. If we have one,
            // we return it. Otherwise we fall back to the popup/child window.

            if (foundOverlappedHWnd != IntPtr.Zero)
                return new NativeWindowWrapper(foundOverlappedHWnd);
            if (foundHWnd != IntPtr.Zero)
                return new NativeWindowWrapper(foundHWnd);

            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (!_fixer.InDesignMode)
                    _fixer.StoreUserSettings();

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private class NativeWindowWrapper : IWin32Window
        {
            public NativeWindowWrapper(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }
    }
}
