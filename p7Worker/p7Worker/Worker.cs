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
    string _checkpointName;
    string _storageDirectory;
    int _responseFrequency;

    public Worker()
    {
        Init();
    }

    void Init()
    {
        _container = "WorkerImage";
        _image = "python:3.10-alpine";
        _payloadName = "payload.py";
        _checkpointName = "checkpoint";
        _storageDirectory = "p7";
        _responseFrequency = 20;
        WorkerInfo.WorkerId = Guid.NewGuid().ToString();
        _handler = new RabbitMQHandler();
        _containerController = new ContainerController();
        _fileOperations = new FileOperations();
        Connect();
    }

    public void Connect()
    {
        _handler.Register(RegisterResponseRecieved);
        Thread.Sleep(100);
        var props = _handler.GetBasicProperties("type");
        _handler.SendMessage($"{WorkerInfo.WorkerId} is active and ready to recieve work!", props);
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

        await _containerController.DeleteContainerAsync(containerID);

        totalTime.Stop();
        Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");
    }

    public async Task RecoverAndExecuteContainerAsync()
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
        _containerController.CreateContainerAsync(_container, _image).RunSynchronously();

        // Move checkpoint into container and start
        string checkpointID = _containerController.GetContainerIDByNameAsync(_container).Result;
        _fileOperations.MoveCheckpointIntoContainer(_checkpointName, checkpointID);
        _containerController.StartAsync(checkpointID).RunSynchronously();

        await _containerController.DeleteContainerAsync(checkpointID);

        totalTime.Stop();
        Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");
    }

    void WorkerConsumer(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        if (!ea.BasicProperties.Headers.ContainsKey("type"))
            return;
        switch (Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["type"]))
        {
            case "startJob":
                var startProps = _handler.GetBasicProperties("type");
                _handler.SendMessage($"{WorkerInfo.WorkerId} is starting job!", startProps);

                var startJobInfo = JsonSerializer.Deserialize<JobStartDTO>(message);
                string downloadStartName = "workerDownload";
                Downloadftpfile(startJobInfo.FTPLink, downloadStartName);
                File.Move($"{downloadStartName}/{_image}", $"/var/lib/docker/images/{_image}");

                _handler.SendMessage($"{WorkerInfo.WorkerId} has downloaded job and is starting on {startJobInfo.JobId}!", startProps);

                PeriodicStillUpResponse(TimeSpan.FromSeconds(_responseFrequency), () =>
                {
                    _handler.SendMessage($"{WorkerInfo.WorkerId} is still working on {startJobInfo.JobId}!", startProps);
                }, async () =>
                {
                    await CreateAndExecuteContainerAsync();
                }).Wait();

                _handler.SendMessage($"{WorkerInfo.WorkerId} is done with {startJobInfo.JobId}!", startProps);
                break;

            case "recoverJob":
                var recoverProps = _handler.GetBasicProperties("type");
                _handler.SendMessage($"{WorkerInfo.WorkerId} is recovering job!", recoverProps);

                var recoverJobInfo = JsonSerializer.Deserialize<JobRecoverDTO>(message);
                string downloadRecoverName = "workerDownload";
                Downloadftpfile(recoverJobInfo.FTPLink, downloadRecoverName);
                File.Move($@"/{_storageDirectory}/{downloadRecoverName}/{_image}", $"/var/lib/docker/images/{_image}");
                File.Move($@"/{_storageDirectory}/{downloadRecoverName}/", $"/var/lib/docker/images/{_image}");

                _handler.SendMessage($"{WorkerInfo.WorkerId} has downloaded job and is starting on {recoverJobInfo.JobId}!", recoverProps);

                PeriodicStillUpResponse(TimeSpan.FromSeconds(_responseFrequency), () =>
                {
                    _handler.SendMessage($"{WorkerInfo.WorkerId} is still working on {recoverJobInfo.JobId}", recoverProps);
                }, async () =>
                {
                    await RecoverAndExecuteContainerAsync();
                }).Wait();

                _handler.SendMessage($"{WorkerInfo.WorkerId} is done with {recoverJobInfo.JobId}", recoverProps);
                break;

            case "stopJob":
                string containerID = _containerController.GetContainerIDByNameAsync(_container).Result;
                _containerController.StopContainer(containerID).Wait();
                var stopProps = _handler.GetBasicProperties("type");
                _handler.SendMessage($"{WorkerInfo.WorkerId} stopped working.", stopProps);
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

    void Downloadftpfile(string ftpLink, string downloadName)
    {
        using (WebClient request = new WebClient())
        {
            //request.Credentials = new NetworkCredential("p1user", "1234");
            byte[] filedata = request.DownloadData(ftpLink);

            using (FileStream file = File.Create(downloadName))
            {
                file.Write(filedata, 0, filedata.Length);
                file.Close();
            }
            Console.WriteLine("Download Complete!");
        }

        File.Move($"{downloadName}", $"/{_storageDirectory}/storage");
    }

    async Task PeriodicStillUpResponse(TimeSpan timeSpan, Action action, Action response)
    {
        var periodicTimer = new PeriodicTimer(timeSpan);
        while (await periodicTimer.WaitForNextTickAsync())
        {
            response();
            action();
        }
    }
}
