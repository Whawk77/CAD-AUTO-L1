using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutoFixtureDim;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(PluginEntry))]
[assembly: CommandClass(typeof(Commands))]
[assembly: AssemblyTitle("AutoFixtureDim")]
[assembly: AssemblyDescription("Semi-automatic fixture part drawing dimensioning for AutoCAD 2020.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("AutoFixtureDim")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("5b5c7bb0-0674-47e7-a027-5d3224f3ebda")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyVersion("1.0.0.0")]
