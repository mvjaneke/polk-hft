namespace POLK_DOTNET.Data
{
    public class CommitteeMember
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public int Order { get; set; } // To define the display order of committee members
    }
}
