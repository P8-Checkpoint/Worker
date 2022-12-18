using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace p7Worker;

public class ContainerController
{
    DockerClient client = new DockerClientConfiguration(
            new Uri("unix:///var/run/docker.sock")).CreateClient();

    FileOperations fo = new FileOperations();

    string PathToContainers = $@"/var/lib/docker/containers/";

    public async Task CreateImageAsync(string imageName)
    {
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = imageName,
                Tag = "latest"
            },
            null,
            new Progress<JSONMessage>(),
            CancellationToken.None);

        Log.Information($"Created image: {imageName}");
    }

    public async Task LoadImageAsync(string imagePath)
    {
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"load -i {imagePath}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        Log.Information($"Loaded image: {imagePath}");
    }

    public async Task CreateContainerAsync(string name, string image)
    {
        // sudo docker create --name name --security-opt seccomp:unconfined image /bin/sh - c $"python3 /home/{payloadName}";
        IList<string> args = new List<string>();
        args.Append($"/bin/sh - c \"python3 /home/payload.py\"");

        // await client.Containers.CreateContainerAsync(new CreateContainerParameters()
        // {
        //     Image = image,
        //     Name = name,
        //     Cmd = args
        // },
        // CancellationToken.None);

        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"create --name {name} --security-opt seccomp:unconfined {image} /bin/sh - c \"python3 /home/payload.py\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        string id = await GetContainerIDByNameAsync(name);

        Log.Information($"Created Container, id: {id}, name: {name}");
    }

    public async Task DeleteContainerAsync(string id)
    {
        await client.Containers.RemoveContainerAsync(
            id,
            new ContainerRemoveParameters
            {
                Force = true,
            },
            CancellationToken.None);

        Log.Information($"Deleted Container: {id}");
    }

    public async Task<string> GetContainerIDByNameAsync(string containerName)
    {
        IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters()
            {
                Limit = 10,
            },
            CancellationToken.None);

        string containerID;

        foreach (var container in containers)
        {
            containerID = container.ID;

            Log.Information($"Container {containerName} has id: \n{containerID}");

            return containerID;
        }

        return "";
    }

    public async Task StartAsync(string id)
    {
        await client.Containers.StartContainerAsync(
            id,
            new ContainerStartParameters(),
            CancellationToken.None);

        Log.Information($"Started container: {id}");
    }

    public async Task StopContainer(string id)
    {
        await client.Containers.StopContainerAsync(
            id,
            new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 30
            },
            CancellationToken.None);

        Log.Information($"Stopped container: {id}");

    }

    public async void Execute(string id, string payloadName, int interval)
    {
        var execTime = Stopwatch.StartNew();

        Log.Information($"Container is running: {id}. Checkpointing every {interval}ms");

        IList<string> args = new List<string>();
        args.Add($"python {payloadName}");
        await client.Exec.StartWithConfigContainerExecAsync(
            id,
            new ContainerExecStartParameters()
            {
                Cmd = args
            },
            CancellationToken.None
        );

        while (await ContainerIsRunningAsync(id) == true)
        {
            for (int i = 1; i > 0; i++)
            {
                string currentCheckpoint = $"checkpoint-{id}";
                Checkpoint(id, currentCheckpoint);
                Log.Information($"Made Checkpoint: {currentCheckpoint}");

                Thread.Sleep(interval);
            }
        }

        execTime.Stop();
        Log.Logger.Information($"Elapsed time for execution {payloadName}: {execTime.ElapsedMilliseconds}ms");
    }

    public async void ExecuteWithoutCheckpointing(string id, string payloadName)
    {

        Log.Information($"Container is running: {id} without checkpointing");
        var execTime = Stopwatch.StartNew();

        IList<string> args = new List<string>();
        args.Add($"python .{payloadName}");
        await client.Exec.StartWithConfigContainerExecAsync(
            id,
            new ContainerExecStartParameters()
            {
                Cmd = args
            },
            CancellationToken.None
        );

        // using (Process process = new Process())
        // {
        //     process.StartInfo.FileName = "docker";
        //     process.StartInfo.Arguments = $"exec {id} .{payloadName}.py";
        //     process.StartInfo.UseShellExecute = false;
        //     process.StartInfo.RedirectStandardOutput = true;
        //     process.Start();
        //     string output = process.StandardOutput.ReadToEnd();
        //     process.WaitForExit();
        // }

        execTime.Stop();
        Log.Logger.Information($"Elapsed time for execution {payloadName}: {execTime.ElapsedMilliseconds}ms");
    }

    public async void Checkpoint(string id, string checkpointName)
    {

        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"checkpoint create {id} {checkpointName} --leave-running";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        Log.Information($"Stopped container: {id}");
    }

    public async Task RestoreAsync(
        string id,
        string checkpointName,
        string containerName,
        string payload,
        string image)
    {
        await CreateContainerAsync(containerName, image);

        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"start {containerName} --checkpoint {checkpointName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        Log.Information($"Restored container, id: {id}, checkpoint: {checkpointName}");
    }

    public async Task<bool> ContainerIsRunningAsync(string id)
    {
        var containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters()
        );

        foreach (var container in containers)
        {
            if (container.Status.StartsWith("Up"))
            {
                return true;
            }
        }

        return false;
    }
}
