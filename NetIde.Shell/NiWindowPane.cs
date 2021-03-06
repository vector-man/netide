﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using log4net;
using NetIde.Shell.Interop;

namespace NetIde.Shell
{
    public abstract class NiWindowPane : NiIsolationClient, INiWindowPane
    {
        private IServiceProvider _serviceProvider;
        private bool _disposed;
        private int _commandTargetCookie;

        public event NiWindowShowEventHandler FrameShow;

        protected virtual void OnFrameShow(NiWindowShowEventArgs e)
        {
            var ev = FrameShow;
            if (ev != null)
                ev(this, e);
        }

        public INiWindowFrame Frame { get; set; }

        public HResult SetSite(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            return HResult.OK;
        }

        public HResult GetSite(out IServiceProvider serviceProvider)
        {
            serviceProvider = _serviceProvider;
            return HResult.OK;
        }

        public virtual HResult Initialize()
        {
            try
            {
                // Frame holds on to a reference to the listener, so we don't have to.

                if (Frame != null)
                    new Listener(this);

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        [DebuggerStepThrough]
        public new object GetService(Type serviceType)
        {
            return _serviceProvider.GetService(serviceType);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_commandTargetCookie != 0)
                {
                    ((INiCommandManager)GetService(typeof(INiCommandManager))).UnregisterCommandTarget(_commandTargetCookie);
                    _commandTargetCookie = 0;
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private class Listener : NiEventSink, INiWindowFrameNotify
        {
            private static readonly ILog Log = LogManager.GetLogger(typeof(Listener));

            private readonly NiWindowPane _window;

            public Listener(NiWindowPane owner)
                : base(owner.Frame)
            {
                _window = owner;
            }

            public void OnShow(NiWindowShow action)
            {
                try
                {
                    _window.OnFrameShow(new NiWindowShowEventArgs(action));

                    if (action == NiWindowShow.Close)
                        Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warn("Failed to publish frame show event", ex);
                }
            }

            public void OnSize()
            {
            }

            public void OnClose(NiFrameCloseMode closeMode, ref bool cancel)
            {
            }
        }
    }
}
