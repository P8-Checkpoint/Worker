using Docker.DotNet.Models;
using p8Worker.DTOs;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace p8Worker.RabbitMQ;

public class RabbitMQHandler
{
    ILogger _logger;
    IModel _channel;
    string _replyConsumerTag;
    string _workerQueueName;

    public RabbitMQHandler(ILogger logger)
    {
        _logger = logger;
    }
    public bool IsConnected { get { return _channel == null ? false : _channel.IsOpen; } }

    void ConnectToServer()
    {
        var factory = new ConnectionFactory() { HostName = "192.168.1.10", UserName = "admin", Password = "admin" };
        var connection = factory.CreateConnection();
        _channel = connection.CreateModel();
    }

    public void Register(EventHandler<BasicDeliverEventArgs> remoteProcedure)
    {
        if (!IsConnected)
        {
            try
            {
                ConnectToServer();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                return;
            }
        }

        var replyQueueName = _channel.QueueDeclare(autoDelete: true, exclusive: true).QueueName;

        var consumer = new EventingBasicConsumer(_channel);

        var correlationId = Guid.NewGuid().ToString();

        consumer.Received += remoteProcedure;

        _replyConsumerTag = _channel.BasicConsume(
            consumer: consumer,
            queue: replyQueueName,
            autoAck: true);

        var props = _channel.CreateBasicProperties();

        props.CorrelationId = correlationId;
        props.ReplyTo = replyQueueName;

        var workerId = WorkerInfoDto.WorkerId;
        var messageBytes = Encoding.UTF8.GetBytes($"{workerId}");

        _channel.BasicPublish(exchange: "server", routingKey: "workerRegister", basicProperties: props, body: messageBytes);

        _logger.Information("Registration sent to server");
    }

    public void DeclareWorkerQueue()
    {
        _workerQueueName = _channel.QueueDeclare("worker_" + WorkerInfoDto.WorkerId, autoDelete: false, exclusive: false);
        _channel.QueueBind(_workerQueueName, "worker", WorkerInfoDto.WorkerId);
    }

    public void Connect()
    {
        var messageBytes = Encoding.UTF8.GetBytes(WorkerInfoDto.WorkerId);
        _channel.BasicPublish(exchange: "server", routingKey: $"{WorkerInfoDto.ServerName}.workerConnect", body: messageBytes);
        _logger.Information("Connected to server {}. You can now freely send messages!");
        _channel.BasicCancel(_replyConsumerTag);
    }

    public void AddWorkerConsumer(EventHandler<BasicDeliverEventArgs> remoteProcedure)
    {
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += remoteProcedure;

        _channel.BasicConsume(
            consumer: consumer,
            queue: _workerQueueName,
            autoAck: true);
    }

    public void SendMessage(string message, IBasicProperties props)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);

        _channel.BasicPublish(exchange: "server", routingKey: $"{WorkerInfoDto.ServerName}.{WorkerInfoDto.WorkerId}", basicProperties: props, body: messageBytes);
    }

    public IBasicProperties GetBasicProperties(string type)
    {
        var props = _channel.CreateBasicProperties();
        props.Headers = new Dictionary<string, object>
        {
            { "type", type }
        };
        return props;
    }
}
