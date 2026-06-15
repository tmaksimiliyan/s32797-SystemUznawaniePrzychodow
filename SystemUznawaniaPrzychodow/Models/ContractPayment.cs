namespace SystemUznawaniaPrzychodow.Models;

public class ContractPayment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
}
