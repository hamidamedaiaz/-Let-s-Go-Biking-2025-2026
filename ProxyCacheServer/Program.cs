
using ProxyCacheService;
using System;
using System.ServiceModel;

namespace ProxyCacheServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var baseAddress = new Uri("http://localhost:8080/ProxyCacheService");
            using (var host = new ServiceHost(typeof(ProxyCacheServiceImpl), baseAddress))
            {
                host.Open();
                Console.WriteLine("ProxyCacheService running at " + baseAddress);
                Console.ReadLine();
            }
        }
    }
}
