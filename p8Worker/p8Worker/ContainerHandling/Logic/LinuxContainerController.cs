using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;
using p8Worker.ContainerHandling.Interfaces;

namespace p8Worker.ContainerHandling.Logic;

public class LinuxContainerController : IContainerHandler
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

    public async Task<string> CreateContainerAsync(string containerName, string image, string payloadName)
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
        return id;
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
        try
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
        }
        catch (AggregateException ex)
        {

        }

        return string.Empty;
    }

    public async Task Start(string id)
    {
        await client.Containers.StartContainerAsync(
            id,
            new ContainerStartParameters(),
            CancellationToken.None);

        Log.Information($"Started container: {id}");
    }

    public async Task StopContainer(string id)
    {
        try
        {
            await client.Containers.KillContainerAsync(
            id,
                new ContainerKillParameters(),
                CancellationToken.None
            );
        }
        catch (Exception ex) { }

        Log.Information($"Stopped container: {id}");

    }


    public async void PruneContainers()
    {
        IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
        new ContainersListParameters()
        {
            Limit = 10,
        },
        CancellationToken.None);

        foreach (var item in containers)
        {
            try
            {
                if (item.State != "exited")
                {
                    await client.Containers.KillContainerAsync(
                    item.ID,
                    new ContainerKillParameters(),
                    CancellationToken.None
                );
                }
            }
            catch (Exception ex) { }
        }

        var result = client.Containers.PruneContainersAsync().Result;

        Log.Information("Pruned Containers");
    }

    public bool Checkpoint(string name, string checkpointName)
    {

        Log.Information($"Checkpointing container: {name}");
        string output = string.Empty;
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"checkpoint create {name} {checkpointName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }
        output = output.Replace("\n", "");
        Log.Information($"Checkpointed container: {output}");

        return output == checkpointName && Restore(checkpointName, name) ? true : false;
    }

    public bool Restore(string checkpointName, string containerName)
    {
        Log.Information($"Restore container: {containerName}, {checkpointName}");
        string output = string.Empty;
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"start --checkpoint {checkpointName} {containerName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }

        Log.Information($"Restore process done: {output}");
        bool retval = output == string.Empty ? true : false;

        var id = GetContainerIDByNameAsync(containerName).Result;
        if (id != null && retval)
        {
            Log.Information($"Waiting for container to start: {id}");
            retval = ContainerIsRunningAsync(id).Result;
        }
        else
        {
            Log.Information($"Failed to start container: {id}");
            retval = false;
        }

        Log.Information($"Restored container, name: {containerName}, checkpoint: {checkpointName}, value: {retval}");
        return retval;
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
