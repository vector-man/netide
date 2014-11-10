﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetIde.Shell.Interop
{
    public interface INiCommandBar : INiCommandBarControl
    {
        string Text { get; set; }
        INiList<INiCommandBarGroup> Controls { get; }
        NiCommandBarKind Kind { get; }
        NiCommandDisplayStyle DisplayStyle { get; set; }
        IResource Image { get; set; }
    }
}
