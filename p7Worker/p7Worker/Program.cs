using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;
using p7Worker;

namespace p7Worker;

internal class Program
{
    static void Main(string[] args)
    {
        
        var worker = new Worker();

        try
        {
            //WorkerInfo.WorkerInfo.DownloadFTPFile();
            //worker.CreateAndExecuteContainerAsync().RunSynchronously();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something went wrong");
            //rabbitMQHandler.SendMessage($"Status: Failed on error: {ex}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
