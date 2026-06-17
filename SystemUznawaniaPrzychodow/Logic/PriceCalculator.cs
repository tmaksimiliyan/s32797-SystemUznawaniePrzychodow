using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Logic;

public static class PriceCalculator
{
    public static decimal CalculateBaseContractPrice(decimal yearlyLicensePrice, int additionalSupportYears)
        => yearlyLicensePrice + additionalSupportYears * 1000m;

    public static Discount? GetBestContractDiscount(IEnumerable<Discount> discounts, DateTime onDate)
        => discounts
            .Where(d => d.DiscountType == DiscountType.Contract
                     && d.DateFrom.Date <= onDate.Date
                     && d.DateTo.Date >= onDate.Date)
            .MaxBy(d => d.Value);

    public static Discount? GetBestSubscriptionDiscount(IEnumerable<Discount> discounts, DateTime onDate)
        => discounts
            .Where(d => d.DiscountType == DiscountType.Subscription
                     && d.DateFrom.Date <= onDate.Date
                     && d.DateTo.Date >= onDate.Date)
            .MaxBy(d => d.Value);

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
