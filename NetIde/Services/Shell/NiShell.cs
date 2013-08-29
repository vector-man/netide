﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NetIde.Services.Env;
using NetIde.Shell;
using NetIde.Shell.Interop;
using NetIde.Util.Forms;
using NetIde.Win32;

namespace NetIde.Services.Shell
{
    internal class NiShell : ServiceBase, INiShell
    {
        private readonly NiConnectionPoint<INiShellEvents> _connectionPoint = new NiConnectionPoint<INiShellEvents>();
        private readonly Dictionary<INiWindowPane, DockContent> _dockContents = new Dictionary<INiWindowPane, DockContent>();
        private readonly NiEnv _env;

        public event EventHandler RequerySuggested;

        protected virtual void OnRequerySuggested(EventArgs e)
        {
            var ev = RequerySuggested;
            if (ev != null)
                ev(this, e);
        }

        public NiShell(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _env = (NiEnv)GetService(typeof(INiEnv));

            Application.AddMessageFilter(new MessageFilter(this));
        }

        public HResult Advise(object sink, out int cookie)
        {
            return _connectionPoint.Advise(sink, out cookie);
        }

        public HResult Advise(INiShellEvents sink, out int cookie)
        {
            return Advise((object)sink, out cookie);
        }

        public HResult Unadvise(int cookie)
        {
            return _connectionPoint.Unadvise(cookie);
        }

