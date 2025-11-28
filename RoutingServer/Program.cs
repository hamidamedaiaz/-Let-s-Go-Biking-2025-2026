using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace RoutingServer
{
    /// <summary>
    /// Entry point for the Routing Service application.
    /// Configures and hosts both REST and SOAP endpoints for the routing service.
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            Uri restAddress = new Uri("http://localhost:8733/RoutingService");
            Uri soapAddress = new Uri("http://localhost:8734/RoutingServiceSOAP");

            WebServiceHost restHost = new WebServiceHost(typeof(RoutingService), restAddress);
            ServiceHost soapHost = new ServiceHost(typeof(RoutingService), soapAddress);

            ConfigureRestEndpoint(restHost);
            ConfigureSoapEndpoint(soapHost);

            try
            {
                StartServices(restHost, soapHost);
                
                Console.WriteLine("Press ENTER to stop both services...");
                Console.ReadLine();

                StopServices(restHost, soapHost);
            }
            catch (Exception ex)
            {
                LogError(ex);
                restHost.Abort();
                soapHost.Abort();
            }
        }

        /// <summary>
        /// Configures the REST endpoint with increased message size limits and CORS support.
        /// </summary>
        /// <param name="restHost">The WebServiceHost instance for REST endpoint</param>
        private static void ConfigureRestEndpoint(WebServiceHost restHost)
        {
            WebHttpBinding webBinding = new WebHttpBinding
            {
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                ReaderQuotas =
                {
                    MaxDepth = 32,
                    MaxStringContentLength = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxBytesPerRead = int.MaxValue,
                    MaxNameTableCharCount = int.MaxValue
                }
            };

            var restEndpoint = restHost.AddServiceEndpoint(
                typeof(IRoutingService),
                webBinding,
                ""
            );
            restEndpoint.Behaviors.Add(new WebHttpBehavior());
            restEndpoint.EndpointBehaviors.Add(new CorsEnablingBehavior());
        }

        /// <summary>
        /// Configures the SOAP endpoint with increased message size limits and metadata support.
        /// </summary>
        /// <param name="soapHost">The ServiceHost instance for SOAP endpoint</param>
        private static void ConfigureSoapEndpoint(ServiceHost soapHost)
        {
            BasicHttpBinding soapBinding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                ReaderQuotas =
                {
                    MaxDepth = 32,
                    MaxStringContentLength = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxBytesPerRead = int.MaxValue,
                    MaxNameTableCharCount = int.MaxValue
                }
            };

            soapHost.AddServiceEndpoint(typeof(IRoutingService), soapBinding, "");

            ServiceMetadataBehavior smb = new ServiceMetadataBehavior
            {
                HttpGetEnabled = true
            };
            soapHost.Description.Behaviors.Add(smb);
        }

        /// <summary>
        /// Starts both REST and SOAP service hosts.
        /// </summary>
        private static void StartServices(WebServiceHost restHost, ServiceHost soapHost)
        {
            restHost.Open();
            Console.WriteLine("[RoutingService] REST endpoint started successfully");
            Console.WriteLine($"[RoutingService] REST URL: {restHost.BaseAddresses[0]}");
            Console.WriteLine($"[RoutingService] Max message size: {((WebHttpBinding)restHost.Description.Endpoints[0].Binding).MaxReceivedMessageSize / 1024 / 1024} MB");

            soapHost.Open();
            Console.WriteLine("[RoutingService] SOAP endpoint started successfully");
            Console.WriteLine($"[RoutingService] SOAP URL: {soapHost.BaseAddresses[0]}?wsdl");
            Console.WriteLine($"[RoutingService] Max message size: {((BasicHttpBinding)soapHost.Description.Endpoints[0].Binding).MaxReceivedMessageSize / 1024 / 1024} MB");
            Console.WriteLine();
        }

        /// <summary>
        /// Gracefully stops both service hosts.
        /// </summary>
        private static void StopServices(WebServiceHost restHost, ServiceHost soapHost)
        {
            restHost.Close();
            soapHost.Close();
            Console.WriteLine("[RoutingService] Services stopped successfully");
        }

        /// <summary>
        /// Logs error details to console.
        /// </summary>
        private static void LogError(Exception ex)
        {
            Console.WriteLine($"[RoutingService] ERROR: {ex.Message}");
            Console.WriteLine($"[RoutingService] Stack trace: {ex.StackTrace}");
        }
    }
}