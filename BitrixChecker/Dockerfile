# 1. Build stage using the .NET 8.0 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2. Copy source and publish
COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o out

# 3. Runtime stage using the .NET 8.0 ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# 5. Chạy file .dll
ENTRYPOINT ["dotnet", "BitrixChecker.dll"]
