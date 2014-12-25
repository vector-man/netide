﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetIde.Shell.Interop;

namespace NetIde.Project.Interop
{
    public interface INiQuerySave
    {
        HResult QuerySaveViaDialog(INiHierarchy[] hiers, out NiQuerySaveResult result);
    }
}
