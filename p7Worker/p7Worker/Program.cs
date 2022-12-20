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
        var _handler = new RabbitMQHandler();
        var worker = new Worker(_handler);

        try
        {
            while (true)
            {
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something went wrong");
            var props = _handler.GetBasicProperties("type");
            Console.WriteLine(ex);
            _handler.SendMessage($"Status: Failed on error: {ex}", props);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
