using p7Worker.DTOs;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace p7Worker;

public class RabbitMQHandler
{
    IModel _channel;
    string _replyConsumerTag;
    string _workerQueueName;

    public RabbitMQHandler()
    {
        Init();
    }

    void Init()
    {
        // var factory = new ConnectionFactory() { HostName = "localhost" };
        var factory = new ConnectionFactory() { UserName = "rabbit2", Password = "rabbit2", HostName = "rabbit2.rabbitmq" };
        var connection = factory.CreateConnection();
        _channel = connection.CreateModel();
    }

    public void Register(EventHandler<BasicDeliverEventArgs> remoteProcedure)
    {
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

        var workerId = WorkerInfo.WorkerId;
        var messageBytes = Encoding.UTF8.GetBytes($"{workerId}");

        _channel.BasicPublish(exchange: "server", routingKey: "workerRegister", basicProperties: props, body: messageBytes);

        Console.WriteLine("Registration sent to server");
    }

    public void DeclareWorkerQueue()
    {
        _workerQueueName = _channel.QueueDeclare("worker_" + WorkerInfo.WorkerId, autoDelete: false, exclusive: false);
        _channel.QueueBind(_workerQueueName, "worker", WorkerInfo.WorkerId);
    }

    public void Connect()
    {
        var messageBytes = Encoding.UTF8.GetBytes(WorkerInfo.WorkerId);
        _channel.BasicPublish(exchange: "server", routingKey: $"{WorkerInfo.ServerName}.workerConnect", body: messageBytes);
        Console.WriteLine("Connected to server {}. You can now freely send messages!");
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

        _channel.BasicPublish(exchange: "server", routingKey: $"{WorkerInfo.ServerName}.{WorkerInfo.WorkerId}", basicProperties: props, body: messageBytes);
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
