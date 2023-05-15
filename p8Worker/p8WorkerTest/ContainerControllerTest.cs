using Xunit;
using System;
using System.Threading;
using System.IO;
using System.Diagnostics;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text.Json;
using System.Reflection;
using FluentFTP;
using Newtonsoft.Json.Linq;
using Serilog;
using Moq;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Text;
using System.Threading;
using p8Worker.ContainerHandling.Logic;

namespace p8WorkerTest;

public class ContainerControllerTests
{

    LinuxContainerController _sut;

    public ContainerControllerTests()
    {
        _sut = new LinuxContainerController();
    }

    [Fact]
    public void IsContainerUp_ContainerIsUp_True()
    {
        // Arrange
        var container = new ContainerListResponse { ID = "1234id", Status = "Up" };

        // Act
        var result = _sut.IsContainerUp(container);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsContainerUp_ContainerIsNotUp_False()
    {
        // Arrange
        var container = new ContainerListResponse { ID = "1234id", Status = "Down" };

        // Act
        var result = _sut.IsContainerUp(container);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsContainerUp_ContainerIsNull_False()
    {
        // Arrange
        ContainerListResponse container = null;

        // Act
        var result = _sut.IsContainerUp(container);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainerInList_ContainerIsInList_ReturnsContainer()
    {
        // Arrange
        var list = new List<ContainerListResponse>
        {
            new ContainerListResponse { ID = "1234id", Status = "Up" },
            new ContainerListResponse { ID = "12345id", Status = "Exited" },
            new ContainerListResponse { ID = "notid", Status = "Down" }
        };
        string id = "1234id";
        ContainerListResponse expectedResponse = new ContainerListResponse { ID = "1234id", Status = "Up" };

        // Act
        ContainerListResponse result = _sut.ContainerInList(list, id);

        // Assert
        Assert.Equal(expectedResponse.ID, result.ID);
        Assert.Equal(expectedResponse.Status, result.Status);
    }

    [Fact]
    public void ContainerInList_ContainerIsNotInList_ReturnsNull()
    {
        // Arrange
        var list = new List<ContainerListResponse>
        {
            new ContainerListResponse { ID = "1234id", Status = "Up" },
            new ContainerListResponse { ID = "12345id", Status = "Exited" },
            new ContainerListResponse { ID = "notid", Status = "Down" }
        };
        string id = "1324id";

        // Act
        var result = _sut.ContainerInList(list, id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ContainerInList_ListIsEmpty_ReturnsNull()
    {
        // Arrange
        var list = new List<ContainerListResponse> { };
        string id = "1324id";

        // Act
        var result = _sut.ContainerInList(list, id);

        // Assert
        Assert.Null(result);
    }
}
