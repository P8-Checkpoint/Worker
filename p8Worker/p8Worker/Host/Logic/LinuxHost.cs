using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using p8Worker.Host.Interfaces;
using Serilog;

namespace p8Worker.Host.Logic
{
    internal class LinuxHost : IHost
    {
        public void MimicMachineFailur()
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "shutdown";
                process.StartInfo.Arguments = $"-r now";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }

            Log.Information("Fault mimicked");
        }
    }
}
