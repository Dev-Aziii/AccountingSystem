using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccountingSystem.API.Services
{
    public class PdfService : IPdfService
    {
        // ====================================================================
        // THEME — single definition
        // ====================================================================
        private static class T
        {
            public const string HeaderBg = "#0f172a";
            public const string HeaderAccent = "#3b82f6";
            public const string HeaderText = "#ffffff";
            public const string HeaderSubtext = "#94a3b8";
            public const string Accent = "#2563eb";
            public const string AccentLight = "#dbeafe";
            public const string TextMain = "#1e293b";
            public const string TextMuted = "#64748b";
            public const string BorderLight = "#e2e8f0";
            public const string BorderDark = "#1e293b";
            public const string SurfaceBox = "#f8fafc";
            public const string SurfaceMid = "#f1f5f9";
        }

        // ====================================================================
        // INVOICE PDF
        // ====================================================================
        public byte[] GenerateInvoicePdf(InvoiceDTO invoice, CompanyDTO company, CustomerDTO customer)
        {
            string invoiceHeaderSvg = @"
                <svg viewBox=""0 0 595 110"" xmlns=""http://www.w3.org/2000/svg"" preserveAspectRatio=""none"">
                    <defs>
                        <linearGradient id=""hg"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""0%"">
                            <stop offset=""0%""   stop-color=""#0f172a""/>
                            <stop offset=""100%"" stop-color=""#1e3a8a""/>
                        </linearGradient>
                        <linearGradient id=""sg"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
                            <stop offset=""0%""   stop-color=""#3b82f6"" stop-opacity=""0.18""/>
                            <stop offset=""100%"" stop-color=""#3b82f6"" stop-opacity=""0""/>
                        </linearGradient>
                    </defs>
                    <rect width=""595"" height=""110"" fill=""url(#hg)""/>
                    <polygon points=""360,0 595,0 595,110 280,110"" fill=""url(#sg)""/>
                    <rect y=""106"" width=""595"" height=""4"" fill=""#3b82f6""/>
                </svg>";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(0);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.SegoeUI).FontColor(T.TextMain));

                    // --- HEADER (110pt, vertically centered via AlignMiddle) ---
                    page.Header().Height(110).Layers(layers =>
                    {
                        layers.Layer().Svg(invoiceHeaderSvg);

                        layers.PrimaryLayer()
                            .PaddingHorizontal(50)
                            .AlignMiddle()
                            .Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text(company.Name)
                                        .FontSize(22).ExtraBold()
                                        .FontColor(T.HeaderText).LetterSpacing(0.03f);

                                    col.Item().PaddingTop(4).Text(company.Address ?? string.Empty)
                                        .FontSize(9).FontColor(T.HeaderSubtext);
                                });

                                row.ConstantItem(160).AlignRight().AlignMiddle().Column(col =>
                                {
                                    col.Item().AlignRight().Text("INVOICE")
                                        .FontSize(22).ExtraBold()
                                        .LetterSpacing(0.12f).FontColor(T.HeaderText);

                                    col.Item().PaddingTop(4).AlignRight()
                                        .Background(T.HeaderAccent)
                                        .PaddingHorizontal(10).PaddingVertical(3)
                                        .Text($"#{invoice.Id:D4}")
                                        .FontSize(9).SemiBold().FontColor(T.HeaderText);
                                });
                            });
                    });

                    // --- CONTENT ---
                    page.Content().PaddingHorizontal(50).PaddingTop(36).PaddingBottom(20).Column(col =>
                    {
                        // 1. Date fields + status pill
                        col.Item().PaddingBottom(28).Row(row =>
                        {
                            row.AutoItem().PaddingRight(24).Column(c =>
                            {
                                c.Item().Text("ISSUE DATE").FontSize(7).Bold()
                                    .FontColor(T.TextMuted).LetterSpacing(0.08f);
                                c.Item().PaddingTop(2).Text($"{DateTime.Now:dd MMMM yyyy}")
                                    .FontSize(11).SemiBold().FontColor(T.TextMain);
                            });

                            row.AutoItem().Column(c =>
                            {
                                c.Item().Text("DUE DATE").FontSize(7).Bold()
                                    .FontColor(T.TextMuted).LetterSpacing(0.08f);
                                c.Item().PaddingTop(2).Text($"{invoice.DueDate:dd MMMM yyyy}")
                                    .FontSize(11).SemiBold().FontColor(T.TextMain);
                            });

                            row.RelativeItem();

                            var isPaid = invoice.Balance <= 0;
                            row.AutoItem().AlignBottom()
                                .Background(isPaid ? "#dcfce7" : T.AccentLight)
                                .PaddingHorizontal(14).PaddingVertical(5)
                                .Text(isPaid ? "PAID" : "OUTSTANDING")
                                .FontSize(8).Bold().LetterSpacing(0.08f)
                                .FontColor(isPaid ? "#16a34a" : T.Accent);
                        });

                        // 2. From / Bill To cards
                        col.Item().PaddingBottom(28).Row(row =>
                        {
                            row.RelativeItem()
                                .Background(T.SurfaceBox).Border(1).BorderColor(T.BorderLight)
                                .Padding(16).Column(c =>
                                {
                                    c.Item().PaddingBottom(8).Text("FROM").FontSize(7).Bold()
                                        .FontColor(T.Accent).LetterSpacing(0.1f);
                                    c.Item().Text(company.Name).FontSize(10).SemiBold();
                                    c.Item().PaddingTop(2).Text(company.Address ?? "—")
                                        .FontSize(9).FontColor(T.TextMuted);
                                    c.Item().PaddingTop(2).Text($"TIN: {company.TaxId ?? "N/A"}")
                                        .FontSize(9).FontColor(T.TextMuted);
                                });

                            row.ConstantItem(16);

                            row.RelativeItem()
                                .Background(T.SurfaceBox).Border(1).BorderColor(T.BorderLight)
                                .Padding(16).Column(c =>
                                {
                                    c.Item().PaddingBottom(8).Text("BILL TO").FontSize(7).Bold()
                                        .FontColor(T.Accent).LetterSpacing(0.1f);
                                    c.Item().Text(customer.Name).FontSize(10).SemiBold();
                                    c.Item().PaddingTop(2).Text(customer.Email)
                                        .FontSize(9).FontColor(T.TextMuted);
                                    c.Item().PaddingTop(2).Text(customer.Phone)
                                        .FontSize(9).FontColor(T.TextMuted);
                                });
                        });

                        // 3. Items table
                        col.Item().PaddingBottom(24).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(32);
                                columns.RelativeColumn();
                                columns.ConstantColumn(110);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("#");
                                header.Cell().Element(HeaderCell).Text("Description");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount");

                                static IContainer HeaderCell(IContainer c) => c
                                    .Background(T.HeaderBg)
                                    .PaddingVertical(9).PaddingHorizontal(8)
                                    .DefaultTextStyle(x => x.FontSize(8).Bold()
                                        .FontColor(T.HeaderText).LetterSpacing(0.06f));
                            });

                            table.Cell().Element(DataCell).Text("01");
                            table.Cell().Element(DataCell).Text(invoice.Description);
                            table.Cell().Element(DataCell).AlignRight()
                                .Text($"{company.Currency} {invoice.TotalAmount:N2}");

                            static IContainer DataCell(IContainer c) => c
                                .BorderBottom(1).BorderColor(T.BorderLight)
                                .PaddingVertical(11).PaddingHorizontal(8);
                        });

                        // 4. Notes + Totals
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().PaddingRight(32).Column(c =>
                            {
                                c.Item()
                                    .Background(T.SurfaceMid).Border(1).BorderColor(T.BorderLight)
                                    .Padding(16).Column(notes =>
                                    {
                                        notes.Item().PaddingBottom(6).Text("Payment Instructions")
                                            .FontSize(9).Bold().FontColor(T.TextMain);
                                        notes.Item()
                                            .Text("Please make payment within 30 days of the invoice date. Include the invoice number as reference.")
                                            .FontSize(8.5f).LineHeight(1.5f).FontColor(T.TextMuted);
                                    });

                                c.Item().PaddingTop(12)
                                    .Background(T.AccentLight).Border(1).BorderColor("#bfdbfe")
                                    .Padding(14).Column(bank =>
                                    {
                                        bank.Item().PaddingBottom(4).Text("Bank Details")
                                            .FontSize(9).Bold().FontColor(T.Accent);
                                        bank.Item().Text($"Account Name: {company.Name}")
                                            .FontSize(8.5f).FontColor(T.TextMuted);
                                    });
                            });

                            row.ConstantItem(220).Column(c =>
                            {
                                c.Item().BorderBottom(1).BorderColor(T.BorderLight)
                                    .PaddingVertical(7).Row(r =>
                                    {
                                        r.RelativeItem().Text("Subtotal")
                                            .FontSize(10).FontColor(T.TextMuted);
                                        r.AutoItem().Text($"{company.Currency} {invoice.TotalAmount:N2}")
                                            .FontSize(10);
                                    });

                                c.Item().BorderBottom(1).BorderColor(T.BorderLight)
                                    .PaddingVertical(7).Row(r =>
                                    {
                                        r.RelativeItem().Text("Amount Paid")
                                            .FontSize(10).FontColor(T.TextMuted);
                                        r.AutoItem().Text($"- {company.Currency} {invoice.PaidAmount:N2}")
                                            .FontSize(10);
                                    });

                                c.Item().PaddingTop(10).Background(T.HeaderBg).Padding(12).Row(r =>
                                {
                                    r.RelativeItem().Text("BALANCE DUE")
                                        .FontSize(9).Bold().LetterSpacing(0.06f)
                                        .FontColor(T.HeaderSubtext);
                                    r.AutoItem().Text($"{company.Currency} {invoice.Balance:N2}")
                                        .FontSize(14).ExtraBold().FontColor(T.HeaderText);
                                });
                            });
                        });
                    });

                    // --- FOOTER ---
                    page.Footer().PaddingHorizontal(50).PaddingBottom(28).Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(T.BorderLight);
                        col.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Text($"Thank you for your business — {company.Name}")
                                .FontSize(8).FontColor(T.TextMuted);
                            r.AutoItem().AlignRight().Text(text =>
                            {
                                text.Span("Page ").FontSize(8).FontColor(T.TextMuted);
                                text.CurrentPageNumber().FontSize(8).FontColor(T.TextMuted);
                                text.Span(" of ").FontSize(8).FontColor(T.TextMuted);
                                text.TotalPages().FontSize(8).FontColor(T.TextMuted);
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        // ====================================================================
        // FINANCIAL REPORT PDF
        // ====================================================================
        public byte[] GenerateFinancialReportPdf(
            TrialBalanceDTO incomeTb, TrialBalanceDTO balanceTb,
            List<AccountDTO> accounts, CompanyDTO company,
            DateTime periodStart, DateTime periodEnd)
        {
            var accountTypes = accounts.ToDictionary(a => a.Code, a => a.Type);

            decimal GetNetBalance(AccountBalanceDTO a)
            {
                if (!accountTypes.ContainsKey(a.AccountCode)) return 0;
                var type = accountTypes[a.AccountCode];
                return (type == "Asset" || type == "Expense")
                    ? a.Debit - a.Credit
                    : a.Credit - a.Debit;
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

            string headerWaveSvg = @"
                <svg viewBox=""0 0 595 90"" xmlns=""http://www.w3.org/2000/svg"" preserveAspectRatio=""none"">
                    <defs>
                        <linearGradient id=""bgGrad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""0%"">
                            <stop offset=""0%""   stop-color=""#0f172a""/>
                            <stop offset=""100%"" stop-color=""#1e3a8a""/>
                        </linearGradient>
                        <linearGradient id=""waveGrad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""0%"">
                            <stop offset=""0%""   stop-color=""#3b82f6"" stop-opacity=""0.25""/>
                            <stop offset=""100%"" stop-color=""#60a5fa"" stop-opacity=""0.10""/>
                        </linearGradient>
                    </defs>
                    <rect width=""595"" height=""90"" fill=""url(#bgGrad)""/>
                    <path d=""M0,90 Q148,55 297,80 T595,65 L595,0 L0,0 Z"" fill=""url(#waveGrad)""/>
                    <path d=""M0,90 Q200,70 400,88 T595,75 L595,0 L0,0 Z"" fill=""#3b82f6"" opacity=""0.07""/>
                    <rect y=""86"" width=""595"" height=""4"" fill=""#3b82f6""/>
                </svg>";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(0);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.SegoeUI).FontColor(T.TextMain));

                    // --- HEADER (90pt, vertically centered via AlignMiddle) ---
                    page.Header().Height(90).Layers(layers =>
                    {
                        layers.Layer().Svg(headerWaveSvg);

                        layers.PrimaryLayer()
                            .PaddingHorizontal(50)
                            .AlignMiddle()
                            .Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text(company.Name)
                                        .FontSize(15).ExtraBold()
                                        .FontColor(T.HeaderText).LetterSpacing(0.04f);
                                    col.Item().PaddingTop(4).Text(company.Address ?? string.Empty)
                                        .FontSize(8).FontColor(T.HeaderSubtext);
                                });

                                row.AutoItem().AlignRight().AlignMiddle().Column(col =>
                                {
                                    col.Item().AlignRight().Text("FINANCIAL STATEMENTS")
                                        .FontSize(12).SemiBold()
                                        .LetterSpacing(0.07f).FontColor(T.HeaderText);
                                    col.Item().PaddingTop(4).AlignRight()
                                        .Text($"{periodStart:MMM yyyy}  –  {periodEnd:MMM yyyy}")
                                        .FontSize(8).FontColor(T.HeaderSubtext);
                                });
                            });
                    });

                    // --- CONTENT ---
                    page.Content().PaddingHorizontal(50).PaddingTop(28).PaddingBottom(20).Column(col =>
                    {
                        // Period sub-header
                        col.Item().PaddingBottom(28).BorderBottom(1).BorderColor(T.BorderLight)
                            .PaddingBottom(14).Row(row =>
                            {
                                row.RelativeItem().Text($"For the period ending {periodEnd:MMMM dd, yyyy}")
                                    .FontSize(10).FontColor(T.TextMuted);
                                row.AutoItem().AlignRight().Text($"Currency: {company.Currency}")
                                    .FontSize(10).FontColor(T.TextMuted);
                            });

                        // Style helpers (local functions)
                        IContainer SectionHeader(IContainer c) => c
                            .PaddingTop(4).PaddingBottom(6)
                            .DefaultTextStyle(x => x.FontSize(14).SemiBold().FontColor(T.TextMain));

                        IContainer SubsectionHeader(IContainer c) => c
                            .Background(T.SurfaceMid)
                            .PaddingVertical(5).PaddingHorizontal(6)
                            .DefaultTextStyle(x => x.FontSize(7.5f).Bold()
                                .FontColor(T.TextMuted).LetterSpacing(0.07f));

                        IContainer TotalRow(IContainer c) => c
                            .PaddingTop(6).BorderTop(1).BorderColor(T.BorderDark).PaddingBottom(4);

                        // -------------------------------------------------------
                        // INCOME STATEMENT
                        // -------------------------------------------------------
                        col.Item().Element(SectionHeader).Text("Income Statement");

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.ConstantColumn(120); });

                            table.Cell().ColumnSpan(2).Element(SubsectionHeader).Text("REVENUE");
                            foreach (var item in revenue)
                            {
                                table.Cell().PaddingVertical(4).PaddingLeft(6).Text(item.AccountName);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().Element(TotalRow).PaddingLeft(6).Text("Total Revenue").SemiBold();
                            table.Cell().Element(TotalRow).AlignRight().Text($"{totalRevenue:N2}").SemiBold();

                            table.Cell().ColumnSpan(2).Element(SubsectionHeader).Text("OPERATING EXPENSES");
                            foreach (var item in expense)
                            {
                                table.Cell().PaddingVertical(4).PaddingLeft(6).Text(item.AccountName);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().Element(TotalRow).PaddingLeft(6).Text("Total Expenses").SemiBold();
                            table.Cell().Element(TotalRow).AlignRight().Text($"({totalExpense:N2})").SemiBold();

                            // Net Income banner
                            table.Cell().ColumnSpan(2).PaddingTop(14);
                            table.Cell().Background(T.HeaderBg).PaddingVertical(10).PaddingHorizontal(10)
                                .Text("NET INCOME").ExtraBold()
                                .FontColor(T.HeaderText).LetterSpacing(0.06f);
                            table.Cell().Background(T.HeaderBg).PaddingVertical(10).PaddingRight(10).AlignRight()
                                .Text($"{company.Currency} {netIncome:N2}").ExtraBold().FontColor(T.HeaderText);
                        });

                        col.Item().PageBreak();

                        // -------------------------------------------------------
                        // BALANCE SHEET
                        // -------------------------------------------------------
                        col.Item().Element(SectionHeader).Text("Balance Sheet");
                        col.Item().PaddingBottom(16).Text($"As of {periodEnd:MMMM dd, yyyy}")
                            .FontSize(9).FontColor(T.TextMuted);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.ConstantColumn(120); });

                            // Assets
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader).Text("ASSETS");
                            foreach (var item in assets)
                            {
                                table.Cell().PaddingVertical(4).PaddingLeft(6).Text(item.AccountName);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().Element(TotalRow).PaddingLeft(6).Text("Total Assets").SemiBold();
                            table.Cell().Element(TotalRow).AlignRight().Text($"{totalAssets:N2}").SemiBold();

                            // Liabilities
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader).Text("LIABILITIES");
                            foreach (var item in liabilities)
                            {
                                table.Cell().PaddingVertical(4).PaddingLeft(6).Text(item.AccountName);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().Element(TotalRow).PaddingLeft(6).Text("Total Liabilities").SemiBold();
                            table.Cell().Element(TotalRow).AlignRight().Text($"{totalLiabilities:N2}").SemiBold();

                            // Equity
                            table.Cell().ColumnSpan(2).Element(SubsectionHeader).Text("EQUITY");
                            foreach (var item in equity)
                            {
                                table.Cell().PaddingVertical(4).PaddingLeft(6).Text(item.AccountName);
                                table.Cell().PaddingVertical(4).AlignRight().Text($"{GetNetBalance(item):N2}");
                            }
                            table.Cell().PaddingVertical(4).PaddingLeft(6)
                                .Text("Net Income (Current Period)").FontColor(T.TextMuted);
                            table.Cell().PaddingVertical(4).AlignRight()
                                .Text($"{netIncome:N2}").FontColor(T.TextMuted);
                            table.Cell().Element(TotalRow).PaddingLeft(6).Text("Total Equity").SemiBold();
                            table.Cell().Element(TotalRow).AlignRight().Text($"{totalEquity:N2}").SemiBold();

                            // Grand total banner
                            table.Cell().ColumnSpan(2).PaddingTop(14);
                            table.Cell().Background(T.HeaderBg).PaddingVertical(10).PaddingHorizontal(10)
                                .Text("TOTAL LIABILITIES & EQUITY").ExtraBold()
                                .FontColor(T.HeaderText).LetterSpacing(0.04f);
                            table.Cell().Background(T.HeaderBg).PaddingVertical(10).PaddingRight(10).AlignRight()
                                .Text($"{company.Currency} {totalLiabilities + totalEquity:N2}")
                                .ExtraBold().FontColor(T.HeaderText);
                        });
                    });

                    // --- FOOTER ---
                    page.Footer().PaddingHorizontal(50).PaddingBottom(26).Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(T.BorderLight);
                        col.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem()
                                .Text($"Generated by Accounting System  •  {DateTime.Now:yyyy-MM-dd HH:mm}")
                                .FontSize(7.5f).FontColor(T.TextMuted);

                            r.AutoItem().AlignRight().Text(text =>
                            {
                                text.Span("Page ").FontSize(7.5f).FontColor(T.TextMuted);
                                text.CurrentPageNumber().FontSize(7.5f).FontColor(T.TextMuted);
                                text.Span(" of ").FontSize(7.5f).FontColor(T.TextMuted);
                                text.TotalPages().FontSize(7.5f).FontColor(T.TextMuted);
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}