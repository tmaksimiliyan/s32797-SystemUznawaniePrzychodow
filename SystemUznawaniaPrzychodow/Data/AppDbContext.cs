using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<IndividualClient> IndividualClients => Set<IndividualClient>();
    public DbSet<CompanyClient> CompanyClients => Set<CompanyClient>();
    public DbSet<Software> Softwares => Set<Software>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractPayment> ContractPayments => Set<ContractPayment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>()
            .HasDiscriminator<string>("ClientType")
            .HasValue<IndividualClient>("Individual")
            .HasValue<CompanyClient>("Company");

        modelBuilder.Entity<IndividualClient>()
            .Property(c => c.FirstName).HasMaxLength(100);
        modelBuilder.Entity<IndividualClient>()
            .Property(c => c.LastName).HasMaxLength(100);
        modelBuilder.Entity<IndividualClient>()
            .Property(c => c.Pesel).HasMaxLength(11);
        modelBuilder.Entity<IndividualClient>()
            .HasIndex(c => c.Pesel).IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<CompanyClient>()
            .Property(c => c.CompanyName).HasMaxLength(200);
        modelBuilder.Entity<CompanyClient>()
            .Property(c => c.Krs).HasMaxLength(10);
        modelBuilder.Entity<CompanyClient>()
            .HasIndex(c => c.Krs).IsUnique();

        modelBuilder.Entity<Client>()
            .Property(c => c.Address).HasMaxLength(300).IsRequired();
        modelBuilder.Entity<Client>()
            .Property(c => c.Email).HasMaxLength(150).IsRequired();
        modelBuilder.Entity<Client>()
            .Property(c => c.Phone).HasMaxLength(20).IsRequired();

        modelBuilder.Entity<Software>()
            .Property(s => s.YearlyLicensePrice).HasPrecision(18, 2);
        modelBuilder.Entity<Discount>()
            .Property(d => d.Value).HasPrecision(5, 2);
        modelBuilder.Entity<Contract>()
            .Property(c => c.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Contract>()
            .Property(c => c.TotalPaid).HasPrecision(18, 2);
        modelBuilder.Entity<Subscription>()
            .Property(s => s.PricePerRenewal).HasPrecision(18, 2);
        modelBuilder.Entity<SubscriptionPayment>()
            .Property(p => p.Amount).HasPrecision(18, 2);

        modelBuilder.Entity<ContractPayment>()
            .Property(p => p.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<ContractPayment>()
            .HasOne(p => p.Contract)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ContractPayment>()
            .HasOne(p => p.Client)
            .WithMany()
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Employee>()
            .Property(e => e.Login).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.Login).IsUnique();
        modelBuilder.Entity<Employee>()
            .Property(e => e.PasswordHash).IsRequired();
    }
}
