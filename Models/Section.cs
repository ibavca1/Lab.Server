namespace Lab.Server.Models
{
    public class Section
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public List<Zone>? Zones { get; set; }
    }
}
