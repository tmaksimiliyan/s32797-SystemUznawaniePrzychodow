namespace SystemUznawaniaPrzychodow.Models;

public class Subscription
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public int SoftwareId { get; set; }
    public Software Software { get; set; } = null!;
    public int RenewalPeriodMonths { get; set; }
    public decimal PricePerRenewal { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<SubscriptionPayment> Payments { get; set; } = new List<SubscriptionPayment>();
}
