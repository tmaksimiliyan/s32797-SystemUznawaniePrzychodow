using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Logic;

public static class PriceCalculator
{
    // Cena bazowa kontraktu: cena roczna + dodatkowe lata wsparcia * 1000 PLN
    public static decimal CalculateBaseContractPrice(decimal yearlyLicensePrice, int additionalSupportYears)
        => yearlyLicensePrice + additionalSupportYears * 1000m;

    // Zwraca najwyższą aktywną zniżkę na kontrakty w danym dniu (lub null jeśli brak)
    public static Discount? GetBestContractDiscount(IEnumerable<Discount> discounts, DateTime onDate)
        => discounts
            .Where(d => d.DiscountType == DiscountType.Contract
                     && d.DateFrom.Date <= onDate.Date
                     && d.DateTo.Date >= onDate.Date)
            .MaxBy(d => d.Value);

    // Zwraca najwyższą aktywną zniżkę na subskrypcje w danym dniu (lub null jeśli brak)
    public static Discount? GetBestSubscriptionDiscount(IEnumerable<Discount> discounts, DateTime onDate)
        => discounts
            .Where(d => d.DiscountType == DiscountType.Subscription
                     && d.DateFrom.Date <= onDate.Date
                     && d.DateTo.Date >= onDate.Date)
            .MaxBy(d => d.Value);

    // Finalna cena kontraktu: najpierw zniżka promocyjna, potem zniżka dla powracającego klienta
    public static decimal CalculateFinalContractPrice(
        decimal basePrice, Discount? bestDiscount, bool isReturningClient)
    {
        var price = basePrice;

        if (bestDiscount is not null)
        {
            price *= 1 - bestDiscount.Value / 100m;
        }

        if (isReturningClient)
        {
            price *= 0.95m;
        }

        return Math.Round(price, 2);
    }

    // Cena za pierwszy okres subskrypcji: zniżka promocyjna + zniżka lojalnościowa
    public static decimal CalculateFirstPeriodSubscriptionPrice(
        decimal yearlyLicensePrice, int renewalPeriodMonths, Discount? bestPromoDiscount, bool isLoyalClient)
    {
        var price = yearlyLicensePrice / 12m * renewalPeriodMonths;

        if (bestPromoDiscount is not null)
        {
            price *= 1 - bestPromoDiscount.Value / 100m;
        }

        if (isLoyalClient)
        {
            price *= 0.95m;
        }

        return Math.Round(price, 2);
    }

    // Cena za kolejne odnowienia: tylko zniżka lojalnościowa (bez promocji)
    public static decimal CalculateRenewalPrice(
        decimal yearlyLicensePrice, int renewalPeriodMonths, bool isLoyalClient)
    {
        var price = yearlyLicensePrice / 12m * renewalPeriodMonths;

        if (isLoyalClient)
        {
            price *= 0.95m;
        }

        return Math.Round(price, 2);
    }
}
