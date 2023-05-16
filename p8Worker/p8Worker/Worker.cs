using System;
using System.Text;
using System.Text.Json;
using Serilog;
using System.Diagnostics;
using p8Worker.DTOs;
using RabbitMQ.Client.Events;
using FluentFTP;
using p8Worker.RabbitMQ;
using p8Worker.ContainerHandling.Interfaces;
using p8Worker.Filehandler;
using DeviceId;
using System.Security.Cryptography;
using p8Worker.Host.Interfaces;
using System.Net.Sockets;
using System.Net;

namespace p8Worker;

public class Worker
{
    enum State
    {
        Idle,
        InitTask,
        Running,
        Finished,
        Cancel
    }

    ILogger _logger;
    RabbitMQHandler _handler;
    IContainerHandler _containerController;
    IFileOperation _fileOperations;
    IHost _host;
    FtpClient _ftpClient;
    string _containerName;
    string _payloadName;
    string _resultName;
    string _imageName;
    string _checkpointName;
    string _storageDirectory;
    string _containerID = string.Empty;
    string _remoteBackupPath = string.Empty;
    int _checkpointCount;
    bool _runTask = false;
    State _workerState = State.Idle;
    ServiceTaskConfiguration _serviceTaskCfg;
    ServiceTaskDto _ServiceTask;
    Stopwatch _serviceTaskSw = new Stopwatch();
    DateTime _startTime = DateTime.UtcNow;
    DateTime _minimunKeepAlive = DateTime.UtcNow;
    Stopwatch _checkpointTime = new Stopwatch();
    DateTime _keepAliveDt = DateTime.UtcNow;
    DateTime _lastCheckPointdt;
    TimeSpan _keepAliveTs = TimeSpan.FromSeconds(5);


    public Worker(RabbitMQHandler handler, IContainerHandler containerController, IFileOperation fileOperations, ILogger logger, string storageDirectory, IHost hostSystem)
    {
        _logger = logger;
        _handler = handler;
        _host = hostSystem;
        _containerController = containerController;
        _fileOperations = fileOperations;
        _storageDirectory = storageDirectory;
        Init();
    }

