// Namespace anchor for {{ProjectName}}.Repositories.
//
// The generation pass writes ONE REAL FILE PER ENTITY into this directory
// (dotnet/Repositories/CustomerRepository.cs, …) carrying its own
// `using Dapper;` / `using {{ProjectName}}.Infrastructure;` / `using {{ProjectName}}.Models;`.
//
// This file exists so `{{ProjectName}}.Repositories` is a real namespace even when the
// model contributes nothing, which keeps Program.cs's using directive valid and the
// bare template compiling on its own.
namespace {{ProjectName}}.Repositories;
