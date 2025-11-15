using SharedModels;
using System;
using System.ServiceModel;
using System.Net.Http;


namespace ProxyCacheService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ProxyCacheServiceImpl : IProxyCacheService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
