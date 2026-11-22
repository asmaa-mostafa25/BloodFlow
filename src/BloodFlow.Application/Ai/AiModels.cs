namespace BloodFlow.Application.Ai
{
    // Shapes of the BloodFlow AI service (FastAPI) requests and responses.
    // The AI service speaks snake_case JSON (expiring_in_3_days, predicted_consumption_14d);
    // the client maps it with AiJsonNamingPolicy. The frontend gets normal camelCase from our API.
    // Dates come back as yyyy-MM-dd without a time, so they are DateOnly.

    public record AiHealth(
        string Status,
        DateOnly Today,
        string DataSource,
        int ConsumptionRows,
        List<DateOnly> DateRange,
        Dictionary<string, string> Methods);

    public record AiStock(DateOnly Today, Dictionary<string, double> Stock);

    public record ForecastPoint(DateOnly Date, double Predicted, double Lower, double Upper);

    /// <summary>Optional current stock per blood type. Types left out use the stock the AI computes from batches.</summary>
    public record StockOverride(Dictionary<string, double>? Stocks);

    public record ShortageDailyPoint(DateOnly Date, double PredictedConsumption, double PredictedStock);

    public record ShortagePrediction(
        string BloodType,
        double CurrentStock,
        int SafetyThreshold,
        double DaysOfSupply,
        double PredictedConsumption14d,
        int? ShortageInDays,
        int? WorstCaseShortageInDays,
        double ExpectedDeficitUnits,
        string AlertLevel,
        string ForecastMethod,
        List<ShortageDailyPoint> Daily);

    public record ExpiryPrediction(
        string BloodType,
        double AvailableUnits,
        double ExpiringIn3Days,
        double ExpiringIn7Days,
        double ExpectedWasteUnits,
        double WasteIfLowDemandUnits,
        double WasteRiskPercent,
        string RiskLevel);

    public record SubstitutionSuggestion(string From, double Units, double NearExpiryUnits);

    public record Substitution(
        string Recipient,
        double UnitsNeeded,
        List<SubstitutionSuggestion> Suggestions,
        double StillUncovered,
        string Action);

    public record NearExpiryItem(string BloodType, double UnitsExpiring7d, List<string> UseFor, string Action);

    public record SubstitutionsResult(List<Substitution> Substitutions, List<NearExpiryItem> NearExpiry);

    public record HistoryPoint(DateOnly Date, double UnitsConsumed);

    public record DemandRequest(string BloodType, int DaysAhead, List<HistoryPoint> History);

    public record DemandForecast(string BloodType, int HistoryDaysUsed, string Method, List<ForecastPoint> Forecast);

    public record ConsumptionRow(DateOnly Date, string BloodType, double UnitsConsumed);

    public record DonationRow(DateOnly Date, string BloodType, double UnitsDonated);

    public record BatchRow(
        string BatchId,
        string BloodType,
        double Units,
        double? UnitsRemaining,
        DateOnly CollectionDate,
        DateOnly? ExpiryDate);

    /// <summary>Mode is "replace" (default) or "append".</summary>
    public record RetrainRequest(
        List<ConsumptionRow> Consumption,
        List<DonationRow>? Donations = null,
        List<BatchRow>? Batches = null,
        string Mode = "append");

    public record RetrainResult(
        string Status,
        string Mode,
        DateOnly Today,
        int ConsumptionRows,
        List<DateOnly> DateRange,
        Dictionary<string, string> Methods,
        List<string> Warnings);
}
