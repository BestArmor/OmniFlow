<div align="center">
  <h1>⚡ OmniFlow</h1>
  <p><strong>High-Performance Log Aggregator & Tailer for Windows</strong></p>
  <p>Zero-allocation log monitoring powered by <code>MemoryMappedFiles</code>, <code>Span&lt;T&gt;</code> and <code>System.Threading.Channels</code>.</p>
  
  <br/>
  
  ![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
  ![.NET 8](https://img.shields.io/badge/.NET%208-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
  ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
  ![WPF](https://img.shields.io/badge/WPF-512BD4?style=for-the-badge&logo=windows&logoColor=white)
  ![License](https://img.shields.io/badge/License-MIT-EC4899?style=for-the-badge)
  
  <br/>
  
  <img src="https://z-cdn-media.chatglm.cn/files/06fc0f84-8c2d-44bc-947c-9ad5799b5bb3.png?auth_key=1882677285-5f6f088638c94d459f268a83ffe3d092-0-61d8aa49193f7636773d755b365cb4ea" width="90%" alt="OmniFlow Dashboard" />
  
  <br/><br/>
</div>

## 📖 Overview
**OmniFlow** is a high-performance, zero-allocation desktop application built for analyzing and monitoring massive log files in real-time. Designed with a Vercel/Linear-inspired dark UI, it provides developers with an intuitive, lag-free experience even when tailing files with millions of lines.

While competitors struggle with memory leaks or outdated interfaces, OmniFlow leverages modern .NET 8 features to deliver native-level performance without compromising on aesthetics.

## ✨ Key Features
- 🧠 **Zero-Memory Leaks:** Utilizes `MemoryMappedFiles` and `Span<T>` to parse gigabytes of logs without OOM exceptions.
- ⚡ **Thread-Safe UI:** Implements `System.Threading.Channels` (Producer/Consumer pattern) to decouple I/O reading from UI rendering.
- 📜 **Structured JSON Support:** Natively parses Serilog/NLog JSON logs using `Utf8JsonReader`, extracting properties into dedicated columns.
- 📊 **Real-time Metrics:** Instantly tracks Total Events, Errors, and Warnings.
- 🔍 **Smart Filtering:** Interactive `ICollectionView` filtering to instantly isolate errors or warnings without reloading data.
- 🎨 **Premium UI/UX:** Custom `WindowChrome`, Glassmorphism, gradient accents, and smooth XAML animations.

## 🛠 Tech Stack
- **Framework:** .NET 8, WPF
- **Architecture:** MVVM (Model-View-ViewModel), Dependency Injection
- **Memory Management:** `System.Buffers.ArrayPool`, `ReadOnlySpan<byte>`, `Utf8JsonReader`
- **Concurrency:** `System.Threading.Channels`

## 🏗 Architecture
OmniFlow follows a strict Clean Architecture approach:

```text
OmniFlow/
├── Models/             # Value types (readonly record struct)
├── Services/           # Core logic (Channels, File Watchers, Parsers)
├── ViewModels/         # State & Command logic (INotifyPropertyChanged)
└── Views/              # XAML UI components
```

## 🗺 Roadmap
We are just getting started. Here is what's coming in future releases:

- [ ] **Multi-File Aggregation:** Tail multiple files simultaneously and merge them by timestamp.
- [ ] **Advanced Regex Search:** Highlight specific patterns and extract data dynamically.
- [ ] **Docker/WSL Integration:** Stream logs directly from Docker containers or WSL distributions.
- [ ] **Export Functionality:** Export filtered logs to a new file or clipboard.
- [ ] **Custom Themes:** User-selectable accent colors and layout configurations.

## 🚀 Getting Started
1. Download the latest release from the [Releases Page](https://github.com/BestArmor/OmniFlow/releases).
2. Extract the archive.
3. Run `OmniFlow.exe`. No .NET runtime installation required.

<div align="center">
  <br/>
  Made with 🔥 by <a href="https://github.com/BestArmor">BestArmor</a>
</div>
