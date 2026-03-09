namespace POLK_DOTNET.Data
{
    public class GalleryAlbum
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Category { get; set; } = null!;
        public string? CoverImageFileName { get; set; }
        public DateTime EventDate { get; set; }
        public ICollection<GalleryImage> Images { get; set; } = new List<GalleryImage>();
    }
}
