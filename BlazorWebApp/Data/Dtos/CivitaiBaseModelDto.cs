namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiBaseModelDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool Nsfw { get; set; }
        public CivitaiBaseModelCreatorDto Creator { get; set; }
    }

    public class CivitaiBaseModelCreatorDto
    {
        public string Username { get; set; }
        public string? Image { get; set; }
    }

    public class CivitaiBaseModelVersionDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DownloadUrl { get; set; }
        public List<string> TrainedWords { get; set; }
    }

    public class CivitaiBaseModelVersionFileDto
    {
        public double SizeKb { get; set; }
        public CivitaiBaseModelVersionMetadataDto Metadata { get; set; }
        public string PickleScanResult { get; set; }
        public string VirusScanResult { get; set; }
        public DateTime? ScannedAt { get; set; }
        public bool? Primary { get; set; }

        public CivitaiBaseModelVersionFileDto() { }
        public CivitaiBaseModelVersionFileDto(CivitaiModelVersionFileDto file)
        {
            SizeKb = file.SizeKb;
            Metadata = file.Metadata;
            PickleScanResult = file.PickleScanResult;
            VirusScanResult = file.VirusScanResult;
            ScannedAt = file.ScannedAt;
            Primary = file.Primary;
        }
    }

    public class CivitaiBaseModelVersionMetadataDto
    {
        public string Fp { get; set; }
        public string Size { get; set; }
        public string Format { get; set; }
    }

    //public class CivitaiBaseModelVersionImageDto
    //{
    //    public string Url { get; set; }
    //    public string Nsfw { get; set; }
    //    public int Width { get; set; }
    //    public int Height { get; set; }
    //    public string Hash { get; set; }
    //    public CivitaiImageMeta? Meta { get; set; }
    //    public object MetaObject { get; set; }

    //    public CivitaiBaseModelVersionImageDto() { }
    //    public CivitaiBaseModelVersionImageDto(CivitaiModelVersionImageDto image)
    //    {
    //        Url = image.Url;
    //        Nsfw = image.Nsfw;
    //        Width = image.Width;
    //        Height = image.Height;
    //        Hash = image.Hash;
    //        Meta = image.Meta;
    //    }
    //}

    public enum CivitaiScanResult { Pending, Success, Danger, Error }
}
