namespace Ziklo.Application;

public sealed record ReportTable(string Kind, string Title, DateOnly From,
    DateOnly To, string[] Headers, int[] NumericColumns,
    IReadOnlyList<string[]> Rows, string Note);

public interface IReportCatalogService
{
    Task<ReportTable> GetAsync(Guid companyId, string kind, DateOnly from,
        DateOnly to, CancellationToken ct);
}
