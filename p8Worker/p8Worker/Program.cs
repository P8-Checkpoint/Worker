using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;
using p8Worker.RabbitMQ;
using p8Worker.ContainerHandling.Logic;
using p8Worker.DTOs;
using p8Worker.Filehandler;
using p8Worker.ContainerHandling.Interfaces;
using Newtonsoft.Json;
using p8Worker.Host.Interfaces;
using p8Worker.Host.Logic;

namespace p8Worker;

internal class Program
{
    static void Main(string[] args)
    {
        IContainerHandler _containerHandler;
        IFileOperation _fileOperation;
        IHost _host;
        bool _keepAlive = true;
        int _crashCounter = 0;
        ILogger _logger = Log.Logger = new LoggerConfiguration()
         .MinimumLevel.Debug()
         .WriteTo.Console()
         .WriteTo.File($"logs/p7-{WorkerInfoDto.WorkerId}-log.txt", rollingInterval: RollingInterval.Day)
         .CreateLogger();
        string storageDir = string.Empty;

#if DEBUGLOCAL
        storageDir = Environment.CurrentDirectory;
        _host = new WindowsHost();
        _containerHandler = new WindowsContainerController();
        _fileOperation = new FileOperationWindows(storageDir);
#else
        _host = new LinuxHost();
        storageDir = "/p7";
        _containerHandler = new LinuxContainerController();
        _fileOperation = new FileOperationsLinux(storageDir, _logger);
#endif

        var _handler = new RabbitMQHandler(_logger);
        var worker = new Worker(_handler, _containerHandler, _fileOperation, _logger, storageDir, _host);

        while (_keepAlive)
        {
            try
            {
                var val = worker.Update().Result;
                _crashCounter = 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Something went wrong");
                var props = _handler.GetBasicProperties("type");
                _logger.Error(ex.ToString());
                _handler.SendMessage($"Status: Failed on error: {ex}", props);
            }
            finally
            {
                //Shutdown application if worker appears to crash
                if(_crashCounter++ > 10)
                {
                    _keepAlive= false;
                    Thread.Sleep(10000);
                }
            }
            Thread.Sleep(100);
        }
    }
}
