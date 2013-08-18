﻿using System.Text;
using System.Collections.Generic;
using System;
using System.Windows.Forms;

namespace NetIde.Services.CommandManager.Controls
{
    internal class MenuItemBarControl : ToolStripMenuItem, IBarControl
    {
        ToolStripItemCollection IBarControl.Items { get { return DropDownItems; } }

        public ControlControl CreateButton(IServiceProvider serviceProvider, NiCommandBarButton button)
        {
            return new ButtonControl.MenuItem(serviceProvider, button);
        }

        public ControlControl CreateComboBox(IServiceProvider serviceProvider, NiCommandBarComboBox comboBox)
        {
            throw new NetIdeException(Labels.ComboBoxNotSupported);
        }

        public ControlControl CreatePopup(IServiceProvider serviceProvider, NiCommandBarPopup popup)
        {
            return new PopupControl<MenuItemBarControl>(serviceProvider, popup, ToolStripItemDisplayStyle.ImageAndText);
        }
    }
}