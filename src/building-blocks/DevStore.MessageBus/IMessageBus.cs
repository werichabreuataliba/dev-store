using System;
using System.Threading.Tasks;
using DevStore.Core.Messages.Integration;
using System.Threading;

namespace DevStore.MessageBus
{
    public interface IMessageBus : IDisposable
    {
       Task ProducerAsync<T>(string topic, T message) where T : IntegrationEvent;
       Task ConsumerAsync<T>(string topic, Func<T, Task> onMessage, CancellationToken cancellation) where T: IntegrationEvent;
    }
}