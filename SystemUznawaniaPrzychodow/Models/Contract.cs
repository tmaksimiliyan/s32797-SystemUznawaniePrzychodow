namespace SystemUznawaniaPrzychodow.Models;

public class Contract
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public int SoftwareId { get; set; }
    public Software Software { get; set; } = null!;
    public string SoftwareVersion { get; set; } = null!;
    public DateTime StartDate { get; set; }
    // EndDate - StartDate musi wynosić od 3 do 30 dni
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    // 0 = 1 rok bazowy; 1/2/3 = dodatkowe lata wsparcia, +1000 PLN każdy
    public int AdditionalSupportYears { get; set; } = 0;
    public bool IsSigned { get; set; } = false;
    public decimal TotalPaid { get; set; } = 0;

    public ICollection<ContractPayment> Payments { get; set; } = new List<ContractPayment>();
}
