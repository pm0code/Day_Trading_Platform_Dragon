using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TradingPlatform.CodeQuality;

public class ReportGenerator
{
    public async Task GenerateHtmlReport(CodeQualityReport report, string outputPath)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<title>Trading Platform Code Quality Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine(".critical { color: #d32f2f; font-weight: bold; }");
        html.AppendLine(".high { color: #f57c00; }");
        html.AppendLine(".medium { color: #fbc02d; }");
        html.AppendLine(".low { color: #388e3c; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine("</style>");
        html.AppendLine("</head><body>");
        
        html.AppendLine($"<h1>Code Quality Report</h1>");
        html.AppendLine($"<p>Generated: {report.GeneratedAt}</p>");
        html.AppendLine($"<p>Analysis Time: {report.AnalysisTime.TotalSeconds:F2} seconds</p>");
        
        html.AppendLine("<h2>Summary</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Severity</th><th>Count</th></tr>");
        html.AppendLine($"<tr><td class='critical'>Critical</td><td>{report.CriticalIssues}</td></tr>");
        html.AppendLine($"<tr><td class='high'>High</td><td>{report.HighPriorityIssues}</td></tr>");
        html.AppendLine($"<tr><td class='medium'>Medium</td><td>{report.MediumPriorityIssues}</td></tr>");
        html.AppendLine($"<tr><td class='low'>Low</td><td>{report.LowPriorityIssues}</td></tr>");
        html.AppendLine("</table>");
        
        if (report.CriticalIssues > 0)
        {
            html.AppendLine("<h2>Critical Issues</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>File</th><th>Line</th><th>Issue</th><th>Category</th></tr>");
            
            foreach (var issue in report.Issues.Where(i => i.Severity == IssueSeverity.Critical))
            {
                html.AppendLine($"<tr>");
                html.AppendLine($"<td>{Path.GetFileName(issue.FilePath)}</td>");
                html.AppendLine($"<td>{issue.Line}</td>");
                html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(issue.Message)}</td>");
                html.AppendLine($"<td>{issue.Category}</td>");
                html.AppendLine($"</tr>");
            }
            
            html.AppendLine("</table>");
        }
        
        html.AppendLine("</body></html>");
        
        await File.WriteAllTextAsync(outputPath, html.ToString());
    }
    
    public async Task GenerateMarkdownReport(CodeQualityReport report, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Trading Platform Code Quality Report");
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Analysis Time: {report.AnalysisTime.TotalSeconds:F2} seconds");
        sb.AppendLine();
        
        sb.AppendLine("## Summary");
        sb.AppendLine($"- **Total Issues**: {report.TotalIssues}");
        sb.AppendLine($"- **Critical**: {report.CriticalIssues}");
        sb.AppendLine($"- **High**: {report.HighPriorityIssues}");
        sb.AppendLine($"- **Medium**: {report.MediumPriorityIssues}");
        sb.AppendLine($"- **Low**: {report.LowPriorityIssues}");
        sb.AppendLine();
        
        sb.AppendLine("## Issues by Category");
        foreach (var cat in report.IssuesByCategory.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"- {cat.Key}: {cat.Value}");
        }
        sb.AppendLine();
        
        if (report.CriticalIssues > 0)
        {
            sb.AppendLine("## Critical Issues");
            foreach (var issue in report.Issues.Where(i => i.Severity == IssueSeverity.Critical).Take(20))
            {
                sb.AppendLine($"### {Path.GetFileName(issue.FilePath)}:{issue.Line}");
                sb.AppendLine($"**{issue.Message}**");
                if (!string.IsNullOrEmpty(issue.CodeSnippet))
                {
                    sb.AppendLine("```csharp");
                    sb.AppendLine(issue.CodeSnippet);
                    sb.AppendLine("```");
                }
                if (!string.IsNullOrEmpty(issue.SuggestedFix))
                {
                    sb.AppendLine($"**Fix**: {issue.SuggestedFix}");
                }
                sb.AppendLine();
            }
        }
        
        await File.WriteAllTextAsync(outputPath, sb.ToString());
    }
    
    public async Task GenerateJsonReport(CodeQualityReport report, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(report, options);
        await File.WriteAllTextAsync(outputPath, json);
    }
}