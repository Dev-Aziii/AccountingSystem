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
        public byte[] GenerateInvoicePdf(InvoiceDTO invoice, CompanyDTO company, CustomerDTO customer)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    // Header
                    page.Header().Row(row =>
                    {
                        // Company Info (Left)
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(company.Name).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text(company.Address ?? "No Address");
                            col.Item().Text($"TIN: {company.TaxId ?? "N/A"}").FontColor(Colors.Grey.Darken1);
                            col.Item().Text(company.Currency).FontColor(Colors.Grey.Darken1);
                        });

                        // Invoice Label (Right)
                        row.ConstantItem(200).Column(col =>
                        {
                            col.Item().AlignRight().Text("INVOICE").FontSize(24).ExtraBold().FontColor(Colors.Grey.Lighten2);
                            col.Item().AlignRight().Text($"#{invoice.Id}").FontSize(14).SemiBold();
                            col.Item().AlignRight().Text($"Date: {invoice.DueDate:MMM dd, yyyy}");

                            var statusColor = invoice.Status == DocumentStatus.Paid ? Colors.Green.Medium : Colors.Red.Medium;
                            var statusText = invoice.Status == DocumentStatus.Paid ? "PAID" : "DUE";
                            col.Item().AlignRight().Text(statusText).FontSize(16).Bold().FontColor(statusColor);
                        });
                    });

                    // Content
                    page.Content().PaddingVertical(30).Column(col =>
                    {
                        // Bill To Section
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Bill To:").FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                                c.Item().Text(customer.Name).FontSize(12);
                                c.Item().Text(customer.Email);
                                c.Item().Text(customer.Phone);
                            });
                        });

                        col.Item().Height(20);

                        // Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.ConstantColumn(120);
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("Description");
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Amount");

                                static IContainer HeaderStyle(IContainer container)
                                {
                                    return container
                                        .Background(Colors.Grey.Lighten4)
                                        .PaddingVertical(5)
                                        .PaddingHorizontal(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .DefaultTextStyle(x => x.SemiBold());
                                }
                            });

                            // Row
                            table.Cell().Element(CellStyle).Text(invoice.Description);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{invoice.TotalAmount:N2}");

                            static IContainer CellStyle(IContainer container)
                            {
                                return container.PaddingVertical(5).PaddingHorizontal(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten4);
                            }
                        });

                        col.Item().AlignRight().PaddingTop(10).Column(c =>
                        {
                            c.Item().Text($"Total: {invoice.TotalAmount:N2}").FontSize(14).Bold();
                            c.Item().Text($"Paid: {invoice.PaidAmount:N2}");
                            c.Item().Text($"Balance Due: {invoice.Balance:N2}").FontSize(12).Bold().FontColor(invoice.Balance > 0 ? Colors.Red.Medium : Colors.Black);
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generated by Accounting System | Page ");
                        x.CurrentPageNumber();
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateFinancialReportPdf(TrialBalanceDTO tb, List<AccountDTO> accounts, CompanyDTO company)
        {
            var accountTypes = accounts.ToDictionary(a => a.Code, a => a.Type);

            decimal GetNetBalance(AccountBalanceDTO a)
            {
                if (!accountTypes.ContainsKey(a.AccountCode)) return 0;
                var type = accountTypes[a.AccountCode];
                if (type == "Asset" || type == "Expense") return a.Debit - a.Credit;
                return a.Credit - a.Debit;
            }

            var revenue = tb.Accounts.Where(a => accountTypes[a.AccountCode] == "Revenue").ToList();
            var expense = tb.Accounts.Where(a => accountTypes[a.AccountCode] == "Expense").ToList();
            var assets = tb.Accounts.Where(a => accountTypes[a.AccountCode] == "Asset").ToList();
            var liabilities = tb.Accounts.Where(a => accountTypes[a.AccountCode] == "Liability").ToList();
            var equity = tb.Accounts.Where(a => accountTypes[a.AccountCode] == "Equity").ToList();

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
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text(company.Name).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().AlignCenter().Text("FINANCIAL STATEMENTS").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.1f);
                        col.Item().AlignCenter().Text($"As of {DateTime.Now:MMMM dd, yyyy}").FontSize(10).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
                    });

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        // Style Helper
                        IContainer SectionHeader(IContainer container) => container.PaddingTop(20).PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Grey.Medium);

                        // --- INCOME STATEMENT ---
                        col.Item().Element(SectionHeader).Text("Income Statement").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });

                            // Revenue
                            table.Cell().PaddingTop(10).Text("REVENUE").Bold().FontColor(Colors.Grey.Darken3);
                            table.Cell();
                            foreach (var item in revenue)
                            {
                                table.Cell().PaddingLeft(15).Text(item.AccountName);
                                table.Cell().AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().PaddingTop(5).Text("Total Revenue").SemiBold();
                            table.Cell().PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{totalRevenue:N2}").SemiBold();

                            // Expenses
                            table.Cell().PaddingTop(15).Text("OPERATING EXPENSES").Bold().FontColor(Colors.Grey.Darken3);
                            table.Cell();
                            foreach (var item in expense)
                            {
                                table.Cell().PaddingLeft(15).Text(item.AccountName);
                                table.Cell().AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().PaddingTop(5).Text("Total Expenses").SemiBold();
                            table.Cell().PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"({totalExpense:N2})").SemiBold();

                            // Net Income
                            table.Cell().PaddingTop(15).Text("NET INCOME").Bold().FontSize(12);
                            table.Cell().PaddingTop(15).BorderTop(1).BorderColor(Colors.Black).AlignRight().Text($"{netIncome:N2}").Bold().FontSize(12);
                            table.Cell(); // Empty filler
                            table.Cell().PaddingTop(2).BorderTop(1).BorderColor(Colors.Black).Height(2); // Double underline effect manually
                        });

                        col.Item().PageBreak();

                        // --- BALANCE SHEET ---
                        col.Item().Element(SectionHeader).Text("Balance Sheet").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });

                            // Assets
                            table.Cell().PaddingTop(10).Text("ASSETS").Bold().FontColor(Colors.Grey.Darken3);
                            table.Cell();
                            foreach (var item in assets)
                            {
                                table.Cell().PaddingLeft(15).Text(item.AccountName);
                                table.Cell().AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().PaddingTop(5).Text("TOTAL ASSETS").Bold();
                            table.Cell().PaddingTop(5).BorderTop(1).BorderColor(Colors.Black).AlignRight().Text($"{totalAssets:N2}").Bold();
                            table.Cell();
                            table.Cell().PaddingTop(2).BorderTop(1).BorderColor(Colors.Black).Height(2);

                            // Liabilities
                            table.Cell().PaddingTop(20).Text("LIABILITIES").Bold().FontColor(Colors.Grey.Darken3);
                            table.Cell();
                            foreach (var item in liabilities)
                            {
                                table.Cell().PaddingLeft(15).Text(item.AccountName);
                                table.Cell().AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().PaddingTop(5).Text("Total Liabilities").SemiBold();
                            table.Cell().PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{totalLiabilities:N2}").SemiBold();

                            // Equity
                            table.Cell().PaddingTop(15).Text("EQUITY").Bold().FontColor(Colors.Grey.Darken3);
                            table.Cell();
                            foreach (var item in equity)
                            {
                                table.Cell().PaddingLeft(15).Text(item.AccountName);
                                table.Cell().AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().PaddingLeft(15).Text("Net Income (Current Period)");
                            table.Cell().AlignRight().Text($"{netIncome:N2}");

                            table.Cell().PaddingTop(5).Text("Total Equity").SemiBold();
                            table.Cell().PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text($"{totalEquity:N2}").SemiBold();

                            // Total L+E
                            table.Cell().PaddingTop(15).Text("TOTAL LIABILITIES & EQUITY").Bold();
                            table.Cell().PaddingTop(15).BorderTop(1).BorderColor(Colors.Black).AlignRight().Text($"{totalLiabilities + totalEquity:N2}").Bold();
                            table.Cell();
                            table.Cell().PaddingTop(2).BorderTop(1).BorderColor(Colors.Black).Height(2);
                        });
                    });

                    page.Footer().AlignCenter().Text(x => {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}