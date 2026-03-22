# ASP.NET Core .NET 10 Testing Guidelines
- Use xUnit and Moq/NSubstitute.
- Always use WebApplicationFactory for Integration Tests.
- Mock Azure Cosmos DB using the TestContainer pattern where possible.
- Ensure all tests follow the Arrange-Act-Assert (AAA) pattern.
- Generate tests using the new Microsoft.Testing.Platform instead of VSTest