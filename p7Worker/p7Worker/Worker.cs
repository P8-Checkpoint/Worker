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
    string _container;
    string _image;
    string _payloadName;
    string _checkpointName;
    string _storageDirectory;
    int _responseFrequency;

    public Worker(RabbitMQHandler handler)
    {
        _handler = handler;
        Init();
    }

    void Init()
    {
        _container = "worker";
        _image = "workerimage.tar";
        _payloadName = "payload.py";
        _checkpointName = "checkpoint";
        _storageDirectory = "p7";
        _responseFrequency = 20;
        WorkerInfo.WorkerId = Guid.NewGuid().ToString();
        // _handler = new RabbitMQHandler();
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
        string image = "python:3.10-alpine";

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
        await _containerController.CreateContainerAsync(_container, image);

        // Start Container
        string containerID = _containerController.GetContainerIDByNameAsync(_container).Result;

        Console.WriteLine($"The id is {containerID}");

        _fileOperations.MovePayloadIntoContainer("20seconds.py", _container);

        await _containerController.StartAsync(containerID);

        await _containerController.DeleteContainerAsync(containerID);

        totalTime.Stop();
        Log.Logger.Information($"Elapsed total time for run {"test"} with payload {_payloadName}: {totalTime.ElapsedMilliseconds}ms");
    }

    public async Task RecoverAndExecuteContainerAsync()
    {
        string image = "python:3.10-alpine";

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
        await _containerController.CreateContainerAsync(_container, image);

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
                Console.WriteLine("Received");
                var startProps = _handler.GetBasicProperties("type");

                var startJobInfo = JsonSerializer.Deserialize<JobStartDTO>(message);
                // string downloadStartName = "workerDownload";

                // Downloadftpfile(startJobInfo.FTPLink, downloadStartName);

                _containerController.LoadImageAsync($"/p7/workerdownload/workerimage.tar").Wait();

                var startResponse = new BasicResponse { WorkerId = WorkerInfo.WorkerId, JobId = startJobInfo.JobId };
                _handler.SendMessage(JsonSerializer.Serialize(startResponse), startProps);

                PeriodicStillUpResponse(TimeSpan.FromSeconds(_responseFrequency), () =>
                {
                    _handler.SendMessage(JsonSerializer.Serialize(startResponse), startProps);
                }, async () =>
                {
                    await CreateAndExecuteContainerAsync();
                }).RunSynchronously();

                // Console.WriteLine(startJobInfo.FTPLink);
                // Console.WriteLine(startJobInfo.JobId);
                // Console.WriteLine(startJobInfo.ResultPath);
                // Console.WriteLine(startJobInfo.SourcePath);

                _handler.SendMessage(JsonSerializer.Serialize(startResponse), startProps);
                Console.WriteLine("All done UWU!");
                break;

            case "recoverJob":
                var recoverProps = _handler.GetBasicProperties("type");

                var recoverJobInfo = JsonSerializer.Deserialize<JobRecoverDTO>(message);
                // string downloadRecoverName = "workerDownload";

                // Downloadftpfile(recoverJobInfo.FTPLink, downloadRecoverName);

                _containerController.LoadImageAsync($"/p7/workerdownload/{_image}").RunSynchronously();

                var recoverResponse = new BasicResponse { WorkerId = WorkerInfo.WorkerId, JobId = recoverJobInfo.JobId };
                _handler.SendMessage(JsonSerializer.Serialize(recoverResponse), recoverProps);

                // PeriodicStillUpResponse(TimeSpan.FromSeconds(_responseFrequency), () =>
                // {
                //     _handler.SendMessage(JsonSerializer.Serialize(recoverResponse), recoverProps);
                // }, async () =>
                // {
                //     await RecoverAndExecuteContainerAsync();
                // }).Wait();

                _handler.SendMessage(JsonSerializer.Serialize(recoverResponse), recoverProps);
                break;

            case "stopJob":
                // string containerID = _containerController.GetContainerIDByNameAsync(_container).Result;
                // _containerController.StopContainer(containerID).Wait();

                Thread.Sleep(100);

                var stopProps = _handler.GetBasicProperties("type");
                var stopResponse = new BasicResponse { WorkerId = WorkerInfo.WorkerId };
                _handler.SendMessage(JsonSerializer.Serialize(stopResponse), stopProps);
                break;

            case "ftpJob":
                var ftpProps = _handler.GetBasicProperties("type");
                var ftpJobInfo = JsonSerializer.Deserialize<JobRecoverDTO>(message);
                Console.WriteLine("yes");
                string ftp = "p1.servicehost:p1user:1234:bcc6c325-d254-4ae3-8606-7b18968e7412/9750304b-4fe7-434c-8089-34d42bee1023/source/testt.py";

                string[] parts = ftp.Split(':');

                foreach (string part in parts)
                {
                    Console.WriteLine(part);
                }

                var client = new FtpClient(parts[0], parts[1], parts[2]);
                string res = "/p7/magnus.py";
                client.AutoConnect();
                client.UploadFile(res, @"bcc6c325-d254-4ae3-8606-7b18968e7412/9750304b-4fe7-434c-8089-34d42bee1023/result/magnus.py");
                Console.WriteLine("sir");
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
            byte[] filedata = request.DownloadData($"/p7/{ftpLink}");

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

        response();
        action();

        // var periodicTimer = new PeriodicTimer(timeSpan);
        // do
        // {
        //     response();
        //     action();
        // }
        // while (await periodicTimer.WaitForNextTickAsync());
    }
}
