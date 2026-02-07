# 1. Dùng Image SDK .NET 9.0 để build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# 2. Copy toàn bộ code vào Docker
COPY . .

# 3. Tải các thư viện và Build
RUN dotnet restore
RUN dotnet publish -c Release -o out

# 4. Dùng Image Runtime .NET 9.0 để chạy ứng dụng
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# 5. Chạy file .dll
ENTRYPOINT ["dotnet", "BitrixChecker.dll"]