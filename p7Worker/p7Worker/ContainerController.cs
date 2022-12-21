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

    public void LoadImage(string imagePath)
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

    public async Task CreateContainerAsync(string containerName, string image, string payloadName)
    {
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"create --name {containerName} --security-opt seccomp:unconfined {image} /bin/sh -c \"python3 {payloadName}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        string id = await GetContainerIDByNameAsync(containerName);

        Log.Information($"Created Container, id: {id}, name: {containerName}");
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

        return string.Empty;
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

    // public async void Execute(string id, string payloadName, int interval)
    // {
    //     var execTime = Stopwatch.StartNew();

    //     Log.Information($"Container is running: {id}. Checkpointing every {interval}ms");

    //     IList<string> args = new List<string>();
    //     args.Add($"python {payloadName}");
    //     await client.Exec.StartWithConfigContainerExecAsync(
    //         id,
    //         new ContainerExecStartParameters()
    //         {
    //             Cmd = args
    //         },
    //         CancellationToken.None
    //     );

    //     while (await ContainerIsRunningAsync(id) == true)
    //     {
    //         for (int i = 1; i > 0; i++)
    //         {
    //             string currentCheckpoint = $"checkpoint-{id}";
    //             Checkpoint(id, currentCheckpoint);
    //             Log.Information($"Made Checkpoint: {currentCheckpoint}");

    //             Thread.Sleep(interval);
    //         }
    //     }

    //     execTime.Stop();
    //     Log.Logger.Information($"Elapsed time for execution {payloadName}: {execTime.ElapsedMilliseconds}ms");
    // }

    public void Checkpoint(string name, string checkpointName)
    {
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"checkpoint create --leave-running {name} {checkpointName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        Log.Information($"Checkpointed container: {name}");
    }

    public async Task RestoreAsync(string checkpointName, string containerName)
    {

        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"start --checkpoint {checkpointName} {containerName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        Log.Information($"Restored container, name: {containerName}, checkpoint: {checkpointName}");
    }

    public async Task<bool> ContainerIsRunningAsync(string id)
    {
        var containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters()
        );

        var container = ContainerInList(containers, id);

        if (IsContainerUp(container))
        {
            return true;
        }

        return false;
    }

    public bool IsContainerUp(ContainerListResponse container)
    {
        if (container == null)
        {
            return false;
        }
        if (container.Status.StartsWith("Up"))
        {
            return true;
        }

        return false;
    }

    public ContainerListResponse ContainerInList(IList<ContainerListResponse> list, string id)
    {
        foreach (var container in list)
        {
            if (container.ID == id)
            {
                return container;
            }
        }

        return null;
    }
}
