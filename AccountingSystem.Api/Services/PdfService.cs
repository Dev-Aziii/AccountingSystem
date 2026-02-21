using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AccountingSystem.API.Services
{
    public class PdfService : IPdfService
    {
        // Color scheme for professional branding
        private static class BrandColors
        {
            public static string Primary = "#1e40af"; // Professional blue
            public static string Secondary = "#64748b"; // Slate gray
            public static string Success = "#16a34a"; // Green
            public static string Danger = "#dc2626"; // Red
            public static string Light = "#f8fafc"; // Very light gray
            public static string Dark = "#0f172a"; // Dark slate
        }

        public byte[] GenerateInvoicePdf(InvoiceDTO invoice, CompanyDTO company, CustomerDTO customer)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(BrandColors.Dark));

                    // Header
                    page.Header().Row(row =>
                    {
                        // Company Info (Left)
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(company.Name)
                                .FontSize(20)
                                .Bold()
                                .FontColor(BrandColors.Primary);

                            col.Item().Text(company.Address ?? "No Address")
                                .FontSize(10);

                            col.Item().Text($"TIN: {company.TaxId ?? "N/A"}")
                                .FontSize(10)
                                .FontColor(BrandColors.Secondary);

                            col.Item().Text(company.Currency)
                                .FontSize(10)
                                .FontColor(BrandColors.Secondary);
                        });

                        // Invoice Label (Right)
                        row.ConstantItem(200).Column(col =>
                        {
                            col.Item().AlignRight().Text("INVOICE")
                                .FontSize(24)
                                .ExtraBold()
                                .FontColor(BrandColors.Primary);

                            col.Item().AlignRight().Text($"#{invoice.Id}")
                                .FontSize(14)
                                .SemiBold();

                            col.Item().AlignRight().Text($"Date: {invoice.DueDate:MMM dd, yyyy}")
                                .FontSize(10);

                            var statusColor = invoice.Status == DocumentStatus.Paid
                                ? BrandColors.Success
                                : BrandColors.Danger;
                            var statusText = invoice.Status == DocumentStatus.Paid ? "PAID" : "DUE";

                            col.Item().AlignRight().Text(statusText)
                                .FontSize(16)
                                .Bold()
                                .FontColor(statusColor);
                        });
                    });

                    // Content with improved spacing
                    page.Content().PaddingVertical(25).Column(col =>
                    {
                        // Bill To Section - Enhanced card-like design
                        col.Item().Background(BrandColors.Light).Padding(15).Column(c =>
                        {
                            c.Item().Text("BILL TO")
                                .FontSize(10)
                                .Bold()
                                .FontColor(BrandColors.Secondary)
                                .LetterSpacing(0.5f);

                            c.Item().PaddingTop(8).Text(customer.Name)
                                .FontSize(14)
                                .Bold()
                                .FontColor(BrandColors.Dark);

                            c.Item().PaddingTop(3).Text(customer.Email)
                                .FontSize(10)
                                .FontColor(BrandColors.Secondary);

                            c.Item().PaddingTop(2).Text(customer.Phone)
                                .FontSize(10)
                                .FontColor(BrandColors.Secondary);
                        });

                        col.Item().Height(25);

                        // Items Table - Professional design
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Description
                                columns.ConstantColumn(140); // Amount
                            });

                            // Header with better styling
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("DESCRIPTION")
                                    .FontSize(10)
                                    .Bold()
                                    .LetterSpacing(0.3f);
                                header.Cell().Element(HeaderStyle).AlignRight().Text("AMOUNT")
                                    .FontSize(10)
                                    .Bold()
                                    .LetterSpacing(0.3f);

                                static IContainer HeaderStyle(IContainer container)
                                {
                                    return container
                                        .Background(BrandColors.Primary)
                                        .PaddingVertical(8)
                                        .PaddingHorizontal(12)
                                        .DefaultTextStyle(x => x.FontColor(Colors.White));
                                }
                            });

                            // Row with better padding
                            table.Cell().Element(CellStyle).Text(invoice.Description)
                                .FontSize(11);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{company.Currency} {invoice.TotalAmount:N2}")
                                .FontSize(11)
                                .SemiBold();

                            static IContainer CellStyle(IContainer container)
                            {
                                return container
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten3)
                                    .PaddingVertical(10)
                                    .PaddingHorizontal(12);
                            }
                        });

                        // Summary section with better visual hierarchy
                        col.Item().AlignRight().PaddingTop(20).Column(c =>
                        {
                            c.Item().Width(250).Column(summary =>
                            {
                                // Subtotal
                                summary.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Subtotal:")
                                        .FontSize(11)
                                        .FontColor(BrandColors.Secondary);
                                    r.ConstantItem(100).AlignRight().Text($"{company.Currency} {invoice.TotalAmount:N2}")
                                        .FontSize(11)
                                        .FontColor(BrandColors.Dark);
                                });

                                summary.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Text("Paid Amount:")
                                        .FontSize(11)
                                        .FontColor(BrandColors.Secondary);
                                    r.ConstantItem(100).AlignRight().Text($"{company.Currency} {invoice.PaidAmount:N2}")
                                        .FontSize(11)
                                        .FontColor(BrandColors.Success);
                                });

                                summary.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                                // Balance Due - Prominent
                                summary.Item().PaddingTop(8).Background(invoice.Balance > 0 ? "#fef2f2" : "#f0fdf4")
                                    .PaddingVertical(10)
                                    .PaddingHorizontal(10)
                                    .Row(r =>
                                    {
                                        r.RelativeItem().Text("Balance Due:")
                                            .FontSize(13)
                                            .Bold()
                                            .FontColor(BrandColors.Dark);
                                        r.ConstantItem(100).AlignRight().Text($"{company.Currency} {invoice.Balance:N2}")
                                            .FontSize(14)
                                            .ExtraBold()
                                            .FontColor(invoice.Balance > 0 ? BrandColors.Danger : BrandColors.Success);
                                    });
                            });
                        });

                        // Payment terms or notes section
                        col.Item().PaddingTop(30).Column(c =>
                        {
                            c.Item().Text("PAYMENT TERMS")
                                .FontSize(10)
                                .Bold()
                                .FontColor(BrandColors.Secondary)
                                .LetterSpacing(0.5f);

                            c.Item().PaddingTop(6).Text("Payment is due within 30 days. Please include invoice number with payment.")
                                .FontSize(9)
                                .FontColor(BrandColors.Secondary)
                                .LineHeight(1.4f);
                        });
                    });

                    // Footer with better styling
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(10).AlignCenter().Row(r =>
                        {
                            r.RelativeItem().AlignCenter().Text(text =>
                            {
                                text.Span("Generated by Accounting System | ")
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                                text.Span("Page ")
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                                text.CurrentPageNumber()
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateFinancialReportPdf(
            TrialBalanceDTO incomeTb,
            TrialBalanceDTO balanceTb,
            List<AccountDTO> accounts,
            CompanyDTO company,
            DateTime periodStart,
            DateTime periodEnd)
        {
            var accountTypes = accounts.ToDictionary(a => a.Code, a => a.Type);

            decimal GetNetBalance(AccountBalanceDTO a)
            {
                if (!accountTypes.ContainsKey(a.AccountCode)) return 0;
                var type = accountTypes[a.AccountCode];
                if (type == "Asset" || type == "Expense") return a.Debit - a.Credit;
                return a.Credit - a.Debit;
            }

            var revenue = incomeTb.Accounts.Where(a => accountTypes.ContainsKey(a.AccountCode) && accountTypes[a.AccountCode] == "Revenue").ToList();
            var expense = incomeTb.Accounts.Where(a => accountTypes.ContainsKey(a.AccountCode) && accountTypes[a.AccountCode] == "Expense").ToList();
            var assets = balanceTb.Accounts.Where(a => accountTypes.ContainsKey(a.AccountCode) && accountTypes[a.AccountCode] == "Asset").ToList();
            var liabilities = balanceTb.Accounts.Where(a => accountTypes.ContainsKey(a.AccountCode) && accountTypes[a.AccountCode] == "Liability").ToList();
            var equity = balanceTb.Accounts.Where(a => accountTypes.ContainsKey(a.AccountCode) && accountTypes[a.AccountCode] == "Equity").ToList();

            var totalRevenue = revenue.Sum(GetNetBalance);
            var totalExpense = expense.Sum(GetNetBalance);
            var netIncome = totalRevenue - totalExpense;
            var totalAssets = assets.Sum(GetNetBalance);
            var totalLiabilities = liabilities.Sum(GetNetBalance);
            var totalEquity = equity.Sum(GetNetBalance) + netIncome;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(BrandColors.Dark));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text(company.Name)
                            .FontSize(20)
                            .Bold()
                            .FontColor(BrandColors.Primary);

                        col.Item().AlignCenter().Text("FINANCIAL STATEMENTS")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(BrandColors.Secondary)
                            .LetterSpacing(0.1f);

                        col.Item().AlignCenter().Text($"Income Statement Period: {periodStart:MMM dd, yyyy} - {periodEnd:MMM dd, yyyy}")
                            .FontSize(10)
                            .FontColor(BrandColors.Secondary);

                        col.Item().AlignCenter().Text($"Balance Sheet As of {periodEnd:MMMM dd, yyyy}")
                            .FontSize(10)
                            .FontColor(BrandColors.Secondary);

                        col.Item().PaddingTop(10).LineHorizontal(2).LineColor(BrandColors.Primary);
                    });

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        // Section Header Style
                        IContainer SectionHeader(IContainer container) => container
                            .Background(BrandColors.Primary)
                            .PaddingVertical(6)
                            .PaddingHorizontal(10)
                            .DefaultTextStyle(x => x.FontColor(Colors.White));

                        IContainer SubsectionHeader(IContainer container) => container
                            .PaddingTop(12)
                            .PaddingBottom(4)
                            .BorderBottom(1)
                            .BorderColor(BrandColors.Secondary);

                        // --- INCOME STATEMENT ---
                        col.Item().Element(SectionHeader).Text("INCOME STATEMENT")
                            .FontSize(13)
                            .ExtraBold()
                            .LetterSpacing(0.8f);

                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.ConstantColumn(140);
                            });

                            // Revenue Section
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader)
                                .Text("REVENUE")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Dark);

                            foreach (var item in revenue)
                            {
                                table.Cell().PaddingLeft(20).PaddingVertical(4).Text(item.AccountName)
                                    .FontSize(10);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{company.Currency} {GetNetBalance(item):N2}")
                                    .FontSize(10);
                            }

                            // Total Revenue
                            table.Cell().PaddingTop(8).PaddingLeft(20).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .Text("Total Revenue")
                                .FontSize(11)
                                .Bold();
                            table.Cell().PaddingTop(8).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .AlignRight()
                                .Text($"{company.Currency} {totalRevenue:N2}")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Success);

                            // Expenses Section
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader)
                                .Text("OPERATING EXPENSES")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Dark);

                            foreach (var item in expense)
                            {
                                table.Cell().PaddingLeft(20).PaddingVertical(4).Text(item.AccountName)
                                    .FontSize(10);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{company.Currency} {GetNetBalance(item):N2}")
                                    .FontSize(10);
                            }

                            // Total Expenses
                            table.Cell().PaddingTop(8).PaddingLeft(20).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .Text("Total Expenses")
                                .FontSize(11)
                                .Bold();
                            table.Cell().PaddingTop(8).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .AlignRight()
                                .Text($"{company.Currency} ({totalExpense:N2})")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Danger);

                            // Net Income - Highlighted
                            table.Cell().ColumnSpan(2).PaddingTop(15);

                            table.Cell().Background(netIncome >= 0 ? "#f0fdf4" : "#fef2f2")
                                .Padding(10)
                                .Text("NET INCOME")
                                .FontSize(13)
                                .ExtraBold();
                            table.Cell().Background(netIncome >= 0 ? "#f0fdf4" : "#fef2f2")
                                .Padding(10)
                                .BorderTop(2)
                                .BorderBottom(2)
                                .BorderColor(BrandColors.Dark)
                                .AlignRight()
                                .Text($"{company.Currency} {netIncome:N2}")
                                .FontSize(13)
                                .ExtraBold()
                                .FontColor(netIncome >= 0 ? BrandColors.Success : BrandColors.Danger);
                        });

                        col.Item().PageBreak();

                        // --- BALANCE SHEET ---
                        col.Item().Element(SectionHeader).Text("BALANCE SHEET")
                            .FontSize(13)
                            .ExtraBold()
                            .LetterSpacing(0.8f);

                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.ConstantColumn(140);
                            });

                            // Assets Section
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader)
                                .Text("ASSETS")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Dark);

                            foreach (var item in assets)
                            {
                                table.Cell().PaddingLeft(20).PaddingVertical(4).Text(item.AccountName)
                                    .FontSize(10);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{company.Currency} {GetNetBalance(item):N2}")
                                    .FontSize(10);
                            }

                            // Total Assets - Emphasized
                            table.Cell().PaddingTop(10).PaddingLeft(20).Background(BrandColors.Light)
                                .PaddingVertical(6)
                                .PaddingHorizontal(8)
                                .BorderTop(2)
                                .BorderBottom(2)
                                .BorderColor(BrandColors.Dark)
                                .Text("TOTAL ASSETS")
                                .FontSize(12)
                                .ExtraBold();
                            table.Cell().PaddingTop(10).Background(BrandColors.Light)
                                .PaddingVertical(6)
                                .PaddingHorizontal(8)
                                .BorderTop(2)
                                .BorderBottom(2)
                                .BorderColor(BrandColors.Dark)
                                .AlignRight()
                                .Text($"{company.Currency} {totalAssets:N2}")
                                .FontSize(12)
                                .ExtraBold();

                            // Liabilities Section
                            table.Cell().ColumnSpan(2).PaddingTop(20).Element(SubsectionHeader)
                                .Text("LIABILITIES")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Dark);

                            foreach (var item in liabilities)
                            {
                                table.Cell().PaddingLeft(20).PaddingVertical(4).Text(item.AccountName)
                                    .FontSize(10);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{company.Currency} {GetNetBalance(item):N2}")
                                    .FontSize(10);
                            }

                            table.Cell().PaddingTop(8).PaddingLeft(20).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .Text("Total Liabilities")
                                .FontSize(11)
                                .Bold();
                            table.Cell().PaddingTop(8).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .AlignRight()
                                .Text($"{company.Currency} {totalLiabilities:N2}")
                                .FontSize(11)
                                .Bold();

                            // Equity Section
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader)
                                .Text("EQUITY")
                                .FontSize(11)
                                .Bold()
                                .FontColor(BrandColors.Dark);

                            foreach (var item in equity)
                            {
                                table.Cell().PaddingLeft(20).PaddingVertical(4).Text(item.AccountName)
                                    .FontSize(10);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{company.Currency} {GetNetBalance(item):N2}")
                                    .FontSize(10);
                            }

                            table.Cell().PaddingLeft(20).PaddingVertical(4).Text("Net Income (Current Period)")
                                .FontSize(10)
                                .Italic();
                            table.Cell().PaddingVertical(4).AlignRight().Text($"{company.Currency} {netIncome:N2}")
                                .FontSize(10)
                                .FontColor(netIncome >= 0 ? BrandColors.Success : BrandColors.Danger);

                            table.Cell().PaddingTop(8).PaddingLeft(20).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .Text("Total Equity")
                                .FontSize(11)
                                .Bold();
                            table.Cell().PaddingTop(8).BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                .AlignRight()
                                .Text($"{company.Currency} {totalEquity:N2}")
                                .FontSize(11)
                                .Bold();

                            // Total Liabilities & Equity - Emphasized
                            table.Cell().PaddingTop(12).PaddingLeft(20).Background(BrandColors.Light)
                                .PaddingVertical(6)
                                .PaddingHorizontal(8)
                                .BorderTop(2)
                                .BorderBottom(2)
                                .BorderColor(BrandColors.Dark)
                                .Text("TOTAL LIABILITIES & EQUITY")
                                .FontSize(12)
                                .ExtraBold();
                            table.Cell().PaddingTop(12).Background(BrandColors.Light)
                                .PaddingVertical(6)
                                .PaddingHorizontal(8)
                                .BorderTop(2)
                                .BorderBottom(2)
                                .BorderColor(BrandColors.Dark)
                                .AlignRight()
                                .Text($"{company.Currency} {totalLiabilities + totalEquity:N2}")
                                .FontSize(12)
                                .ExtraBold();

                            // Balance check indicator
                            var isBalanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity)) < 0.01m;
                            if (isBalanced)
                            {
                                table.Cell().ColumnSpan(2).PaddingTop(10).AlignCenter()
                                    .Background("#f0fdf4")
                                    .PaddingVertical(8)
                                    .PaddingHorizontal(8)
                                    .Text("✓ Balance Sheet is Balanced")
                                    .FontSize(10)
                                    .Bold()
                                    .FontColor(BrandColors.Success);
                            }
                        });
                    });

                    // Enhanced Footer
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(10).Row(r =>
                        {
                            r.RelativeItem().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                                .FontSize(8)
                                .FontColor(BrandColors.Secondary);

                            r.RelativeItem().AlignCenter().Text(text =>
                            {
                                text.Span("Page ")
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                                text.CurrentPageNumber()
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                                text.Span(" of ")
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                                text.TotalPages()
                                    .FontSize(8)
                                    .FontColor(BrandColors.Secondary);
                            });

                            r.RelativeItem().AlignRight().Text("Confidential")
                                .FontSize(8)
                                .FontColor(BrandColors.Secondary);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
