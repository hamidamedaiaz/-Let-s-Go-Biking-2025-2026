using System;
using System.ServiceModel;
using SharedModels;

namespace RoutingServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var factory = new ChannelFactory<IProxyCacheService>(
            new BasicHttpBinding(),
            new EndpointAddress("http://localhost:8080/ProxyCacheService")
        );

            var proxy = factory.CreateChannel();
            var contracts = proxy.GetAvailableContracts();

            Console.WriteLine("Contracts disponibles:");
            foreach (var c in contracts)
                Console.WriteLine($"- {c.Name}");

            ((IClientChannel)proxy).Close();
        }
    }
}
