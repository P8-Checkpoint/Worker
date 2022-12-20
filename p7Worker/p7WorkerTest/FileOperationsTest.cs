using p7Worker;

namespace p7WorkerTest;

public class FileOperationsTest
{
    public void MovePayloadIntoContainerTest()
    {

    }
    public void ExtractResultFromContainerTest()
    {

    }
    public void MoveCheckpointFromContainerTest()
    {

    }
    public void MoveAllCheckpointsFromContainerTest()
    {

    }
    public void MoveCheckpointIntoContainerTest()
    {

    }
    public void PredFileTest()
    {
        var fo = new FileOperations("/p7");
        string filePath = "/p7/predTest.py";
        var file = File.Create(filePath);
        file.Close();
    }
}
