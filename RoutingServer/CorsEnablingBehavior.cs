using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace RoutingServer
{
    /// <summary>
    /// Endpoint behavior that enables Cross-Origin Resource Sharing (CORS) for REST endpoints.
    /// Allows web applications from different origins to access the service.
    /// </summary>
    public class CorsEnablingBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime) { }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new CorsMessageInspector());
        }

        public void Validate(ServiceEndpoint endpoint) { }
    }

    /// <summary>
    /// Message inspector that adds CORS headers to HTTP responses.
    /// </summary>
    public class CorsMessageInspector : IDispatchMessageInspector
    {
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            return null;
        }

        /// <summary>
        /// Adds CORS headers to the HTTP response before sending.
        /// </summary>
        /// <param name="reply">The outgoing message</param>
        /// <param name="correlationState">Correlation state (unused)</param>
        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            var httpHeader = reply.Properties["httpResponse"] as System.ServiceModel.Channels.HttpResponseMessageProperty;
            if (httpHeader != null)
            {
                httpHeader.Headers.Add("Access-Control-Allow-Origin", "*");
                httpHeader.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                httpHeader.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept");
            }
        }
    }
}