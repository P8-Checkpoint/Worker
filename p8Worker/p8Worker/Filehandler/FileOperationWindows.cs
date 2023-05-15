using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p8Worker.Filehandler
{
    internal class FileOperationWindows : IFileOperation
    {
        public FileOperationWindows(string pathToHome)
        {
            this.pathToHome = pathToHome;
        }
        string pathToContainers = $@"/var/lib/docker/containers";
        string pathToHome { get; set; }

        void IFileOperation.ExtractResultFromContainer(string resultName, string containerID)
        {
            return;
        }

        void IFileOperation.MoveAllCheckpointsFromContainer(string containerID)
        {
            return;
        }

        void IFileOperation.MoveCheckpointFromContainer(string checkpointName, string containerID)
        {
            return;
        }

        void IFileOperation.MoveCheckpointIntoContainer(string checkpoint, string containerID)
        {
            return;
        }

        void IFileOperation.MovePayloadIntoContainer(string payloadName, string containerID)
        {
            string payload = Path.Combine(pathToHome, payloadName);

            using (Process process = new Process())
            {
                process.StartInfo.FileName = "docker";
                process.StartInfo.Arguments = $"cp {payload} {containerID}:{payloadName}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }
        }

        void IFileOperation.PredFile(string filePath)
        {
            // Add a line to the beginning of the file
            string startLine = "import sys \n \nf = open(\"worker.result\", \"w\") \nsys.stdout = f";
            string currentContent = File.ReadAllText(filePath);
            File.WriteAllText(filePath, startLine + Environment.NewLine + currentContent);

            // Add a line to the end of the file
            string endLine = "sys.stdout = sys.__stdout__ \nf.close()";
            File.AppendAllText(filePath, Environment.NewLine + endLine);
        }
    }
}
