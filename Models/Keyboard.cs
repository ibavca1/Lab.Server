namespace Lab.Server.Models
{
    public class Keyboard
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public string? Eui { get; set; }
        public string? Version { get; set; }
        public string? Type { get; set; }
        public string? Platform { get; set; }
        public string? Interface { get; set; }
        public string? Adv { get; set; }
        public bool Sound { get; set; }
        public bool Tamper { get; set; }
    }
}
