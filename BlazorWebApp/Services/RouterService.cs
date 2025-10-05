using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

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

        public async Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters)
        {
            if (_m.IsComfyUIUp)
            {
                var model = _m.State.Generation.SDModel;
                var vae = _m.State.Generation.Vae;
                var workflow = _m.GetCurrentWorkflow(ModeType.Txt2Img);
                return await _capi.PostTxt2Img(parameters, _m.ComfyWSClientId, workflow.Prompt, model, vae);
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
