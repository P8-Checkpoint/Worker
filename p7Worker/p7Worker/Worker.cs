using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Serilog;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Docker.DotNet;
using Docker.DotNet.Models;
using p7Worker.WorkerInfo;
using p7Worker.DTOs;
using RabbitMQ.Client;

namespace p7Worker;

public class Worker
{
    RabbitMQHandler handler;
    ContainerController containerController;
    FileOperations fileOperations;
    string _serverName;
    string _replyQueueName;
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
        containerController = new ContainerController();
        fileOperations = new FileOperations();
    }

    public void Connect(RabbitMQHandler rabbitMQHandler)
    {
        rabbitMQHandler.Register();
        Thread.Sleep(100);
        string workerID = WorkerInfo.WorkerInfo.GetWorkerId();
        rabbitMQHandler.SendMessage($"{workerID} is active and ready to recieve work1");
    }

    public async Task CreateAndExecuteContainerAsync()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/p7-{WorkerInfo.WorkerInfo.GetWorkerId()}-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Console.WriteLine("Starting Program...");
        Log.Information($"Hello, {Environment.UserName}!");

        // Log total elapsed time per run
        var totalTime = Stopwatch.StartNew();

        // Create a container
        await containerController.CreateContainerAsync(_container, _image);

        // Start Container
        string containerID = await containerController.GetContainerIDByNameAsync(_container);
        await containerController.StartAsync(containerID);
        // sudo docker start -a f74a2f3f24c99857de4a91fa8deb56d16b8e640052ba16f19d7e025f30be3746

        // Execute payload
        // cc.Execute(containerID, payloadName, 500);
        containerController.ExecuteWithoutCheckpointing(containerID, _payloadName);

        // Extract all checkpoint files
        // fileOperations.MoveAllCheckpointsFromContainer(containerID);
        // sudo docker cp 595fe85ccd872649b4dc64a7af3dfa2562fcceef1a8c1aa285b283ba3e7e8f31:/worker.result./ worker.result

        await containerController.DeleteContainerAsync(containerID);

        totalTime.Stop();
        Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");
        // Log.Logger.Information($"Elapsed total time for run {i} with payload {payloadName}: {totalTime.ElapsedMilliseconds}ms");
    }
}
