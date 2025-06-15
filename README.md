# TCS UI Toolkit Utilities

A collection of small editor windows and runtime helpers that streamline workflows when using Unity's UI Toolkit.

## Features
- **UxmlToCSharpConverter** – converts a UXML asset into a C# class and optionally extracts inline styles.
- **UxmlStyleExtractor** – pulls inline style information from a UXML file into a USS stylesheet.
- **DataBindingExtensions** – helper methods that simplify configuring `DataBinding` objects at runtime.

## Installation
1. Open **Window → Package Manager** in Unity.
2. Click the **`+`** button and choose **Add package from git URL...**
3. Enter the repository URL, for example:
   ```
   https://github.com/Ddemon26/TCS.UiToolkitUtils.git
   ```

## Using the Editor Windows
### UxmlToCSharpConverter
Open via **`Tools/TCS/UXML to C# Class Converter`**. Select a UXML asset, choose an output folder and class name, configure any options, then click **Generate Files** to create the C# and optional USS files.

### UxmlStyleExtractor
Open via **`Tools/TCS/UXML Style Extractor`**. Pick a UXML asset and an output path, then click **Save USS File** to write the extracted style sheet.

## Runtime Extensions
The `TCS.UiToolkitUtils` namespace provides extension methods for `DataBinding`.

```csharp
var binding = new DataBinding()
    .Configure(() => viewModel.SomeProperty, BindingMode.TwoWay);
```

The `Configure` calls set up the data source path, binding mode and update trigger, reducing UI Toolkit boilerplate.
