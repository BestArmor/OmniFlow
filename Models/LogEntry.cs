using System;

namespace OmniFlow.Models;

public readonly record struct LogEntry(
    DateTime Timestamp, 
    string Level, 
    string Message, 
    string SourceFile);