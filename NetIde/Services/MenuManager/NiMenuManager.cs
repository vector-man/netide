﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetIde.Services.CommandManager;
using NetIde.Services.CommandManager.Controls;
using NetIde.Services.Env;
using NetIde.Shell;
using NetIde.Shell.Interop;

namespace NetIde.Services.MenuManager
{
    internal class NiMenuManager : ServiceBase, INiMenuManager
    {
        private static readonly int MaxInterval = (int)((Stopwatch.Frequency) / 1000 * 300);

        private readonly MainForm _mainForm;
        private readonly List<NiCommandBar> _commandBars = new List<NiCommandBar>();
        private readonly Timer _timer;
        private long _lastUpdate;
        private readonly NiCommandManager _commandManager;

        public NiMenuManager(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _mainForm = ((NiEnv)GetService(typeof(INiEnv))).MainForm;
            _commandManager = (NiCommandManager)GetService(typeof(INiCommandManager));

            _timer = new Timer
            {
                Interval = MaxInterval
            };

            _timer.Tick += (s, e) =>
            {
                _timer.Stop();

                QueryStatus();
            };

            System.Windows.Forms.Application.Idle += (s, e) =>
            {
                if (Stopwatch.GetTimestamp() - _lastUpdate > MaxInterval)
                    QueryStatus();
                else if (!_timer.Enabled)
                    _timer.Start();
            };
        }

        public HResult RegisterCommandBar(INiCommandBar commandBar)
        {
            try
            {
                if (commandBar == null)
                    throw new ArgumentNullException("commandBar");

                var command = (NiCommandBar)commandBar;

                if (command.Control != null)
                    throw new NetIdeException(Labels.CommandBarAlreadyRegistered);

                _commandBars.Add(command);

                command.Control = CreateHost(command);

                _mainForm.InsertCommandBar(command.Control);

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        private BarControl CreateHost(NiCommandBar commandBar)
        {
            switch (commandBar.Kind)
            {
                case NiCommandBarKind.Menu:
                    return new BarControl<MenuItemBarControl>(this, commandBar);

                case NiCommandBarKind.Toolbar:
                    return new BarControl<ToolStripBarControl>(this, commandBar);

                case NiCommandBarKind.Popup:
                    return new BarControl<ContextMenuStripBarControl>(this, commandBar);

                default:
                    throw new NetIdeException(Labels.InvalidCommandBarStyle);
            }
        }

        public HResult UnregisterCommandBar(INiCommandBar commandBar)
        {
            try
            {
                if (commandBar == null)
                    throw new ArgumentNullException("commandBar");

                var command = (NiCommandBar)commandBar;

                if (!_commandBars.Contains(command))
                    return HResult.False;

                command.Control.Dispose();
                command.Control = null;

                _commandBars.Remove(command);

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }

        public void QueryStatus()
        {
            _lastUpdate = Stopwatch.GetTimestamp();

            if (_mainForm.IsDisposing || _mainForm.IsDisposed)
                return;

            foreach (var bar in _commandBars)
            {
                QueryStatus(bar);
            }
        }

        public void QueryStatus(ICommandBarContainer bar)
        {
            foreach (var container in bar.Controls)
            {
                foreach (var command in container.Controls)
                {
                    if (!(command is INiCommandBarPopup))
                        QueryCommandStatus(command);
                }
            }
        }

        private void QueryCommandStatus(INiCommandBarControl command)
        {
            NiCommandStatus status;
            var hr = _commandManager.QueryStatus(command.Id, out status);
            ErrorUtil.ThrowOnFailure(hr);

            if ((status & NiCommandStatus.Supported) != 0)
            {
                command.IsEnabled = (status & NiCommandStatus.Enabled) != 0;
                command.IsVisible = (status & NiCommandStatus.Invisible) == 0;

                var button = command as INiCommandBarButton;

                if (button != null)
                    button.IsLatched = (status & NiCommandStatus.Latched) != 0;
            }

            var comboBox = command as NiCommandBarComboBox;

            if (comboBox != null && comboBox.IsVisible)
            {
                object result;
                hr = _commandManager.Exec(command.Id, null, out result);
                ErrorUtil.ThrowOnFailure(hr);

                if (hr == HResult.OK)
                    comboBox.SelectedValue = (string)result;
                else
                    comboBox.SelectedValue = null;
            }
        }

        public HResult ShowContextMenu(Guid popupId, Point location)
        {
            try
            {
                var commandManager = (INiCommandManager)GetService(typeof(INiCommandManager));

                INiCommandBar commandBar;
                var hr = commandManager.FindCommandBar(popupId, out commandBar);
                if (!ErrorUtil.Success(hr))
                    return hr;

                if (commandBar == null)
                    return HResult.False;

                if (commandBar.Kind != NiCommandBarKind.Popup)
                    throw new NetIdeException(Labels.MenuIsNotPopup);

                var host = CreateHost((NiCommandBar)commandBar);

                QueryStatus(host.Bar);

                var contextMenu = (ContextMenuStrip)host.Control;

                contextMenu.Disposed += (s, e) => host.Dispose();

                contextMenu.Show(location);

                return HResult.OK;
            }
            catch (Exception ex)
            {
                return ErrorUtil.GetHResult(ex);
            }
        }
    }
}