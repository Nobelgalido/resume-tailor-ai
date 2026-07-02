using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace ResumeTailorAI.Web.Services;

/// <summary>
/// Extracts plain text from an uploaded PDF resume via iText7 (AGPL — free for this
/// public open-source project; a commercial license is only needed for closed-source use).
/// Operates entirely on an in-memory stream; never writes to disk.
/// </summary>
public static class PdfResumeTextExtractor
{
    /// <summary>Returns the extracted text, or null if the file isn't a readable PDF with text content.</summary>
    public static string? TryExtractText(Stream pdfStream)
    {
        try
        {
            using var reader = new PdfReader(pdfStream);
            using var document = new PdfDocument(reader);

            var sb = new StringBuilder();
            for (var pageNum = 1; pageNum <= document.GetNumberOfPages(); pageNum++)
            {
                var pageText = PdfTextExtractor.GetTextFromPage(
                    document.GetPage(pageNum),
                    new LocationTextExtractionStrategy());

                if (string.IsNullOrWhiteSpace(pageText)) continue;

                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(pageText.Trim());
            }

            var result = sb.ToString().Trim();
            return result.Length > 0 ? result : null;
        }
        catch
        {
            // Covers encrypted/corrupt/non-PDF input alike — the caller shows one
            // friendly message regardless of the specific failure.
            return null;
        }
    }
}
