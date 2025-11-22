using Microsoft.Extensions.Configuration;
using ProxyCacheService;
using System;
using System.IO;
using System.ServiceModel;

namespace ProxyCacheServer { 
    internal class Program
    {
        static void Main(string[] args)
        {
          

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config\\jcdecaux.json", optional: false, reloadOnChange: true)
                .Build();

            var proxyService = new ProxyCacheServiceImpl(configuration);
            var baseAddress = new Uri("http://localhost:8080/ProxyCacheService");
            using (var host = new ServiceHost(proxyService, baseAddress))
            {
                host.Open();
                Console.WriteLine("ProxyCacheService running at " + baseAddress);
                Console.WriteLine("\nPress Enter to stop the service...");
                Console.ReadLine();
            }
        }
    }
}
