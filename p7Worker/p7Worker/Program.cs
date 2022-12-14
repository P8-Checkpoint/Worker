using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace p7Worker;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Change these values to suit your needs
        string container = "magnustest1";
        string image = "busybox";
        string storageDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string payloadName = "4seconds";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/p7-{payloadName}-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        ContainerController cc = new ContainerController();
        FileOperations fo = new FileOperations();

        try
        {
            var rabbitMQHandler = new RabbitMQHandler();
            rabbitMQHandler.Register();
            Thread.Sleep(100);
            rabbitMQHandler.SendMessage($"Hello {Environment.UserName}");

            Console.WriteLine("Hello");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something went wrong");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
