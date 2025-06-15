# TCS UiToolkitUtils – Agents & Architecture Guide

This document outlines the editor windows and runtime extensions that ship with Tent City Studio's **UI Toolkit Utils** package. Each component acts as an *agent* that assists with authoring and binding UI Toolkit layouts.

---

## 1. Core Tools & Extensions

| Agent | Purpose | Key API |
| ----- | ------- | ------- |
| **UxmlToCSharpConverter** (Editor) | Converts a UXML layout into a partial C# class with optional data‑binding helpers. | `Tools/TCS/UXML to C# Class Converter` |
| **UxmlStyleExtractor** (Editor) | Extracts inline styles from a UXML file into a new USS stylesheet. | `Tools/TCS/UXML Style Extractor` |
| **USSNameGenerator** (Source Generator) | Creates constants for USS class names and binds properties marked with `CreateBindIDAttribute`. | Build‑time generator |
| **DataBindingExtensions** (Runtime) | Fluent helpers for configuring `DataBinding` objects. | `Configure(...)` |

---

## 2. Attributes for Code Generation

| Attribute | Attach to | Effect |
| --------- | --------- | ------ |
| `USSNameAttribute` | field | Generates a constant for a USS class name. |
| `CreateBindIDAttribute` | field / property | Creates a `PropertyPath` ID used for binding. |
| `AutoBindAttribute` | field | Marks fields for automatic element lookup. |
| `ReadOnlyAttribute` | field / property | Indicates the UI should disable editing. |

---

## 3. Using the Tools

1. Open **UXML to C# Class Converter** from the Unity Tools menu to generate a strongly typed view class.
2. Use **UXML Style Extractor** to move inline styles into a separate USS file.
3. Annotate fields with `[USSName]` or `[CreateBindID]` and build the project to run the source generator.
4. At runtime, utilise `DataBindingExtensions` to configure bindings programmatically.

---

## 4. Directory Reference

```
Editor/                  — Editor windows and supporting UI
Editor/UI/               — UXML assets used by the windows
Runtime/                 — Runtime utilities
Runtime/Extensions/      — Data binding helper methods
Runtime/Packages/Generator — Source generator and attribute definitions
```

---

*Last updated: 6 June 2025*
