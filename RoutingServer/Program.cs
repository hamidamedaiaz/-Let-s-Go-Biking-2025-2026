using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace RoutingServer
{
    public class Program
    {
        static void Main(string[] args)
        {
            Uri baseAddress = new Uri("http://localhost:8733/RoutingService");
            using (var host = new WebServiceHost(typeof(RoutingService), baseAddress))
            {
                var endpoint = host.AddServiceEndpoint(
                    typeof(IRoutingService),
                    new WebHttpBinding(),
                    ""
                    );
                endpoint.Behaviors.Add(new WebHttpBehavior());
                endpoint.EndpointBehaviors.Add(new CorsEnablingBehavior());

                host.Open();
                Console.WriteLine("RoutingService REST running at " + baseAddress);
                Console.WriteLine("endpoint:", baseAddress);
                Console.ReadLine();
            }
        }
    }
}
