using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Previews the Fund-In transaction
/// Step 2: Transaction Preview
/// </summary>
public class PreviewExecutor(FundInService fundInService, ILogger<PreviewExecutor> logger)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.Preview",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.Preview", "1.0.0");

    private static readonly Counter<int> PreviewsCounter = Meter.CreateCounter<int>(
        "fundin_previews",
        "previews",
        "Number of Fund-In previews generated"
    );

    private static readonly Histogram<double> PreviewAmountHistogram =
        Meter.CreateHistogram<double>(
            "fundin_preview_amount",
            "SAR",
            "Preview transaction amounts"
        );

    /// <summary>
    /// Execute preview of Fund-In transaction
    /// </summary>
    public async Task<PreviewResult> ExecuteAsync(
        FundInWorkflowRequest request,
        AccountsContext accountsContext,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundInPreview");
        activity?.SetTag("cif", request.Cif);
        activity?.SetTag("amount", request.Amount);
        activity?.SetTag("currency", request.Currency);

        try
        {
            // Validate sufficient funds
            if (!accountsContext.HasSufficientFunds)
            {
                logger.LogWarning(
                    "Insufficient funds: Required {Required}, Available {Available}",
                    request.Amount,
                    accountsContext.SelectedAccountBalance
                );

                return new PreviewResult
                {
                    Success = false,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ErrorMessage =
                        $"Insufficient funds. Required: {request.Amount:N2} {request.Currency}, "
                        + $"Available: {accountsContext.SelectedAccountBalance:N2} {request.Currency}",
                };
            }

            logger.LogInformation(
                "Previewing Fund-In: {Amount} {Currency} from {Account} to {Portfolio}",
                request.Amount,
                request.Currency,
                request.SourceAccountId,
                request.TargetPortfolioNumber
            );

            var previewRequest = new FundInPreviewRequest
            {
                SourceAccountId = request.SourceAccountId,
                TargetPortfolioNumber = request.TargetPortfolioNumber,
                Amount = request.Amount,
            };

            var response = await fundInService.PreviewFundInAsync(
                request.Cif,
                previewRequest,
                accessToken
            );

            if (!response.Success)
            {
                logger.LogWarning(
                    "Preview failed: {Message} (Code: {Code})",
                    response.Message,
                    response.ErrorCode
                );

                return new PreviewResult
                {
                    Success = false,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ErrorMessage = response.Message ?? "Preview failed",
                };
            }

            var data = response.Data!;

            PreviewsCounter.Add(1);
            PreviewAmountHistogram.Record((double)request.Amount);

            activity?.SetTag("transaction_id", data.TransactionId);
            activity?.SetTag("fees", data.Fees);
            activity?.SetTag("total_amount", data.TotalAmount);

            logger.LogInformation(
                "Preview successful: TransactionId={TransactionId}, Fees={Fees}, Total={Total}, Portfolio={Portfolio}",
                data.TransactionId,
                data.Fees,
                data.TotalAmount,
                data.TargetPortfolioName
            );

            return new PreviewResult
            {
                Success = true,
                TransactionId = data.TransactionId,
                Amount = data.Amount,
                Currency = data.Currency ?? request.Currency,
                Fees = data.Fees,
                TotalAmount = data.TotalAmount,
                FundName = data.TargetPortfolioName, // Use portfolio name as fund name
                EstimatedUnits = null, // Not provided by API
                CurrentNav = null, // Not provided by API
                ExpiresAt = null, // Not provided by API
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Preview execution failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
