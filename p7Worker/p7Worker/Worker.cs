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
using FluentFTP;

namespace p7Worker;

public class Worker
{
    RabbitMQHandler _handler;
    ContainerController _containerController;
    FileOperations _fileOperations;
    FtpClient _ftpClient;
    string _containerName;
    string _payloadName;
    string _resultName;
    string _imageName;
    string _checkpointName;
    string _storageDirectory;
    int _responseFrequency;
    int _checkpointFrequency;
    bool _runningContainer;

    public Worker(RabbitMQHandler handler, ContainerController containerController, FileOperations fileOperations)
    {
        _handler = handler;
        _containerController = containerController;
        _fileOperations = fileOperations;
        Init();
    }

    void Init()
    {
        _containerName = "worker";
        _checkpointName = "checkpoint";
        _storageDirectory = "/p7";
        _resultName = "worker.result";
        _imageName = "python:3.10-alpine";
        _responseFrequency = 10000;
        _checkpointFrequency = 5000;
        WorkerInfo.WorkerId = Guid.NewGuid().ToString();
        _runningContainer = false;
        Connect();
    }

    public void Connect()
    {
        _handler.Register(RegisterResponseRecieved);
    }

    // public async Task CreateAndExecuteContainerAsync(string remoteBackupPath)
    // {
    //     Log.Logger = new LoggerConfiguration()
    //         .MinimumLevel.Debug()
    //         .WriteTo.Console()
    //         .WriteTo.File($"logs/p7-{WorkerInfo.WorkerId}-log.txt", rollingInterval: RollingInterval.Day)
    //         .CreateLogger();

    //     Log.Information($"Hello, {Environment.UserName}!");

    //     // Create a container
    //     await _containerController.CreateContainerAsync(_containerName, _imageName, _payloadName);

    //     // Log total elapsed time per run
    //     var totalTime = Stopwatch.StartNew();

    //     // Start Container
    //     string containerID = _containerController.GetContainerIDByNameAsync(_containerName).Result;

    //     _fileOperations.PredFile(Path.Combine(_storageDirectory, _payloadName));
    //     _fileOperations.MovePayloadIntoContainer(_payloadName, _containerName);

    //     await _containerController.StartAsync(containerID);

    //     bool running = _containerController.ContainerIsRunningAsync(containerID).Result;
    //     int i = 0;
    //     while (running)
    //     {
    //         try
    //         {
    //             string checkpointNamei = _checkpointName + i.ToString();
    //             _containerController.Checkpoint(_containerName, _checkpointName + i.ToString());

    //             _fileOperations.MoveCheckpointFromContainer(checkpointNamei, containerID);

    //             _ftpClient.UploadDirectory(Path.Combine(_storageDirectory, checkpointNamei), $"{remoteBackupPath}{checkpointNamei}");
    //             Console.Write("\nUploaded checkpoint\n");

    //             Thread.Sleep(_checkpointFrequency);
    //             i++;

    //             running = _containerController.ContainerIsRunningAsync(containerID).Result;
    //             Console.WriteLine("\nRunning = " + _containerController.ContainerIsRunningAsync(containerID).Result);
    //         }
    //         catch (Exception ex)
    //         {
    //             Console.WriteLine(ex);
    //             break;
    //         }
    //     }

    //     totalTime.Stop();
    //     Log.Logger.Information($"\nElapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");

    //     Console.WriteLine($"\nExtracting result with name {_resultName} from container {containerID}");
    //     _fileOperations.ExtractResultFromContainer(_resultName, containerID);

    //     await _containerController.DeleteContainerAsync(containerID);

    //     _runningContainer = false;
    // }

    public async Task StartOrRecoverContainerAsync(string remoteBackupPath, string startRecover)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/p7-{WorkerInfo.WorkerId}-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information($"Hello, {Environment.UserName}!");

        // Create a container
        await _containerController.CreateContainerAsync(_containerName, _imageName, _payloadName);

        // Log total elapsed time per run
        var totalTime = Stopwatch.StartNew();

