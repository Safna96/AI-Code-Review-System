FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/CodeReview.sln ./backend/
COPY backend/src/CodeReview.Api/CodeReview.Api.csproj ./backend/src/CodeReview.Api/
COPY backend/src/CodeReview.Core/CodeReview.Core.csproj ./backend/src/CodeReview.Core/
COPY backend/src/CodeReview.Tests/CodeReview.Tests.csproj ./backend/src/CodeReview.Tests/
RUN dotnet restore ./backend/src/CodeReview.Api/CodeReview.Api.csproj

COPY backend/. ./backend/
RUN dotnet publish ./backend/src/CodeReview.Api/CodeReview.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CodeReview.Api.dll"]
