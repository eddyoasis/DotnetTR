using TradingLimitMVC.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TradingLimitMVC.Services
{
    public interface IPRPDFService
    {
        Task<byte[]> GeneratePRPdfAsync(PurchaseRequisition pr);
        Task<string> SavePRPdfAsync(PurchaseRequisition pr);
    }
    public class PRPDFService : IPRPDFService
    {
        private readonly ILogger<PRPDFService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly string _pdfDirectory;
        private readonly string _logoPath;
        public PRPDFService(ILogger<PRPDFService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
            _pdfDirectory = Path.Combine(_environment.WebRootPath, "generated-pdfs");
            _logoPath = Path.Combine(_environment.WebRootPath, "images", "KgiLogo.png");
            if (!Directory.Exists(_pdfDirectory))
                Directory.CreateDirectory(_pdfDirectory);
            QuestPDF.Settings.License = LicenseType.Community;
        }
        public async Task<byte[]> GeneratePRPdfAsync(PurchaseRequisition pr)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));
                        page.Header().Column(column =>
                        {
                            ComposeHeader(column, pr);
                        });
                        page.Content().Column(column =>
                        {
                            ComposeContent(column, pr);
                        });
                        page.Footer().AlignCenter().Text($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm:ss}").FontSize(8);
                    });
                });
                return document.GeneratePdf();
            });
        }
        private void ComposeHeader(ColumnDescriptor column, PurchaseRequisition pr)
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    if (File.Exists(_logoPath))
                    {
                        col.Item().Image(_logoPath).FitWidth();
                    }
                    else
                    {
                        col.Item().Text("KGI").FontSize(24).Bold();
                    }
                });
                row.RelativeItem().AlignRight().Text($"Date: {DateTime.Now:dd MMM yyyy}");
            });
            column.Item().PaddingTop(10).Text("Purchase Requisition").FontSize(20).Bold();
        }
        private void ComposeContent(ColumnDescriptor column, PurchaseRequisition pr)
        {
            // PR Reference Section
            column.Item().Element(c => ComposePRReference(c, pr));
            // Request Details
            column.Item().PaddingTop(15).Element(c => ComposeRequestDetails(c, pr));
            // Cost Centers
            if (pr.CostCenters?.Any() == true)
            {
                column.Item().PaddingTop(15).Element(c => ComposeCostCenters(c, pr));
            }
            // Purchase Items
            if (pr.Items?.Any() == true)
            {
                column.Item().PaddingTop(15).Element(c => ComposePurchaseItems(c, pr));
            }
            // Approval Workflow
            if (pr.WorkflowSteps?.Any() == true)
            {
                column.Item().PaddingTop(15).Element(c => ComposeApprovalWorkflow(c, pr));
            }
        }
        private void ComposePRReference(IContainer container, PurchaseRequisition pr)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("PR No:").Bold();
                table.Cell().Padding(5).Text(pr.PRReference ?? "N/A");
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Date:").Bold();
                table.Cell().Padding(5).Text(pr.CreatedDate.ToString("dd MMM yyyy"));
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("PR Internal No:").Bold();
                table.Cell().Padding(5).Text(pr.PRInternalNo ?? "N/A");
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Status:").Bold();
                table.Cell().Padding(5).Text(pr.StatusDisplayName);
            });
        }
        private void ComposeRequestDetails(IContainer container, PurchaseRequisition pr)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Grey.Lighten4).Padding(8)
                    .Text("Request Details").FontSize(13).Bold();
                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                    });
                    AddDetailRow(table, "Company:", pr.Company, "Department:", pr.Department);
                    AddDetailRow(table, "Type:", pr.TypeOfPurchase ?? "N/A", "Currency:", pr.QuotationCurrency ?? "USD");
                    AddDetailRow(table, "Total Amount:", $"{pr.QuotationCurrency ?? "USD"} {pr.TotalAmount:N2}",
                                "Status:", pr.StatusDisplayName);
                    AddDetailRow(table, "Submitted By:", pr.SubmittedBy ?? "N/A",
                                "Submitted Date:", pr.SubmittedDate?.ToString("dd MMM yyyy") ?? "N/A");
                });
                if (!string.IsNullOrEmpty(pr.ShortDescription))
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Description:").Bold();
                        col.Item().Text(pr.ShortDescription);
                    });
                }
                if (!string.IsNullOrEmpty(pr.Reason))
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Reason:").Bold();
                        col.Item().Text(pr.Reason);
                    });
                }
            });
        }
        private void ComposeCostCenters(IContainer container, PurchaseRequisition pr)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Grey.Lighten4).Padding(8)
                    .Text("Cost Center Distribution").FontSize(13).Bold();
                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(0.5f);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("No").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Cost Center").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Amount").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Percentage").FontColor(Colors.White).Bold().FontSize(9);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Approver").FontColor(Colors.White).Bold().FontSize(9);
                    });
                    int index = 1;
                    foreach (var cc in pr.CostCenters)
                    {
                        var percentage = pr.TotalAmount > 0
                            ? ((cc.Amount ?? 0) / pr.TotalAmount * 100).ToString("F2")
                            : "0.00";
                        table.Cell().Padding(4).Text(index.ToString()).FontSize(8);
                        table.Cell().Padding(4).Text(cc.Name).FontSize(8);
                        table.Cell().Padding(4).Text($"{pr.QuotationCurrency ?? "USD"} {cc.Amount:N2}").FontSize(8);
                        table.Cell().Padding(4).Text($"{percentage}%").FontSize(8);
                        table.Cell().Padding(4).Text($"{cc.Approver}\n{cc.ApproverEmail}").FontSize(8);
                        index++;
                    }
                    table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Padding(5).Text("Total").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                        .Text($"{pr.QuotationCurrency ?? "USD"} {pr.TotalAmount:N2}").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("100.00%").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("");
                });
            });
        }
        private void ComposePurchaseItems(IContainer container, PurchaseRequisition pr)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Grey.Lighten4).Padding(8)
                    .Text("Purchase Items").FontSize(13).Bold();
                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(0.5f);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(0.6f);
                        columns.RelativeColumn(0.6f);
                        columns.RelativeColumn(0.5f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(1.2f);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("No").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Description").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Qty").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Unit Price").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Disc%").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Disc$").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("GST").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Amount").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Supplier").FontColor(Colors.White).Bold().FontSize(8);
                    });
                    int itemNo = 1;
                    foreach (var item in pr.Items)
                    {
                        // Get GST value correctly from item, default to "0%" if null/empty
                        string gstDisplay = string.IsNullOrEmpty(item.GST) ? "0%" : item.GST;

                        // Ensure GST has % symbol
                        if (!gstDisplay.EndsWith("%"))
                        {
                            gstDisplay += "%";
                        }

                        table.Cell().Padding(3).Text(itemNo.ToString()).FontSize(7);
                        table.Cell().Padding(3).Text(item.Description ?? "N/A").FontSize(7);
                        table.Cell().Padding(3).Text(item.Quantity.ToString()).FontSize(7);
                        table.Cell().Padding(3).Text($"{item.UnitPrice:N2}").FontSize(7);
                        table.Cell().Padding(3).Text($"{item.DiscountPercent:N2}%").FontSize(7);
                        table.Cell().Padding(3).Text($"{item.DiscountAmount:N2}").FontSize(7);
                        table.Cell().Padding(3).Text(gstDisplay).FontSize(7);
                        table.Cell().Padding(3).Text($"{item.Amount:N2}").FontSize(7);
                        table.Cell().Padding(3).Text(item.SuggestedSupplier ?? "N/A").FontSize(7);
                        itemNo++;
                    }
                    var subtotal = pr.Items.Sum(i => i.Amount);
                    var gstTotal = pr.Items.Sum(i =>
                    {
                        // Parse GST percentage from item
                        var gstString = i.GST ?? "0%";
                        var gstRate = decimal.TryParse(gstString.Replace("%", ""), out var rate) ? rate : 0;
                        return i.Amount * gstRate / 100;
                    });
                    table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten3).Padding(5)
                        .AlignRight().Text("Subtotal:").Bold();
                    table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Padding(5)
                        .Text($"{pr.QuotationCurrency ?? "USD"} {subtotal:N2}").Bold();
                    table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten3).Padding(5)
                        .AlignRight().Text("GST Total:").Bold();
                    table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Padding(5)
                        .Text($"{pr.QuotationCurrency ?? "USD"} {gstTotal:N2}").Bold();
                    table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten3).Padding(5)
                        .AlignRight().Text("Total Amount:").Bold();
                    table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Padding(5)
                        .Text($"{pr.QuotationCurrency ?? "USD"} {subtotal + gstTotal:N2}").Bold();
                });
            });
        }
        private void ComposeApprovalWorkflow(IContainer container, PurchaseRequisition pr)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Grey.Lighten4).Padding(8)
                    .Text("Approval Workflow").FontSize(13).Bold();
                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(0.5f);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(0.6f);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Step").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Role").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Approver").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Department").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Date/Time").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Status").FontColor(Colors.White).Bold().FontSize(8);
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Action").FontColor(Colors.White).Bold().FontSize(8);
                    });
                    foreach (var step in pr.WorkflowSteps.OrderBy(s => s.StepOrder))
                    {
                        table.Cell().Padding(4).Text(step.StepOrder.ToString()).FontSize(8);
                        table.Cell().Padding(4).Text(step.ApproverRole).FontSize(8);
                        table.Cell().Padding(4).Text($"{step.ApproverName}\n{step.ApproverEmail}").FontSize(7);
                        table.Cell().Padding(4).Text(step.Department ?? "N/A").FontSize(8);
                        table.Cell().Padding(4).Text(step.ActionDate?.ToString("dd MMM yyyy\nHH:mm") ?? "-").FontSize(7);
                        var statusBg = step.Status == ApprovalStatus.Approved ? Colors.Green.Lighten3 :
                                      step.Status == ApprovalStatus.Rejected ? Colors.Red.Lighten3 :
                                      step.Status == ApprovalStatus.Pending ? Colors.Yellow.Lighten3 : Colors.White;
                        table.Cell().Background(statusBg).Padding(4).Text(step.Status.ToString()).FontSize(8);
                        var icon = step.Status == ApprovalStatus.Approved ? "" :
                                  step.Status == ApprovalStatus.Rejected ? "?" :
                                  step.Status == ApprovalStatus.Pending ? "" : "-";
                        table.Cell().Padding(4).Text(icon).FontSize(8);
                    }
                });
                var comments = pr.WorkflowSteps.Where(s => !string.IsNullOrEmpty(s.Comments)).ToList();
                if (comments.Any())
                {
                    column.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text("Approval Comments:").Bold().FontSize(11);
                        foreach (var step in comments)
                        {
                            col.Item().PaddingLeft(15).PaddingTop(5)
                                .Text($"• {step.ApproverName} ({step.ApproverRole}): {step.Comments}")
                                .FontSize(9);
                        }
                    });
                }
            });
        }
        private void AddDetailRow(TableDescriptor table, string label1, string value1, string label2, string value2)
        {
            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label1).Bold();
            table.Cell().Padding(5).Text(value1);
            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label2).Bold();
            table.Cell().Padding(5).Text(value2);
        }
        public async Task<string> SavePRPdfAsync(PurchaseRequisition pr)
        {
            var fileName = $"PR_{pr.PRReference}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_pdfDirectory, fileName);
            var pdfBytes = await GeneratePRPdfAsync(pr);
            await File.WriteAllBytesAsync(filePath, pdfBytes);
            return filePath;
        }
    }
}
