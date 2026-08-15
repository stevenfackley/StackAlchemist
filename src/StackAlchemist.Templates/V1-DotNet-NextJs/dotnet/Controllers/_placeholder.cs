// Namespace anchor for {{ProjectName}}.Controllers.
//
// The generation pass writes ONE REAL FILE PER ENTITY into this directory
// (dotnet/Controllers/CustomerEndpoints.cs, …), each a static class exposing a
// `Map{Entity}Endpoints(this WebApplication app)` extension that Program.cs calls from
// its RouteRegistrations zone. Endpoint code cannot live in the zone itself: top-level
// statements are legal only in Program.cs.
//
// This file exists so `{{ProjectName}}.Controllers` is a real namespace even when the
// model contributes nothing, which keeps Program.cs's using directive valid and the
// bare template compiling on its own.
namespace {{ProjectName}}.Controllers;
