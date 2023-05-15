using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;
using p8Worker.ContainerHandling.Interfaces;

namespace p8Worker.ContainerHandling.Logic;

public class WindowsContainerController : IContainerHandler
{
    DockerClient client = new DockerClientConfiguration()
     .CreateClient();

    //string PathToContainers = $@"/var/lib/docker/containers/";

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
        List<string> cmds = new List<string>();
        //cmds.Add($"/bin/bash -c python3 {payloadName}");
        cmds.Add($"/bin/sh -c 'python3 {payloadName}'");

        string[] imageParts = image.Split(':');

        await client.Images.CreateImageAsync(
      new ImagesCreateParameters
      {
          FromImage = imageParts[0],
          Tag = imageParts[1],
      },
      null,
      new Progress<JSONMessage>());

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

        //await client.Containers.CreateContainerAsync(new CreateContainerParameters()
        //{
        //    Name = "workerWindows",
        //    Image = image,
        //    Cmd = cmds
        //    //HostConfig = new HostConfig()
        //    //{
        //    //    DNS = new[] { "8.8.8.8", "8.8.4.4" }
        //    //}
        //});

        //using (Process process = new Process())
        //{
        //    process.StartInfo.FileName = "docker";
        //    process.StartInfo.Arguments = $"create --name {containerName} --security-opt seccomp:unconfined {image} /bin/sh -c \"python3 {payloadName}\"";
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.Start();
        //    string output = process.StandardOutput.ReadToEnd();
        //    process.WaitForExit();
        //}

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



            string containerID = containers.Where(c => c.Names.Contains($"/{containerName}")).FirstOrDefault()?.ID;
            if (containerID != null || containerID != string.Empty)
            {
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

    public async Task MimicMachineFailure()
    {
        //using (Process process = new Process())
        //{
        //    process.StartInfo.FileName = "shutdown";
        //    process.StartInfo.Arguments = $"-r now";
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.Start();
        //    string output = process.StandardOutput.ReadToEnd();
        //    process.WaitForExit();
        //}

        Log.Information("Fault mimicked");
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
            catch (Exception ex) {}
        }

        var result = client.Containers.PruneContainersAsync().Result;

        Log.Information("Pruned Containers");
    }

    public bool Checkpoint(string name, string checkpointName)
    {
        //using (Process process = new Process())
        //{
        //    process.StartInfo.FileName = "docker";
        //    process.StartInfo.Arguments = $"checkpoint create --leave-running {name} {checkpointName}";
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.Start();
        //    string output = process.StandardOutput.ReadToEnd();
        //    process.WaitForExit();
        //}

        Log.Information($"Checkpointed container: {name}");
        return true;
    }

    public bool Restore(string checkpointName, string containerName)
    {

        //using (Process process = new Process())
        //{
        //    process.StartInfo.FileName = "docker";
        //    process.StartInfo.Arguments = $"start --checkpoint {checkpointName} {containerName}";
        //    process.StartInfo.UseShellExecute = false;
        //    process.StartInfo.RedirectStandardOutput = true;
        //    process.Start();
        //    string output = process.StandardOutput.ReadToEnd();
        //    process.WaitForExit();
        //}

        Log.Information($"Restored container, name: {containerName}, checkpoint: {checkpointName}");
        return true;
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
