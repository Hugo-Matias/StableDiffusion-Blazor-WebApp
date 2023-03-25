using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class ImageService
    {
        private readonly SDAPIService _api;
        private readonly IOService _io;
        private readonly AppState _app;
        private readonly MagickService _magick;
        private readonly DatabaseService _db;
        private readonly ProgressService _progress;
        private PeriodicTimer? _timer;
        private SharedParameters _parsingParams;
        private int _canvasSourceWidth;
        private int _canvasSourceHeight;

        public event Action OnChange;

        public ImageService(SDAPIService api, IOService io, AppState app, MagickService magick, DatabaseService db, ProgressService progress)
        {
            _api = api;
            _io = io;
            _app = app;
            _magick = magick;
            _db = db;
            _progress = progress;
        }

        public async Task<ImagesDto> GetImages(ModeType mode)
        {
            _app.IsConverging = true;
            var progress = new BaseProgress() { BarColor = MudBlazor.Color.Primary };
            StartProgressChecker(progress);

            //_app.GridImage = string.Empty;

            ImagesDto images = new();
            string scriptName = string.Empty;

            try
            {
                // Using a temporary parameters model to parse selected dropdown Styles without writing them to the input fields
                switch (mode)
                {
                    case ModeType.Img2Img:
                        _parsingParams = Parser.ParseParameters(new SharedParameters(_app.ParametersImg2Img), _app.CurrentStyles);
                        CreateControlNetUnits(ref _parsingParams, _app.ParametersImg2Img.Scripts.ControlNet);
                        CreateScriptParameters(ref _parsingParams, _app.ParametersImg2Img.Scripts.Cutoff, "cutoff");
                        CreateScriptParameters(ref _parsingParams, _app.ParametersImg2Img.Scripts.DynamicPrompts, GetDynamicPromptsVersion());
                        CreateScriptParameters(ref _parsingParams, _app.ParametersImg2Img.Scripts.MultiDiffusionTiledDiffusion, "Tiled Diffusion");
                        CreateScriptParameters(ref _parsingParams, _app.ParametersImg2Img.Scripts.MultiDiffusionTiledVae, "Tiled VAE");
                        scriptName = CreateScriptParameters(ref _parsingParams, _app.ParametersImg2Img.Scripts.UltimateUpscale, "Ultimate SD upscale");
                        var img2imgParams = new Img2ImgParameters(_parsingParams);
                        img2imgParams.InitImages = _app.ParametersImg2Img.InitImages;
                        img2imgParams.Mask = _app.ParametersImg2Img.Mask;
                        img2imgParams.MaskBlur = _app.ParametersImg2Img.MaskBlur;
                        img2imgParams.ResizeMode = _app.ParametersImg2Img.ResizeMode;
                        img2imgParams.InpaintingFill = _app.ParametersImg2Img.InpaintingFill;
                        img2imgParams.InpaintFullRes = _app.ParametersImg2Img.InpaintFullRes;
                        img2imgParams.InpaintFullResPadding = _app.ParametersImg2Img.InpaintFullResPadding;
                        img2imgParams.InpaintingMaskInvert = _app.ParametersImg2Img.InpaintingMaskInvert;
                        SetSourceImageSize();
                        _app.Images = await _api.PostImg2Img(img2imgParams);
                        _app.SerializeInfo();
                        break;

                    case ModeType.Extras:
                        _parsingParams = _app.ParametersUpscale;
                        _app.GeneratedUpscaleImage = await _api.PostExtraSingle(_app.ParametersUpscale);
                        if (_app.GeneratedUpscaleImage != null && !string.IsNullOrWhiteSpace(_app.GeneratedUpscaleImage.Image))
                        {
                            var upscaledResolution = _magick.GetImageSize(_app.GeneratedUpscaleImage.Image);
                            _parsingParams.Width = upscaledResolution.Item1;
                            _parsingParams.Height = upscaledResolution.Item2;
                            var html = new HtmlDocument();
                            html.LoadHtml(_app.GeneratedUpscaleImage.Info);
                            _app.GeneratedUpscaleImage.Info = html.DocumentNode.InnerText;
                        }
                        break;

                    default:
                        _parsingParams = Parser.ParseParameters(new SharedParameters(_app.ParametersTxt2Img), _app.CurrentStyles);
                        CreateControlNetUnits(ref _parsingParams, _app.ParametersTxt2Img.Scripts.ControlNet);
                        CreateScriptParameters(ref _parsingParams, _app.ParametersTxt2Img.Scripts.Cutoff, "cutoff");
                        CreateScriptParameters(ref _parsingParams, _app.ParametersTxt2Img.Scripts.DynamicPrompts, GetDynamicPromptsVersion());
                        CreateScriptParameters(ref _parsingParams, _app.ParametersTxt2Img.Scripts.MultiDiffusionTiledDiffusion, "Tiled Diffusion");
                        CreateScriptParameters(ref _parsingParams, _app.ParametersTxt2Img.Scripts.MultiDiffusionTiledVae, "Tiled VAE");
                        var txt2imgParams = new Txt2ImgParameters(_parsingParams);
                        txt2imgParams.EnableHR = _app.ParametersTxt2Img.EnableHR;
                        if (txt2imgParams.EnableHR != null && (bool)txt2imgParams.EnableHR)
                        {
                            txt2imgParams.FirstphaseWidth = _app.ParametersTxt2Img.Width;
                            txt2imgParams.FirstphaseHeight = _app.ParametersTxt2Img.Height;
                            txt2imgParams.HRUpscaler = _app.ParametersTxt2Img.HRUpscaler;
                            txt2imgParams.HRScale = _app.ParametersTxt2Img.HRScale;
                            txt2imgParams.HRWidth = _app.ParametersTxt2Img.HRWidth;
                            txt2imgParams.HRHeight = _app.ParametersTxt2Img.HRHeight;
                            txt2imgParams.HRSecondPassSteps = _app.ParametersTxt2Img.HRSecondPassSteps;
                            txt2imgParams.DenoisingStrength = _app.ParametersTxt2Img.DenoisingStrength;
                        }
                        _app.Images = await _api.PostTxt2Img(txt2imgParams);
                        _app.SerializeInfo();
                        break;
                }


                if ((bool)_app.Options.SamplesSave)
                {
                    switch (mode)
                    {
                        case ModeType.Img2Img:
                            images = await SaveImages(Outdir.Img2ImgSamples, Outdir.Img2ImgGrid, scriptName);
                            break;
                        case ModeType.Extras:
                            images = await SaveUpscaleImage();
                            break;
                        default:
                            images = await SaveImages(Outdir.Txt2ImgSamples, Outdir.Txt2ImgGrid, scriptName);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync(e.ToString());
            }

            StopProgressChecker(progress.Id);
            _app.IsConverging = false;

            NotifyStateChanged();
            return images;
        }

        private void CreateControlNetUnits(ref SharedParameters parameters, List<ScriptParametersControlNet> units)
        {
            if (_app.ControlNetEnabled && units != null && units.Count > 0)
            {
                foreach (var unit in units)
                {
                    unit.InputImage = Parser.RemoveBase64Header(unit.InputImage);
                }
                parameters.AlwaysOnScripts = new() { { "controlnet", new Dictionary<string, List<ScriptParametersControlNet>>() { { "args", units } } } };
            }
        }

        private string CreateScriptParameters(ref SharedParameters parameters, BaseScriptParameters scriptParam, string payloadKey)
        {
            if (scriptParam != null && scriptParam.IsEnabled)
            {
                var argsArray = scriptParam.GetType().GetProperties().Select(p => p.GetValue(scriptParam, null)).ToArray();
                // Since the shared ScriptParameteresBase properties are loaded last and order is important, we need to reorder them
                var tempList = argsArray.ToList();
                if (scriptParam.IsAlwaysOn)
                {
                    // Last element at this point is BaseScriptParameters.IsAlwaysOn, since we don't need the value in the payload it's just discarded
                    tempList.RemoveAt(tempList.Count - 1);
                    // Last element at this point is BaseScriptParameters.IsEnabled, the value we need move to top
                    var isEnabledValue = tempList[tempList.Count - 1];
                    tempList.RemoveAt(tempList.Count - 1);
                    tempList.Insert(0, isEnabledValue);

                    //Expand MultiDiffusion box region controls
                    if (payloadKey == "Tiled Diffusion")
                    {
                        var controls = tempList[tempList.Count - 1];
                        tempList.RemoveAt(tempList.Count - 1);
                        foreach (var control in (List<ScriptParametersMultiDiffusionBBoxControl>)controls)
                        {
                            tempList.AddRange(control.GetType().GetProperties().Select(p => p.GetValue(control, null)).ToArray());
                        }
                    }

                    argsArray = tempList.ToArray();
                    var payloadValue = new Dictionary<string, object[]>() { { "args", argsArray } };
                    if (parameters.AlwaysOnScripts == null) parameters.AlwaysOnScripts = new() { { payloadKey, payloadValue } };
                    else parameters.AlwaysOnScripts.Add(payloadKey, payloadValue);
                }
                else
                {
                    // Remove the 2 shared values from the payload since they are not needed on triggered scripts like Ultimate Upscale
                    tempList.RemoveAt(tempList.Count - 1);
                    tempList.RemoveAt(tempList.Count - 1);
                    argsArray = tempList.ToArray();
                    parameters.ScriptName = payloadKey;
                    parameters.ScriptArgs = argsArray;
                    return payloadKey;
                }
            }
            return string.Empty;
        }

        public async Task<ImagesDto> SaveImages(Outdir outdirSamples, Outdir? outdirGrid, string scriptName)
        {
            DirectoryInfo saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirSamples));
            ImagesDto savedImages = new() { PageCount = 1, HasNext = false, HasPrev = false, CurrentPage = 1, Images = new() };

            var fileIndex = _io.GetFileIndex(saveDir.FullName, outdirSamples);
            var mode = Parser.ModeTypeFromOutdir(outdirSamples);

            for (int i = 0; i < _app.Images.Images.Count; i++)
            {
                fileIndex++;
                var extension = _app.Options.SamplesFormat.ToLowerInvariant();

                // Under certain conditions, SD returns images without info data, creating a mismatch between Images and Info list size.
                // To prevent crashing later in the method when info is parsed and saved, we must preemptively break the execution and save the extra images to disk.
                // These images aren't added to the database.
                if (i >= _app.ImagesInfo.InfoTexts.Length)
                {
                    if ((outdirSamples == Outdir.Txt2ImgSamples && _app.ParametersTxt2Img.AlwaysOnScripts != null && _app.ParametersTxt2Img.AlwaysOnScripts["controlnet"] != null) ||
                        (outdirSamples == Outdir.Img2ImgSamples && _app.ParametersImg2Img.AlwaysOnScripts != null && _app.ParametersImg2Img.AlwaysOnScripts["controlnet"] != null))
                    {
                        var cnImagePath = $"{GetImagePath(saveDir.FullName, fileIndex - 1, mode)}-ControlNet Annotator.{extension}";
                        await _io.SaveFileToDisk(cnImagePath, Convert.FromBase64String(_app.Images.Images[i]));
                    }
                    else if (outdirSamples == Outdir.Img2ImgSamples && scriptName == "Ultimate SD upscale")
                    {
                        var seamfixPath = $"{GetImagePath(saveDir.FullName, fileIndex - 1, mode)}-SeamFix.{extension}";
                        await _io.SaveFileToDisk(seamfixPath, Convert.FromBase64String(_app.Images.Images[i]));
                    }
                    break;
                }
                var info = Parser.ParseInfoStrings(_app.ImagesInfo.InfoTexts[i], mode);
                Dictionary<string, string>? param = null;
                if (info != null) param = Parser.ParseInfoParameters(info["param"]);
                _app.CurrentSeed = param != null && !string.IsNullOrEmpty(param["Seed"]) ? long.Parse(param["Seed"]) : (long)_parsingParams.Seed;

                var fullpath = GetImagePath(saveDir.FullName, fileIndex, mode);
                var imagePath = $"{fullpath}.{extension}";

                await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(_app.Images.Images[i]));

                if ((bool)_app.Options.SaveTxt)
                {
                    var infoPath = $"{fullpath}.txt";
                    _io.SaveText(infoPath, _app.ImagesInfo.InfoTexts[i]);
                    savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples, info, infoPath));
                }
                else
                    savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples, info));
            }

            if (outdirGrid != null && (bool)_app.Options.GridSave && (_app.Images.Images.Count > 1 && (bool)_app.Options.GridOnlyIfMultiple))
            {
                saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirGrid));
                fileIndex = _io.GetFileIndex(saveDir.FullName, (Outdir)outdirGrid) + 1;
                string fullpath = Path.Combine(saveDir.FullName, $"grid-{fileIndex.ToString().PadLeft(4, '0')}");
                string extension = _app.Options.GridFormat.ToLowerInvariant();
                string gridPath = $"{fullpath}.{extension}";

                _app.GridImage = await _magick.SaveGrid(_app.Images.Images, gridPath);

                if ((bool)_app.Options.SaveTxt)
                {
                    _io.SaveText($"{fullpath}.txt", _app.ImagesInfo.InfoTexts[0]);
                }
            }
            return savedImages;
        }

        public async Task<ImagesDto?> SaveUpscaleImage()
        {
            //if (_upscaledImage == null) return null;

            DirectoryInfo saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(Outdir.Extras));
            ImagesDto savedImage = new() { PageCount = 1, HasNext = false, HasPrev = false, CurrentPage = 1, Images = new() };

            var fileIndex = _io.GetFileIndex(saveDir.FullName, Outdir.Extras) + 1;
            var fullpath = Path.Combine(saveDir.FullName, fileIndex.ToString().PadLeft(5, '0'));
            var extension = _app.Options.SamplesFormat.ToLowerInvariant();
            var imagePath = $"{fullpath}.{extension}";
            await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(_app.GeneratedUpscaleImage.Image));

            var info = Parser.ParseInfoStrings(_app.GeneratedUpscaleImage.Info, ModeType.Extras);
            if ((bool)_app.Options.SaveTxt)
            {
                var infoPath = $"{fullpath}.txt";
                _io.SaveText(infoPath, _app.GeneratedUpscaleImage.Info);
                savedImage.Images.Add(await AddImageToDb(imagePath, Outdir.Extras, info, infoPath));
            }
            else
                savedImage.Images.Add(await AddImageToDb(imagePath, Outdir.Extras, info));

            return savedImage;
        }

        private async Task<Image> AddImageToDb(string path, Outdir outdir, Dictionary<string, string> info, string infoPath = null)
        {
            Image image = new();

            image.Path = path;
            image.ProjectId = _app.CurrentProjectId;
            if (info != null) image.Info = info["param"];
            if (outdir == Outdir.Txt2ImgSamples && _app.ParametersTxt2Img.EnableHR == true)
            {
                var resizeRes = Parser.ParseHighresResolution((int)_parsingParams.Width, (int)_parsingParams.Height, _app.ParametersTxt2Img.HRWidth, _app.ParametersTxt2Img.HRHeight, _app.ParametersTxt2Img.HRScale);
                image.Width = resizeRes.Item1;
                image.Height = resizeRes.Item2;
            }
            else if (outdir == Outdir.Img2ImgSamples)
            {

                if (info == null || string.IsNullOrWhiteSpace(info["param"]))
                {
                    image.Width = _canvasSourceWidth;
                    image.Height = _canvasSourceHeight;
                }
                else
                {
                    var param = Parser.ParseInfoParameters(info["param"]);
                    // Handles upscaling scripts (Ultimate Upscale) edge cases where the input image has lower resolution than the output info
                    if (param != null && param.ContainsKey("Size") && !string.IsNullOrWhiteSpace(param["Size"]))
                    {
                        var size = param["Size"].Split("x", 2, StringSplitOptions.RemoveEmptyEntries);
                        var sizeW = int.Parse(size[0].Trim());
                        var sizeH = int.Parse(size[1].Trim());
                        image.Width = sizeW > _canvasSourceWidth ? sizeW : _canvasSourceWidth;
                        image.Height = sizeH > _canvasSourceHeight ? sizeH : _canvasSourceHeight;
                    }
                    else
                    {
                        image.Width = _canvasSourceWidth;
                        image.Height = _canvasSourceHeight;
                    }
                }
            }
            else
            {
                var param = Parser.ParseInfoParameters(info["param"]);
                // Handles upscaling scripts (MultiDiffusion) edge cases where the output resolution is higher than the parameters passed into the api
                if (param != null && param.ContainsKey("Size") && !string.IsNullOrWhiteSpace(param["Size"]))
                {
                    var size = param["Size"].Split("x", 2, StringSplitOptions.RemoveEmptyEntries);
                    var sizeW = int.Parse(size[0].Trim());
                    var sizeH = int.Parse(size[1].Trim());
                    image.Width = sizeW > _parsingParams.Width ? sizeW : (int)_parsingParams.Width;
                    image.Height = sizeH > _parsingParams.Height ? sizeH : (int)_parsingParams.Height;
                }
                else
                {
                    image.Width = (int)_parsingParams.Width;
                    image.Height = (int)_parsingParams.Height;
                }
            }
            if (infoPath != null) { image.InfoPath = infoPath; }
            if (outdir != Outdir.Extras)
            {
                image.Prompt = info != null ? info["prompt"] : _parsingParams.Prompt;
                image.NegativePrompt = info != null ? info["negative"] : _parsingParams.NegativePrompt;
                image.SamplerId = await _db.GetSampler(_parsingParams.SamplerIndex);
                image.Steps = (int)_parsingParams.Steps;
                image.Seed = _app.CurrentSeed;
                image.CfgScale = (float)_parsingParams.CfgScale;
                image.DenoisingStrength = _parsingParams.DenoisingStrength;
            }

            image.ModeId = await _db.GetMode(Parser.ModeTypeFromOutdir(outdir));

            return await _db.AddImage(image);
        }

        public async Task DownloadImageAsPng(string url, string path, bool overwrite = true)
        {
            if (File.Exists(path) && !overwrite) return;
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(url);
            var response = await httpClient.GetByteArrayAsync(url);
            Directory.CreateDirectory(new FileInfo(path).DirectoryName);
            await File.WriteAllBytesAsync(path, _magick.ConvertToPng(response));
        }

        private string GetImagePath(string path, int fileIndex, ModeType mode)
        {
            string infoname = _app.ConvertPathPattern(_app.Options.FilenamePatternSamples, mode);
            string filename = $"{fileIndex.ToString().PadLeft(5, '0')}-{infoname}";
            return Path.Combine(path, filename);
        }

        /// <summary>
        /// In Img2Img, Width and Height are related to the section being masked and not the final image. <br/>
        /// This method will set global variables with the proper dimensions to be used in the image's data.
        /// </summary>
        private void SetSourceImageSize()
        {
            var data = Regex.Replace(_app.CanvasImageData, @"data.+?,", "");
            var size = _magick.GetImageSize(data);
            _canvasSourceWidth = size.Item1;
            _canvasSourceHeight = size.Item2;
        }

        private string GetDynamicPromptsVersion()
        {
            var scriptFile = Path.Combine(_app.CmdFlags.BaseDir, "extensions", "sd-dynamic-prompts", "sd_dynamic_prompts", "dynamic_prompting.py");
            foreach (var line in _io.LoadTextLines(scriptFile))
            {
                if (line.StartsWith("VERSION = "))
                {
                    var version = line.Split(" = ", 2)[1].Replace("\"", "").Trim();
                    return $"Dynamic Prompts v{version}";
                }
            }
            return string.Empty;
        }

        private async void StartProgressChecker(BaseProgress progress)
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _progress.Add(progress);

            while (await _timer.WaitForNextTickAsync())
            {
                var current = await _api.GetProgress();
                _app.Progress = current;
                _progress.Update(progress.Id, current.Value * 100);
                NotifyStateChanged();
            }
        }

        private void StopProgressChecker(Guid id)
        {
            _timer?.Dispose();
            _progress.Remove(id);
            _app.Progress = new();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