    void Init()
    {
        _containerName = "worker";
        _checkpointName = "checkpoint";
        //_storageDirectory = "/p7";
        _resultName = "done.txt";
        _imageName = "python:3.10-alpine";
        string deviceId = new DeviceIdBuilder()
                        .AddMachineName()
                        .AddOsVersion()
                        .OnWindows(windows => windows
                            .AddProcessorId()
                            .AddMotherboardSerialNumber()
                            .AddSystemDriveSerialNumber())
                        .OnLinux(linux => linux
                            .AddMotherboardSerialNumber()
                            .AddSystemDriveSerialNumber())
                         .ToString();
        using (MD5 md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(deviceId));
            WorkerInfoDto.WorkerId = new Guid(hash).ToString();
        }
        Task.Run(KeepAlive);
    }

    bool _runDummy = false;
    bool _igonrebackend = false;
    void DummyRun()
    {
        if (_runDummy)
        {
            _logger.Information("Starting dummy task!");
            _ServiceTask = new ServiceTaskDto(Guid.Parse("ec85dd75-3f2b-4273-8735-121d71747a57"),
                @"192.168.1.10:p1user:p1user:0a45d592-9708-42cc-aea0-60714113564e/ec85dd75-3f2b-4273-8735-121d71747a57/source/Experiment_mu0_0131_interval37500_seed_401_1.py",
                @"192.168.1.10:p1user:p1user:0a45d592-9708-42cc-aea0-60714113564e/ec85dd75-3f2b-4273-8735-121d71747a57/result/",
                @"192.168.1.10:p1user:p1user:0a45d592-9708-42cc-aea0-60714113564e/ec85dd75-3f2b-4273-8735-121d71747a57/backup/");
            _startTime = DateTime.UtcNow;
            _runTask = true;
            _runDummy = false;
            _igonrebackend = true;
        }
    }

    public async Task<bool> Update()
    {
        if (!_handler.IsConnected)
            Connect();

        DummyRun();
        if (!_runTask && _workerState != State.Idle)
            _workerState = State.Cancel;

        switch (_workerState)
        {
            case State.Idle:
                if (_runTask)
                    _workerState = State.InitTask;
                break;
            case State.InitTask:
                _logger.Information("Init new task");
                _handler.SendMessage(_ServiceTask.Id.ToString(), _handler.GetBasicProperties("startJob"));
                //cleanup
                await InitForNewTask();
                _checkpointCount = 0;
                _serviceTaskSw.Restart();
                await StartServiceTask();
                _lastCheckPointdt = DateTime.UtcNow;
                _minimunKeepAlive = _startTime = DateTime.UtcNow;
                _workerState = State.Running;
                break;
            case State.Running:
                if (_containerController.ContainerIsRunningAsync(_containerID).Result)
                {
                    if (DateTime.UtcNow - _lastCheckPointdt > _serviceTaskCfg?.CheckPointInterval)
                    {
                        //this process take some time

                        string checkpointNamei = _checkpointName + _checkpointCount.ToString();
                        _logger.Information($"Creating checkpoint {checkpointNamei}");

                        _checkpointTime.Restart();
                        if (!_containerController.Checkpoint(_containerName, checkpointNamei))
                        {
                            _logger.Information($"Failed to checkpoint! shutting down!");
                            Thread.Sleep(1000);
                            _host.MimicMachineFailur();
                        }
                        _checkpointTime.Stop();
                        _logger.Information($"Checkpointing took total seconds: {(_checkpointTime.ElapsedMilliseconds / 1000).ToString("000.000")}");
                        _lastCheckPointdt = DateTime.UtcNow;
                        _fileOperations.MoveCheckpointFromContainer(checkpointNamei, _containerID);

                        _logger.Information($"Uploading checkpoint {_checkpointCount}");
                        _ftpClient.UploadDirectory(Path.Combine(_storageDirectory, checkpointNamei), $"{_remoteBackupPath}{checkpointNamei}");

                        _checkpointCount++;
                        _minimunKeepAlive = DateTime.UtcNow;
                    }
                }
                else if (DateTime.UtcNow - _minimunKeepAlive > TimeSpan.FromSeconds(10))
                {
                    _workerState = State.Finished;
                    _serviceTaskSw.Stop();
                    _logger.Information($"Done Running Container: {_containerID}, total executing time: {_serviceTaskSw.Elapsed}");
                }
                break;
            case State.Finished:
                //extract from container
                _logger.Information("Extracting result from container");
                _fileOperations.ExtractResultFromContainer(_resultName, _containerID);
                //Upload
                _logger.Information($"Uploading result to result path: {_ServiceTask.ResultPath}");
                UploadFTPfile($"{_ServiceTask.ResultPath.Split(":").Last()}{_resultName}");
                _handler.SendMessage(_ServiceTask.Id.ToString(), _handler.GetBasicProperties("jobDone"));
                //reset
                _runTask = false;
                _serviceTaskCfg = null;
                _ServiceTask = null;
                _workerState = State.Idle;
                _igonrebackend = false;
                await InitForNewTask();
                break;
            case State.Cancel:
                await _containerController.StopContainer(_containerID);
                _logger.Information("Stopped");
                _handler.SendMessage(_ServiceTask.Id.ToString(), _handler.GetBasicProperties("stopJob"));
                var val = _ServiceTask.ResultPath.Split(":").Last().Split("/");
                _ftpClient.DeleteDirectory($"{val[0]}");
                await InitForNewTask();
                _serviceTaskCfg = null;
                _ServiceTask = null;
                _workerState = State.Idle;
                break;
        }

        //simulate system failure
        if (_serviceTaskCfg != null && _serviceTaskCfg.ShutOffDelay != TimeSpan.MinValue)
        {
            if (DateTime.UtcNow - _startTime > _serviceTaskCfg.ShutOffDelay)
            {
                _logger.Information($"System failure! time elapsed {DateTime.UtcNow - _startTime}");
                Thread.Sleep(1000);
                _host.MimicMachineFailur();
            }
        }
        return true;
    }

    void KeepAlive()
    {
        while (true)
        {
            if (_handler.IsConnected)
            {
                //Keep alive
                if (DateTime.UtcNow - _keepAliveDt > _keepAliveTs)
                {
                    if (_workerState == State.Running)
                        _logger.Information($"Still alive, state: {Enum.GetName(typeof(State), _workerState)}, Container: {_containerID}");
                    else
                        _logger.Information($"Still alive, state: {Enum.GetName(typeof(State), _workerState)}");
                    _keepAliveDt = DateTime.UtcNow;
                    WorkerReportDTO workerReport = new WorkerReportDTO(WorkerInfoDto.WorkerId, _ServiceTask != null ? _ServiceTask.Id : Guid.Empty, GetLANIp());
                    _handler.SendMessage(JsonSerializer.Serialize(workerReport), _handler.GetBasicProperties("report"));
                }
            }
            Thread.Sleep(100);
        }
    }

    async Task InitForNewTask()
    {
        _logger.Information("InitForNewTask");
        _containerController.PruneContainers();

        DirectoryInfo di = new DirectoryInfo(_storageDirectory);
        try
        {
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }
        catch (Exception ex) { }
    }

    async Task StartServiceTask()
    {
        string[] startParts = _ServiceTask.SourcePath.Split(':');
        string[] backupParts = _ServiceTask.BackupPath.Split(':');
        _payloadName = _ServiceTask.SourcePath.Split('/').Last();
        _logger.Information("\nSet PayloadName");

        _logger.Information("\nPayloadName: " + _payloadName);
        _logger.Information("\nResultPath: " + $"{_ServiceTask.ResultPath.Split(":").Last()}{_resultName}");
        _logger.Information("\nBackupPath: " + $"{backupParts.Last()}{_checkpointName}");

        _ftpClient = new FtpClient(startParts[0], startParts[1], startParts[2]);
        _ftpClient.Connect();
        DownloadFTPFile(startParts[3], _payloadName);
        _logger.Information("\nDownloaded source");
        _remoteBackupPath = backupParts.Last();

        _checkpointCount = 0;
        while (true)
        {
            if (!_ftpClient.DirectoryExists(backupParts.Last() + _checkpointName + _checkpointCount.ToString()))
            {
                _checkpointCount--;
                if (_checkpointCount < 0)
                    _checkpointCount = 0;
                break;
            }
            _checkpointCount++;
        }

        string checkpointPath = backupParts.Last() + _checkpointName + _checkpointCount.ToString();
        _logger.Information($"checkpoint path: {checkpointPath}");
        if (_ftpClient.DirectoryExists(checkpointPath))
        {
            _logger.Information("Starting container from checkpoint");
            DownloadFTPFolder(checkpointPath, _checkpointName); // Checkpoint
            await StartOrRecoverContainerAsync(true);
            _checkpointCount++;
        }
        else
        {
            _logger.Information("Starting container from scratch");
            await StartOrRecoverContainerAsync();
        }
        _logger.Information("StartServiceTask completed");
    }

    void Connect()
    {
        _handler.Register(RegisterResponseRecieved);
    }

    public async Task<bool> StartOrRecoverContainerAsync(bool startRecover = false)
    {
        _logger.Information($"Hello, {Environment.UserName}!");

        // Create a container
        await _containerController.CreateContainerAsync(_containerName, _imageName, _payloadName);
        _logger.Information($"name:{_containerName}, image: {_imageName}, payload: {_payloadName}");
        // Move checkpoint into container and start
        _containerID = _containerController.GetContainerIDByNameAsync(_containerName).Result;
        _logger.Information($"name:{_containerName}");
        // Move payload into Container
        if (startRecover)
        {
            _fileOperations.MoveCheckpointIntoContainer(_checkpointName, _containerID);
        }
        _logger.Information($"checkpointName:{_checkpointName}, id: {_containerID}");
        // Start Container
        _fileOperations.PredFile(Path.Combine(_storageDirectory, _payloadName));

        _logger.Information($"_storageDirectory:{_storageDirectory}, _payloadName: {_payloadName}");
        _fileOperations.MovePayloadIntoContainer(_payloadName, _containerName);
        _logger.Information($"_payloadName:{_payloadName}, _containerName: {_containerName}");

        if (startRecover)
        {
            _logger.Information($"Starting from checkpoint: {_checkpointName}, Container name: {_containerName}");
            if (!_containerController.Restore(_checkpointName, _containerName))
            {
                _logger.Information($"Failed to start checkpoint! shutting down!");
                Thread.Sleep(1000);
                _host.MimicMachineFailur();
            }
        }
        else
        {
            _logger.Information($"Starting a new container {_containerID}");
            _containerController.Start(_containerID);
        }

        _logger.Information($"Starting container {_containerID} completed");
        return true;
    }

    void WorkerConsumer(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();

        var message = Encoding.UTF8.GetString(body);
        _logger.Information(message);

        if (!ea.BasicProperties.Headers.ContainsKey("type"))
        {
            return;
        }
        if (!_igonrebackend)
        {
            double d = 0;
            string msgType = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["type"]);
            switch (msgType)
            {
                case "recoverJob":
                case "startJob":
                    _logger.Information($"{msgType} recieved");
                    _startTime = DateTime.UtcNow; //to avoid raise condition of setting this in main loop
                    if (ea.BasicProperties.Headers.ContainsKey("interval"))
                    {
                        _serviceTaskCfg = new ServiceTaskConfiguration();
                        d = double.Parse(Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["fail"])); // Get fail time
                        if (d <= 0)
                            _serviceTaskCfg.ShutOffDelay = TimeSpan.MinValue;
                        else
                            _serviceTaskCfg.ShutOffDelay = new TimeSpan(0, 0, (int)d);

                        d = int.Parse(Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["interval"])); // Get interval time
                        _logger.Information("Checkpoint frequency: " + d + "ms");
                        _serviceTaskCfg.CheckPointInterval = new TimeSpan(0, 0, 0, 0, (int)d);
                        _logger.Information("ServiceTaskCfg: {@_serviceTaskCfg}", _serviceTaskCfg);
                    }
                    else
                    {
                        _serviceTaskCfg = new ServiceTaskConfiguration()
                        {
                            CheckPointInterval = new TimeSpan(0, 1, 0),
                            ShutOffDelay = TimeSpan.MinValue
                        };
                        _logger.Information("ServiceTaskCfg: @{_serviceTaskCfg}", _serviceTaskCfg);
                    }

                    _ServiceTask = JsonSerializer.Deserialize<ServiceTaskDto>(message);
                    _logger.Information("New job: {@_ServiceTask}", _ServiceTask);
                    _runTask = true;
                    break;
                case "stopJob":
                    _runTask = false;
                    break;

                default:
                    break;
            }
        }
    }

    void RegisterResponseRecieved(object? model, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var response = Encoding.UTF8.GetString(body);

        RegisterResponseDTO? responseJson = JsonSerializer.Deserialize<RegisterResponseDTO>(response);
        WorkerInfoDto.WorkerId = responseJson.WorkerId;
        WorkerInfoDto.ServerName = responseJson.ServerName;

        _handler.DeclareWorkerQueue();
        _handler.Connect();
        _handler.AddWorkerConsumer(WorkerConsumer);
    }

    void DownloadFTPFile(string remoteSourcePath, string localFileName)
    {
        string localSourcePath = Path.Combine(_storageDirectory, localFileName);

        _ftpClient.DownloadFile(localSourcePath, remoteSourcePath);
    }

    void DownloadFTPFolder(string remoteSourcePath, string localFileName)
    {
        string localSourcePath = Path.Combine(_storageDirectory, localFileName);

        _ftpClient.DownloadDirectory(_storageDirectory, remoteSourcePath);

        var split = remoteSourcePath.Split("/");

        var path = $"{_storageDirectory}/home/ftpuser/{split[0]}/{split[1]}/{split[2]}/{split[3]}";
        Directory.Move(path, localSourcePath);

    }

    void UploadFTPfile(string remoteResultPath)
    {
        string localResultPath = Path.Combine(_storageDirectory, _resultName);

        _ftpClient.UploadFile(localResultPath, remoteResultPath);
    }

    string GetLANIp()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.ToString().Split('.')[0] == "192")
            {
                return ip.ToString();
            }
        }
        return "No LANIp";
    }
}
