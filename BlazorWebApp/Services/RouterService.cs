using BlazorWebApp.Extensions;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public class RouterService
    {
        private readonly SDAPIService _sdapi;
        private readonly IServiceProvider _serviceProvider;
        private readonly ManagerService _m;

        public RouterService(SDAPIService sdapi, IServiceProvider serviceProvider, ManagerService m)
        {
            _sdapi = sdapi;
            _serviceProvider = serviceProvider;
            _m = m;
        }

        public async Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters)
        {
            if (_m.IsComfyUIUp)
            {
                var checkpoint = _m.State.Generation.SDModel;
                var vae = _m.State.Generation.Vae;
                return await _serviceProvider.UseComfyAPI(c => c.PostTxt2Img(parameters, checkpoint, vae));
            }
            if (_m.IsWebuiUp)
                return await _sdapi.PostTxt2Img(parameters);
            throw new InvalidOperationException("No backend available");
        }

        public async Task<GeneratedImages> PostImg2Img(Img2ImgParameters parameters)
        {
            if (_m.IsComfyUIUp)
                //return await _capi.PostImg2Img(parameters);
                throw new Exception("Not implemented");
            if (_m.IsWebuiUp)
                return await _sdapi.PostImg2Img(parameters);
            throw new InvalidOperationException("No backend available");
        }
    }
}
