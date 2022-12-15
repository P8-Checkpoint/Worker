using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Serilog;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Docker.DotNet;
using Docker.DotNet.Models;
using p7Worker.DTOs;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net;

namespace p7Worker;

public class Worker
{
    RabbitMQHandler _handler;
    ContainerController _containerController;
    FileOperations _fileOperations;
    string _container;
    string _image;
    string _payloadName;
    string _storageDirectory;


    public Worker()
    {
        Init();
    }

    void Init()
    {
        _container = "WorkerImage";
        _image = "Benchmark";
        _payloadName = "payload.py";
        WorkerInfo.WorkerId = Guid.NewGuid().ToString();
        _handler = new RabbitMQHandler();
        _containerController = new ContainerController();
        _fileOperations = new FileOperations();
        Connect();
    }

    public void Connect()
    {
        _handler.Register();
        _handler.AddWorkerConsumer(WorkerConsumer);
        Thread.Sleep(100);
        _handler.SendMessage($"{WorkerInfo.WorkerId} is active and ready to recieve work1");
    }

    public async Task CreateAndExecuteContainerAsync()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/p7-{WorkerInfo.WorkerId}-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Console.WriteLine("Starting Program...");
        Log.Information($"Hello, {Environment.UserName}!");

        // Log total elapsed time per run
        var totalTime = Stopwatch.StartNew();

        // Create a container
        await _containerController.CreateContainerAsync(_container, _image);

        // Start Container
        string containerID = await _containerController.GetContainerIDByNameAsync(_container);
        await _containerController.StartAsync(containerID);
        // sudo docker start -a f74a2f3f24c99857de4a91fa8deb56d16b8e640052ba16f19d7e025f30be3746

        // Execute payload
        // cc.Execute(containerID, payloadName, 500);
        _containerController.ExecuteWithoutCheckpointing(containerID, _payloadName);

        // Extract all checkpoint files
        // fileOperations.MoveAllCheckpointsFromContainer(containerID);
        // sudo docker cp 595fe85ccd872649b4dc64a7af3dfa2562fcceef1a8c1aa285b283ba3e7e8f31:/worker.result./ worker.result

        await _containerController.DeleteContainerAsync(containerID);

        totalTime.Stop();
        Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");
        // Log.Logger.Information($"Elapsed total time for run {i} with payload {payloadName}: {totalTime.ElapsedMilliseconds}ms");
    }

    void WorkerConsumer(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        if (!ea.BasicProperties.Headers.ContainsKey("type"))
            return;

        switch (ea.BasicProperties.Headers["type"].ToString())
        {
            case "startJob":
                var startJobInfo = JsonSerializer.Deserialize<JobStartDTO>(message);

                break;

            case "recoverJob":
                var recoverJobInfo = JsonSerializer.Deserialize<JobStartDTO>(message);

                break;
            case "type3":
                break;
            case "type4":
                break;
            case "type5":
                break;

            default:
                break;
        }
    }

    void Downloadftpfile(string ftpLink)
    {
        string downloadname = "workerdownload";
        string imagename = "workerimage";

        using (WebClient request = new WebClient())
        {
            //request.Credentials = new NetworkCredential("p1user", "1234");
            byte[] filedata = request.DownloadData(ftpLink);

            using (FileStream file = File.Create(downloadname))
            {
                file.Write(filedata, 0, filedata.Length);
                file.Close();
            }
            Console.WriteLine("download complete");
        }

        File.Move($"{downloadname}/{imagename}", $"/var/lib/docker/images/{imagename}");
    }
}
