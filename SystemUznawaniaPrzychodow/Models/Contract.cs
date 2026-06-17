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
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public int AdditionalSupportYears { get; set; } = 0;
    public bool IsSigned { get; set; } = false;
    public decimal TotalPaid { get; set; } = 0;

    public ICollection<ContractPayment> Payments { get; set; } = new List<ContractPayment>();
}
