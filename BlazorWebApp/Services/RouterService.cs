using BlazorWebApp.Data.Entities;
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
                var model = _m.GetCurrentModel(ModeType.Txt2Img);
                var vae = _m.GetCurrentVae(ModeType.Txt2Img);
                var workflow = parameters.Comfy.Workflow ?? _m.ParametersTxt2Img.Comfy.Workflow;
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

        public async Task<GeneratedVideos> PostImg2Vid(Img2VidParameters parameters)
        {
            if (_m.IsComfyUIUp)
            {
                var workflow = parameters.Comfy.Workflow ?? _m.ParametersImg2Vid.Comfy.Workflow;
                if (workflow == null)
                    throw new InvalidOperationException("No workflow configured for Img2Vid generation");

                return await _capi.PostImg2Vid(parameters.ToComfyUI(), _m.ComfyWSClientId, workflow);
            }

            throw new InvalidOperationException("Img2Vid is only supported on ComfyUI backend");
        }
    }
}
