using Xunit;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text.Json;
using System.Reflection;
using FluentFTP;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Text;
using System.Threading;
using p7Worker;
using Moq;

namespace p7WorkerTest;

public class WorkerTests
{
    Worker _sut;
    private readonly Mock<RabbitMQHandler> _rabbitMQHandlerMock;
    private readonly Mock<ContainerController> _containerControllerMock;
    private readonly Mock<FileOperations> _fileOperationsMock;

    public WorkerTests()
    {
        _rabbitMQHandlerMock = new Mock<RabbitMQHandler>();
        _containerControllerMock = new Mock<ContainerController>();
        _fileOperationsMock = new Mock<FileOperations>();
        _sut = new Worker(_rabbitMQHandlerMock.Object, _containerControllerMock.Object, _fileOperationsMock.Object);
    }
    public void CreateAndExecuteContainerAsync_situation_expectedOutcome()
    {
        //arrange
        _containerControllerMock.Setup(x => x.CreateContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        //act
        _sut.CreateAndExecuteContainerAsync("").RunSynchronously();

        //assert
    }
}
