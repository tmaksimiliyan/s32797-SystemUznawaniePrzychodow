namespace SystemUznawaniaPrzychodow.Models;

public class Software
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string CurrentVersion { get; set; } = null!;
    public string Category { get; set; } = null!;
    public decimal YearlyLicensePrice { get; set; }
    public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
