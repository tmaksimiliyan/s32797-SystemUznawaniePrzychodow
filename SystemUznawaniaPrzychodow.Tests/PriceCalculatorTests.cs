using SystemUznawaniaPrzychodow.Logic;
using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Tests;

public class PriceCalculatorTests
{
    [Fact]
    public void BaseContractPrice_NoExtraYears_ReturnsYearlyPrice()
    {
        var result = PriceCalculator.CalculateBaseContractPrice(5000m, 0);
        Assert.Equal(5000m, result);
    }

    [Theory]
    [InlineData(1, 6000)]
    [InlineData(2, 7000)]
    [InlineData(3, 8000)]
    public void BaseContractPrice_ExtraYears_AddsThousandPerYear(int extraYears, decimal expected)
    {
        var result = PriceCalculator.CalculateBaseContractPrice(5000m, extraYears);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FinalContractPrice_NoDiscountNoLoyalty_ReturnBasePrice()
    {
        var result = PriceCalculator.CalculateFinalContractPrice(5000m, null, false);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void FinalContractPrice_WithDiscount_AppliesDiscount()
    {
        var discount = MakeDiscount(DiscountType.Contract, 10m);
        var result = PriceCalculator.CalculateFinalContractPrice(5000m, discount, false);
        Assert.Equal(4500m, result);
    }

    [Fact]
    public void FinalContractPrice_LoyalClient_AppliesFivePercent()
    {
        var result = PriceCalculator.CalculateFinalContractPrice(5000m, null, true);
        Assert.Equal(4750m, result);
    }

    [Fact]
    public void FinalContractPrice_DiscountAndLoyalty_AppliesBoth()
    {
        var discount = MakeDiscount(DiscountType.Contract, 10m);
        var result = PriceCalculator.CalculateFinalContractPrice(5000m, discount, true);
        Assert.Equal(4275m, result);
    }

    [Fact]
    public void GetBestContractDiscount_MultipleActive_ReturnsHighest()
    {
        var today = DateTime.Today;
        var discounts = new List<Discount>
        {
            MakeDiscount(DiscountType.Contract, 5m, today.AddDays(-1), today.AddDays(1)),
            MakeDiscount(DiscountType.Contract, 15m, today.AddDays(-1), today.AddDays(1)),
            MakeDiscount(DiscountType.Contract, 10m, today.AddDays(-1), today.AddDays(1))
        };

        var best = PriceCalculator.GetBestContractDiscount(discounts, today);
        Assert.Equal(15m, best!.Value);
    }

    [Fact]
    public void GetBestContractDiscount_NoActiveDiscount_ReturnsNull()
    {
        var today = DateTime.Today;
        var discounts = new List<Discount>
        {
            MakeDiscount(DiscountType.Contract, 10m, today.AddDays(-10), today.AddDays(-1))
        };

        var best = PriceCalculator.GetBestContractDiscount(discounts, today);
        Assert.Null(best);
    }

    [Fact]
    public void GetBestContractDiscount_IgnoresSubscriptionDiscounts()
    {
        var today = DateTime.Today;
        var discounts = new List<Discount>
        {
            MakeDiscount(DiscountType.Subscription, 20m, today.AddDays(-1), today.AddDays(1))
        };

        var best = PriceCalculator.GetBestContractDiscount(discounts, today);
        Assert.Null(best);
    }

    [Fact]
    public void FirstPeriodPrice_NoDiscountNoLoyalty_ReturnsMonthlyFraction()
    {
        var result = PriceCalculator.CalculateFirstPeriodSubscriptionPrice(1200m, 1, null, false);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void FirstPeriodPrice_WithPromoDiscount_AppliesDiscount()
    {
        var promo = MakeDiscount(DiscountType.Subscription, 10m);
        var result = PriceCalculator.CalculateFirstPeriodSubscriptionPrice(1200m, 1, promo, false);
        Assert.Equal(90m, result);
    }

    [Fact]
    public void FirstPeriodPrice_LoyalClient_AppliesFivePercent()
    {
        var result = PriceCalculator.CalculateFirstPeriodSubscriptionPrice(1200m, 1, null, true);
        Assert.Equal(95m, result);
    }

    [Fact]
    public void FirstPeriodPrice_PromoAndLoyalty_AppliesBoth()
    {
        var promo = MakeDiscount(DiscountType.Subscription, 10m);
        var result = PriceCalculator.CalculateFirstPeriodSubscriptionPrice(1200m, 1, promo, true);
        Assert.Equal(85.5m, result);
    }

    [Fact]
    public void RenewalPrice_LoyalClient_AppliesFivePercent()
    {
        var result = PriceCalculator.CalculateRenewalPrice(1200m, 1, true);
        Assert.Equal(95m, result);
    }

    [Fact]
    public void RenewalPrice_NotLoyal_ReturnsFullPrice()
    {
        var result = PriceCalculator.CalculateRenewalPrice(1200m, 1, false);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void RenewalPrice_ThreeMonths_ReturnsQuarterlyFraction()
    {
        var result = PriceCalculator.CalculateRenewalPrice(1200m, 3, false);
        Assert.Equal(300m, result);
    }

    private static Discount MakeDiscount(DiscountType type, decimal value, DateTime? from = null, DateTime? to = null) => new()
    {
        DiscountType = type,
        Value = value,
        DateFrom = from ?? DateTime.Today.AddDays(-1),
        DateTo = to ?? DateTime.Today.AddDays(1),
        Name = "Test",
        SoftwareId = 1
    };
}
