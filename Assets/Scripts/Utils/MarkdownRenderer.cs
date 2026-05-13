using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class MarkdownRenderer : BindableElement
{
    static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseGridTables()
        .UseAutoLinks()
        .Build();

    string _value = "";
    string _basePath;
    float _maxImageHeight = 260;
    bool _openExternalLinks = true;
    bool _selectable;

    public MarkdownRenderer()
    {
        AddToClassList("markdown-renderer");
    }

    [UxmlAttribute]
    public string value
    {
        get => _value;
        set
        {
            _value = value ?? "";
            Render();
        }
    }

    [UxmlAttribute]
    public string basePath
    {
        get => _basePath;
        set
        {
            _basePath = value;
            Render();
        }
    }

    [UxmlAttribute]
    public float maxImageHeight
    {
        get => _maxImageHeight;
        set
        {
            _maxImageHeight = Math.Max(1, value);
            Render();
        }
    }

    [UxmlAttribute]
    public bool openExternalLinks
    {
        get => _openExternalLinks;
        set
        {
            _openExternalLinks = value;
            Render();
        }
    }

    [UxmlAttribute]
    public bool selectable
    {
        get => _selectable;
        set
        {
            _selectable = value;
            Render();
        }
    }

    public void SetMarkdownWithoutNotify(string markdown)
    {
        _value = markdown ?? "";
        Render();
    }

    void Render()
    {
        Clear();

        if (string.IsNullOrWhiteSpace(_value))
            return;

        var document = Markdown.Parse(_value, Pipeline);
        foreach (var block in document)
        {
            RenderBlock(block, this);
        }
    }

    void RenderBlock(Block block, VisualElement parent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                parent.Add(CreateInlineLabel(heading.Inline, $"markdown-heading markdown-heading-{heading.Level}"));
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(paragraph, parent);
                break;
            case ListBlock list:
                RenderList(list, parent);
                break;
            case QuoteBlock quote:
                var quoteElement = new VisualElement();
                quoteElement.AddToClassList("markdown-blockquote");
                RenderChildBlocks(quote, quoteElement);
                parent.Add(quoteElement);
                break;
            case FencedCodeBlock fencedCode:
                parent.Add(CreateCodeBlock(fencedCode.Lines.ToString(), fencedCode.Info));
                break;
            case CodeBlock code:
                parent.Add(CreateCodeBlock(code.Lines.ToString(), null));
                break;
            case ThematicBreakBlock:
                var breakElement = new VisualElement();
                breakElement.AddToClassList("markdown-hr");
                parent.Add(breakElement);
                break;
            case Table table:
                RenderTable(table, parent);
                break;
            case HtmlBlock html:
                parent.Add(CreatePlainTextLabel(html.Lines.ToString(), "markdown-html"));
                break;
            default:
                parent.Add(CreatePlainTextLabel(block.ToString(), "markdown-unsupported"));
                break;
        }
    }

    void RenderChildBlocks(ContainerBlock source, VisualElement parent)
    {
        foreach (var child in source)
        {
            RenderBlock(child, parent);
        }
    }

    void RenderParagraph(ParagraphBlock paragraph, VisualElement parent)
    {
        if (paragraph.Inline == null)
            return;

        var text = BuildRichText(paragraph.Inline, out var handlers, out var images).Trim();
        if (!string.IsNullOrEmpty(text))
        {
            parent.Add(CreateRichLabel(text, "markdown-paragraph", handlers));
        }

        foreach (var image in images)
        {
            parent.Add(CreateImageElement(image.Url, image.AltText));
        }
    }

    void RenderList(ListBlock list, VisualElement parent)
    {
        var listElement = new VisualElement();
        listElement.AddToClassList("markdown-list");
        listElement.AddToClassList(list.IsOrdered ? "markdown-list-ordered" : "markdown-list-unordered");

        var index = ParseOrderedStart(list.OrderedStart);
        foreach (var itemBlock in list.OfType<ListItemBlock>())
        {
            var item = new VisualElement();
            item.AddToClassList("markdown-list-item");

            var marker = new Label(list.IsOrdered ? $"{index}." : "\u2022");
            marker.AddToClassList("markdown-list-marker");
            item.Add(marker);

            var content = new VisualElement();
            content.AddToClassList("markdown-list-content");
            RenderChildBlocks(itemBlock, content);
            item.Add(content);

            listElement.Add(item);
            index++;
        }

        parent.Add(listElement);
    }

    int ParseOrderedStart(string orderedStart)
    {
        return int.TryParse(orderedStart, out var start) ? start : 1;
    }

    VisualElement CreateCodeBlock(string code, string info)
    {
        var root = new VisualElement();
        root.AddToClassList("markdown-code-block");

        if (!string.IsNullOrWhiteSpace(info))
        {
            var infoLabel = CreatePlainTextLabel(info.Trim(), "markdown-code-info");
            root.Add(infoLabel);
        }

        var codeLabel = CreatePlainTextLabel(code ?? "", "markdown-code-text");
        root.Add(codeLabel);
        return root;
    }

    void RenderTable(Table table, VisualElement parent)
    {
        var scroller = new ScrollView(ScrollViewMode.Horizontal);
        scroller.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scroller.horizontalScrollerVisibility = ScrollerVisibility.Auto;
        scroller.RegisterCallback<WheelEvent>(OnTableWheel, TrickleDown.TrickleDown);
        scroller.AddToClassList("markdown-table-scroll");

        var tableElement = new VisualElement();
        tableElement.AddToClassList("markdown-table");

        var rows = table.OfType<TableRow>().ToList();
        var columnWidths = CalculateTableColumnWidths(rows);

        foreach (var row in rows)
        {
            var rowElement = new VisualElement();
            rowElement.AddToClassList("markdown-table-row");
            if (row.IsHeader)
                rowElement.AddToClassList("markdown-table-header-row");

            var columnIndex = 0;
            foreach (var cell in row.OfType<TableCell>())
            {
                var cellElement = new VisualElement();
                cellElement.AddToClassList("markdown-table-cell");
                if (row.IsHeader)
                    cellElement.AddToClassList("markdown-table-header-cell");

                var columnWidth = columnIndex < columnWidths.Count ? columnWidths[columnIndex] : 120f;
                cellElement.style.width = columnWidth;
                cellElement.style.minWidth = columnWidth;
                cellElement.style.maxWidth = columnWidth;

                if (cell.Count == 0)
                {
                    cellElement.Add(CreatePlainTextLabel("", "markdown-table-text"));
                }
                else
                {
                    RenderChildBlocks(cell, cellElement);
                }

                rowElement.Add(cellElement);
                columnIndex++;
            }

            tableElement.Add(rowElement);
        }

        scroller.Add(tableElement);
        parent.Add(scroller);
    }

    void OnTableWheel(WheelEvent evt)
    {
        if (evt.shiftKey)
            return;

        var tableScrollView = evt.currentTarget as ScrollView;
        var outerScrollView = FindOuterScrollView(tableScrollView);
        if (outerScrollView == null)
            return;

        outerScrollView.scrollOffset += new Vector2(0, evt.delta.y * 18f);
        evt.StopImmediatePropagation();
    }

    static ScrollView FindOuterScrollView(VisualElement element)
    {
        var current = element?.parent;
        while (current != null)
        {
            if (current is ScrollView scrollView)
                return scrollView;

            current = current.parent;
        }

        return null;
    }

    List<float> CalculateTableColumnWidths(List<TableRow> rows)
    {
        var columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.OfType<TableCell>().Count());
        var widths = Enumerable.Repeat(96f, columnCount).ToList();

        foreach (var row in rows)
        {
            var columnIndex = 0;
            foreach (var cell in row.OfType<TableCell>())
            {
                var text = GetCellPlainText(cell);
                var maxLineWidth = text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .DefaultIfEmpty("")
                    .Max(GetApproximateTextWidth);

                widths[columnIndex] = Math.Max(widths[columnIndex], Math.Clamp(maxLineWidth + 24f, 72f, 420f));
                columnIndex++;
            }
        }

        return widths;
    }

    string GetCellPlainText(TableCell cell)
    {
        var builder = new StringBuilder();
        foreach (var block in cell)
        {
            AppendBlockPlainText(block, builder);
        }

        return builder.ToString();
    }

    void AppendBlockPlainText(Block block, StringBuilder builder)
    {
        switch (block)
        {
            case LeafBlock leaf when leaf.Inline != null:
                AppendInlinePlainText(leaf.Inline, builder);
                builder.AppendLine();
                break;
            case CodeBlock code:
                builder.AppendLine(code.Lines.ToString());
                break;
            case ContainerBlock container:
                foreach (var child in container)
                    AppendBlockPlainText(child, builder);
                break;
            default:
                builder.AppendLine(block.ToString());
                break;
        }
    }

    void AppendInlinePlainText(ContainerInline container, StringBuilder builder)
    {
        foreach (var child in container)
            AppendInlinePlainText(child, builder);
    }

    void AppendInlinePlainText(Inline inline, StringBuilder builder)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
            case LinkInline link:
                AppendInlinePlainText(link, builder);
                break;
            case AutolinkInline autoLink:
                builder.Append(autoLink.Url);
                break;
            case HtmlInline html:
                builder.Append(html.Tag);
                break;
            case ContainerInline container:
                AppendInlinePlainText(container, builder);
                break;
            default:
                builder.Append(inline.ToString());
                break;
        }
    }

    static float GetApproximateTextWidth(string text)
    {
        var width = 0f;
        foreach (var c in text ?? "")
        {
            if (char.IsWhiteSpace(c))
                width += 4f;
            else if (c > 127)
                width += 12f;
            else if (char.IsUpper(c))
                width += 8f;
            else
                width += 7f;
        }

        return width;
    }

    Label CreateInlineLabel(ContainerInline inline, string classNames)
    {
        var text = BuildRichText(inline, out var handlers, out var images).Trim();
        return CreateRichLabel(text, classNames, handlers);
    }

    Label CreateRichLabel(string text, string classNames, Dictionary<string, Action> handlers)
    {
        var label = new Label(text ?? "");
        label.enableRichText = true;
        ApplySelectable(label);
        label.AddToClassList("markdown-text");
        AddClasses(label, classNames);

        if (handlers.Count > 0)
            Utils.RegisterLinkTag(label, handlers);

        return label;
    }

    Label CreatePlainTextLabel(string text, string classNames)
    {
        var label = new Label(text ?? "");
        label.enableRichText = false;
        ApplySelectable(label);
        label.AddToClassList("markdown-text");
        AddClasses(label, classNames);
        return label;
    }

    void ApplySelectable(Label label)
    {
        label.selection.isSelectable = _selectable;
        label.selection.doubleClickSelectsWord = _selectable;
        label.selection.tripleClickSelectsLine = _selectable;
        label.focusable = _selectable;
        label.pickingMode = PickingMode.Position;

        if (_selectable)
        {
            label.AddToClassList(TextElement.selectableUssClassName);
            return;
        }

        label.RemoveFromClassList(TextElement.selectableUssClassName);
    }

    static void AddClasses(VisualElement element, string classNames)
    {
        if (string.IsNullOrWhiteSpace(classNames))
            return;

        foreach (var className in classNames.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            element.AddToClassList(className);
        }
    }

    string BuildRichText(ContainerInline inline, out Dictionary<string, Action> handlers, out List<MarkdownImage> images)
    {
        var builder = new StringBuilder();
        handlers = new Dictionary<string, Action>();
        images = new List<MarkdownImage>();

        if (inline != null)
            AppendInlineChildren(inline, builder, handlers, images);

        return builder.ToString();
    }

    void AppendInlineChildren(ContainerInline container, StringBuilder builder, Dictionary<string, Action> handlers, List<MarkdownImage> images)
    {
        foreach (var child in container)
        {
            AppendInline(child, builder, handlers, images);
        }
    }

    void AppendInline(Inline inline, StringBuilder builder, Dictionary<string, Action> handlers, List<MarkdownImage> images)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(EscapeRichText(literal.Content.ToString()));
                break;
            case LineBreakInline lineBreak:
                builder.Append(lineBreak.IsHard ? "\n" : " ");
                break;
            case CodeInline code:
                builder.Append("<color=#d7c496>");
                builder.Append(EscapeRichText(code.Content));
                builder.Append("</color>");
                break;
            case EmphasisInline emphasis:
                AppendEmphasis(emphasis, builder, handlers, images);
                break;
            case LinkInline link:
                AppendLink(link, builder, handlers, images);
                break;
            case AutolinkInline autoLink:
                AppendUrl(autoLink.Url, autoLink.Url, builder, handlers);
                break;
            case HtmlInline html:
                builder.Append(EscapeRichText(html.Tag));
                break;
            case ContainerInline container:
                AppendInlineChildren(container, builder, handlers, images);
                break;
            default:
                builder.Append(EscapeRichText(inline.ToString()));
                break;
        }
    }

    void AppendEmphasis(EmphasisInline emphasis, StringBuilder builder, Dictionary<string, Action> handlers, List<MarkdownImage> images)
    {
        var tag = emphasis.DelimiterChar == '~' ? "s" : emphasis.DelimiterCount >= 2 ? "b" : "i";
        builder.Append('<').Append(tag).Append('>');
        AppendInlineChildren(emphasis, builder, handlers, images);
        builder.Append("</").Append(tag).Append('>');
    }

    void AppendLink(LinkInline link, StringBuilder builder, Dictionary<string, Action> handlers, List<MarkdownImage> images)
    {
        var labelBuilder = new StringBuilder();
        var nestedHandlers = new Dictionary<string, Action>();
        var nestedImages = new List<MarkdownImage>();
        AppendInlineChildren(link, labelBuilder, nestedHandlers, nestedImages);

        var hasLabel = !string.IsNullOrWhiteSpace(labelBuilder.ToString());
        var label = hasLabel ? labelBuilder.ToString() : EscapeRichText(link.Url);
        var altText = StripRichText(label ?? "");

        if (link.IsImage)
        {
            images.Add(new MarkdownImage(link.Url, altText));
            if (!string.IsNullOrWhiteSpace(altText))
                builder.Append(EscapeRichText(altText));
            return;
        }

        foreach (var handler in nestedHandlers)
            handlers[handler.Key] = handler.Value;

        images.AddRange(nestedImages);
        AppendUrl(link.Url, label, builder, handlers);
    }

    void AppendUrl(string url, string label, StringBuilder builder, Dictionary<string, Action> handlers)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var handlerId = $"md-link-{handlers.Count}";
        if (_openExternalLinks && IsExternalUrl(url))
        {
            handlers[handlerId] = () => OpenUrl(url);
            builder.Append("<link=\"").Append(handlerId).Append("\">");
        }

        builder.Append("<color=#40a0ff><u>");
        builder.Append(label);
        builder.Append("</u></color>");

        if (handlers.ContainsKey(handlerId))
            builder.Append("</link>");
    }

    void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var dialogRoot = DialogRoot.Instance;
        if (dialogRoot != null)
        {
            dialogRoot.PopupConfirmOpenURLDialog(url);
            return;
        }

        Application.OpenURL(url);
    }

    VisualElement CreateImageElement(string url, string altText)
    {
        var root = new VisualElement();
        root.AddToClassList("markdown-image-root");

        var image = new Image();
        image.AddToClassList("markdown-image");
        image.scaleMode = ScaleMode.ScaleToFit;
        image.style.maxHeight = _maxImageHeight;
        root.Add(image);

        var resolvedUrl = ResolveContentPath(url);
        var placeholder = CreatePlainTextLabel(string.IsNullOrWhiteSpace(altText) ? resolvedUrl : altText, "markdown-image-alt");
        root.Add(placeholder);

        if (string.IsNullOrWhiteSpace(resolvedUrl))
            return root;

        UnityWebRequestImageReader.Instance.RequestIfNotRequestedYetOtherwiseExecuteDirectly(new ImageFetchTask
        {
            path = resolvedUrl,
            textureCallbacks = new List<Action<Texture2D>>
            {
                texture =>
                {
                    image.image = texture;
                    placeholder.style.display = DisplayStyle.None;
                }
            }
        });

        return root;
    }

    string ResolveContentPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (IsExternalUrl(url) || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (Path.IsPathRooted(url))
            return url;

        var root = string.IsNullOrWhiteSpace(_basePath) ? Application.streamingAssetsPath : _basePath;
        return Path.Combine(root, url).Replace('\\', '/');
    }

    static bool IsExternalUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));
    }

    static string EscapeRichText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    static string StripRichText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("<b>", "")
            .Replace("</b>", "")
            .Replace("<i>", "")
            .Replace("</i>", "")
            .Replace("<s>", "")
            .Replace("</s>", "")
            .Replace("<color=#d7c496>", "")
            .Replace("</color>", "");
    }

    readonly struct MarkdownImage
    {
        public readonly string Url;
        public readonly string AltText;

        public MarkdownImage(string url, string altText)
        {
            Url = url;
            AltText = altText;
        }
    }
}
