using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.ContainerHandling.Interfaces
{
    public interface IContainerHandler
    {
        void PruneContainers();
        Task<bool> ContainerIsRunningAsync(string id);
        bool IsContainerUp(ContainerListResponse container);
        bool Restore(string checkpointName, string containerName);
        ContainerListResponse ContainerInList(IList<ContainerListResponse> list, string id);
        bool Checkpoint(string name, string checkpointName);
        Task StopContainer(string id);
        Task Start(string id);
        Task<string> GetContainerIDByNameAsync(string containerName);
        Task DeleteContainerAsync(string id);
        Task<string> CreateContainerAsync(string containerName, string image, string payloadName);
        //void LoadImage(string imagePath);
        Task CreateImageAsync(string imageName);
    }
}
