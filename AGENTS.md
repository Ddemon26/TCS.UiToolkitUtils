# TCS CmdConsole – Agents & Architecture Guide

This document describes the **runtime “agents”** that make up the Tent City Studio Command Console package.
An *agent* is any class that cooperates with **`ConsoleManager`** at runtime – parsers, command/variable bindings, or UI helpers.
Use it as a reference when you add new bindings or extend the console.

---

## 1. Core Runtime Agents

| Agent                        | Purpose                                                                                                                  | Key public API                                                                                    |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| **`ConsoleManager`**         | Central registry that executes console text, stores variables and commands, and provides autocompletion and help.        | `Execute(string)`, `RegisterVar`, `UnregisterVar`, `VoidCmds`, `Variables`, `GetHints`, `GetHelp` |
| **`VariableDefinition`**     | Immutable record that wraps a variable **getter / setter** pair and the variable’s CLR type.                             | `(Type TargetType, Func<object> Getter, Action<object> Setter)`                                   |
| **`CommandDefinition`**      | Immutable record that wraps a command handler delegate.                                                                  | `Func<string[], string> Handler`                                                                  |
| **`ArgumentParserRegistry`** | Discovers and orders `IParser` implementations then performs recursive argument conversion before a command is executed. | `Parse(string arg, Type targetType)`                                                              |
| **`ConsoleWindow`** (UI)     | Simple in‑game UI built with UXML & USS that forwards user text to `ConsoleManager`.                                     | `Open()`, `Close()`                                                                               |

---

## 2. Attribute‑Driven Bindings

| Attribute                           | Attach to            | Effect                                                                                                                                                                                                               |
| ----------------------------------- | -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConsoleBind`                       | **class**            | Tells the source‑generator in *Runtime/packages/ConsoleGenerator* to create a **partial class** that registers all variables & methods marked below when the component is enabled, and unregisters them on disposal. |
| `ConsoleVariable(category?, name?)` | **field / property** | Exposes a variable in the console as `category.name` (defaults to the member name and root category if omitted).                                                                                                     |
| `ConsoleMethod(name?)`              | **method**           | Exposes a method as a console **command**. Parameters are parsed via `ArgumentParserRegistry`.                                                                                                                       |

> **Example**

```csharp
[ConsoleBind]
public partial class PlayerController : MonoBehaviour
{
    [ConsoleVariable("player", "speed")]
    float m_speed = 5f;

    [ConsoleMethod]
    void GodMode(bool value) => m_isInvincible = value;
}
```

After compilation the generator produces a partial class that calls:

```csharp
ConsoleManager.RegisterVar("player.speed", typeof(float),
    () => m_speed, v => m_speed = (float)v);

ConsoleManager.VoidCmds["godMode"] = args => { GodMode(bool.Parse(args[0])); return "OK"; };
```

---

## 3. Built‑in Parsers

`ArgumentParserRegistry` ships with 24 specialised parsers that together handle nearly every Unity‑centric type you are likely to need.

| Parser                                                    | Accepted target types                                                      |         |                |
| --------------------------------------------------------- | -------------------------------------------------------------------------- | ------- | -------------- |
| `PrimitiveParser`                                         | All numeric primitives (`int`, `float`, `double`, …), `char`, `byte`, etc. |         |                |
| `BoolParser`                                              | `bool`                                                                     |         |                |
| `StringParser`                                            | `string`                                                                   |         |                |
| `ColorParser`                                             | `UnityEngine.Color` in hex #RRGGBB\[AA] or “r,g,b,a”                       |         |                |
| `Vector2Parser` / `Vector3Parser` / `Vector4Parser`       | \`UnityEngine.Vector\[2                                                    | 3       | 4]\` (“x,y,z”) |
| `Vector2IntParser` / `Vector3IntParser`                   | \`UnityEngine.Vector\[2                                                    | 3]Int\` |                |
| `QuaternionParser`                                        | `UnityEngine.Quaternion` (“x,y,z,w” or Euler)                              |         |                |
| `EnumParser`                                              | Any `enum`                                                                 |         |                |
| `GameObjectParser`                                        | `UnityEngine.GameObject` by name                                           |         |                |
| `ComponentParser`                                         | Any `UnityEngine.Component` subtype by game‑object path                    |         |                |
| `CollectionParser`, `EnumerableParser`                    | `ICollection<T>`, `IEnumerable<T>` from comma‑separated values             |         |                |
| `NullableParser`                                          | `Nullable<T>`                                                              |         |                |
| `GenericParser`, `BasicParser`, `PolymorphicParser`, etc. | Internals that fall back to reflection when no specialised parser matches. |         |                |

> **Need a new type?**
> Implement `IParser`, give it a **positive** `Priority` if it should pre‑empt the generics, then add an instance to `ArgumentParserRegistry` **before** the first call to `ConsoleManager.Execute()`.

---

## 4. Extending the Console

1. **Add a C# class** to your Unity project and mark it with `[ConsoleBind]`.
2. Decorate fields / properties with `[ConsoleVariable]` and methods with `[ConsoleMethod]`.
3. Play! The console UI auto‑registers when the component is instantiated.

---

## 5. Building & Testing

| Task             | Command                                                |
| ---------------- | ------------------------------------------------------ |
| Restore packages | `dotnet restore`                                       |
| Run tests        | `dotnet test` (tests live in **ConsoleManager.Tests**) |
| Generate docs    | None yet – run the game and type `help`                |

> The old Node/Vite workspace scripts referenced in the previous `AGENTS.md` have been removed; the project is now 100 % **C#/.NET 8** and **Unity 2022+**.

---

## 6. Directory Reference

```
Runtime/                 — Console runtime library
Runtime/ConsoleUI/       — UXML/USS + logic for in‑game window
Runtime/Parsing/         — Argument parsers
Runtime/packages/ConsoleGenerator — Roslyn source‑generator that wires up bindings
ConsoleManager.Tests/    — MSTest unit tests
Editor/ (future)         — In‑editor tooling (empty at v1.0.1)
```

---

*Last updated: 6 June 2025*
