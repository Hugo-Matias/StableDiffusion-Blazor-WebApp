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
        private readonly ManagerService _m;
        private readonly MagickService _magick;
        private readonly DatabaseService _db;
        private readonly ProgressService _progress;
        private PeriodicTimer? _timer;
        private SharedParameters _parsingParams;
        private Txt2ImgParameters _txt2imgParams;
        private int _canvasSourceWidth;
        private int _canvasSourceHeight;

        public event Action OnChange;

        public ImageService(SDAPIService api, IOService io, ManagerService m, MagickService magick, DatabaseService db, ProgressService progress)
        {
            _api = api;
            _io = io;
            _m = m;
            _magick = magick;
            _db = db;
            _progress = progress;
        }

        public async Task<ImagesDto> GetImages(ModeType mode)
        {
            _m.IsConverging = true;
            var progress = new BaseProgress() { BarColor = MudBlazor.Color.Primary };
            StartProgressChecker(progress);

            //_m.GridImage = string.Empty;

            ImagesDto images = new();
            string scriptName = string.Empty;

            try
            {
                // Using a temporary parameters model to parse selected dropdown Styles without writing them to the input fields
                switch (mode)
                {
                    case ModeType.Img2Img:
                        _parsingParams = Parser.ParseParameters(new SharedParameters(_m.ParametersImg2Img), _m.State.Generation.Styles);
                        CreateControlNetUnits(ref _parsingParams, _m.ParametersImg2Img.Scripts.ControlNet);
                        Parser.CreateScriptParameters("cutoff", ref _parsingParams, _m.ParametersImg2Img.Scripts.Cutoff);
                        Parser.CreateScriptParameters(_m.GetDynamicPromptsVersion(), ref _parsingParams, _m.ParametersImg2Img.Scripts.DynamicPrompts);
                        Parser.CreateScriptParameters("Tiled Diffusion", ref _parsingParams, _m.ParametersImg2Img.Scripts.MultiDiffusionTiledDiffusion);
                        Parser.CreateScriptParameters("Tiled VAE", ref _parsingParams, _m.ParametersImg2Img.Scripts.MultiDiffusionTiledVae);
                        Parser.CreateScriptParameters("Regional Prompter", ref _parsingParams, _m.ParametersImg2Img.Scripts.RegionalPrompter);
                        scriptName = Parser.CreateScriptParameters("Ultimate SD upscale", ref _parsingParams, _m.ParametersImg2Img.Scripts.UltimateUpscale);
                        scriptName = Parser.CreateScriptParameters("X/Y/Z plot", ref _parsingParams, _m.ParametersImg2Img.Scripts.XYZPlot);
                        var img2imgParams = new Img2ImgParameters(_parsingParams);
                        img2imgParams.InitImages = _m.ParametersImg2Img.InitImages;
                        img2imgParams.Mask = _m.ParametersImg2Img.Mask;
                        img2imgParams.MaskBlur = _m.ParametersImg2Img.MaskBlur;
                        img2imgParams.ResizeMode = _m.ParametersImg2Img.ResizeMode;
                        img2imgParams.InpaintingFill = _m.ParametersImg2Img.InpaintingFill;
                        img2imgParams.InpaintFullRes = _m.ParametersImg2Img.InpaintFullRes;
                        img2imgParams.InpaintFullResPadding = _m.ParametersImg2Img.InpaintFullResPadding;
                        img2imgParams.InpaintingMaskInvert = _m.ParametersImg2Img.InpaintingMaskInvert;
                        SetSourceImageSize();
                        _m.Images = await _api.PostImg2Img(img2imgParams);
                        _m.SerializeInfo();
                        break;

                    case ModeType.Extras:
                        _parsingParams = _m.ParametersUpscale;
                        _m.GeneratedUpscaleImage = await _api.PostExtraSingle(_m.ParametersUpscale);
                        if (_m.GeneratedUpscaleImage != null && !string.IsNullOrWhiteSpace(_m.GeneratedUpscaleImage.Image))
                        {
                            var upscaledResolution = _magick.GetImageSize(_m.GeneratedUpscaleImage.Image);
                            _parsingParams.Width = upscaledResolution.Item1;
                            _parsingParams.Height = upscaledResolution.Item2;
                            var html = new HtmlDocument();
                            html.LoadHtml(_m.GeneratedUpscaleImage.Info);
                            _m.GeneratedUpscaleImage.Info = html.DocumentNode.InnerText;
                        }
                        break;

                    default:
                        _parsingParams = Parser.ParseParameters(new SharedParameters(_m.ParametersTxt2Img), _m.State.Generation.Styles);
                        CreateControlNetUnits(ref _parsingParams, _m.ParametersTxt2Img.Scripts.ControlNet);
                        Parser.CreateScriptParameters("cutoff", ref _parsingParams, _m.ParametersTxt2Img.Scripts.Cutoff);
                        Parser.CreateScriptParameters(_m.GetDynamicPromptsVersion(), ref _parsingParams, _m.ParametersTxt2Img.Scripts.DynamicPrompts);
                        Parser.CreateScriptParameters("Tiled Diffusion", ref _parsingParams, _m.ParametersTxt2Img.Scripts.MultiDiffusionTiledDiffusion);
                        Parser.CreateScriptParameters("Tiled VAE", ref _parsingParams, _m.ParametersTxt2Img.Scripts.MultiDiffusionTiledVae);
                        Parser.CreateScriptParameters("Regional Prompter", ref _parsingParams, _m.ParametersTxt2Img.Scripts.RegionalPrompter);
                        scriptName = Parser.CreateScriptParameters("X/Y/Z plot", ref _parsingParams, _m.ParametersTxt2Img.Scripts.XYZPlot);
                        _txt2imgParams = new Txt2ImgParameters(_parsingParams);
                        _txt2imgParams.EnableHR = _m.ParametersTxt2Img.EnableHR;
                        if (_txt2imgParams.EnableHR != null && (bool)_txt2imgParams.EnableHR)
                        {
                            _txt2imgParams.FirstphaseWidth = _m.ParametersTxt2Img.Width;
                            _txt2imgParams.FirstphaseHeight = _m.ParametersTxt2Img.Height;
                            _txt2imgParams.HRUpscaler = _m.ParametersTxt2Img.HRUpscaler;
                            _txt2imgParams.HRScale = _m.ParametersTxt2Img.HRScale;
                            _txt2imgParams.HRWidth = _m.ParametersTxt2Img.HRWidth;
                            _txt2imgParams.HRHeight = _m.ParametersTxt2Img.HRHeight;
                            _txt2imgParams.HRSecondPassSteps = _m.ParametersTxt2Img.HRSecondPassSteps;
                            _txt2imgParams.DenoisingStrength = _m.ParametersTxt2Img.DenoisingStrength;
                        }
                        _m.Images = await _api.PostTxt2Img(_txt2imgParams);
                        _m.SerializeInfo();
                        break;
                }

                if ((bool)_m.Options.SamplesSave)
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
            _m.IsConverging = false;

            NotifyStateChanged();
            return images;
        }

        private void CreateControlNetUnits(ref SharedParameters parameters, List<ScriptParametersControlNet> units)
        {
            if (_m.ControlNetEnabled && units != null && units.Count > 0)
            {
                var nulledUnits = new List<ScriptParametersControlNet?>();
                foreach (var unit in units)
                {
                    if (unit.Model.Equals("None", StringComparison.InvariantCultureIgnoreCase) && unit.Preprocessor == ControlNetPreprocessor.none) nulledUnits.Add(null);
                    else
                    {
                        unit.InputImage = Parser.RemoveBase64Header(unit.InputImage);
                        nulledUnits.Add(unit);
                    }
                }
                parameters.AlwaysOnScripts = new() { { "controlnet", new Dictionary<string, List<ScriptParametersControlNet>>() { { "args", nulledUnits } } } };
            }
        }

        public async Task<ImagesDto> SaveImages(Outdir outdirSamples, Outdir? outdirGrid, string scriptName)
        {
            await _m.GetOptions();
            DirectoryInfo saveDir = _io.CreateDirectory(_m.GetCurrentSaveFolder(outdirSamples));
            ImagesDto savedImages = new() { PageCount = 1, HasNext = false, HasPrev = false, CurrentPage = 1, Images = new() };

            var fileIndex = _io.GetFileIndex(saveDir.FullName, outdirSamples);
            var mode = Parser.ModeTypeFromOutdir(outdirSamples);

            for (int i = 0; i < _m.Images.Images.Count; i++)
            {
                fileIndex++;
                var extension = _m.Options.SamplesFormat.ToLowerInvariant();

                // Under certain conditions, SD returns images without info data, creating a mismatch between Images and Info list size.
                // To prevent crashing later in the method when info is parsed and saved, we must preemptively break the execution and save the extra images to disk.
                // These images aren't added to the database.
                if (i >= _m.ImagesInfo.InfoTexts.Length)
                {
                    if ((outdirSamples == Outdir.Txt2ImgSamples && _txt2imgParams.AlwaysOnScripts != null && _txt2imgParams.AlwaysOnScripts.ContainsKey("controlnet") && _txt2imgParams.AlwaysOnScripts["controlnet"] != null) ||
                        (outdirSamples == Outdir.Img2ImgSamples && _m.ParametersImg2Img.AlwaysOnScripts != null && _m.ParametersImg2Img.AlwaysOnScripts.ContainsKey("controlnet") && _m.ParametersImg2Img.AlwaysOnScripts["controlnet"] != null))
                    {
                        var cnImagePath = $"{GetImagePath(saveDir.FullName, fileIndex - 1, mode)}-ControlNet Annotator {i - _m.ImagesInfo.InfoTexts.Length + 1}.{extension}";
                        await _io.SaveFileToDisk(cnImagePath, Convert.FromBase64String(_m.Images.Images[i]));
                    }
                    else if (outdirSamples == Outdir.Img2ImgSamples && scriptName == "Ultimate SD upscale")
                    {
                        var seamfixPath = $"{GetImagePath(saveDir.FullName, fileIndex - 1, mode)}-SeamFix.{extension}";
                        await _io.SaveFileToDisk(seamfixPath, Convert.FromBase64String(_m.Images.Images[i]));
                    }
                    continue;
                }
                var info = Parser.ParseInfoStrings(_m.ImagesInfo.InfoTexts[i], mode);
                Dictionary<string, string>? param = null;
                if (info != null) param = Parser.ParseInfoParameters(info["param"]);
                _m.State.Generation.Seed = param != null && !string.IsNullOrEmpty(param["Seed"]) ? long.Parse(param["Seed"]) : (long)_parsingParams.Seed;

                var fullpath = GetImagePath(saveDir.FullName, fileIndex, mode);
                var imagePath = $"{fullpath}.{extension}";

                await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(_m.Images.Images[i]));

                if ((bool)_m.Options.SaveTxt)
                {
                    var infoPath = $"{fullpath}.txt";
                    _io.SaveText(infoPath, _m.ImagesInfo.InfoTexts[i]);
                    savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples, info, infoPath));
                }
                else
                    savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples, info));
            }

            if (outdirGrid != null && (bool)_m.Options.GridSave && (_m.Images.Images.Count > 1 && (bool)_m.Options.GridOnlyIfMultiple))
            {
                saveDir = _io.CreateDirectory(_m.GetCurrentSaveFolder(outdirGrid));
                fileIndex = _io.GetFileIndex(saveDir.FullName, (Outdir)outdirGrid) + 1;
                string fullpath = Path.Combine(saveDir.FullName, $"grid-{fileIndex.ToString().PadLeft(4, '0')}");
                string extension = _m.Options.GridFormat.ToLowerInvariant();
                string gridPath = $"{fullpath}.{extension}";

                _m.GridImage = await _magick.SaveGrid(_m.Images.Images, gridPath);

                if ((bool)_m.Options.SaveTxt)
                {
                    _io.SaveText($"{fullpath}.txt", _m.ImagesInfo.InfoTexts[0]);
                }
            }
            return savedImages;
        }

        public async Task<ImagesDto?> SaveUpscaleImage()
        {
            //if (_upscaledImage == null) return null;

            DirectoryInfo saveDir = _io.CreateDirectory(_m.GetCurrentSaveFolder(Outdir.Extras));
            ImagesDto savedImage = new() { PageCount = 1, HasNext = false, HasPrev = false, CurrentPage = 1, Images = new() };

            var fileIndex = _io.GetFileIndex(saveDir.FullName, Outdir.Extras) + 1;
            var fullpath = Path.Combine(saveDir.FullName, fileIndex.ToString().PadLeft(5, '0'));
            var extension = _m.Options.SamplesFormat.ToLowerInvariant();
            var imagePath = $"{fullpath}.{extension}";
            await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(_m.GeneratedUpscaleImage.Image));

            var info = Parser.ParseInfoStrings(_m.GeneratedUpscaleImage.Info, ModeType.Extras);
            if ((bool)_m.Options.SaveTxt)
            {
                var infoPath = $"{fullpath}.txt";
                _io.SaveText(infoPath, _m.GeneratedUpscaleImage.Info);
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
            image.ProjectId = _m.State.Gallery.ProjectId;
            if (info != null) image.Info = info["param"];
            if (outdir == Outdir.Txt2ImgSamples && _txt2imgParams.EnableHR == true)
            {
                var resizeRes = Parser.ParseHighresResolution((int)_parsingParams.Width, (int)_parsingParams.Height, _txt2imgParams.HRWidth, _txt2imgParams.HRHeight, _txt2imgParams.HRScale);
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
                image.Seed = _m.State.Generation.Seed;
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
            string infoname = _m.ConvertPathPattern(_m.Options.FilenamePatternSamples, mode);
            string filename = $"{fileIndex.ToString().PadLeft(5, '0')}-{infoname}";
            return Path.Combine(path, filename);
        }

        /// <summary>
        /// In Img2Img, Width and Height are related to the section being masked and not the final image. <br/>
        /// This method will set global variables with the proper dimensions to be used in the image's data.
        /// </summary>
        private void SetSourceImageSize()
        {
            var data = Regex.Replace(_m.CanvasImageData, @"data.+?,", "");
            var size = _magick.GetImageSize(data);
            _canvasSourceWidth = size.Item1;
            _canvasSourceHeight = size.Item2;
        }

        private async void StartProgressChecker(BaseProgress progress)
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _progress.Add(progress);

            while (await _timer.WaitForNextTickAsync())
            {
                var current = await _api.GetProgress();
                _m.Progress = current;
                _progress.Update(progress.Id, current.Value * 100);
                NotifyStateChanged();
            }
        }

        private void StopProgressChecker(Guid id)
        {
            _timer?.Dispose();
            _progress.Remove(id);
            _m.Progress = new();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
