using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ResumeTailorAI.Web.Services;

/// <summary>
/// Renders a tailored resume (markdown) to a single-column, ATS-style PDF via iText7's
/// layout API — reusing the same Markdig parser already used for on-page rendering.
/// Operates entirely in memory; never writes to disk. The first heading is treated as
/// the candidate's name and the paragraph right after it as the contact line — both are
/// centered to match a conventional resume header; markdown links become clickable PDF
/// links.
/// </summary>
public static class MarkdownPdfRenderer
{
    public static byte[] Render(string markdown, MarkdownPipeline pipeline)
    {
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        var blocks = document.ToList();

        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            using var layout = new Document(pdf, PageSize.LETTER);
            layout.SetMargins(48, 54, 48, 54);

            var isHeaderName = blocks.Count > 0 && blocks[0] is HeadingBlock { Level: 1 };

            for (var i = 0; i < blocks.Count; i++)
            {
                var centered = (i == 0 && isHeaderName) || (i == 1 && isHeaderName && blocks[i] is ParagraphBlock);
                RenderBlock(layout, blocks[i], centered);
            }
        }

        return stream.ToArray();
    }

    private static void RenderBlock(Document doc, Block block, bool centered = false)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var (size, marginTop, marginBottom) = heading.Level switch
                {
                    1 => (20f, 0f, 2f),
                    2 => (12f, 13f, 4f),
                    _ => (11f, 8f, 3f)
                };
                var headingPara = new Paragraph()
                    .SetFontSize(size)
                    .SimulateBold()
                    .SetMultipliedLeading(1.1f)
                    .SetMarginTop(marginTop)
                    .SetMarginBottom(marginBottom);
                if (centered) headingPara.SetTextAlignment(TextAlignment.CENTER);
                if (heading.Level == 2)
                {
                    headingPara.SetBorderBottom(new SolidBorder(ColorConstants.BLACK, 0.75f));
                    headingPara.SetPaddingBottom(2);
                }
                AddInlines(headingPara, heading.Inline);
                doc.Add(headingPara);
                break;

            case ParagraphBlock para:
                var bodyPara = centered
                    ? new Paragraph().SetFontSize(10f).SetMarginTop(0).SetMarginBottom(10)
                    : new Paragraph().SetFontSize(10.5f).SetMarginTop(0).SetMarginBottom(5);
                bodyPara.SetMultipliedLeading(1.15f);
                if (centered) bodyPara.SetTextAlignment(TextAlignment.CENTER);
                AddInlines(bodyPara, para.Inline);
                doc.Add(bodyPara);
                break;

            case ListBlock list:
                var iList = new List()
                    .SetSymbolIndent(14)
                    .SetMarginTop(2)
                    .SetMarginBottom(9)
                    .SetFontSize(10.5f);
                foreach (var itemBlock in list)
                {
                    if (itemBlock is not ListItemBlock listItem) continue;

                    var itemPara = new Paragraph().SetMarginTop(0).SetMarginBottom(2).SetMultipliedLeading(1.15f);
                    foreach (var child in listItem)
                    {
                        if (child is ParagraphBlock p) AddInlines(itemPara, p.Inline);
                    }
                    var listItemElement = new ListItem();
                    listItemElement.Add(itemPara);
                    iList.Add(listItemElement);
                }
                doc.Add(iList);
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderBlock(doc, child);
                break;

            case ThematicBreakBlock:
                doc.Add(new Paragraph(" ").SetMarginBottom(2).SetFontSize(4));
                break;

            case ContainerBlock container:
                foreach (var child in container)
                    RenderBlock(doc, child);
                break;
        }
    }

    private static void AddInlines(Paragraph para, Inline? inline, bool bold = false, bool italic = false)
    {
        for (var node = inline; node is not null; node = node.NextSibling)
        {
            switch (node)
            {
                case LinkInline link:
                    var label = ExtractPlainText(link.FirstChild);
                    var url = link.Url ?? string.Empty;
                    var pdfLink = new Link(label, PdfAction.CreateURI(url))
                        .SetFontColor(ColorConstants.BLUE)
                        .SetUnderline();
                    if (bold) pdfLink.SimulateBold();
                    if (italic) pdfLink.SimulateItalic();
                    para.Add(pdfLink);
                    break;

                case LiteralInline literal:
                    para.Add(MakeText(literal.Content.ToString(), bold, italic));
                    break;

                case CodeInline code:
                    para.Add(new Text(code.Content).SetFontFamily("Courier"));
                    break;

                case LineBreakInline:
                    para.Add(new Text("\n"));
                    break;

                case EmphasisInline emphasis:
                    var isBold = bold || emphasis.DelimiterCount >= 2;
                    var isItalic = italic || emphasis.DelimiterCount % 2 == 1;
                    AddInlines(para, emphasis.FirstChild, isBold, isItalic);
                    break;

                case ContainerInline container:
                    AddInlines(para, container.FirstChild, bold, italic);
                    break;
            }
        }
    }

    private static string ExtractPlainText(Inline? inline)
    {
        var text = new System.Text.StringBuilder();
        for (var node = inline; node is not null; node = node.NextSibling)
        {
            switch (node)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case ContainerInline container:
                    text.Append(ExtractPlainText(container.FirstChild));
                    break;
            }
        }
        return text.ToString();
    }

    private static Text MakeText(string content, bool bold, bool italic)
    {
        var text = new Text(content);
        if (bold) text.SimulateBold();
        if (italic) text.SimulateItalic();
        return text;
    }
}
