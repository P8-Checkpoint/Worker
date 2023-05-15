using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using p8Worker.Host.Interfaces;

namespace p8Worker.Host.Logic
{
    internal class WindowsHost : IHost
    {
        public void MimicMachineFailur()
        {
            Thread.Sleep(1);
        }
    }
}
