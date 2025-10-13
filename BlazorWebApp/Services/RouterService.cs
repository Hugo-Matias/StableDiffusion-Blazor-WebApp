using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    public class RouterService
    {
        private readonly SDAPIService _sdapi;
        private readonly ComfyUIService _capi;
        private readonly ManagerService _m;

        public RouterService(SDAPIService sdapi, ComfyUIService capi, ManagerService m)
        {
            _sdapi = sdapi;
            _capi = capi;
            _m = m;
        }

        public async Task<IEnumerable<string>> SearchLoras(Backend backend, string search = "")
        {
            return backend switch
            {
                Backend.WebUI => throw new Exception("GetLoras not implemented for WebUI"),
                Backend.ComfyUI => string.IsNullOrEmpty(search) ? await _capi.GetLoras() : await _capi.SearchLoras(search),
                _ => [],
            };
        }

        public async Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters)
        {
            if (_m.IsComfyUIUp)
            {
                var model = _m.State.Generation.SDModel;
                var vae = _m.State.Generation.Vae;
                var workflow = parameters.Comfy.Workflow.Prompt;
                return await _capi.PostTxt2Img(parameters.ToTxt2ImgComfyUI(model, vae), _m.ComfyWSClientId, workflow);
            }
            if (_m.IsWebuiUp)
                return await _sdapi.PostTxt2Img(parameters.ToTxt2ImgWebUI());

            throw new InvalidOperationException("No backend available");
        }

        public async Task<GeneratedImages> PostImg2Img(Img2ImgParameters parameters)
        {
            if (_m.IsComfyUIUp)
                //return await _capi.PostImg2Img(parameters);
                throw new Exception("Not implemented");
            if (_m.IsWebuiUp)
                return await _sdapi.PostImg2Img(parameters.ToImg2ImgWebUI());
            throw new InvalidOperationException("No backend available");
        }
    }
}
