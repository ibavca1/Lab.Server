
namespace Lab.Server.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public string? Pin { get; set; }
        public string? Key { get; set; }
        public int Permission { get; set; }
    }
}

