﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;

namespace p7Worker.WorkerInfo;

public static class WorkerInfo
{
    public static string GetWorkerId()
    {
        using StreamReader r = new("WorkerInfo.json");
        string json = r.ReadToEnd();
        WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);
        return info.WorkerId;
    }

    public static string GetServerName()
    {
        using StreamReader r = new("WorkerInfo.json");
        string json = r.ReadToEnd();
        WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);
        return info.ServerName;
    }

    public static void SetWorkerInfo(WorkerInfoJson workerInfo)
    {
        string fileName = "WorkerInfo.json";
        string jsonString = JsonSerializer.Serialize(workerInfo);
        File.WriteAllText(fileName, jsonString);
    }

    public static void DownloadFTPFile()
    {
        string downloadName = "WorkerDownload";
        string imageName = "WorkerImage";

        using StreamReader r = new("WorkerInfo.json");
        string json = r.ReadToEnd();
        WorkerInfoJson? info = JsonSerializer.Deserialize<WorkerInfoJson>(json);

        string ftpfullpath = info.FTPLink;

        using (WebClient request = new WebClient())
        {
            // request.Credentials = new NetworkCredential("UserName", "P@55w0rd");
            byte[] fileData = request.DownloadData(ftpfullpath);

            using (FileStream file = File.Create(downloadName))
            {
                file.Write(fileData, 0, fileData.Length);
                file.Close();
            }
            Console.WriteLine("Download Complete");
        }

        File.Move($"{downloadName}/{imageName}", $"/var/lib/docker/images/{imageName}");
    }
}
