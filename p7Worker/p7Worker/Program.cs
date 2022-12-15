﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;
using p7Worker;
using p7Worker.WorkerInfo;

namespace p7Worker;

internal class Program
{
    static void Main(string[] args)
    {
        // Change these values to suit your needs
        var rabbitMQHandler = new RabbitMQHandler();
        var worker = new Worker();

        try
        {
            worker.Connect(rabbitMQHandler);
            WorkerInfo.WorkerInfo.DownloadFTPFile();
            worker.CreateAndExecuteContainerAsync().RunSynchronously();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something went wrong");
            rabbitMQHandler.SendMessage($"Status: Failed on error: {ex}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
