using DevStore.Catalog.API.Models;
using DevStore.Core.DomainObjects;
using DevStore.Core.Messages.Integration;
using DevStore.MessageBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevStore.Catalog.API.Services
{
    public class CatalogIntegrationHandler : BackgroundService
    {
        private readonly IMessageBus _bus;
        private readonly IServiceProvider _serviceProvider;

        public CatalogIntegrationHandler(IServiceProvider serviceProvider, IMessageBus bus)
        {
            _serviceProvider = serviceProvider;
            _bus = bus;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _bus.ConsumerAsync<OrderAuthorizedIntegrationEvent>("OrderAuthorized", WriteDownInventory, stoppingToken);
        }

        private async Task WriteDownInventory(OrderAuthorizedIntegrationEvent message)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var productsWithAvailableStock = new List<Product>();
                var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();

                var productsId = string.Join(",", message.Items.Select(c => c.Key));
                var products = await productRepository.GetProductsById(productsId);

                if (products.Count != message.Items.Count)
                {
                    await CancelOrderWithoutStock(message);
                    return;
                }

                foreach (var product in products)
                {
                    var productUnits = message.Items.FirstOrDefault(p => p.Key == product.Id).Value;

                    if (product.IsAvailable(productUnits))
                    {
                        product.TakeFromInventory(productUnits);
                        productsWithAvailableStock.Add(product);
                    }
                }

                if (productsWithAvailableStock.Count != message.Items.Count)
                {
                    await CancelOrderWithoutStock(message);
                    return;
                }

                foreach (var product in productsWithAvailableStock)
                {
                    productRepository.Update(product);
                }

                if (!await productRepository.UnitOfWork.Commit())
                {
                    throw new DomainException($"Problems updating stock for order {message.OrderId}");
                }

                var productTaken = new OrderLoweredStockIntegrationEvent(message.CustomerId, message.OrderId);
                await _bus.ProducerAsync("OrderLowered", productTaken);
            }
        }

        public async Task CancelOrderWithoutStock(OrderAuthorizedIntegrationEvent message)
        {
            var orderCancelled = new OrderCanceledIntegrationEvent(message.CustomerId, message.OrderId);
            await _bus.ProducerAsync("OrderLowered", orderCancelled);
        }
    }
}