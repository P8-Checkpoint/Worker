using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace p7Worker;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Change these values to suit your needs
        string container = "magnustest1";
        string image = "busybox";
        string storageDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string payloadName = "4seconds";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/p7-{payloadName}-log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        ContainerController cc = new ContainerController();
        FileOperations fo = new FileOperations();

        try
        {
            Console.WriteLine("Starting Program...");
            Log.Information($"Hello, {Environment.UserName}!");

            // Create the image for use in the execution
            await cc.CreateImageAsync(image);

            for (int i = 1; i <= 13; i++)
            {
                // Log total elapsed time per run
                var totalTime = Stopwatch.StartNew();

                // Path to the payload
                string filePath = System.IO.Path.Combine(storageDirectory, $"{payloadName}.zip");

                if (System.IO.File.Exists(filePath))
                {
                    // Create a container
                    await cc.CreateContainerAsync(container, image, filePath);

                    // Extract payload into container
                    string containerID = await cc.GetContainerIDByNameAsync(container);

                    // Start Container
                    await cc.StartAsync(containerID);

                    // Execute payload
                    cc.Execute(containerID, payloadName, 500);
                    // cc.ExecuteWithoutCheckpointing(containerID, payloadName);

                    // Extract all checkpoint files
                    fo.MoveAllCheckpointsFromContainer(containerID);

                    await cc.DeleteContainerAsync(containerID);
                }

                totalTime.Stop();
                Log.Logger.Information($"Elapsed time total for run{i} with payload {payloadName}: {totalTime.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something went wrong");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}