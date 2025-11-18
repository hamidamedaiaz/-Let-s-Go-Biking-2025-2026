using Microsoft.Extensions.Configuration;
using ProxyCacheService;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;

namespace ProxyCacheServer { 
    internal class Program
    {
        static void Main(string[] args)
        {
            // Configuration en mémoire avec les clés API directement dans le code
            var inMemorySettings = new Dictionary<string, string>
            {
                {"JCDApiKey", "32e76f776d7d87c60aa9c7f3c8a2700d886b0909"},
                {"ORSApiKey", "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6IjRkOWQ3ZjljNTRhYTRkYTU4NzMzYmQzYjhiZTJmYTEwIiwiaCI6Im11cm11cjY0In0="},
                {"BaseUrl", "https://api.jcdecaux.com/vls/v3"}
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var proxyService = new ProxyCacheServiceImpl(configuration);
            var baseAddress = new Uri("http://localhost:8080/ProxyCacheService");
            using (var host = new ServiceHost(proxyService, baseAddress))
            {
                host.Open();
                Console.WriteLine("ProxyCacheService running at " + baseAddress);
                Console.WriteLine("JCDecaux API Key: " + configuration["JCDApiKey"]);
                Console.WriteLine("OpenRouteService API Key: " + configuration["ORSApiKey"]);
                Console.WriteLine("\nPress Enter to stop the service...");
                Console.ReadLine();
            }
        }
    }
}
