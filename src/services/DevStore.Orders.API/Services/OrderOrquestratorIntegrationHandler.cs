using DevStore.Core.Messages.Integration;
using DevStore.MessageBus;
using DevStore.Orders.API.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevStore.Orders.API.Services
{
    public class OrderOrquestratorIntegrationHandler : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderOrquestratorIntegrationHandler> _logger;
        private Timer _timer;

        public OrderOrquestratorIntegrationHandler(ILogger<OrderOrquestratorIntegrationHandler> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Order service initialized.");

            _timer = new Timer(OrquestrateOrders, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(15));

            return Task.CompletedTask;
        }

        private async void OrquestrateOrders(object state)
        {
            using var scope = _serviceProvider.CreateScope();

            var orderQueries = scope.ServiceProvider.GetRequiredService<IOrderQueries>();
            var order = await orderQueries.GetAuthorizedOrders();

            if (order == null) return;

            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            var authorizedOrder = new OrderAuthorizedIntegrationEvent(order.CustomerId, order.Id,
                order.OrderItems.ToDictionary(p => p.ProductId, p => p.Quantity));

            await bus.ProducerAsync("OrderLowered", authorizedOrder);

            _logger.LogInformation($"Order ID: {order.Id} was sent to lower at stock.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Order service finished.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}