
dotnet ef migrations add "InitialMigration" -c ApplicationDbContext --output-dir Database/Migrations/PostgreSQL -s .\src\Web.Api\ -p .\src\Infrastructure\ --verbose -- --environment Development

dotnet ef database update -s .\src\Web.Api\ -p .\src\Infrastructure\ -- --environment Development
