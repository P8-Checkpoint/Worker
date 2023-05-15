using Xunit;
using Moq;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text.Json;
using System.Reflection;
using FluentFTP;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Text;
using System.Threading;
using p8Worker;
using p8Worker.Filehandler;

namespace p8WorkerTest;

public class FileOperationsTest
{
    public FileOperationsTest()
    {
        //var fo = new FileOperationsLinux("/p7");
        //string filePath = "/p7/predTest.py";
        //var file = File.Create(filePath);
        //file.Close();
    }
}
