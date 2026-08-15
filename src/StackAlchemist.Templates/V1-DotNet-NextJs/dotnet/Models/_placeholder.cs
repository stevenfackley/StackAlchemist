// Namespace anchor for {{ProjectName}}.Models.
//
// The generation pass writes ONE REAL FILE PER ENTITY into this directory
// (dotnet/Models/Customer.cs, dotnet/Models/Invoice.cs, …), each with its own
// file-scoped namespace and usings — the layout a .NET developer expects, and the
// only layout in which two entities can carry different usings without colliding.
//
// This file exists so `{{ProjectName}}.Models` is a real namespace even when the model
// contributes nothing, which keeps Program.cs's `using {{ProjectName}}.Models;` valid
// and the bare template compiling on its own.
namespace {{ProjectName}}.Models;