        // Move checkpoint into container and start
        string containerID = _containerController.GetContainerIDByNameAsync(_containerName).Result;

        // Move payload into Container
        if (startRecover == "recover")
        {
            _fileOperations.MoveCheckpointIntoContainer(_checkpointName, containerID);
        }

        // Start Container
        _fileOperations.PredFile(Path.Combine(_storageDirectory, _payloadName));
        _fileOperations.MovePayloadIntoContainer(_payloadName, _containerName);

        if (startRecover == "recover")
        {
            await _containerController.RestoreAsync(_checkpointName, _containerName);
        }
        else if (startRecover == "start")
        {
            await _containerController.StartAsync(containerID);
        }

        bool running = _containerController.ContainerIsRunningAsync(containerID).Result;
        int i = 0;
        while (running)
        {
            try
            {
                string checkpointNamei = _checkpointName + i.ToString();
                _containerController.Checkpoint(_containerName, _checkpointName + i.ToString());

                _fileOperations.MoveCheckpointFromContainer(checkpointNamei, containerID);

                _ftpClient.UploadDirectory(Path.Combine(_storageDirectory, checkpointNamei), $"{remoteBackupPath}{checkpointNamei}");
                Console.Write("\nUploaded checkpoint\n");

                Thread.Sleep(_checkpointFrequency);
                i++;

                running = _containerController.ContainerIsRunningAsync(containerID).Result;
                Console.WriteLine("\nRunning = " + _containerController.ContainerIsRunningAsync(containerID).Result);
            }
            catch (System.Exception ex)
            {

                Console.WriteLine(ex);
                break;
            }
        }

        totalTime.Stop();
        Log.Logger.Information($"\nElapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");

        Console.WriteLine($"\nExtracting result with name {_resultName} from container {containerID}");
        _fileOperations.ExtractResultFromContainer(_resultName, containerID);

        await _containerController.DeleteContainerAsync(containerID);

