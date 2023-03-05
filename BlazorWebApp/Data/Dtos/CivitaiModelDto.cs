﻿using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiModelDto : CivitaiBaseModelDto
    {
        [JsonPropertyName("poi")]
        public bool PersonOfInterest { get; set; }
        public bool AllowNoCredit { get; set; }
        public string AllowCommercialUse { get; set; }
        public bool AllowDerivatives { get; set; }
        public bool AllowDifferentLicense { get; set; }
        public CivitaiModelRankDto Rank { get; set; }
        public List<CivitaiModelVersionDto> ModelVersions { get; set; }
    }

    public class CivitaiModelRankDto
    {
        public int DownloadCountAllTime { get; set; }
        public int CommentCountAllTime { get; set; }
        public int FavoriteCountAllTime { get; set; }
        public int RatingCountAllTime { get; set; }
        public double RatingAllTime { get; set; }
    }

    public class CivitaiModelVersionDto : CivitaiBaseModelVersionDto
    {
        public int ModelId { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string BaseModel { get; set; }
        public double EarlyAccessTimeFrame { get; set; }
        public List<CivitaiModelVersionFileDto> Files { get; set; }
        public List<CivitaiModelVersionImageDto> Images { get; set; }
    }

    public class CivitaiModelVersionFileDto : CivitaiBaseModelVersionFileDto
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string Type { get; set; }
        public string PickleScanMessage { get; set; }
        public CivitaiModelVersionFileHashesDto Hashes { get; set; }
    }

    public class CivitaiModelVersionFileHashesDto
    {
        public string AutoV1 { get; set; }
        public string AutoV2 { get; set; }
        public string SHA256 { get; set; }
        public string CRC32 { get; set; }
        public string BLAKE3 { get; set; }
    }

    public class CivitaiModelVersionImageDto : CivitaiBaseModelVersionImageDto
    {
        public string GenerationProcess { get; set; }
        public bool NeedsReview { get; set; }
        public List<CivitaiModelVersionImageTagDto> Tags { get; set; }
    }

    public class CivitaiModelVersionImageTagDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsCategory { get; set; }
    }
}
