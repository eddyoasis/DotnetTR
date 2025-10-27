
using TradingLimitMVC.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace TradingLimitMVC.Services
{
    public interface IPDFService
    {
        Task<byte[]> GeneratePRPdfAsync(PurchaseRequisition pr);
        Task<byte[]> GeneratePOPdfAsync(PurchaseOrder po);
        Task<string> GenerateAndSavePRPdfAsync(PurchaseRequisition pr);
        Task<string> GenerateAndSavePOPdfAsync(PurchaseOrder po);
    }
    public class PDFService : IPDFService
    {
        private readonly ILogger<PDFService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly string _pdfDirectory;
        private readonly string _logoPath;
        public PDFService(ILogger<PDFService> logger, IWebHostEnvironment environment)
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
                        page.Header().Element(c => ComposeHeaderPR(c, pr));
                        page.Content().Element(c => ComposeContentPR(c, pr));
                        page.Footer().AlignCenter().Text($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm:ss}").FontSize(8);
                    });
                });
                return document.GeneratePdf();
            });
        }
        public async Task<byte[]> GeneratePOPdfAsync(PurchaseOrder po)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.Header().Element(c => ComposeHeaderPO(c, po));
                        page.Content().Element(c => ComposeContentPO(c, po));
                        // FOOTER WITH COMPANY INFO
                        page.Footer().Element(c => ComposeFooterPO(c, po));
                    });
                });
                return document.GeneratePdf();
            });
        }
        private void ComposeFooterPO(IContainer container, PurchaseOrder po)
        {
            container.PaddingTop(20).Row(row =>
            {
                // Left: Invoice Instructions
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Please send invoice with copy of this PO to :")
                        .FontSize(8).Italic();
                    col.Item().PaddingTop(5).Column(addressCol =>
                    {
                        addressCol.Item().Text("KGI Securities (Singapore) Pte.Ltd.").FontSize(8).Bold();
                        addressCol.Item().Text("4 Shenton Way").FontSize(8);
                        addressCol.Item().Text("#13-01 SGX Centre 2 Singapore 068807").FontSize(8);
                    });
                });
                // Right: Signature Section
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("For and on behalf of").FontSize(8).Italic();
                    col.Item().PaddingTop(5).Text("KGI Securities (Singapore) Pte.Ltd.")
                        .FontSize(8).Bold();
                });
            });
        }

        private void ComposeHeaderPR(IContainer container, PurchaseRequisition pr)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    if (File.Exists(_logoPath))
                    {
                        column.Item().Image(_logoPath).FitWidth();
                    }
                    else
                    {
                        column.Item().Text("KGI").FontSize(24).Bold();
                    }
                });
                row.RelativeItem().AlignRight().Text($"Date: {DateTime.Now:dd MMM yyyy}");
            });
            container.PaddingTop(10).Text("Purchase Requisition").FontSize(20).Bold();
        }

        private void ComposeContentPR(IContainer container, PurchaseRequisition pr)
        {
            container.Column(column =>
            {
                // PR Details
                column.Item().Table(table =>
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
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("PR Internal No (System):").Bold();
                    table.Cell().Padding(5).Text(pr.PRInternalNo ?? "N/A");
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Status:").Bold();
                    table.Cell().Padding(5).Text(pr.StatusDisplayName);
                });
                // Request Information
                column.Item().PaddingTop(15).Column(col =>
                {
                    col.Item().Background(Colors.Grey.Lighten4).Padding(8).Text("Request Information").FontSize(14).Bold();
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                        });
                        AddInfoRow(table, "Company:", pr.Company, "Department:", pr.Department);
                        AddInfoRow(table, "Submitted By:", pr.SubmittedBy ?? "N/A",
                                  "Submitted Date:", pr.SubmittedDate?.ToString("dd MMM yyyy") ?? "N/A");
                        AddInfoRow(table, "Currency:", pr.QuotationCurrency ?? "USD",
                                  "Total Amount:", $"{pr.QuotationCurrency ?? "USD"} {pr.TotalAmount:N2}");
                    });
                    if (!string.IsNullOrEmpty(pr.ShortDescription))
                    {
                        col.Item().PaddingTop(10).Text("Description:").Bold();
                        col.Item().Text(pr.ShortDescription);
                    }
                    if (!string.IsNullOrEmpty(pr.Reason))
                    {
                        col.Item().PaddingTop(10).Text("Reason:").Bold();
                        col.Item().Text(pr.Reason);
                    }
                });
                // Cost Centers
                if (pr.CostCenters?.Any() == true)
                {
                    column.Item().PaddingTop(15).Column(col =>
                    {
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8)
                            .Text("Cost Center Distribution").FontSize(14).Bold();
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("No").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Cost Center").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Amount").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Percentage").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Approver").FontColor(Colors.White).Bold();
                            });
                            int index = 1;
                            foreach (var cc in pr.CostCenters)
                            {
                                var percentage = pr.TotalAmount > 0
                                    ? ((cc.Amount ?? 0) / pr.TotalAmount * 100).ToString("F2")
                                    : "0.00";
                                table.Cell().Padding(4).Text(index.ToString());
                                table.Cell().Padding(4).Text(cc.Name);
                                table.Cell().Padding(4).Text($"{pr.QuotationCurrency ?? "USD"} {cc.Amount:N2}");
                                table.Cell().Padding(4).Text($"{percentage}%");
                                table.Cell().Padding(4).Text(cc.Approver);
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
                // Purchase Items
                if (pr.Items?.Any() == true)
                {
                    column.Item().PaddingTop(15).Column(col =>
                    {
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8)
                            .Text("Purchase Items").FontSize(14).Bold();
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(0.6f);
                                columns.RelativeColumn(0.8f);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Item").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Description").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Quantity").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Unit Price").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Discount").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("GST").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Amount").FontColor(Colors.White).Bold().FontSize(9);
                            });
                            int itemNo = 1;
                            foreach (var item in pr.Items)
                            {
                                table.Cell().Padding(4).Text(itemNo.ToString()).FontSize(8);
                                table.Cell().Padding(4).Text(item.Description ?? "N/A").FontSize(8);
                                table.Cell().Padding(4).Text(item.Quantity.ToString()).FontSize(8);
                                table.Cell().Padding(4).Text($"{item.UnitPrice:N2}").FontSize(8);

                                var discount = item.DiscountPercent > 0
                                    ? $"{item.DiscountPercent}%"
                                    : $"{item.DiscountAmount:N2}";
                                table.Cell().Padding(4).Text(discount).FontSize(8);

                                table.Cell().Padding(4).Text(item.GST ?? "0%").FontSize(8);
                                table.Cell().Padding(4).Text($"{item.Amount:N2}").FontSize(8);
                                itemNo++;
                            }
                            var subtotal = pr.Items.Sum(i => i.Amount);
                            var gstTotal = pr.Items.Sum(i => {
                                var gstRate = decimal.TryParse(i.GST?.Replace("%", ""), out var rate) ? rate : 0;
                                return i.Amount * gstRate / 100;
                            });
                            table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3).Padding(5)
                                .AlignRight().Text("Subtotal:").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                .Text($"{pr.QuotationCurrency ?? "USD"} {subtotal:N2}").Bold();
                            table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten3).Padding(5)
                                .AlignRight().Text("GST 9%:").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                .Text($"{pr.QuotationCurrency ?? "USD"} {gstTotal:N2}").Bold();
                            table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3).Padding(5)
                                .AlignRight().Text("Total Amount:").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                                .Text($"{pr.QuotationCurrency ?? "USD"} {subtotal + gstTotal:N2}").Bold();
                        });
                    });
                }
                // Approval Workflow
                if (pr.WorkflowSteps?.Any() == true)
                {
                    column.Item().PaddingTop(20).Column(col =>
                    {
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8)
                            .Text("Approval Workflow").FontSize(14).Bold();
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(0.6f);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Step").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Role").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Approver").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Status").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Date/Time").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Action").FontColor(Colors.White).Bold().FontSize(9);
                            });
                            foreach (var step in pr.WorkflowSteps.OrderBy(s => s.StepOrder))
                            {
                                table.Cell().Padding(4).Text(step.StepOrder.ToString()).FontSize(8);
                                table.Cell().Padding(4).Text(step.ApproverRole).FontSize(8);
                                table.Cell().Padding(4).Text($"{step.ApproverName}\n{step.ApproverEmail}").FontSize(7);

                                var statusBg = step.Status == ApprovalStatus.Approved ? Colors.Green.Lighten3 :
                                              step.Status == ApprovalStatus.Rejected ? Colors.Red.Lighten3 :
                                              step.Status == ApprovalStatus.Pending ? Colors.Yellow.Lighten3 : Colors.White;

                                table.Cell().Background(statusBg).Padding(4).Text(step.Status.ToString()).FontSize(8);
                                table.Cell().Padding(4).Text(step.ActionDate?.ToString("dd MMM yyyy\nHH:mm") ?? "-").FontSize(7);

                                var icon = step.Status == ApprovalStatus.Approved ? "" :
                                          step.Status == ApprovalStatus.Rejected ? "?" :
                                          step.Status == ApprovalStatus.Pending ? "" : "-";
                                table.Cell().Padding(4).Text(icon).FontSize(8);
                            }
                        });
                        var comments = pr.WorkflowSteps.Where(s => !string.IsNullOrEmpty(s.Comments)).ToList();
                        if (comments.Any())
                        {
                            col.Item().PaddingTop(15).Text("Comments:").Bold();
                            foreach (var step in comments)
                            {
                                col.Item().PaddingLeft(20).PaddingBottom(5)
                                    .Text($"{step.ApproverName} ({step.ApproverRole}): {step.Comments}")
                                    .FontSize(10);
                            }
                        }
                    });
                }
            });
        }

        private void ComposeContentPO(IContainer container, PurchaseOrder po)
        {
            var currency = po.PurchaseRequisition?.QuotationCurrency ?? po.Currency ?? "SGD";
            container.Column(column =>
            {
                // ============================================================
                // SECTION 1: VENDOR & DELIVERY ADDRESS (Bordered Box)
                // ============================================================
                column.Item().PaddingTop(15).Border(1).BorderColor(Colors.Black).Column(boxCol =>
                {
                    boxCol.Item().Row(row =>
                    {
                        // LEFT: VENDOR
                        row.RelativeItem().Padding(10).Column(vendorCol =>
                        {
                            vendorCol.Item().Row(r =>
                            {
                                r.AutoItem().Text("Vendor : ").FontSize(9).SemiBold();
                                r.AutoItem().Text(po.Vendor ?? "N/A").FontSize(9).SemiBold();
                            });
                            if (!string.IsNullOrEmpty(po.VendorAddress))
                            {
                                vendorCol.Item().PaddingTop(5).PaddingLeft(15)
                                    .Text(po.VendorAddress)
                                    .FontSize(8)
                                    .LineHeight(1.2f);
                            }
                            vendorCol.Item().PaddingTop(10).Column(contact =>
                            {
                                if (!string.IsNullOrEmpty(po.Attention))
                                {
                                    contact.Item().Row(r =>
                                    {
                                        r.ConstantItem(50).Text("Attn").FontSize(8);
                                        r.AutoItem().Text(": " + po.Attention).FontSize(8);
                                    });
                                }
                                if (!string.IsNullOrEmpty(po.PhoneNo))
                                {
                                    contact.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(50).Text("Tel No").FontSize(8);
                                        r.AutoItem().Text(": " + po.PhoneNo).FontSize(8);
                                    });
                                }
                                if (!string.IsNullOrEmpty(po.FaxNo))
                                {
                                    contact.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(50).Text("Fax No").FontSize(8);
                                        r.AutoItem().Text(": " + po.FaxNo).FontSize(8);
                                    });
                                }
                                if (!string.IsNullOrEmpty(po.Email))
                                {
                                    contact.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(50).Text("Email").FontSize(8);
                                        r.AutoItem().Text(": " + po.Email).FontSize(8);
                                    });
                                }
                            });
                        });
                        // RIGHT: DELIVERY ADDRESS
                        row.RelativeItem().Padding(10).Column(deliveryCol =>
                        {
                            deliveryCol.Item().Text("Delivery Address")
                                .FontSize(9).SemiBold();

                            if (!string.IsNullOrEmpty(po.DeliveryAddress))
                            {
                                deliveryCol.Item().PaddingTop(5).PaddingLeft(15)
                                    .Text(po.DeliveryAddress)
                                    .FontSize(8)
                                    .LineHeight(1.2f);
                            }
                            deliveryCol.Item().PaddingTop(10).Column(contact =>
                            {
                                // FIX: Use PR Contact Person
                                var contactPerson = po.PurchaseRequisition?.ContactPerson
                                    ?? po.POOriginator
                                    ?? "N/A";

                                contact.Item().Row(r =>
                                {
                                    r.ConstantItem(50).Text("Attn").FontSize(8);
                                    r.AutoItem().Text(": " + contactPerson).FontSize(8);
                                });

                                // FIX: Use PR Contact Phone
                                var contactPhone = po.PurchaseRequisition?.ContactPhoneNo ?? "";

                                if (!string.IsNullOrEmpty(contactPhone))
                                {
                                    contact.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(50).Text("Tel No").FontSize(8);
                                        r.AutoItem().Text(": " + contactPhone).FontSize(8);
                                    });
                                }
                            });
                        });
                    });
                    // ============================================================
                    // PAYMENT TERMS ROW (Inside the box, at bottom)
                    // ============================================================
                    boxCol.Item().BorderTop(1).BorderColor(Colors.Black)
                        .Padding(8).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Payment Terms (Days) :").FontSize(8).SemiBold();
                                col.Item().Text(po.PaymentTerms ?? "30 Days").FontSize(8);
                            });
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Due date :").FontSize(8).SemiBold();
                                col.Item().Text(po.DeliveryDate?.ToString("dd MMM yyyy") ?? "N/A").FontSize(8);
                            });
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("PO Originator :").FontSize(8).SemiBold();
                                // Prioritize actual user data over contact info
                                var poOriginator = po.PurchaseRequisition?.SubmittedBy
                                    ?? po.POOriginator
                                    ?? po.SubmittedBy
                                    ?? "N/A";
                                col.Item().Text(poOriginator).FontSize(8);
                            });
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Order Issued by :").FontSize(8).SemiBold();
                                // Use OrderIssuedBy or fallback to originator
                                var orderIssuedBy = po.OrderIssuedBy
                                    ?? po.PurchaseRequisition?.SubmittedBy
                                    ?? po.POOriginator
                                    ?? po.SubmittedBy
                                    ?? "N/A";
                                col.Item().Text(orderIssuedBy).FontSize(8);
                            });
                        }); 
                });
                // ============================================================
                // SECTION 2: ITEMS TABLE
                // ============================================================
                if (po.Items?.Any() == true)
                {
                    column.Item().PaddingTop(15).Border(1).BorderColor(Colors.Black)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);    // Item
                                columns.RelativeColumn(4);     // Description
                                columns.ConstantColumn(55);    // Quantity
                                columns.ConstantColumn(70);    // Price
                                columns.ConstantColumn(55);    // Discount
                                columns.ConstantColumn(75);    // Amount
                            });
                            // Table Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3)
                                    .Padding(5).AlignCenter().Text("Item").FontSize(8).SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3)
                                    .Padding(5).Text("Description").FontSize(8).SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3)
                                    .Padding(5).AlignCenter().Text("Quantity").FontSize(8).SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3)
                                    .Padding(5).AlignRight().Text("Price").FontSize(8).SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3)
                                    .Padding(5).AlignCenter().Text("Discount").FontSize(8).SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3)
                                    .Padding(5).AlignRight().Text("Amount").FontSize(8).SemiBold();
                            });
                            // Table Body - Items
                            int itemNo = 1;
                            foreach (var item in po.Items)
                            {
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(5).AlignCenter().Text(itemNo.ToString()).FontSize(8);
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(5).Column(descCol =>
                                    {
                                        if (!string.IsNullOrEmpty(item.Description))
                                        {
                                            descCol.Item().Text(item.Description).FontSize(8);
                                        }
                                        if (!string.IsNullOrEmpty(item.Details) && item.Details != item.Description)
                                        {
                                            descCol.Item().PaddingTop(2)
                                                .Text(item.Details).FontSize(7).FontColor(Colors.Grey.Darken1);
                                        }
                                    });
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(5).AlignCenter().Text(item.Quantity.ToString()).FontSize(8);
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(5).AlignRight().Text(item.UnitPrice.ToString("N2")).FontSize(8);
                                var discountText = item.DiscountPercent > 0
                                    ? $"{item.DiscountPercent:N0}%"
                                    : item.DiscountAmount > 0
                                        ? item.DiscountAmount.ToString("N2")
                                        : "";
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(5).AlignCenter().Text(discountText).FontSize(8);
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(5).AlignRight().Text(item.Amount.ToString("N2")).FontSize(8);
                                itemNo++;
                            }
                            // ============================================================
                            // INTEGRATED TOTALS SECTION (GST + Total Amount)
                            // ============================================================
                            var subtotal = po.Items?.Sum(i => i.Amount) ?? 0;
                            var gstTotal = po.Items?.Sum(i =>
                            {
                                var gstRate = decimal.TryParse(i.GST?.Replace("%", ""), out var rate) ? rate : 0;
                                return i.Amount * gstRate / 100;
                            }) ?? 0;
                            var grandTotal = subtotal + gstTotal;
                            // Get GST percentage from first item
                            var gstPercent = "0";
                            var firstItem = po.Items?.FirstOrDefault();
                            if (firstItem != null && !string.IsNullOrEmpty(firstItem.GST))
                            {
                                gstPercent = firstItem.GST.Replace("%", "").Trim();
                            }
                            // GST Row
                            table.Cell().ColumnSpan(5).BorderTop(1).BorderColor(Colors.Black)
                                .Background(Colors.Grey.Lighten4)
                                .Padding(5).AlignRight()
                                .Text($"GST {gstPercent}% : {currency}")
                                .FontSize(9).SemiBold();
                            table.Cell().BorderTop(1).BorderColor(Colors.Black)
                                .Background(Colors.Grey.Lighten4)
                                .Padding(5).AlignRight()
                                .Text(gstTotal.ToString("N2"))
                                .FontSize(9).SemiBold();
                            // Total Amount Row
                            table.Cell().ColumnSpan(5).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .Background(Colors.Grey.Lighten3)
                                .Padding(5).AlignRight()
                                .Text($"Total Amount : {currency}")
                                .FontSize(10).Bold();
                            table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .Background(Colors.Grey.Lighten3)
                                .Padding(5).AlignRight()
                                .Text(grandTotal.ToString("N2"))
                                .FontSize(10).Bold();
                        });
                }
                
            });
        }
        private void ComposeHeaderPO(IContainer container, PurchaseOrder po)
        {
            container.Column(column =>
            {
                // Row 1: Logo (Left) and Empty Space (Right)
                column.Item().Row(row =>
                {
                    // Logo Section (Left)
                    row.RelativeItem().Column(col =>
                    {
                        if (File.Exists(_logoPath))
                        {
                            col.Item().Height(60).Width(90).Image(_logoPath);
                        }
                        else
                        {
                            col.Item().Text("KGI").FontSize(20).Bold();
                        }
                    });
                    // Empty space on right
                    row.RelativeItem();
                });
                // Row 2: Title "Purchase Order" - LEFT ALIGNED
                column.Item().PaddingTop(10)
                    .Text("Purchase Order").FontSize(16).Bold();
                // Row 3: PO/PR Numbers (Left) and Date (Right) - ALIGNED
                column.Item().PaddingTop(8).Row(row =>
                {
                    // Left: PO & PR Numbers
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(50).Text("PO No :").FontSize(9);
                            r.AutoItem().Text(po.PONo ?? po.POReference ?? "N/A").FontSize(9);
                        });
                        col.Item().PaddingTop(3).Row(r =>
                        {
                            r.ConstantItem(50).Text("PR No :").FontSize(9);
                            r.AutoItem().Text(po.PRReference ?? "N/A").FontSize(9);
                        });
                    });
                    // Right: Date - ALIGNED WITH PO NO
                    row.RelativeItem().AlignRight().AlignTop().Row(r =>
                    {
                        r.AutoItem().Text("Date : ").FontSize(9);
                        r.AutoItem().Text(po.IssueDate.ToString("d MMM yyyy")).FontSize(9);
                    });
                });
            });
        }
        private void AddInfoRow(TableDescriptor table, string label1, string value1, string label2, string value2)
        {
            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label1).Bold();
            table.Cell().Padding(5).Text(value1);
            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label2).Bold();
            table.Cell().Padding(5).Text(value2);
        }
        public async Task<string> GenerateAndSavePRPdfAsync(PurchaseRequisition pr)
        {
            var fileName = $"PR_{pr.PRReference}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_pdfDirectory, fileName);
            var pdfBytes = await GeneratePRPdfAsync(pr);
            await File.WriteAllBytesAsync(filePath, pdfBytes);
            return filePath;
        }
        public async Task<string> GenerateAndSavePOPdfAsync(PurchaseOrder po)
        {
            var fileName = $"PO_{po.POReference}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_pdfDirectory, fileName);
            var pdfBytes = await GeneratePOPdfAsync(po);
            await File.WriteAllBytesAsync(filePath, pdfBytes);
            return filePath;
        }
    }
}
