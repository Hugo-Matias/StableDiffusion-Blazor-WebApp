﻿namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiModelsModelDto : CivitaiBaseModelDto
    {
        public List<string> Tags { get; set; }
        public List<CivitaiModelsModelVersionDto> ModelVersions { get; set; }
        public CivitaiModelsModelDto() { }
        public CivitaiModelsModelDto(CivitaiModelDto model)
        {
            Id = model.Id;
            Name = model.Name;
            Description = model.Description;
            Type = model.Type;
            Nsfw = model.Nsfw;
            Creator = model.Creator;
            Tags = model.Tags;
            ModelVersions = new();
            foreach (var version in model.ModelVersions)
            {
                ModelVersions.Add(new(version));
            }
        }
    }

    public class CivitaiModelsModelVersionDto : CivitaiBaseModelVersionDto
    {
        public List<CivitaiBaseModelVersionFileDto> Files { get; set; }
        public List<CivitaiModelVersionImageDto> Images { get; set; }
        public CivitaiModelsModelVersionDto() { }
        public CivitaiModelsModelVersionDto(CivitaiModelVersionDto model)
        {
            Files = new();
            foreach (var file in model.Files)
            {
                Files.Add(new(file));
            }

            Images = model.ImagesData;
        }
    }
}