        _runningContainer = false;
    }

    void WorkerConsumer(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();

        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(message);

        if (!ea.BasicProperties.Headers.ContainsKey("type"))
        {
            return;
        }


        switch (Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["type"]))
        {
            case "startJob":
                Console.WriteLine("\nStarted Job\n");
                var startJobInfo = JsonSerializer.Deserialize<JobStartDTO>(message);
                _handler.SendMessage(startJobInfo.Id.ToString(), _handler.GetBasicProperties("startJob"));

                string[] startParts = startJobInfo.SourcePath.Split(':');
                _payloadName = startJobInfo.SourcePath.Split('/').Last();
                Console.WriteLine("\nSet PayloadName");

                Console.WriteLine("\nPayloadName: " + _payloadName);
                Console.WriteLine("\nResultPath: " + $"{startJobInfo.ResultPath.Split(":").Last()}{_resultName}");
                Console.WriteLine("\nBackupPath: " + $"{startJobInfo.BackupPath.Split(":").Last()}{_checkpointName}");

                _ftpClient = new FtpClient(startParts[0], startParts[1], startParts[2]);
                _ftpClient.Connect();
                DownloadFTPFile(startParts[3]);
                Console.WriteLine("\nDownloaded source");

                _runningContainer = true;

                var startTask = Task.Run(() =>
                {
                    Console.WriteLine("\nCreating Container");
                    StartOrRecoverContainerAsync(startJobInfo.BackupPath.Split(":").Last(), "start");
                    Console.WriteLine("\nDone creating and running");

                    while (_runningContainer)
                    {
                        Console.WriteLine("\nResponse sent");
                        Thread.Sleep(_responseFrequency);
                        WorkerReportDTO workerReport = new WorkerReportDTO(WorkerInfo.WorkerId, Guid.Parse(startJobInfo.Id.ToString()));
                        _handler.SendMessage(JsonSerializer.Serialize(workerReport), _handler.GetBasicProperties("report"));
                    }
                });

                Task.WaitAll(startTask);

                Console.WriteLine("\nDone Running Container");

                Console.WriteLine("\nUploading :" + $"{startJobInfo.ResultPath.Split(":").Last()}{_resultName}");
                UploadFTPfile($"{startJobInfo.ResultPath.Split(":").Last()}{_resultName}");
                Console.WriteLine("\nUploaded result");
                Console.WriteLine("\nDone with job");
                _handler.SendMessage(startJobInfo.Id.ToString(), _handler.GetBasicProperties("jobDone"));
                break;

            case "recoverJob":
                var recoverJobInfo = JsonSerializer.Deserialize<JobRecoverDTO>(message);
                _handler.SendMessage(recoverJobInfo.Id.ToString(), _handler.GetBasicProperties("recoverJob"));
                string[] recoverParts = recoverJobInfo.SourcePath.Split(':');
                _payloadName = recoverJobInfo.SourcePath.Split('/').Last();
                Console.WriteLine("\nSet PayloadName");

                Console.WriteLine("\nPayloadName " + _payloadName);
                Console.WriteLine("\nResultPath: " + $"{recoverJobInfo.ResultPath.Split(":").Last()}{_resultName}");
                Console.WriteLine("\nBackupPath: " + $"{recoverJobInfo.BackupPath.Split(":").Last()}{_checkpointName}");

                _ftpClient = new FtpClient(recoverParts[0], recoverParts[1], recoverParts[2]);
                _ftpClient.Connect();
                DownloadFTPFile(recoverParts[3]); // Source
                Console.WriteLine("\nDownloaded source");
                DownloadFTPFile(recoverJobInfo.BackupPath.Split(":")[3]); // Checkpoint
                Console.WriteLine("\nDownloaded recovery backup");

                _runningContainer = true;

                var recoverTask = Task.Run(() =>
                {
                    Console.WriteLine("\nCreating Container");
                    StartOrRecoverContainerAsync(recoverJobInfo.BackupPath.Split(":").Last(), "recover");
                    Console.WriteLine("\nDone Creating and running");

                    while (_runningContainer)
                    {
                        Console.WriteLine("\nResponse sent");
                        Thread.Sleep(_responseFrequency);
                        WorkerReportDTO workerReport = new WorkerReportDTO(WorkerInfo.WorkerId, Guid.Parse(recoverJobInfo.Id.ToString()));
                        _handler.SendMessage(JsonSerializer.Serialize(workerReport), _handler.GetBasicProperties("report"));
                    }
                });

                Task.WaitAll(recoverTask);

                Console.WriteLine("\nDone Running Container");

                Console.WriteLine("\nUploading :" + $"{recoverJobInfo.ResultPath.Split(":").Last()}{_resultName}");
                UploadFTPfile($"{recoverJobInfo.ResultPath.Split(":").Last()}{_resultName}");
                Console.WriteLine("\nUploaded result");
                Console.WriteLine("\nDone with job");
                _handler.SendMessage(recoverJobInfo.Id.ToString(), _handler.GetBasicProperties("jobDone"));
                break;

            case "stopJob":
                string containerID = _containerController.GetContainerIDByNameAsync(_containerName).Result;
                _containerController.StopContainer(containerID).Wait();
                Console.WriteLine("Stopped");
                break;

            default:
                break;
        }
    }

    void RegisterResponseRecieved(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var response = Encoding.UTF8.GetString(body);

        RegisterResponseDTO? responseJson = JsonSerializer.Deserialize<RegisterResponseDTO>(response);
        WorkerInfo.WorkerId = responseJson.WorkerId;
        WorkerInfo.ServerName = responseJson.ServerName;
        _handler.DeclareWorkerQueue();
        _handler.Connect();
        _handler.AddWorkerConsumer(WorkerConsumer);
    }

    void DownloadFTPFile(string remoteSourcePath)
    {
        string localSourcePath = Path.Combine(_storageDirectory, _payloadName);

        _ftpClient.DownloadFile(localSourcePath, remoteSourcePath);
    }

    void UploadFTPfile(string remoteResultPath)
    {
        string localResultPath = Path.Combine(_storageDirectory, _resultName);

        _ftpClient.UploadFile(localResultPath, remoteResultPath);
    }
}
