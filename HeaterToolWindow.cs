using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace GPU_Heater_VS
{
    [Guid("c8d4a6e2-1b3f-4e5a-9a8c-7f2e1d0b3a4c")]
    public class HeaterToolWindow : ToolWindowPane
    {
        public HeaterToolWindow() : base(null)
        {
            this.Caption = "GPU-Heater Coder";
            this.Content = new HeaterToolWindowControl();
        }
    }
}
