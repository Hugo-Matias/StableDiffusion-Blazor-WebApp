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
        private PeriodicTimer? _timer;
        private SharedParameters _parsingParams;
        private int _canvasSourceWidth;
        private int _canvasSourceHeight;

        public event Action OnChange;

        public ImageService(SDAPIService api, IOService io, AppState app, MagickService magick, DatabaseService db)
        {
            _api = api;
            _io = io;
            _app = app;
            _magick = magick;
            _db = db;
        }

        public async Task<ImagesDto> GetImages(ModeType mode)
        {
            _app.IsConverging = true;
            StartProgressChecker();

            //_app.GridImage = string.Empty;

            ImagesDto images = new();

            try
            {
                // Using a temporary parameters model to parse selected dropdown Styles without writing them to the input fields
                switch (mode)
                {
                    case ModeType.Img2Img:
                        _parsingParams = Parser.ParseParameters(new SharedParameters(_app.ParametersImg2Img), _app.CurrentStyles);
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
                            images = await SaveImages(Outdir.Img2ImgSamples, Outdir.Img2ImgGrid);
                            break;
                        case ModeType.Extras:
                            images = await SaveUpscaleImage();
                            break;
                        default:
                            images = await SaveImages(Outdir.Txt2ImgSamples, Outdir.Txt2ImgGrid);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync(e.ToString());
            }

            StopProgressChecker();
            _app.IsConverging = false;

            NotifyStateChanged();
            return images;
        }

        public async Task<ImagesDto> SaveImages(Outdir outdirSamples, Outdir? outdirGrid = null)
        {
            DirectoryInfo saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirSamples));
            ImagesDto savedImages = new() { PageCount = 1, HasNext = false, HasPrev = false, CurrentPage = 1, Images = new() };

            var fileIndex = _io.GetFileIndex(saveDir.FullName, outdirSamples);
            _app.CurrentSeed = _app.ImagesInfo.Seed;

            for (int i = 0; i < _app.Images.Images.Count; i++)
            {
                fileIndex++;

                string fullpath = GetImagePath(saveDir.FullName, fileIndex);
                string extension = _app.Options.SamplesFormat.ToLowerInvariant();
                string imagePath = $"{fullpath}.{extension}";

                await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(_app.Images.Images[i]));


                if ((bool)_app.Options.SaveTxt)
                {
                    var infoPath = $"{fullpath}.txt";
                    _io.SaveText(infoPath, _app.ImagesInfo.InfoTexts[i]);
                    savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples, infoPath));
                }
                else
                    savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples));

                _app.CurrentSeed++;
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

            if ((bool)_app.Options.SaveTxt)
            {
                var infoPath = $"{fullpath}.txt";
                _io.SaveText(infoPath, _app.GeneratedUpscaleImage.Info);
                savedImage.Images.Add(await AddImageToDb(imagePath, Outdir.Extras, infoPath));
            }
            else
                savedImage.Images.Add(await AddImageToDb(imagePath, Outdir.Extras));

            return savedImage;
        }

        public string LoadImage(string imagePath) => _io.GetBase64FromFile(imagePath);

        public async Task<string> LoadImageAsync(string imagePath) => await _io.GetBase64FromFileAsync(imagePath);

        //public async Task<string> LoadImageAsync(string imagePath) => await _magick.LoadImage(imagePath);

        private async Task<Image> AddImageToDb(string path, Outdir outdir, string infoPath = null)
        {
            Image image = new();

            image.Path = path;
            image.ProjectId = _app.CurrentProjectId;
            if (outdir == Outdir.Txt2ImgSamples && _app.ParametersTxt2Img.EnableHR == true)
            {
                var resizeRes = Parser.ParseHighresResolution((int)_parsingParams.Width, (int)_parsingParams.Height, _app.ParametersTxt2Img.HRWidth, _app.ParametersTxt2Img.HRHeight, _app.ParametersTxt2Img.HRScale);
                image.Width = resizeRes.Item1;
                image.Height = resizeRes.Item2;
            }
            else if (outdir == Outdir.Img2ImgSamples)
            {
                image.Width = _canvasSourceWidth;
                image.Height = _canvasSourceHeight;
            }
            else
            {
                image.Width = (int)_parsingParams.Width;
                image.Height = (int)_parsingParams.Height;
            }
            if (infoPath != null) { image.InfoPath = infoPath; }
            if (outdir != Outdir.Extras)
            {
                image.Prompt = _parsingParams.Prompt;
                image.NegativePrompt = _parsingParams.NegativePrompt;
                image.SamplerId = await _db.GetSampler(_parsingParams.SamplerIndex);
                image.Steps = (int)_parsingParams.Steps;
                image.Seed = (long)_app.CurrentSeed;
                image.CfgScale = (float)_parsingParams.CfgScale;
                image.DenoisingStrength = _parsingParams.DenoisingStrength;
            }

            ModeType mode;
            switch (outdir)
            {
                case Outdir.Txt2ImgSamples:
                    mode = ModeType.Txt2Img;
                    break;

                case Outdir.Img2ImgSamples:
                    mode = ModeType.Img2Img;
                    break;

                case Outdir.Extras:
                    mode = ModeType.Extras;
                    break;

                default:
                    mode = ModeType.Txt2Img;
                    break;
            }
            image.ModeId = await _db.GetMode(mode);

            return await _db.AddImage(image);
        }

        private string GetImagePath(string path, int fileIndex)
        {
            string infoname = _app.ConvertPathPattern(_app.Options.FilenamePatternSamples);
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

        private async void StartProgressChecker()
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (await _timer.WaitForNextTickAsync())
            {
                _app.Progress = await _api.GetProgress();
                NotifyStateChanged();
            }
        }

        private void StopProgressChecker()
        {
            _timer?.Dispose();
            _app.Progress = new();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
