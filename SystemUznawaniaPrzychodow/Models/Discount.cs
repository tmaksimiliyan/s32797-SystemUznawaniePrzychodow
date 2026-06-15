namespace SystemUznawaniaPrzychodow.Models;

public enum DiscountType
{
    Contract,
    Subscription
}

public class Discount
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int SoftwareId { get; set; }
    public Software Software { get; set; } = null!;
}