        public HResult CreateToolWindow(INiWindowPane windowPane, NiDockStyle dockStyle, NiToolWindowOrientation toolWindowOrientation, out INiWindowFrame toolWindow)
        {
            toolWindow = null;

            try
            {
                if (windowPane == null)
                    throw new ArgumentNullException("windowPane");

                var dockContent = new DockContent(windowPane, dockStyle, toolWindowOrientation)
                {
                    Site = new SiteProxy(this)
                };

                dockContent.Disposed += dockContent_Disposed;

                _dockContents.Add(windowPane, dockContent);

                toolWindow = dockContent.GetProxy();

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        void dockContent_Disposed(object sender, EventArgs e)
        {
            _dockContents.Remove(((DockContent)sender).WindowPane);
        }

        public HResult ShowMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            DialogResult result;
            return ShowMessageBox(text, caption, buttons, icon, out result);
        }

        public HResult ShowMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, out DialogResult result)
        {
            result = DialogResult.None;

            try
            {
                result = MessageBox.Show(
                    GetActiveWindow(),
                    text,
                    caption ?? _env.MainWindow.Caption,
                    buttons,
                    icon
                );

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public HResult BrowseForFolder(string title, NiBrowseForFolderOptions options, out string selectedFolder)
        {
            selectedFolder = null;

            try
            {
                var browser = new FolderBrowser
                {
                    Title = title,
                    ShowEditBox = (options & NiBrowseForFolderOptions.HideEditBox) == 0,
                    ShowNewFolderButton = (options & NiBrowseForFolderOptions.HideNewFolderButton) == 0
                };

                if (browser.ShowDialog(GetActiveWindow()) == DialogResult.OK)
                {
                    selectedFolder = browser.SelectedPath;

                    return HResult.OK;
                }

                return HResult.False;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public IWin32Window GetActiveWindow()
        {
            return new NativeWindowWrapper(NativeMethods.GetActiveWindow());
        }

        public HResult SaveDocDataToFile(NiSaveMode mode, INiPersistFile persistFile, string fileName, out string newFileName, out bool saved)
        {
            newFileName = null;
            saved = false;

            try
            {
                bool isDirty;
                ErrorUtil.ThrowOnFailure(persistFile.IsDirty(out isDirty));

                if (!isDirty)
                {
                    newFileName = fileName;
                    saved = true;

                    return HResult.OK;
                }

                switch (mode)
                {
                    case NiSaveMode.SaveAs:
                    case NiSaveMode.SaveCopyAs:
                        throw new NotImplementedException();

                    case NiSaveMode.Save:
                        INiHierarchy hier;
                        INiWindowFrame windowFrame;
                        ErrorUtil.ThrowOnFailure(((INiOpenDocumentManager)GetService(typeof(INiOpenDocumentManager))).IsDocumentOpen(
                            fileName,
                            out hier,
                            out windowFrame
                        ));

                        if (hier != null)
                        {
                            NiQuerySaveResult querySaveResult;
                            var hr = QuerySaveViaDialog(new[] { hier }, out querySaveResult);

                            if (ErrorUtil.Failure(hr))
                                return hr;

                            switch (querySaveResult)
                            {
                                case NiQuerySaveResult.Cancel:
                                    return HResult.OK;

                                case NiQuerySaveResult.DoNotSave:
                                    // We pretend the document was saved if the
                                    // user asked us not to save the document.

                                    saved = true;
                                    return HResult.OK;
                            }
                        }
                        break;
                }

                newFileName = fileName;
                saved = true;

                return persistFile.Save(newFileName, true);
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public HResult QuerySaveViaDialog(INiHierarchy[] hiers, out NiQuerySaveResult result)
        {
            result = NiQuerySaveResult.Cancel;

            try
            {
                if (hiers == null)
                    throw new ArgumentNullException("hiers");

                switch (SaveHierarchiesForm.ShowDialog(this, hiers))
                {
                    case DialogResult.Yes: result = NiQuerySaveResult.Save; break;
                    case DialogResult.No: result = NiQuerySaveResult.DoNotSave; break;
                }

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public HResult GetWindowFrameForWindowPane(INiWindowPane windowPane, out INiWindowFrame windowFrame)
        {
            windowFrame = null;

            try
            {
                DockContent dockContent;
                if (_dockContents.TryGetValue(windowPane, out dockContent))
                    windowFrame = dockContent.GetProxy();

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public HResult GetDocumentWindowIterator(out INiIterator<INiWindowFrame> iterator)
        {
            return _env.MainForm.GetDocumentWindowIterator(out iterator);
        }

        internal void RaiseRequery()
        {
            OnRequerySuggested(EventArgs.Empty);
            _connectionPoint.ForAll(p => p.OnRequerySuggested());
        }

        private class MessageFilter : IMessageFilter
        {
            private readonly NiShell _shell;
            private bool _pending;

            public MessageFilter(NiShell shell)
            {
                _shell = shell;

                QueueRequery();
            }

            public bool PreFilterMessage(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_KEYUP:
                    case WM_KILLFOCUS:
                    case WM_LBUTTONUP:
                    case WM_MBUTTONUP:
                    case WM_MENURBUTTONUP:
                    case WM_MOUSEHWHEEL:
                    case WM_MOUSEWHEEL:
                    case WM_NCLBUTTONUP:
                    case WM_NCMBUTTONUP:
                    case WM_NCRBUTTONUP:
                    case WM_NCXBUTTONUP:
                    case WM_RBUTTONUP:
                    case WM_SETFOCUS:
                    case WM_SYSKEYUP:
                    case WM_XBUTTONUP:
                        QueueRequery();
                        break;
                }

                return false;
            }

            private void QueueRequery()
            {
                if (!_pending)
                {
                    _pending = true;
                    SynchronizationContext.Current.Post(Requery, null);
                }
            }

            private void Requery(object state)
            {
                _pending = false;
                _shell.RaiseRequery();
            }

            private const int WM_KEYUP = 0x101;
            private const int WM_SYSKEYUP = 0x105;
            private const int WM_LBUTTONUP = 0x202;
            private const int WM_MBUTTONUP = 0x208;
            private const int WM_MENURBUTTONUP = 0x122;
            private const int WM_NCLBUTTONUP = 0xA2;
            private const int WM_NCMBUTTONUP = 0xA8;
            private const int WM_NCRBUTTONUP = 0xA5;
            private const int WM_RBUTTONUP = 0x205;
            private const int WM_XBUTTONUP = 0x20C;
            private const int WM_MOUSEWHEEL = 0x20A;
            private const int WM_MOUSEHWHEEL = 0x20E;
            private const int WM_SETFOCUS = 0x7;
            private const int WM_KILLFOCUS = 0x8;
            private const int WM_NCXBUTTONUP = 0xAC;
        }

        private class NativeWindowWrapper : IWin32Window
        {
            public IntPtr Handle { get; private set; }

            public NativeWindowWrapper(IntPtr handle)
            {
                Handle = handle;
            }
        }
    }
}
