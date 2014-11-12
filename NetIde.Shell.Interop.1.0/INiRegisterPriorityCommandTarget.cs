﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetIde.Shell.Interop
{
    public interface INiRegisterPriorityCommandTarget
    {
        HResult RegisterPriorityCommandTarget(INiCommandTarget commandTarget, out int cookie);
        HResult UnregisterPriorityCommandTarget(int cookie);
    }
}