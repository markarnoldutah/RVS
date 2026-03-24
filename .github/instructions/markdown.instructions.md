---
description: 'Markdown documentation standards for this repository'
applyTo: '**/*.md'
---

## Markdown Content Rules

1. **Headings**: Use `##` for top-level sections and `###` for subsections. Reserve `#` for the document title only.
2. **Lists**: Use `-` for unordered lists and `1.` for ordered lists. Indent nested lists with two spaces.
3. **Code Blocks**: Use fenced code blocks with a language specifier (e.g., ```csharp, ```json).
4. **Links**: Use descriptive link text — avoid bare URLs or "click here".
5. **Tables**: Use markdown tables for structured data. Align columns and include headers.
6. **Whitespace**: Use blank lines to separate sections. Avoid excessive blank lines.

## Formatting Guidelines

- Keep lines under 400 characters.
- Use **bold** for emphasis on key terms in lists and tables.
- Use backticks for inline code, file names, class names, and CLI commands.
- No YAML front matter unless the file is a Copilot instructions file (`.instructions.md`) requiring `description` and `applyTo` fields.