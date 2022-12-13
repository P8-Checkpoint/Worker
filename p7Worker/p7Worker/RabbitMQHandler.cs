using p7Worker.DTOs;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p7Worker
{
    public class RabbitMQHandler
    {
        IModel _channel;
        string _replyConsumerTag;

        public RabbitMQHandler()
        {
            Init();
        }

        void Init()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();
        }

        void Register()
        {

        }
        
        void CreateRegisterReplyConsumer(EventHandler<BasicDeliverEventArgs> remoteProcedure)
        {
            var replyQueueName = _channel.QueueDeclare(autoDelete: true, exclusive: true).QueueName;
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var response = Encoding.UTF8.GetString(body);

                if (ea.BasicProperties.CorrelationId == _correlationId)
                {
                    RegisterResponseDTO? responseDto = JsonSerializer.Deserialize<RegisterResponseDTO>(response);
                    _workerId = responseDto?.WorkerId ?? string.Empty;
                    Console.WriteLine(_workerId);
                    _serverName = responseDto?.ServerName ?? string.Empty;
                    Connect();
                }
                else
                {
                    Console.WriteLine("Expected correlationId: {0} but received response with correlationId: {1}", _correlationId, ea.BasicProperties.CorrelationId);
                }
            };

            _replyConsumerTag = _channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);
        }

        void Connect()
        {

        }
    }
}
