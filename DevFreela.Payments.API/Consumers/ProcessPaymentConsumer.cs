using DevFreela.Payments.API.Models;
using DevFreela.Payments.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DevFreela.Payments.API.Consumers
{
    public class ProcessPaymentConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _userRabbitMQ;
        private readonly string _passwordRabbitMQ;
        private readonly string _hostName;
        private const string QUEUE = "Payments";
        private const string PAYMENT_APROVED_QUEUE = "PaymentsApproved";

        public ProcessPaymentConsumer(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;

            _userRabbitMQ = configuration.GetSection("RabbitMQ:User").Value;
            _passwordRabbitMQ = configuration.GetSection("RabbitMQ:Password").Value;
            _hostName = configuration.GetSection("RabbitMQ:HostName").Value;

            var connectionFactory = new ConnectionFactory()
            {
                Uri = new Uri($"amqp://{_userRabbitMQ}:{_passwordRabbitMQ}@{_hostName}/"),
                ConsumerDispatchConcurrency = 1,
            };

            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();

            // Garantir que a fila esteja sendo consumida
            _channel.QueueDeclare(
                queue: QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
                );

            // Garantir que a fila esteja sendo consumida
            _channel.QueueDeclare(
                queue: PAYMENT_APROVED_QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
                );

        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (sender, eventArgs) =>
            {
                var paymentInfoBytes = eventArgs.Body.ToArray();
                var paymentInfoJson = Encoding.UTF8.GetString(paymentInfoBytes);
                var paymentInfo = JsonSerializer.Deserialize<PaymentInfoInputModel>(paymentInfoJson);

                ProcessPayment(paymentInfo);

                var paymentApproved = new PaymentApprovedIntegrationEvent(paymentInfo.IdProject);
                var paymentApprovedJson = JsonSerializer.Serialize(paymentApproved);
                var paymentApprovedBytes = Encoding.UTF8.GetBytes(paymentApprovedJson);

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: PAYMENT_APROVED_QUEUE,
                    basicProperties: null,
                    body: paymentApprovedBytes
                    );

                _channel.BasicAck(eventArgs.DeliveryTag, false);
            };

            _channel.BasicConsume(QUEUE, false, consumer);

            return Task.CompletedTask;
        }

        public void ProcessPayment(PaymentInfoInputModel paymentInfo)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                paymentService.Process(paymentInfo);
            }
        }
    }
}
