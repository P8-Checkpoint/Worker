﻿using System;
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
    string _checkpointName;
    string _storageDirectory;
    int _responseFrequency;
    int _checkpointFrequency;
    bool _runningContainer;

    public Worker(RabbitMQHandler handler)
    {
        _handler = handler;
        Init();
    }

    void Init()
    {
        _containerName = "worker";
        _checkpointName = "checkpoint";
        _storageDirectory = "/p7";
        _resultName = "worker.result";
        _responseFrequency = 10000;
        _checkpointFrequency = 5000;
        WorkerInfo.WorkerId = Guid.NewGuid().ToString();
        _containerController = new ContainerController();
        _fileOperations = new FileOperations(_storageDirectory);
        _runningContainer = false;
        Connect();
    }

    public void Connect()
    {
        _handler.Register(RegisterResponseRecieved);
    }

    public async Task CreateAndExecuteContainerAsync(string remoteBackupPath)
    {
        string image = "python:3.10-alpine";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/p7-{WorkerInfo.WorkerId}-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information($"Hello, {Environment.UserName}!");

        // Create a container
        await _containerController.CreateContainerAsync(_containerName, image, _payloadName);

        // Log total elapsed time per run
        var totalTime = Stopwatch.StartNew();

        // Start Container
        string containerID = _containerController.GetContainerIDByNameAsync(_containerName).Result;

        Console.WriteLine(Path.Combine(_storageDirectory, _payloadName));
        _fileOperations.PredFile(Path.Combine(_storageDirectory, _payloadName));

        _fileOperations.MovePayloadIntoContainer(_payloadName, _containerName);

        await _containerController.StartAsync(containerID);

        bool running = _containerController.ContainerIsRunningAsync(containerID).Result;
        int i = 0;
        while (running)
        {
            string checkpointNamei = _checkpointName + i.ToString();
            _containerController.Checkpoint(_containerName, _checkpointName + i.ToString());

            _fileOperations.MoveCheckpointFromContainer(checkpointNamei, containerID);
            // _ftpClient.UploadFile(Path.Combine(_storageDirectory, _checkpointName), remoteBackupPath);
            Console.Write("\nUploaded checkpoint\n");
            Thread.Sleep(_checkpointFrequency);
            i++;
            running = _containerController.ContainerIsRunningAsync(containerID).Result;
        }

        totalTime.Stop();
        Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");

        _fileOperations.ExtractResultFromContainer(_resultName, containerID);

        await _containerController.DeleteContainerAsync(containerID);

        _runningContainer = false;
    }

    // public async Task RecoverAndExecuteContainerAsync()
    // {
    //     string image = "python:3.10-alpine";

    //     Log.Logger = new LoggerConfiguration()
    //         .MinimumLevel.Debug()
    //         .WriteTo.Console()
    //         .WriteTo.File($"logs/p7-{WorkerInfo.WorkerId}-log.txt", rollingInterval: RollingInterval.Day)
    //         .CreateLogger();

    //     Console.WriteLine("Starting Program...");
    //     Log.Information($"Hello, {Environment.UserName}!");

    //     // Log total elapsed time per run
    //     var totalTime = Stopwatch.StartNew();

    //     // Create a container
    //     await _containerController.CreateContainerAsync(_containerName, image, Path.Combine(_storageDirectory, _payloadName));

    //     // Move checkpoint into container and start
    //     string checkpointID = _containerController.GetContainerIDByNameAsync(_containerName).Result;
    //     _fileOperations.MoveCheckpointIntoContainer(_checkpointName, checkpointID);
    //     _containerController.StartAsync(checkpointID).RunSynchronously();

    //     await _containerController.DeleteContainerAsync(checkpointID);

    //     totalTime.Stop();
    //     Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");
    // }

    void WorkerConsumer(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();

        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(message);

        if (!ea.BasicProperties.Headers.ContainsKey("type"))
        {
            return;
        }

        var props = _handler.GetBasicProperties("type");

        switch (Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["type"]))
        {
            case "startJob":
                var startJobInfo = JsonSerializer.Deserialize<JobStartDTO>(message);
                _handler.SendMessage(JsonSerializer.Serialize(startJobInfo), props);

                string[] parts = startJobInfo.FTPLink.Split(':');
                _payloadName = startJobInfo.SourcePath.Split('/').Last();

                Console.WriteLine("Set PayloadName");
                _payloadName = "20seconds.py";

                // _ftpClient = new FtpClient(parts[0], parts[1], parts[2]);
                // _ftpClient.AutoConnect();
                // DownloadFTPfile(startJobInfo.SourcePath);
                Console.WriteLine("Downloaded source");

                Console.WriteLine("Running Container True");
                _runningContainer = true;
                // CreateAndExecuteContainerAsync(startJobInfo.BackupPath);

                var task = Task.Run(() =>
                {
                    Console.WriteLine("\n\nCreating Container");
                    CreateAndExecuteContainerAsync("/p7/backup/checkpoint");
                    Console.WriteLine("\n\nDone creating and running");

                    while (_runningContainer)
                    {
                        Console.WriteLine("\n\nResponse sent");
                        Thread.Sleep(_responseFrequency);
                        // WorkerReportDTO workerReport = new WorkerReportDTO(WorkerInfo.WorkerId, Guid.Parse(startJobInfo.JobId));
                        // _handler.SendMessage(JsonSerializer.Serialize(workerReport), props);
                        Console.WriteLine("\n\nContainer is running outer:" + _runningContainer);
                    }
                });

                Task.WaitAll(task);

                Console.WriteLine("Done Running Container");

                // UploadFTPfile(startJobInfo.ResultPath);
                Console.WriteLine("Uploaded result");
                _handler.SendMessage(JsonSerializer.Serialize(startJobInfo), props);
                Console.WriteLine("Done UWU");
                break;

            // case "recoverJob":
            //     var recoverJobInfo = JsonSerializer.Deserialize<JobRecoverDTO>(message);
            //     _handler.SendMessage(JsonSerializer.Serialize(recoverJobInfo), props);

            //     DownloadFTPfile(recoverJobInfo.FTPLink, recoverJobInfo.SourcePath); // Source
            //     DownloadFTPfile(recoverJobInfo.FTPLink, recoverJobInfo.BackupPath); // Checkpoint

            //     _checkpointName = recoverJobInfo.BackupPath.Split("/").Last();
            //     _runningContainer = true;
            //     RecoverAndExecuteContainerAsync();

            //     Task.Run(() =>
            //     {
            //         while (_runningContainer)
            //         {
            //             Thread.Sleep(_responseFrequency);
            //             WorkerReportDTO workerReport = new WorkerReportDTO(WorkerInfo.WorkerId, Guid.Parse(recoverJobInfo.JobId));
            //             _handler.SendMessage(JsonSerializer.Serialize(workerReport), props);
            //         }
            //     });

            //     UploadFTPfile(recoverJobInfo.FTPLink, recoverJobInfo.ResultPath);
            //     _handler.SendMessage(JsonSerializer.Serialize(recoverJobInfo), props);
            //     break;

            case "stopJob":
                string containerID = _containerController.GetContainerIDByNameAsync(_containerName).Result;
                _containerController.StopContainer(containerID).Wait();
                Console.WriteLine("Stopped");
                // _handler.SendMessage("Stopped", props);
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

    void DownloadFTPfile(string remoteSourcePath)
    {
        string localSourcePath = Path.Combine(_storageDirectory, _payloadName);

        _ftpClient.DownloadFile(localSourcePath, remoteSourcePath);
    }

    void UploadFTPfile(string remoteResultPath)
    {
        string localResultPath = Path.Combine(_storageDirectory, _resultName);

        _ftpClient.UploadFile(localResultPath, Path.Combine(remoteResultPath, _resultName));
    }
}
