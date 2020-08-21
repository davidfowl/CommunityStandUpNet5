# Linker Web Playground

This is an ASP.NET Core application running on .NET 5 with aggressive linking turned for ASP.NET and the Base Class Libraries. This will
not trim the application logic so contorllers will work just fine. At the moment, Microsoft.AspNetCore.DataProtection* is untrimmable so those assemblies are skipped.