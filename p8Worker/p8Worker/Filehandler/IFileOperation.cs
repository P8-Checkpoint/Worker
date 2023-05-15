using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.Filehandler
{
    public interface IFileOperation
    {
        void MovePayloadIntoContainer(string payloadName, string containerID);
        void ExtractResultFromContainer(string resultName, string containerID);
        void MoveCheckpointFromContainer(string checkpointName, string containerID);
        void MoveAllCheckpointsFromContainer(string containerID);
        void MoveCheckpointIntoContainer(string checkpoint, string containerID);
        void PredFile(string filePath);
    }
}
